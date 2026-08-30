namespace AudioQualityAnalyzer.Analysis.Transcoding;

/// <summary>
/// Approximate expected effective bandwidth per encoded bitrate, based on LAME's typical lowpass
/// defaults. Other encoders vary, which is exactly why this is used as one interpolated signal
/// among several rather than a hard cutoff rule (05-IMPLEMENTATION-PLAN.md Phase 9: "Não usar uma
/// regra única baseada em frequência").
/// </summary>
public static class TranscodingSettings
{
    /// <summary>Input = bitrate (kbps), Output = expected effective bandwidth (Hz).</summary>
    public static readonly IReadOnlyList<(double Input, double Output)> ExpectedBandwidthTable =
    [
        (32, 8_000),
        (48, 10_000),
        (64, 11_000),
        (96, 13_500),
        (112, 15_000),
        (128, 16_000),
        (160, 18_000),
        (192, 19_000),
        (224, 19_500),
        (256, 19_800),
        (320, 20_500),
    ];

    /// <summary>Bandwidth shortfall (kHz) below which is normal encoder-to-encoder variance, not evidence.</summary>
    public const double NoDeficitToleranceHz = 1_000;

    /// <summary>Shortfall at or above this saturates the bandwidth sub-score at 100.</summary>
    public const double MaxDeficitForFullScoreHz = 8_000;

    public const double SharpCutoffDbPerOctaveThreshold = 12.0;
    public const double MaxSharpnessBonusPoints = 10.0;

    /// <summary>Tolerance (kbps) between declared and measured average bitrate for CBR before flagging it as its own piece of evidence.</summary>
    public const double CbrBitrateMismatchToleranceKbps = 2.0;
    public const double BitrateMismatchBonusPoints = 15.0;
}
