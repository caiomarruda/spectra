using System.Text;
using AudioQualityAnalyzer.Audio.Mp3;
using AudioQualityAnalyzer.Core.Enums;
using Xunit;

namespace AudioQualityAnalyzer.Tests.Mp3;

public class XingLameTagTests
{
    [Fact]
    public void TryRead_XingIdWithFramesAndBytesFlags_ParsesTotals()
    {
        var header = BuildStereoV1Header(out var frame, extraLength: 8 + 4 + 4);
        var tagOffset = 4 + 32; // header (no CRC) + stereo side info.

        WriteAscii(frame, tagOffset, "Xing");
        WriteUInt32BigEndian(frame, tagOffset + 4, 0x3); // FRAMES | BYTES flags.
        WriteUInt32BigEndian(frame, tagOffset + 8, 12345); // total frames.
        WriteUInt32BigEndian(frame, tagOffset + 12, 987654); // total bytes.

        var tag = XingLameTag.TryRead(frame, header);

        Assert.NotNull(tag);
        Assert.True(tag!.IsVbrTag);
        Assert.Equal(12345, tag.TotalFrames);
        Assert.Equal(987654, tag.TotalBytes);
    }

    [Fact]
    public void TryRead_InfoIdWithLameExtension_ParsesEncoderDelayAndPadding()
    {
        var header = BuildStereoV1Header(out var frame, extraLength: 8 + 21 + 3 + 4);
        var tagOffset = 4 + 32;

        WriteAscii(frame, tagOffset, "Info");
        WriteUInt32BigEndian(frame, tagOffset + 4, 0x0); // No frames/bytes/toc/quality fields.

        var lameOffset = tagOffset + 8;
        WriteAscii(frame, lameOffset, "LAME3.100");
        // Encoder Delay (576) and Padding (1152) packed as two 12-bit values across 3 bytes.
        var delayPaddingOffset = lameOffset + 21;
        frame[delayPaddingOffset] = 0x24;
        frame[delayPaddingOffset + 1] = 0x04;
        frame[delayPaddingOffset + 2] = 0x80;

        var tag = XingLameTag.TryRead(frame, header);

        Assert.NotNull(tag);
        Assert.False(tag!.IsVbrTag);
        Assert.Equal("LAME3.100", tag.EncoderVersion);
        Assert.Equal(576, tag.EncoderDelaySamples);
        Assert.Equal(1152, tag.EncoderPaddingSamples);
    }

    [Fact]
    public void TryRead_NoVbrTagPresent_ReturnsNull()
    {
        var header = BuildStereoV1Header(out var frame, extraLength: 16);
        WriteAscii(frame, 4 + 32, "JUNK");

        var tag = XingLameTag.TryRead(frame, header);

        Assert.Null(tag);
    }

    private static Mp3FrameHeader BuildStereoV1Header(out byte[] frame, int extraLength)
    {
        var headerBytes = Mp3TestDataBuilder.BuildHeaderBytes(
            MpegVersion.Version1, MpegLayer.LayerIII, bitrateIndex: 9, sampleRateIndex: 0,
            padding: false, channelMode: ChannelMode.Stereo);
        Mp3FrameHeader.TryParse(headerBytes, 0, out var header);

        frame = new byte[4 + 32 + extraLength];
        headerBytes.CopyTo(frame, 0);
        return header;
    }

    private static void WriteAscii(byte[] buffer, int offset, string value) =>
        Encoding.ASCII.GetBytes(value).CopyTo(buffer, offset);

    private static void WriteUInt32BigEndian(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }
}
