namespace AudioQualityAnalyzer.Core.Models;

public sealed record StereoCorrelationPoint
{
    public required TimeSpan Time { get; init; }
    public required double Correlation { get; init; }
}
