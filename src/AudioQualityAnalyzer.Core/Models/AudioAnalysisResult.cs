namespace AudioQualityAnalyzer.Core.Models;

/// <summary>
/// Aggregate result of analyzing a single audio file. Extended incrementally as later
/// analysis phases (spectral, loudness, dynamics, clipping, stereo, transcoding, scoring)
/// are implemented — see 05-IMPLEMENTATION-PLAN.md.
/// </summary>
public sealed record AudioAnalysisResult
{
    public required AudioFileInfo FileInfo { get; init; }
    public required FormatInfo FormatInfo { get; init; }
    public required EncodingAnalysis EncodingAnalysis { get; init; }
    public required WaveformAnalysis WaveformAnalysis { get; init; }
}
