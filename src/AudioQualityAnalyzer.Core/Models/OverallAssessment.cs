namespace AudioQualityAnalyzer.Core.Models;

/// <summary>
/// 03-QUALITY-DETECTION.md: scoring never hides the underlying metrics — facts (the other
/// AudioAnalysisResult members) come first, this is the interpretation layer on top of them.
/// </summary>
public sealed record OverallAssessment
{
    public required double EncodingQualityScore { get; init; }
    public required double SpectralQualityScore { get; init; }
    public required double TechnicalQualityScore { get; init; }
    public required double MasteringQualityScore { get; init; }
    public required double OverallQualityScore { get; init; }

    /// <summary>A short human-readable summary, e.g. "GOOD 320 KBPS" or "320 KBPS / POSSIBLE TRANSCODE" (03-QUALITY-DETECTION.md "Casos obrigatórios").</summary>
    public required string Verdict { get; init; }

    /// <summary>All findings across every analysis stage, most severe first.</summary>
    public required IReadOnlyList<AnalysisFinding> Findings { get; init; }
}
