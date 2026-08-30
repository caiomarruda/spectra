using AudioQualityAnalyzer.Analysis.Scoring;
using AudioQualityAnalyzer.Analysis.Transcoding;
using AudioQualityAnalyzer.Core.Enums;
using AudioQualityAnalyzer.Core.Models;
using Xunit;

namespace AudioQualityAnalyzer.Tests.Analysis;

public class QualityScorerTests
{
    [Fact]
    public void Analyze_Clean128KbpsFile_IsGoodNotBelowAverage()
    {
        // Regression test: a clean, un-transcoded 128 kbps file must not be verdicted as
        // "below average" just because its bitrate-tier encoding score is naturally lower than
        // a 320 kbps file's — that would contradict the app's own "bitrate != quality" principle.
        var encoding = CreateEncoding(128, 128, BitrateMode.ConstantBitRate);
        var spectral = CreateSpectral(effectiveBandwidthHz: 15_900, sharpness: 20, consistency: 1.0, ConfidenceLevel.High);
        var dynamics = CreateDynamics(crestFactorDb: 16.6);
        var clipping = CreateClipping(clippedSamples: 0, severe: false);
        var loudness = CreateLoudness(integratedLufs: -10.8, truePeakDbfs: -1.0, lra: 6.7);
        var noise = CreateNoise(dcOffset: false, excessiveSilence: false);
        var transcoding = TranscodingAnalyzer.Analyze(encoding, spectral);

        var result = QualityScorer.Analyze(encoding, spectral, dynamics, clipping, loudness, stereo: null, noise, transcoding);

        Assert.Equal("GOOD 128 KBPS", result.Verdict);
    }

    [Fact]
    public void Analyze_LikelyTranscoded_VerdictMentionsPossibleTranscode()
    {
        var encoding = CreateEncoding(320, 320, BitrateMode.ConstantBitRate);
        var spectral = CreateSpectral(effectiveBandwidthHz: 15_500, sharpness: 21, consistency: 1.0, ConfidenceLevel.High);
        var dynamics = CreateDynamics(crestFactorDb: 14);
        var clipping = CreateClipping(clippedSamples: 0, severe: false);
        var loudness = CreateLoudness(integratedLufs: -10, truePeakDbfs: -1.0, lra: 6);
        var noise = CreateNoise(dcOffset: false, excessiveSilence: false);
        var transcoding = TranscodingAnalyzer.Analyze(encoding, spectral);

        var result = QualityScorer.Analyze(encoding, spectral, dynamics, clipping, loudness, stereo: null, noise, transcoding);

        Assert.Contains("POSSIBLE TRANSCODE", result.Verdict);
    }

    [Fact]
    public void Analyze_SevereClippingAndHeavyLimiting_VerdictIsPoorMastering()
    {
        var encoding = CreateEncoding(320, 320, BitrateMode.ConstantBitRate);
        var spectral = CreateSpectral(effectiveBandwidthHz: 20_500, sharpness: 2, consistency: 0.9, ConfidenceLevel.High);
        var dynamics = CreateDynamics(crestFactorDb: 4.0);
        var clipping = CreateClipping(clippedSamples: 5000, severe: true);
        var loudness = CreateLoudness(integratedLufs: -6, truePeakDbfs: 1.5, lra: 1.0);
        var noise = CreateNoise(dcOffset: false, excessiveSilence: false);
        var transcoding = TranscodingAnalyzer.Analyze(encoding, spectral);

        var result = QualityScorer.Analyze(encoding, spectral, dynamics, clipping, loudness, stereo: null, noise, transcoding);

        Assert.Contains("POOR MASTERING", result.Verdict);
    }

    [Fact]
    public void Analyze_LowLoudnessButOtherwiseClean_DoesNotPenalizeMasteringScore()
    {
        // 03-QUALITY-DETECTION.md: "Loudness baixo não é defeito por si só".
        var encoding = CreateEncoding(320, 320, BitrateMode.ConstantBitRate);
        var spectral = CreateSpectral(effectiveBandwidthHz: 20_500, sharpness: 2, consistency: 0.9, ConfidenceLevel.High);
        var dynamics = CreateDynamics(crestFactorDb: 18);
        var clipping = CreateClipping(clippedSamples: 0, severe: false);
        var quietLoudness = CreateLoudness(integratedLufs: -28, truePeakDbfs: -6, lra: 8);
        var noise = CreateNoise(dcOffset: false, excessiveSilence: false);
        var transcoding = TranscodingAnalyzer.Analyze(encoding, spectral);

        var result = QualityScorer.Analyze(encoding, spectral, dynamics, clipping, quietLoudness, stereo: null, noise, transcoding);

        Assert.Equal(100, result.MasteringQualityScore);
        Assert.Contains("LOW LOUDNESS", result.Verdict);
    }

