using Spectra.Core.Enums;

namespace Spectra.Core.Models;

/// <summary>
/// Declared bitrate comes from the first frame header (or the Xing/Info header when present).
/// Average bitrate is measured directly from total_audio_bits / audio_duration, per spec section 2.1 —
/// it must never be assumed equal to the declared value.
/// </summary>
public sealed record EncodingAnalysis
{
    public required int DeclaredBitrateKbps { get; init; }
    public required double AverageBitrateKbps { get; init; }
    public required int MinimumBitrateKbps { get; init; }
    public required int MaximumBitrateKbps { get; init; }
    public required BitrateMode BitrateMode { get; init; }
    public required int FrameCount { get; init; }
    public required bool HasXingHeader { get; init; }
    public required bool HasLameTag { get; init; }
}
