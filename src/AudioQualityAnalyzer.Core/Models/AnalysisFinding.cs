using AudioQualityAnalyzer.Core.Enums;

namespace AudioQualityAnalyzer.Core.Models;

/// <summary>
/// 03-QUALITY-DETECTION.md "Evidence System": every non-obvious conclusion the analyzer reaches
/// must carry the facts it was derived from, not just a verdict.
/// </summary>
public sealed record AnalysisFinding
{
    public required string Code { get; init; }
    public required string Title { get; init; }
    public required Severity Severity { get; init; }
    public required ConfidenceLevel Confidence { get; init; }
    public required string Description { get; init; }
    public required IReadOnlyList<string> Evidence { get; init; }
    public required IReadOnlyDictionary<string, double> Metrics { get; init; }
}
