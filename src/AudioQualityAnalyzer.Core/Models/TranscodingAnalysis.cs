using AudioQualityAnalyzer.Core.Enums;

namespace AudioQualityAnalyzer.Core.Models;

/// <summary>
/// Never asserts historical origin with certainty — see 05-IMPLEMENTATION-PLAN.md "Critical Rule
/// About Conclusions". <see cref="Findings"/> carries the evidence the probability is based on.
/// </summary>
public sealed record TranscodingAnalysis
{
    public required double Probability { get; init; }
    public required TranscodingProbabilityLabel Label { get; init; }
    public required ConfidenceLevel Confidence { get; init; }
    public required IReadOnlyList<AnalysisFinding> Findings { get; init; }
}
