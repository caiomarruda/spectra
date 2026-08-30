namespace AudioQualityAnalyzer.Core.Models;

/// <summary>Only produced for multi-channel audio — there is no stereo image to describe for mono files.</summary>
public sealed record StereoAnalysis
{
    /// <summary>Zero-lag L/R correlation in [-1, 1]: +1 identical (mono-compatible), 0 uncorrelated, -1 fully inverted.</summary>
    public required double CorrelationCoefficient { get; init; }

    /// <summary>Signed: positive means the right channel is louder.</summary>
    public required double ChannelBalanceDb { get; init; }

    /// <summary>RMS of the mono downmix relative to the average per-channel RMS. Near 1 = no loss summing to mono; near 0 = severe phase cancellation.</summary>
    public required double MonoCompatibilityRatio { get; init; }

    public required double MidEnergyDb { get; init; }
    public required double SideEnergyDb { get; init; }
    public required double SideToMidRatioDb { get; init; }

    public required bool IsChannelEffectivelyMissing { get; init; }
    public required bool IsSeverelyImbalanced { get; init; }
    public required bool IsMonoDisguisedAsStereo { get; init; }
    public required bool HasPhaseProblems { get; init; }
    public required bool HasPolarityInversion { get; init; }
    public required bool HasExcessiveSideContent { get; init; }
}
