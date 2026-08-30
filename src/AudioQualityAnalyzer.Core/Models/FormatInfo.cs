using AudioQualityAnalyzer.Core.Enums;

namespace AudioQualityAnalyzer.Core.Models;

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
}
