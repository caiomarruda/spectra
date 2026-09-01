namespace Spectra.Core.Models;

public sealed record SpectralBandEnergy
{
    public required string Label { get; init; }
    public required double LowHz { get; init; }
    public required double HighHz { get; init; }
    public required double AverageEnergyDb { get; init; }
}
