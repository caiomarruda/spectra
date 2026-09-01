using System.Numerics;
using Spectra.Core.Decoding;
using Spectra.Core.Enums;
using Spectra.Core.Models;
using MathNet.Numerics.IntegralTransforms;

namespace Spectra.Analysis.Spectral;

/// <summary>
/// Single-pass STFT: computes the standard spectral descriptors (02-AUDIO-ANALYSIS-SPEC.md
/// section 5) and the effective-bandwidth / cutoff-detection algorithm (section 6) from the
/// same frames, since recomputing the FFT twice for what is conceptually "phase 5" and
/// "phase 6" in the plan would be pure waste.
/// </summary>
public static class SpectralAnalyzer
{
    // Only exists to keep log10(0) from throwing — must sit far below any realistic per-bin power
    // (even a single quiet bin in a single frame), or it becomes an artificial floor that a
    // relative (-50 dB) threshold can dip below, making genuinely empty bins register as "present".
    // That was a real bug: 1e-12 (~ -120 dB) was close enough to typical per-frame reference
    // levels that it happened routinely on real music.
    private const double Epsilon = 1e-30;

    public static SpectralAnalysis Analyze(DecodedAudio audio)
    {
        var sampleRate = audio.SampleRateHz;
        var mono = MixToMono(audio);
        var window = BuildHannWindow(SpectralSettings.FftSize);
        var binCount = SpectralSettings.FftSize / 2 + 1;
        var binFrequencies = BuildBinFrequencies(binCount, sampleRate);
        var bandBinRanges = BuildBandBinRanges(binCount, sampleRate);
        var referenceBinRange = FrequencyRangeToBins(SpectralSettings.ReferenceBandLowHz, SpectralSettings.ReferenceBandHighHz, binCount, sampleRate);

        var averagePowerSum = new double[binCount];
        var frames = new List<SpectralFrameSummary>();
        var perFrameCutoffHz = new List<double>();
        var perFrameTotalEnergyDb = new List<double>();

        double centroidSum = 0, bandwidthSum = 0, rolloffSum = 0, flatnessSum = 0, contrastSum = 0, fluxSum = 0;
        var validFrameCount = 0;
        var fluxFrameCount = 0;
        double[]? previousPower = null;

        var complexBuffer = new Complex[SpectralSettings.FftSize];

        var frameCount = 0;
        for (var start = 0; start < mono.Length || frameCount == 0; start += SpectralSettings.HopSize)
        {
            frameCount++;
            FillWindowedFrame(mono, start, window, complexBuffer);
            Fourier.Forward(complexBuffer, FourierOptions.Default);

            var power = new double[binCount];
            for (var bin = 0; bin < binCount; bin++)
            {
                var magnitude = complexBuffer[bin].Magnitude / SpectralSettings.FftSize;
                power[bin] = magnitude * magnitude;
                averagePowerSum[bin] += power[bin];
            }

            var totalEnergy = power.Sum();
            var totalEnergyDb = ToDb(totalEnergy);
            perFrameTotalEnergyDb.Add(totalEnergyDb);

            var bandDb = new double[FrequencyBands.Definitions.Count];
            for (var b = 0; b < bandBinRanges.Count; b++)
            {
                var (lowBin, highBin) = bandBinRanges[b];
                bandDb[b] = ToDb(SumRange(power, lowBin, highBin));
            }

            var frameDb = ToDbSpectrum(power);
            var smoothedFrameDb = Smooth(frameDb);
            var referenceLevelDb = ToDb(MaxRange(power, referenceBinRange.LowBin, referenceBinRange.HighBin));
            var cutoffBin = FindCutoffBin(smoothedFrameDb, referenceLevelDb + SpectralSettings.CutoffThresholdDb);
            var cutoffHz = binFrequencies[cutoffBin];
            perFrameCutoffHz.Add(cutoffHz);

            double centroidHz = 0, rolloffHz = 0;
            if (totalEnergy > Epsilon)
            {
                centroidHz = ComputeCentroid(power, binFrequencies, totalEnergy);
                var bandwidthHz = ComputeBandwidth(power, binFrequencies, centroidHz, totalEnergy);
                rolloffHz = ComputeRolloff(power, binFrequencies, totalEnergy, 0.85);
                var flatness = ComputeFlatness(power);
                var contrast = ComputeContrast(power, bandBinRanges);

                centroidSum += centroidHz;
                bandwidthSum += bandwidthHz;
                rolloffSum += rolloffHz;
                flatnessSum += flatness;
                contrastSum += contrast;
                validFrameCount++;
            }

            if (previousPower is not null)
            {
                fluxSum += ComputeFlux(power, previousPower);
                fluxFrameCount++;
            }
            previousPower = power;

            frames.Add(new SpectralFrameSummary
            {
                Time = TimeSpan.FromSeconds((double)start / sampleRate),
                CentroidHz = centroidHz,
                RolloffHz = rolloffHz,
                DetectedCutoffHz = cutoffHz,
                TotalEnergyDb = totalEnergyDb,
                BandEnergiesDb = bandDb,
            });

            if (start + SpectralSettings.FftSize >= mono.Length)
            {
                break;
            }
        }

        var averagePower = averagePowerSum.Select(sum => sum / frameCount).ToArray();
        var averagePowerDb = averagePower.Select(ToDb).ToArray();
        var smoothedAveragePowerDb = Smooth(averagePowerDb);

        // The track-wide cutoff is the median of the per-frame cutoff detections, not a value
        // re-derived from the time-averaged spectrum: averaging power over thousands of frames
        // dilutes any single bin far below the level it reaches when content is actually present
        // there (real content at a given frequency is intermittent — silence between notes), which
        // biased a threshold-on-the-average-spectrum approach toward Nyquist on real, dynamic
        // music. Per-frame detection compares each frame only against its own reference level, so
        // it doesn't have this problem, and aggregating those answers over time is also a more
        // direct read of section 6's "persistência ao longo do tempo" requirement.
        // Whole-track silence is checked once here, on the raw waveform's own peak amplitude (a
        // universal, directly-interpretable dBFS scale) rather than folded into the per-frame
        // loop's own internal FFT-energy units, which aren't calibrated to any absolute reference.
        var peakAmplitude = mono.Length > 0 ? mono.Max(MathF.Abs) : 0f;
        var (trackCutoffHz, consistency, confidence) = peakAmplitude < SpectralSettings.TrackSilencePeakAmplitude
            ? (0, 0, ConfidenceLevel.Low)
            : ComputeTrackCutoff(perFrameCutoffHz, perFrameTotalEnergyDb);
        var trackCutoffBin = FrequencyToBin(trackCutoffHz, binCount, sampleRate);

        var octaveBelowBin = FrequencyToBin(trackCutoffHz / 2.0, binCount, sampleRate);
        var cutoffSharpness = smoothedAveragePowerDb[octaveBelowBin] - smoothedAveragePowerDb[trackCutoffBin];

        var bandEnergies = FrequencyBands.Definitions
            .Select((def, i) => new SpectralBandEnergy
            {
                Label = def.Label,
                LowHz = def.LowHz,
                HighHz = def.HighHz,
                AverageEnergyDb = ToDb(SumRange(averagePowerSum, bandBinRanges[i].LowBin, bandBinRanges[i].HighBin) / frameCount),
            })
            .ToList();

        return new SpectralAnalysis
        {
            SpectralCentroidHz = validFrameCount > 0 ? centroidSum / validFrameCount : 0,
            SpectralBandwidthHz = validFrameCount > 0 ? bandwidthSum / validFrameCount : 0,
            SpectralRolloffHz = validFrameCount > 0 ? rolloffSum / validFrameCount : 0,
            SpectralFlatness = validFrameCount > 0 ? flatnessSum / validFrameCount : 0,
            SpectralFluxAverage = fluxFrameCount > 0 ? fluxSum / fluxFrameCount : 0,
            SpectralContrast = validFrameCount > 0 ? contrastSum / validFrameCount : 0,
            BandEnergies = bandEnergies,
            EffectiveBandwidthHz = trackCutoffHz,
            BandwidthConfidence = confidence,
            CutoffFrequencyHz = trackCutoffHz,
            CutoffSharpnessDbPerOctave = cutoffSharpness,
            CutoffConsistency = consistency,
            AverageSpectrumDb = averagePowerDb,
            FramesOverTime = frames,
        };
    }