    [Fact]
    public void Analyze_OverallScore_IsWithinValidRange()
    {
        var encoding = CreateEncoding(192, 192, BitrateMode.ConstantBitRate);
        var spectral = CreateSpectral(effectiveBandwidthHz: 19_000, sharpness: 8, consistency: 0.85, ConfidenceLevel.Medium);
        var dynamics = CreateDynamics(crestFactorDb: 10);
        var clipping = CreateClipping(clippedSamples: 100, severe: false);
        var loudness = CreateLoudness(integratedLufs: -12, truePeakDbfs: -0.5, lra: 5);
        var noise = CreateNoise(dcOffset: true, excessiveSilence: true);
        var transcoding = TranscodingAnalyzer.Analyze(encoding, spectral);

        var result = QualityScorer.Analyze(encoding, spectral, dynamics, clipping, loudness, stereo: null, noise, transcoding);

        Assert.InRange(result.OverallQualityScore, 0, 100);
        Assert.InRange(result.EncodingQualityScore, 0, 100);
        Assert.InRange(result.TechnicalQualityScore, 0, 100);
        Assert.NotEmpty(result.Findings);
    }

    private static EncodingAnalysis CreateEncoding(int declaredKbps, double averageKbps, BitrateMode mode) => new()
    {
        DeclaredBitrateKbps = declaredKbps,
        AverageBitrateKbps = averageKbps,
        MinimumBitrateKbps = declaredKbps,
        MaximumBitrateKbps = declaredKbps,
        BitrateMode = mode,
        FrameCount = 1000,
        HasXingHeader = false,
        HasLameTag = false,
    };

    private static SpectralAnalysis CreateSpectral(double effectiveBandwidthHz, double sharpness, double consistency, ConfidenceLevel confidence) => new()
    {
        SpectralCentroidHz = 1500,
        SpectralBandwidthHz = 2000,
        SpectralRolloffHz = 3000,
        SpectralFlatness = 0.01,
        SpectralFluxAverage = 0,
        SpectralContrast = 50,
        BandEnergies = [],
        EffectiveBandwidthHz = effectiveBandwidthHz,
        BandwidthConfidence = confidence,
        CutoffFrequencyHz = effectiveBandwidthHz,
        CutoffSharpnessDbPerOctave = sharpness,
        CutoffConsistency = consistency,
        AverageSpectrumDb = [],
        FramesOverTime = [],
    };

    private static DynamicRangeAnalysis CreateDynamics(double crestFactorDb) => new()
    {
        CrestFactorDb = crestFactorDb,
        RmsWindowMinDb = -40,
        RmsWindowMaxDb = -10,
        RmsWindowMedianDb = -15,
        RmsWindowStdDevDb = 5,
        PercentSamplesNearFullScale = 0,
    };

    private static ClippingAnalysis CreateClipping(long clippedSamples, bool severe) => new()
    {
        TotalClippedSamples = clippedSamples,
        ClippedPercentage = clippedSamples > 0 ? 0.01 : 0,
        ClipEventCount = clippedSamples > 0 ? 1 : 0,
        LongestClipDuration = severe ? TimeSpan.FromMilliseconds(5) : TimeSpan.Zero,
        ClippedSamplesPerChannel = [clippedSamples],
        IsSevere = severe,
    };

    private static LoudnessAnalysis CreateLoudness(double integratedLufs, double truePeakDbfs, double lra) => new()
    {
        IntegratedLufs = integratedLufs,
        MomentaryMaxLufs = integratedLufs + 3,
        ShortTermMaxLufs = integratedLufs + 2,
        LoudnessRangeLu = lra,
        SamplePeakDbfs = truePeakDbfs - 0.1,
        TruePeakDbfs = truePeakDbfs,
        SamplePeakPerChannelDbfs = [truePeakDbfs - 0.1],
        TruePeakPerChannelDbfs = [truePeakDbfs],
        LoudnessOverTime = [],
    };

    private static NoiseAnalysis CreateNoise(bool dcOffset, bool excessiveSilence) => new()
    {
        NoiseFloorDb = -60,
        DcOffsetPerChannel = [dcOffset ? 0.02 : 0.0],
        HasSignificantDcOffset = dcOffset,
        HasExcessiveInternalSilence = excessiveSilence,
    };
}
