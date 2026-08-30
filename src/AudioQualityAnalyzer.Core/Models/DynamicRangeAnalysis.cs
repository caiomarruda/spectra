namespace AudioQualityAnalyzer.Core.Models;

public sealed record DynamicRangeAnalysis
{
    public required double CrestFactorDb { get; init; }
    public required double RmsWindowMinDb { get; init; }
    public required double RmsWindowMaxDb { get; init; }
    public required double RmsWindowMedianDb { get; init; }
    public required double RmsWindowStdDevDb { get; init; }

    /// <summary>Percentage of samples within 1 dBFS of full scale — a "hot"/limited master, distinct from actual clipping (see <see cref="ClippingAnalysis"/>).</summary>
    public required double PercentSamplesNearFullScale { get; init; }
}
