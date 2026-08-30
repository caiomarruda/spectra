namespace AudioQualityAnalyzer.Core.Models;

/// <summary>
/// Per-STFT-frame descriptors, kept for charting (spectrogram-over-time in the HTML report)
/// and for the effective-bandwidth persistence check, which needs frame-level cutoff data
/// rather than a single track-wide average.
/// </summary>
public sealed record SpectralFrameSummary
{
    public required TimeSpan Time { get; init; }
    public required double CentroidHz { get; init; }
    public required double RolloffHz { get; init; }
    public required double DetectedCutoffHz { get; init; }
    public required double TotalEnergyDb { get; init; }
    public required IReadOnlyList<double> BandEnergiesDb { get; init; }
}
