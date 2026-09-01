namespace Spectra.Core.Models;

public sealed record ClippingAnalysis
{
    public required long TotalClippedSamples { get; init; }
    public required double ClippedPercentage { get; init; }
    public required int ClipEventCount { get; init; }
    public required TimeSpan LongestClipDuration { get; init; }
    public required IReadOnlyList<long> ClippedSamplesPerChannel { get; init; }

    /// <summary>True when clipping is sustained rather than isolated single-sample events — the distinction the spec asks for between negligible and audible clipping.</summary>
    public required bool IsSevere { get; init; }
}
