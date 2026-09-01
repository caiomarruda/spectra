namespace Spectra.Analysis.Stereo;

public static class StereoSettings
{
    /// <summary>A channel more than this much quieter than the other is treated as effectively absent.</summary>
    public const double MissingChannelDb = 40.0;

    /// <summary>Balance beyond this is a severe (not merely stylistic) imbalance.</summary>
    public const double SevereImbalanceDb = 6.0;

    /// <summary>Correlation above this, combined with negligible side energy, indicates real mono content duplicated to both channels rather than an actual stereo mix.</summary>
    public const double MonoDisguiseCorrelationThreshold = 0.98;
    public const double MonoDisguiseSideToMidDb = -30.0;

    /// <summary>Correlation below this indicates meaningful out-of-phase content.</summary>
    public const double PhaseProblemCorrelationThreshold = -0.3;

    /// <summary>Correlation this close to -1 indicates near-total polarity inversion, almost always a mixing/encoding bug rather than an artistic choice.</summary>
    public const double PolarityInversionCorrelationThreshold = -0.95;

    /// <summary>Side energy this much louder than mid indicates unusually wide/lateral content (e.g. aggressive stereo widening).</summary>
    public const double ExcessiveSideContentDb = 6.0;
}
