using Spectra.Core.Enums;

namespace Spectra.Core.Models;

public sealed record FormatInfo
{
    public required string Format { get; init; }
    public required MpegVersion MpegVersion { get; init; }
    public required MpegLayer MpegLayer { get; init; }
    public required int SampleRateHz { get; init; }
    public required int Channels { get; init; }
    public required ChannelMode ChannelMode { get; init; }
    public string? Encoder { get; init; }
    public int? EncoderDelaySamples { get; init; }
    public int? PaddingSamples { get; init; }

    /// <summary>Bits per PCM sample as stored in the file. Null for lossy formats (MP3), where the decoded output has no inherent bit depth.</summary>
    public int? BitsPerSample { get; init; }
}
