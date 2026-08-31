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
    public required SpectralAnalysis SpectralAnalysis { get; init; }
    public required LoudnessAnalysis LoudnessAnalysis { get; init; }
    public required DynamicRangeAnalysis DynamicRangeAnalysis { get; init; }
    public required ClippingAnalysis ClippingAnalysis { get; init; }

    /// <summary>Null for mono files — there is no stereo image to describe.</summary>
    public StereoAnalysis? StereoAnalysis { get; init; }

    public required TranscodingAnalysis TranscodingAnalysis { get; init; }
    public required NoiseAnalysis NoiseAnalysis { get; init; }
    public required OverallAssessment OverallAssessment { get; init; }

    /// <summary>
    /// Non-fatal caveats about this analysis — e.g. decoding stopped early because of a
    /// corrupted frame partway through the file, so every metric above reflects only the
    /// successfully decoded portion, not the whole track. Empty when nothing is amiss.
    /// </summary>
    public required IReadOnlyList<string> Warnings { get; init; }
}
