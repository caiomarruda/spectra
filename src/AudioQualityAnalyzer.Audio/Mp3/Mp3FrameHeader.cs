using AudioQualityAnalyzer.Core.Enums;

namespace AudioQualityAnalyzer.Audio.Mp3;

internal readonly record struct Mp3FrameHeader
{
    public required long OffsetInFile { get; init; }
    public required MpegVersion Version { get; init; }
    public required MpegLayer Layer { get; init; }
    public required bool HasCrc { get; init; }
    public required int BitrateKbps { get; init; }
    public required int SampleRateHz { get; init; }
    public required bool Padding { get; init; }
    public required ChannelMode ChannelMode { get; init; }
    public required int FrameLengthBytes { get; init; }
    public required int SamplesPerFrame { get; init; }
    public required int SideInfoSize { get; init; }

    public int HeaderAndCrcSize => HasCrc ? 6 : 4;

    public static bool TryParse(ReadOnlySpan<byte> buffer, long offsetInFile, out Mp3FrameHeader header)
    {
        header = default;

        if (buffer.Length < 4)
        {
            return false;
        }

        // Sync word: 11 bits set.
        if (buffer[0] != 0xFF || (buffer[1] & 0xE0) != 0xE0)
        {
            return false;
        }

        var versionBits = (buffer[1] >> 3) & 0x03;
        var version = versionBits switch
        {
            0b00 => MpegVersion.Version2_5,
            0b10 => MpegVersion.Version2,
            0b11 => MpegVersion.Version1,
            _ => MpegVersion.Unknown, // 0b01 reserved
        };
        if (version == MpegVersion.Unknown)
        {
            return false;
        }

        var layerBits = (buffer[1] >> 1) & 0x03;
        var layer = layerBits switch
        {
            0b01 => MpegLayer.LayerIII,
            0b10 => MpegLayer.LayerII,
            0b11 => MpegLayer.LayerI,
            _ => MpegLayer.Unknown, // 0b00 reserved
        };
        if (layer == MpegLayer.Unknown)
        {
            return false;
        }

        var hasCrc = (buffer[1] & 0x01) == 0;

        var bitrateIndex = (buffer[2] >> 4) & 0x0F;
        var bitrateKbps = Mp3Tables.GetBitrateKbps(version, layer, bitrateIndex);
        if (bitrateKbps < 0)
        {
            return false;
        }

        var sampleRateIndex = (buffer[2] >> 2) & 0x03;
        var sampleRateHz = Mp3Tables.GetSampleRateHz(version, sampleRateIndex);
        if (sampleRateHz < 0)
        {
            return false;
        }

        var padding = ((buffer[2] >> 1) & 0x01) != 0;

        var channelModeBits = (buffer[3] >> 6) & 0x03;
        var channelMode = channelModeBits switch
        {
            0b00 => ChannelMode.Stereo,
            0b01 => ChannelMode.JointStereo,
            0b10 => ChannelMode.DualChannel,
            0b11 => ChannelMode.Mono,
            _ => ChannelMode.Unknown,
        };

        if (bitrateKbps == 0)
        {
            // Free format: frame length cannot be derived from the header alone.
            return false;
        }

        var frameLength = Mp3Tables.GetFrameLengthBytes(version, layer, bitrateKbps, sampleRateHz, padding);
        if (frameLength < 4)
        {
            return false;
        }

        header = new Mp3FrameHeader
        {
            OffsetInFile = offsetInFile,
            Version = version,
            Layer = layer,
            HasCrc = hasCrc,
            BitrateKbps = bitrateKbps,
            SampleRateHz = sampleRateHz,
            Padding = padding,
            ChannelMode = channelMode,
            FrameLengthBytes = frameLength,
            SamplesPerFrame = Mp3Tables.GetSamplesPerFrame(version, layer),
            SideInfoSize = Mp3Tables.GetSideInfoSize(version, channelMode),
        };
        return true;
    }
}