    private static float[] MixToMono(DecodedAudio audio)
    {
        var frameCount = audio.ChannelCount > 0 ? audio.Channels[0].Length : 0;
        var mono = new float[frameCount];
        for (var i = 0; i < frameCount; i++)
        {
            float sum = 0;
            for (var c = 0; c < audio.ChannelCount; c++)
            {
                sum += audio.Channels[c][i];
            }
            mono[i] = sum / audio.ChannelCount;
        }
        return mono;
    }

    private static double[] BuildHannWindow(int size)
    {
        var window = new double[size];
        for (var n = 0; n < size; n++)
        {
            window[n] = 0.5 * (1 - Math.Cos(2 * Math.PI * n / (size - 1)));
        }
        return window;
    }

    private static void FillWindowedFrame(float[] mono, int start, double[] window, Complex[] destination)
    {
        for (var i = 0; i < destination.Length; i++)
        {
            var sampleIndex = start + i;
            var sample = sampleIndex < mono.Length ? mono[sampleIndex] : 0f;
            destination[i] = new Complex(sample * window[i], 0);
        }
    }

    private static double[] BuildBinFrequencies(int binCount, int sampleRate)
    {
        var frequencies = new double[binCount];
        for (var bin = 0; bin < binCount; bin++)
        {
            frequencies[bin] = bin * (double)sampleRate / SpectralSettings.FftSize;
        }
        return frequencies;
    }

