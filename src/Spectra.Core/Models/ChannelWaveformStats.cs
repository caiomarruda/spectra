namespace Spectra.Core.Models;

public sealed record ChannelWaveformStats
{
    public required int ChannelIndex { get; init; }
    public required float Peak { get; init; }
    public required float Rms { get; init; }
    public required float MinSample { get; init; }
    public required float MaxSample { get; init; }
}
