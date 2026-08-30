using AudioQualityAnalyzer.Core.Enums;

namespace AudioQualityAnalyzer.Core.Models;

public sealed record SpectralAnalysis
{
    public required double SpectralCentroidHz { get; init; }
    public required double SpectralBandwidthHz { get; init; }
    public required double SpectralRolloffHz { get; init; }
    public required double SpectralFlatness { get; init; }
    public required double SpectralFluxAverage { get; init; }
    public required double SpectralContrast { get; init; }
    public required IReadOnlyList<SpectralBandEnergy> BandEnergies { get; init; }

    /// <summary>Highest frequency at which energy is persistently present, not just the last non-zero FFT bin.</summary>
    public required double EffectiveBandwidthHz { get; init; }
    public required ConfidenceLevel BandwidthConfidence { get; init; }
    public required double CutoffFrequencyHz { get; init; }
    public required double CutoffSharpnessDbPerOctave { get; init; }

    /// <summary>Fraction (0-1) of analyzed frames whose own detected cutoff agrees with <see cref="CutoffFrequencyHz"/>.</summary>
    public required double CutoffConsistency { get; init; }

    public required IReadOnlyList<double> AverageSpectrumDb { get; init; }
    public required IReadOnlyList<SpectralFrameSummary> FramesOverTime { get; init; }
}