    private static List<(int LowBin, int HighBin)> BuildBandBinRanges(int binCount, int sampleRate) =>
        FrequencyBands.Definitions
            .Select(def => FrequencyRangeToBins(def.LowHz, def.HighHz, binCount, sampleRate))
            .Select(r => (r.LowBin, r.HighBin))
            .ToList();

    private static (int LowBin, int HighBin) FrequencyRangeToBins(double lowHz, double highHz, int binCount, int sampleRate)
    {
        var lowBin = Math.Clamp((int)Math.Ceiling(lowHz * SpectralSettings.FftSize / sampleRate), 0, binCount - 1);
        var highBin = Math.Clamp((int)Math.Floor(highHz * SpectralSettings.FftSize / sampleRate), lowBin, binCount - 1);
        return (lowBin, highBin);
    }

    private static int FrequencyToBin(double frequencyHz, int binCount, int sampleRate) =>
        Math.Clamp((int)Math.Round(frequencyHz * SpectralSettings.FftSize / sampleRate), 0, binCount - 1);

    private static double SumRange(double[] values, int lowBin, int highBin)
    {
        double sum = 0;
        for (var i = lowBin; i <= highBin; i++)
        {
            sum += values[i];
        }
        return sum;
    }

    private static double AverageRange(double[] values, int lowBin, int highBin) =>
        SumRange(values, lowBin, highBin) / (highBin - lowBin + 1);

    /// <summary>
    /// Peak power in a range. Used for the cutoff-detection reference level instead of the mean:
    /// the mean power across a wide band is dominated by however many bins happen to be empty,
    /// which has nothing to do with how loud real content in that band actually gets.
    /// </summary>
    private static double MaxRange(double[] values, int lowBin, int highBin)
    {
        var max = 0.0;
        for (var i = lowBin; i <= highBin; i++)
        {
            if (values[i] > max)
            {
                max = values[i];
            }
        }
        return max;
    }

    private static double ToDb(double power) => 10.0 * Math.Log10(power + Epsilon);

    private static double[] ToDbSpectrum(double[] power)
    {
        var db = new double[power.Length];
        for (var i = 0; i < power.Length; i++)
        {
            db[i] = ToDb(power[i]);
        }
        return db;
    }

    /// <summary>5-bin moving average; guards cutoff detection against single-bin spikes/notches.</summary>
    private static double[] Smooth(double[] values)
    {
        const int radius = 2;
        var result = new double[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            var lo = Math.Max(0, i - radius);
            var hi = Math.Min(values.Length - 1, i + radius);
            double sum = 0;
            for (var j = lo; j <= hi; j++)
            {
                sum += values[j];
            }
            result[i] = sum / (hi - lo + 1);
        }
        return result;
    }

    /// <summary>Highest-frequency bin still at/above threshold — the edge of where content is present.</summary>
    private static int FindCutoffBin(double[] smoothedDb, double thresholdDb)
    {
        for (var bin = smoothedDb.Length - 1; bin >= 0; bin--)
        {
            if (smoothedDb[bin] >= thresholdDb)
            {
                return bin;
            }
        }
        return 0;
    }

    private static double ComputeCentroid(double[] power, double[] frequencies, double totalEnergy)
    {
        double weighted = 0;
        for (var i = 0; i < power.Length; i++)
        {
            weighted += frequencies[i] * power[i];
        }
        return weighted / totalEnergy;
    }

