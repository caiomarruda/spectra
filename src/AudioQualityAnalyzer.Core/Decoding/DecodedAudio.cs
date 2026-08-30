namespace AudioQualityAnalyzer.Core.Decoding;

/// <summary>
/// De-interleaved PCM audio ready for signal analysis. Each entry in <see cref="Channels"/>
/// holds one channel's samples in the [-1, 1] range, all of equal length.
/// </summary>
public sealed record DecodedAudio
{
    public required int SampleRateHz { get; init; }
    public required int ChannelCount { get; init; }
    public required IReadOnlyList<float[]> Channels { get; init; }
    public required string DecoderName { get; init; }
    public required string? DecoderVersion { get; init; }
    public required int SourceSampleRateHz { get; init; }
}
