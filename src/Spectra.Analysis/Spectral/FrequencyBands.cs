namespace Spectra.Analysis.Spectral;

/// <summary>The minimum band set required by 02-AUDIO-ANALYSIS-SPEC.md section 5.</summary>
public static class FrequencyBands
{
    public static readonly IReadOnlyList<(string Label, double LowHz, double HighHz)> Definitions =
    [
        ("0-20Hz", 0, 20),
        ("20-60Hz", 20, 60),
        ("60-120Hz", 60, 120),
        ("120-250Hz", 120, 250),
        ("250-500Hz", 250, 500),
        ("500Hz-1kHz", 500, 1_000),
        ("1-2kHz", 1_000, 2_000),
        ("2-4kHz", 2_000, 4_000),
        ("4-8kHz", 4_000, 8_000),
        ("8-12kHz", 8_000, 12_000),
        ("12-16kHz", 12_000, 16_000),
        ("16-18kHz", 16_000, 18_000),
        ("18-20kHz", 18_000, 20_000),
        ("20-22.05kHz", 20_000, 22_050),
    ];
}