    private static double ComputeBandwidth(double[] power, double[] frequencies, double centroidHz, double totalEnergy)
    {
        double weighted = 0;
        for (var i = 0; i < power.Length; i++)
        {
            var diff = frequencies[i] - centroidHz;
            weighted += power[i] * diff * diff;
        }
        return Math.Sqrt(weighted / totalEnergy);
    }

    private static double ComputeRolloff(double[] power, double[] frequencies, double totalEnergy, double fraction)
    {
        var target = totalEnergy * fraction;
        double cumulative = 0;
        for (var i = 0; i < power.Length; i++)
        {
            cumulative += power[i];
            if (cumulative >= target)
            {
                return frequencies[i];
            }
        }
        return frequencies[^1];
    }

    private static double ComputeFlatness(double[] power)
    {
        double logSum = 0;
        double arithmeticSum = 0;
        var count = power.Length - 1; // exclude DC bin.
        for (var i = 1; i < power.Length; i++)
        {
            logSum += Math.Log(power[i] + Epsilon);
            arithmeticSum += power[i];
        }
        var geometricMean = Math.Exp(logSum / count);
        var arithmeticMean = arithmeticSum / count;
        return geometricMean / (arithmeticMean + Epsilon);
    }

    /// <summary>Simplified per-band peak-vs-mean contrast (not the full multi-octave Jiang et al. algorithm).</summary>
    private static double ComputeContrast(double[] power, List<(int LowBin, int HighBin)> bandBinRanges)
    {
        double sum = 0;
        foreach (var (lowBin, highBin) in bandBinRanges)
        {
            double max = 0, total = 0;
            for (var i = lowBin; i <= highBin; i++)
            {
                var db = ToDb(power[i]);
                if (db > max)
                {
                    max = db;
                }
                total += db;
            }
            var mean = total / (highBin - lowBin + 1);
            sum += max - mean;
        }
        return sum / bandBinRanges.Count;
    }

    private static double ComputeFlux(double[] power, double[] previousPower)
    {
        double sumSquares = 0;
        for (var i = 0; i < power.Length; i++)
        {
            var diff = power[i] - previousPower[i];
            sumSquares += diff * diff;
        }
        return Math.Sqrt(sumSquares / power.Length);
    }

    private static (double CutoffHz, double Consistency, ConfidenceLevel Confidence) ComputeTrackCutoff(
        List<double> perFrameCutoffHz, List<double> perFrameTotalEnergyDb)
    {
        if (perFrameCutoffHz.Count == 0)
        {
            return (0, 0, ConfidenceLevel.Low);
        }

        // A frame only counts as evidence if it is loud relative to the track's own loudest moment
        // — this excludes quiet passages within an otherwise dynamic track. Deliberately relative,
        // not an absolute dB floor: an earlier version used a fixed absolute threshold calibrated
        // against a single loud reference file, which silently discarded every frame (confidence
        // Low, 0% bandwidth) on a merely quieter — but perfectly legitimate — recording. The
        // degenerate "whole track is silent" case this guarded against is instead handled once,
        // up front, by the caller checking the track's overall peak amplitude directly.
        var loudestDb = perFrameTotalEnergyDb.Max();
        var relativeFloor = loudestDb + SpectralSettings.SilentFrameRelativeThresholdDb;

        var consideredCutoffs = new List<double>();
        for (var i = 0; i < perFrameCutoffHz.Count; i++)
        {
            if (perFrameTotalEnergyDb[i] >= relativeFloor)
            {
                consideredCutoffs.Add(perFrameCutoffHz[i]);
            }
        }

        if (consideredCutoffs.Count < 10)
        {
            return (0, 0, ConfidenceLevel.Low);
        }

        consideredCutoffs.Sort();
        var median = consideredCutoffs[consideredCutoffs.Count / 2];

        var agreeingFrames = consideredCutoffs.Count(c => Math.Abs(c - median) <= SpectralSettings.CutoffConsistencyToleranceHz);
        var consistency = (double)agreeingFrames / consideredCutoffs.Count;
        var confidence = consistency switch
        {
            >= 0.8 => ConfidenceLevel.High,
            >= 0.5 => ConfidenceLevel.Medium,
            _ => ConfidenceLevel.Low,
        };
        return (median, consistency, confidence);
    }
}
