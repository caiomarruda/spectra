using Spectra.Core.Decoding;
using Spectra.Core.Models;

namespace Spectra.Analysis.Dynamics;

public static class DynamicRangeAnalyzer
{
    /// <summary>-1 dBFS, expressed as a linear amplitude — the "near full scale" threshold used to flag hot/limited masters, distinct from actual clipping.</summary>
    private const float NearFullScaleLinearThreshold = 0.89125f;

    public static DynamicRangeAnalysis Analyze(DecodedAudio audio, WaveformAnalysis waveform)
    {
        var crestFactorDb = waveform.RmsAmplitude > 0
            ? 20.0 * Math.Log10(waveform.PeakAmplitude / waveform.RmsAmplitude)
            : double.PositiveInfinity;

        var rmsWindowDb = waveform.RmsOverTime
            .Select(w => w.Rms > 0 ? 20.0 * Math.Log10(w.Rms) : double.NegativeInfinity)
            .Where(db => !double.IsNegativeInfinity(db))
            .OrderBy(db => db)
            .ToList();

        double minDb = 0, maxDb = 0, medianDb = 0, stdDevDb = 0;
        if (rmsWindowDb.Count > 0)
        {
            minDb = rmsWindowDb[0];
            maxDb = rmsWindowDb[^1];
            medianDb = rmsWindowDb[rmsWindowDb.Count / 2];
            var mean = rmsWindowDb.Average();
            var variance = rmsWindowDb.Average(db => (db - mean) * (db - mean));
            stdDevDb = Math.Sqrt(variance);
        }

        long nearFullScaleCount = 0;
        long totalSamples = 0;
        for (var c = 0; c < audio.ChannelCount; c++)
        {
            var samples = audio.Channels[c];
            totalSamples += samples.Length;
            foreach (var sample in samples)
            {
                if (MathF.Abs(sample) >= NearFullScaleLinearThreshold)
                {
                    nearFullScaleCount++;
                }
            }
        }

        return new DynamicRangeAnalysis
        {
            CrestFactorDb = crestFactorDb,
            RmsWindowMinDb = minDb,
            RmsWindowMaxDb = maxDb,
            RmsWindowMedianDb = medianDb,
            RmsWindowStdDevDb = stdDevDb,
            PercentSamplesNearFullScale = totalSamples > 0 ? 100.0 * nearFullScaleCount / totalSamples : 0,
        };
    }
}
