namespace AudioQualityAnalyzer.Core.Models;

public sealed record AudioFileInfo
{
    public required string FullPath { get; init; }
    public required string FileName { get; init; }
    public required string Extension { get; init; }
    public required long SizeInBytes { get; init; }
    public required TimeSpan Duration { get; init; }
}
