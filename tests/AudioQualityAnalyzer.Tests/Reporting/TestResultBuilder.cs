using AudioQualityAnalyzer.Core.Enums;
using AudioQualityAnalyzer.Core.Models;

namespace AudioQualityAnalyzer.Tests.Reporting;

/// <summary>Builds a fully-populated, small but realistic AudioAnalysisResult for reporter tests.</summary>
internal static class TestResultBuilder
{
    public static AudioAnalysisResult Build(bool includeStereo = true)
    {
        var bandEnergies = Enumerable.Range(0, 14)
            .Select(i => new SpectralBandEnergy { Label = $"Band{i}", LowHz = i * 1000, HighHz = (i + 1) * 1000, AverageEnergyDb = -60 + i })
            .ToList();

        var spectralFrames = Enumerable.Range(0, 20)
            .Select(i => new SpectralFrameSummary
            {
                Time = TimeSpan.FromSeconds(i * 0.1),
                CentroidHz = 1500,
                RolloffHz = 3000,
                DetectedCutoffHz = 16000,
                TotalEnergyDb = -20,
                BandEnergiesDb = Enumerable.Repeat(-60.0, 14).ToList(),
            })
            .ToList();

        var loudnessOverTime = Enumerable.Range(0, 20)
            .Select(i => new LoudnessTimePoint { Time = TimeSpan.FromSeconds(i * 0.1), MomentaryLufs = -14, ShortTermLufs = -13 })
            .ToList();

        var rmsOverTime = Enumerable.Range(0, 20)
            .Select(i => new RmsWindow { StartTime = TimeSpan.FromSeconds(i * 0.1), Rms = 0.1f, Peak = 0.3f })
            .ToList();

        var correlationOverTime = Enumerable.Range(0, 20)
            .Select(i => new StereoCorrelationPoint { Time = TimeSpan.FromSeconds(i * 0.5), Correlation = 0.7 })
            .ToList();

        var finding = new AnalysisFinding
        {
            Code = "TEST_FINDING",
            Title = "Test finding",
            Severity = Severity.Info,
            Confidence = ConfidenceLevel.High,
            Description = "A test finding.",
            Evidence = ["Evidence line 1"],
            Metrics = new Dictionary<string, double> { ["X"] = 1.0 },
        };

        return new AudioAnalysisResult
        {
            FileInfo = new AudioFileInfo
            {
                FullPath = "/tmp/test.mp3",
                FileName = "test.mp3",
                Extension = ".mp3",
                SizeInBytes = 1_000_000,
                Duration = TimeSpan.FromSeconds(2),
            },
            FormatInfo = new FormatInfo
            {
                Format = "MP3",
                MpegVersion = MpegVersion.Version1,
                MpegLayer = MpegLayer.LayerIII,
                SampleRateHz = 44100,
                Channels = 2,
                ChannelMode = ChannelMode.Stereo,
                Encoder = "LAME3.100",
                EncoderDelaySamples = 576,
                PaddingSamples = 1152,
            },
            EncodingAnalysis = new EncodingAnalysis
            {
                DeclaredBitrateKbps = 128,
                AverageBitrateKbps = 128,
                MinimumBitrateKbps = 128,
                MaximumBitrateKbps = 128,
                BitrateMode = BitrateMode.ConstantBitRate,
                FrameCount = 100,
                HasXingHeader = false,
                HasLameTag = true,
            },
            WaveformAnalysis = new WaveformAnalysis
            {
                PeakAmplitude = 0.9f,
                RmsAmplitude = 0.1f,
                MinSample = -0.9f,
                MaxSample = 0.9f,
                LeadingSilence = TimeSpan.Zero,
                TrailingSilence = TimeSpan.Zero,
                PerChannel =
                [
                    new ChannelWaveformStats { ChannelIndex = 0, Peak = 0.9f, Rms = 0.1f, MinSample = -0.9f, MaxSample = 0.9f },
                    new ChannelWaveformStats { ChannelIndex = 1, Peak = 0.85f, Rms = 0.1f, MinSample = -0.85f, MaxSample = 0.85f },
                ],
                RmsOverTime = rmsOverTime,
            },
            SpectralAnalysis = new SpectralAnalysis
            {
                SpectralCentroidHz = 1500,
                SpectralBandwidthHz = 2000,
                SpectralRolloffHz = 3000,
                SpectralFlatness = 0.01,
                SpectralFluxAverage = 0,
                SpectralContrast = 50,
                BandEnergies = bandEnergies,
                EffectiveBandwidthHz = 16000,
                BandwidthConfidence = ConfidenceLevel.High,
                CutoffFrequencyHz = 16000,
                CutoffSharpnessDbPerOctave = 20,
                CutoffConsistency = 1.0,
                AverageSpectrumDb = Enumerable.Repeat(-60.0, 2049).ToList(),
                FramesOverTime = spectralFrames,
            },
            LoudnessAnalysis = new LoudnessAnalysis
            {
                IntegratedLufs = -14,
                MomentaryMaxLufs = -10,
                ShortTermMaxLufs = -11,
                LoudnessRangeLu = 6,
                SamplePeakDbfs = -1,
                TruePeakDbfs = -0.5,
                SamplePeakPerChannelDbfs = [-1, -1.2],
                TruePeakPerChannelDbfs = [-0.5, -0.6],
                LoudnessOverTime = loudnessOverTime,
            },
            DynamicRangeAnalysis = new DynamicRangeAnalysis
            {
                CrestFactorDb = 12,
                RmsWindowMinDb = -40,
                RmsWindowMaxDb = -10,
                RmsWindowMedianDb = -15,
                RmsWindowStdDevDb = 5,
                PercentSamplesNearFullScale = 0.01,
            },
            ClippingAnalysis = new ClippingAnalysis
            {
                TotalClippedSamples = 10,
                ClippedPercentage = 0.001,
                ClipEventCount = 2,
                LongestClipDuration = TimeSpan.FromMilliseconds(0.5),
                ClippedSamplesPerChannel = [5, 5],
                IsSevere = false,
            },
            StereoAnalysis = includeStereo
                ? new StereoAnalysis
                {
                    CorrelationCoefficient = 0.7,
                    ChannelBalanceDb = -0.5,
                    MonoCompatibilityRatio = 0.9,
                    MidEnergyDb = -14,
                    SideEnergyDb = -20,
                    SideToMidRatioDb = -6,
                    IsChannelEffectivelyMissing = false,
                    IsSeverelyImbalanced = false,
                    IsMonoDisguisedAsStereo = false,
                    HasPhaseProblems = false,
                    HasPolarityInversion = false,
                    HasExcessiveSideContent = false,
                    CorrelationOverTime = correlationOverTime,
                }
                : null,
            TranscodingAnalysis = new TranscodingAnalysis
            {
                Probability = 9,
                Label = TranscodingProbabilityLabel.VeryUnlikely,
                Confidence = ConfidenceLevel.High,
                Findings = [finding],
            },
            NoiseAnalysis = new NoiseAnalysis
            {
                NoiseFloorDb = -55,
                DcOffsetPerChannel = [0.0001, 0.0001],
                HasSignificantDcOffset = false,
                HasExcessiveInternalSilence = false,
            },
            OverallAssessment = new OverallAssessment
            {
                EncodingQualityScore = 65,
                SpectralQualityScore = 76,
                TechnicalQualityScore = 95,
                MasteringQualityScore = 85,
                OverallQualityScore = 79,
                Verdict = "GOOD 128 KBPS",
                Findings = [finding],
            },
        };
    }
}
