using Spectra.Core.Enums;

namespace Spectra.Tests.Mp3;

/// <summary>
/// Builds synthetic MPEG frame headers for unit tests. Payload bytes are left as zeros —
/// header/frame-length tests only need valid headers, not decodable audio content.
/// </summary>
internal static class Mp3TestDataBuilder
{
    public static byte[] BuildHeaderBytes(
        MpegVersion version,
        MpegLayer layer,
        int bitrateIndex,
        int sampleRateIndex,
        bool padding,
        ChannelMode channelMode,
        bool hasCrc = false)
    {
        var versionBits = version switch
        {
            MpegVersion.Version1 => 0b11,
            MpegVersion.Version2 => 0b10,
            MpegVersion.Version2_5 => 0b00,
            _ => throw new ArgumentOutOfRangeException(nameof(version)),
        };
        var layerBits = layer switch
        {
            MpegLayer.LayerIII => 0b01,
            MpegLayer.LayerII => 0b10,
            MpegLayer.LayerI => 0b11,
            _ => throw new ArgumentOutOfRangeException(nameof(layer)),
        };
        var protectionBit = hasCrc ? 0 : 1;
        var channelModeBits = channelMode switch
        {
            ChannelMode.Stereo => 0b00,
            ChannelMode.JointStereo => 0b01,
            ChannelMode.DualChannel => 0b10,
            ChannelMode.Mono => 0b11,
            _ => throw new ArgumentOutOfRangeException(nameof(channelMode)),
        };

        var b1 = (byte)(0xE0 | (versionBits << 3) | (layerBits << 1) | protectionBit);
        var b2 = (byte)((bitrateIndex << 4) | (sampleRateIndex << 2) | ((padding ? 1 : 0) << 1));
        var b3 = (byte)(channelModeBits << 6);

        return [0xFF, b1, b2, b3];
    }

    public static byte[] BuildFrame(
        MpegVersion version,
        MpegLayer layer,
        int bitrateIndex,
        int sampleRateIndex,
        int frameLengthBytes,
        bool padding = false,
        ChannelMode channelMode = ChannelMode.Stereo)
    {
        var frame = new byte[frameLengthBytes];
        var header = BuildHeaderBytes(version, layer, bitrateIndex, sampleRateIndex, padding, channelMode);
        header.CopyTo(frame, 0);
        return frame;
    }

    public static byte[] BuildId3v2Tag(int payloadSize)
    {
        var tag = new byte[10 + payloadSize];
        tag[0] = (byte)'I';
        tag[1] = (byte)'D';
        tag[2] = (byte)'3';
        tag[3] = 4; // version
        tag[4] = 0; // revision
        tag[5] = 0; // flags (no footer)
        tag[6] = (byte)((payloadSize >> 21) & 0x7F);
        tag[7] = (byte)((payloadSize >> 14) & 0x7F);
        tag[8] = (byte)((payloadSize >> 7) & 0x7F);
        tag[9] = (byte)(payloadSize & 0x7F);
        return tag;
    }
}
