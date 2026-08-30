namespace AudioQualityAnalyzer.Core.Models;

public sealed record BatchTrackResult
{
    public required string RelativePath { get; init; }
    public required AudioAnalysisResult Result { get; init; }
}
