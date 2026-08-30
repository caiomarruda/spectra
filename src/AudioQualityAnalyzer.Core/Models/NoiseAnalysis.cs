namespace AudioQualityAnalyzer.Core.Models;

/// <summary>
/// A conservative subset of 02-AUDIO-ANALYSIS-SPEC.md section 12: noise floor and DC offset are
/// measured directly. Hum/hiss narrowband detection is not implemented — the STFT's ~10.8 Hz bin
/// resolution at 4096 points is too coarse to reliably separate 50/60 Hz mains hum from a bass
/// note without a dedicated narrowband analysis, which was judged not worth the false-positive
/// risk for this pass. Never penalize noise by itself: a legitimate analog source can have an
/// elevated floor (spec section 12).
/// </summary>
public sealed record NoiseAnalysis
{
    /// <summary>10th percentile of windowed RMS, excluding leading/trailing silence — a robust noise-floor estimate.</summary>
    public required double NoiseFloorDb { get; init; }
    public required IReadOnlyList<double> DcOffsetPerChannel { get; init; }
    public required bool HasSignificantDcOffset { get; init; }
    public required bool HasExcessiveInternalSilence { get; init; }
}
