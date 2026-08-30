namespace AudioQualityAnalyzer.Core.Models;

public sealed record WaveformAnalysis
{
    public required float PeakAmplitude { get; init; }
    public required float RmsAmplitude { get; init; }
    public required float MinSample { get; init; }
    public required float MaxSample { get; init; }
    public required TimeSpan LeadingSilence { get; init; }
    public required TimeSpan TrailingSilence { get; init; }
    public required IReadOnlyList<ChannelWaveformStats> PerChannel { get; init; }
    public required IReadOnlyList<RmsWindow> RmsOverTime { get; init; }
}
