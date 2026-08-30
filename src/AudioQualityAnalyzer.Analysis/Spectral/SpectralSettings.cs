namespace AudioQualityAnalyzer.Analysis.Spectral;

/// <summary>
/// Centralized STFT configuration (02-AUDIO-ANALYSIS-SPEC.md section 4: "Centralizar a
/// configuração e evitar números mágicos").
/// </summary>
public static class SpectralSettings
{
    public const int FftSize = 4096;
    public const double OverlapRatio = 0.5;
    public const int HopSize = (int)(FftSize * (1 - OverlapRatio));

    /// <summary>
    /// Relative-energy threshold, in dB below the reference level, used to decide whether
    /// content is still "present" at a given frequency. -50 dB sits well below normal program
    /// material but well above decoder dither/quantization noise, which keeps false positives
    /// (mistaking noise floor for a real cutoff) low per the spec's low-false-positive goal.
    /// </summary>
    public const double CutoffThresholdDb = -50.0;

    /// <summary>Reference band used to establish the "normal signal level" a cutoff is measured against.</summary>
    public const double ReferenceBandLowHz = 1000.0;
    public const double ReferenceBandHighHz = 6000.0;

    /// <summary>Frames whose total energy falls below this, relative to the track's loudest frame, are excluded from cutoff-consistency voting (near-silence would trivially "cut off" everywhere).</summary>
    public const double SilentFrameRelativeThresholdDb = -40.0;

    /// <summary>Tolerance for counting a per-frame cutoff as agreeing with the track-wide cutoff.</summary>
    public const double CutoffConsistencyToleranceHz = 1000.0;

    /// <summary>
    /// A frame below this absolute total-energy level is excluded from cutoff-consistency voting
    /// even if it is the "loudest" frame in the track — needed alongside the relative
    /// (<see cref="SilentFrameRelativeThresholdDb"/>) check because a track that is uniformly
    /// near-silent throughout has no dynamic range for the relative check to work with.
    /// </summary>
    public const double AbsoluteMinimumFrameEnergyDb = -60.0;
}
