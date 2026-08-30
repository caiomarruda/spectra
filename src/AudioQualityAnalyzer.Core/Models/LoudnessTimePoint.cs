namespace AudioQualityAnalyzer.Core.Models;

public sealed record LoudnessTimePoint
{
    public required TimeSpan Time { get; init; }
    public required double MomentaryLufs { get; init; }
    public required double ShortTermLufs { get; init; }
}
