namespace Spectra.Analysis.Scoring;

/// <summary>
/// All scoring thresholds and weights in one place. These are reasoned defaults, not calibrated
/// against a reference dataset (05-IMPLEMENTATION-PLAN.md Phase 13/14 — "Nunca calibrar com
/// apenas uma música" — requires a multi-track original/transcoded reference collection this
/// environment does not have). Treat scores as a first, adjustable pass.
/// </summary>
public static class ScoringSettings
{
    /// <summary>Encoding-quality curve: Input = bitrate (kbps), Output = 0-100 score. Standard MP3 bitrate tiers.</summary>
    public static readonly IReadOnlyList<(double Input, double Output)> BitrateQualityCurve =
    [
        (32, 10), (64, 35), (96, 55), (128, 65), (160, 75), (192, 85), (224, 92), (256, 95), (320, 100),
    ];

    public const double BitrateMismatchPenalty = 20.0;

    /// <summary>Effective bandwidth at/above this is treated as "full" for scoring purposes.</summary>
    public const double FullBandwidthReferenceHz = 20_000;

    public const double SeverClippingPenalty = 30.0;
    public const double ClippingPenaltyPerPercent = 1000.0; // percentage points * this, capped below.
    public const double MaxMinorClippingPenalty = 20.0;

    public const double SevereImbalancePenalty = 15.0;
    public const double PolarityInversionPenalty = 30.0;
    public const double PhaseProblemPenalty = 15.0;
    public const double DcOffsetPenalty = 10.0;
    public const double ExcessiveSilencePenalty = 10.0;

    public const double HeavyLimitingCrestFactorDb = 6.0;
    public const double HeavyLimitingPenalty = 25.0;
    public const double ModerateLimitingCrestFactorDb = 8.0;
    public const double ModerateLimitingPenalty = 10.0;
    public const double TruePeakOverPenalty = 10.0;
    public const double LowLoudnessRangeLu = 3.0;
    public const double LowLoudnessRangePenalty = 10.0;

    public const double EncodingWeight = 0.30;
    public const double SpectralWeight = 0.25;
    public const double TechnicalWeight = 0.25;
    public const double MasteringWeight = 0.20;

    /// <summary>
    /// Gates the "GOOD" verdict on spectral/technical/mastering being clean — deliberately
    /// excludes the encoding (bitrate-tier) score, which by design scores a low bitrate lower
    /// regardless of whether anything is actually wrong with the file. A clean, un-transcoded
    /// 128 kbps encode is "good for what it claims to be", not "below average" just because its
    /// bitrate ceiling is lower than 320 kbps — conflating the two would contradict the app's own
    /// founding principle (01-PROJECT-OVERVIEW.md: "Não considerar 320 kbps = boa qualidade").
    /// </summary>
    public const double GoodComponentScoreThreshold = 70.0;

    public const double PoorMasteringScoreThreshold = 60.0;
    public const double LowLoudnessLufsThreshold = -20.0;
}
