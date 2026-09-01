using Spectra.Analysis.Transcoding;
using Spectra.Core.Enums;
using Spectra.Core.Models;
using Xunit;

namespace Spectra.Tests.Analysis;

public class TranscodingAnalyzerTests
{
    [Fact]
    public void Analyze_128KbpsFileWithMatchingBandwidth_IsVeryUnlikely()
    {
        var encoding = CreateEncoding(declaredKbps: 128, averageKbps: 128, BitrateMode.ConstantBitRate);
        var spectral = CreateSpectral(effectiveBandwidthHz: 15_900, sharpnessDbPerOctave: 20, cutoffConsistency: 1.0, ConfidenceLevel.High);

        var result = TranscodingAnalyzer.Analyze(encoding, spectral);

        Assert.Equal(TranscodingProbabilityLabel.VeryUnlikely, result.Label);
    }

    [Fact]
    public void Analyze_Declared320ButBandwidthMatches128_IsHighlyLikely()
    {
        var encoding = CreateEncoding(declaredKbps: 320, averageKbps: 320, BitrateMode.ConstantBitRate);
        var spectral = CreateSpectral(effectiveBandwidthHz: 15_500, sharpnessDbPerOctave: 21, cutoffConsistency: 1.0, ConfidenceLevel.High);

        var result = TranscodingAnalyzer.Analyze(encoding, spectral);

        Assert.True(result.Probability > 60, $"Expected high probability, got {result.Probability}");
        Assert.Contains(result.Findings, f => f.Code == "TRANSCODING_SPECTRAL_CUTOFF");
    }

    [Fact]
    public void Analyze_LowBandwidthConfidence_DampensProbabilityEvenWithDeficit()
    {
        var encodingHighConfidence = CreateEncoding(declaredKbps: 320, averageKbps: 320, BitrateMode.ConstantBitRate);
        var lowConfidenceSpectral = CreateSpectral(effectiveBandwidthHz: 15_500, sharpnessDbPerOctave: 21, cutoffConsistency: 0.3, ConfidenceLevel.Low);
        var highConfidenceSpectral = CreateSpectral(effectiveBandwidthHz: 15_500, sharpnessDbPerOctave: 21, cutoffConsistency: 1.0, ConfidenceLevel.High);

        var lowConfidenceResult = TranscodingAnalyzer.Analyze(encodingHighConfidence, lowConfidenceSpectral);
        var highConfidenceResult = TranscodingAnalyzer.Analyze(encodingHighConfidence, highConfidenceSpectral);

        Assert.True(lowConfidenceResult.Probability < highConfidenceResult.Probability);
    }

    [Fact]
    public void Analyze_CbrDeclaredBitrateMismatchesMeasured_AddsBitrateMismatchFinding()
    {
        var encoding = CreateEncoding(declaredKbps: 320, averageKbps: 300, BitrateMode.ConstantBitRate);
        var spectral = CreateSpectral(effectiveBandwidthHz: 20_500, sharpnessDbPerOctave: 5, cutoffConsistency: 0.9, ConfidenceLevel.High);

        var result = TranscodingAnalyzer.Analyze(encoding, spectral);

        Assert.Contains(result.Findings, f => f.Code == "ENCODING_BITRATE_MISMATCH");
    }

    [Fact]
    public void Analyze_NeverExceeds100OrGoesBelow0()
    {
        var encoding = CreateEncoding(declaredKbps: 320, averageKbps: 250, BitrateMode.ConstantBitRate);
        var spectral = CreateSpectral(effectiveBandwidthHz: 8_000, sharpnessDbPerOctave: 40, cutoffConsistency: 1.0, ConfidenceLevel.High);

        var result = TranscodingAnalyzer.Analyze(encoding, spectral);

        Assert.InRange(result.Probability, 0, 100);
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

    private static SpectralAnalysis CreateSpectral(
        double effectiveBandwidthHz, double sharpnessDbPerOctave, double cutoffConsistency, ConfidenceLevel confidence) => new()
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
        CutoffSharpnessDbPerOctave = sharpnessDbPerOctave,
        CutoffConsistency = cutoffConsistency,
        AverageSpectrumDb = [],
        FramesOverTime = [],
    };
}
