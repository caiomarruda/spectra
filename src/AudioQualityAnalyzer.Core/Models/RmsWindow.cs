namespace AudioQualityAnalyzer.Core.Models;

public sealed record RmsWindow
{
    public required TimeSpan StartTime { get; init; }
    public required float Rms { get; init; }
    public required float Peak { get; init; }
}
