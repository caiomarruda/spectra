using Spectra.Audio.Mp3;
using Spectra.Core.Enums;
using Xunit;

namespace Spectra.Tests.Mp3;

public class Mp3FrameHeaderTests
{
    [Fact]
    public void TryParse_Mpeg1LayerIII_128Kbps_44100Stereo_ParsesAllFields()
    {
        var bytes = Mp3TestDataBuilder.BuildHeaderBytes(
            MpegVersion.Version1, MpegLayer.LayerIII, bitrateIndex: 9, sampleRateIndex: 0,
            padding: false, channelMode: ChannelMode.Stereo);

        var parsed = Mp3FrameHeader.TryParse(bytes, offsetInFile: 0, out var header);

        Assert.True(parsed);
        Assert.Equal(MpegVersion.Version1, header.Version);
        Assert.Equal(MpegLayer.LayerIII, header.Layer);
        Assert.Equal(128, header.BitrateKbps);
        Assert.Equal(44100, header.SampleRateHz);
        Assert.Equal(ChannelMode.Stereo, header.ChannelMode);
        Assert.Equal(417, header.FrameLengthBytes);
        Assert.Equal(1152, header.SamplesPerFrame);
        Assert.Equal(32, header.SideInfoSize);
        Assert.False(header.HasCrc);
    }

    [Fact]
    public void TryParse_PaddingBitSet_AddsOneByteToFrameLength()
    {
        var withoutPadding = Mp3TestDataBuilder.BuildHeaderBytes(
            MpegVersion.Version1, MpegLayer.LayerIII, bitrateIndex: 9, sampleRateIndex: 0,
            padding: false, channelMode: ChannelMode.Stereo);
        var withPadding = Mp3TestDataBuilder.BuildHeaderBytes(
            MpegVersion.Version1, MpegLayer.LayerIII, bitrateIndex: 9, sampleRateIndex: 0,
            padding: true, channelMode: ChannelMode.Stereo);

        Mp3FrameHeader.TryParse(withoutPadding, 0, out var a);
        Mp3FrameHeader.TryParse(withPadding, 0, out var b);

        Assert.Equal(a.FrameLengthBytes + 1, b.FrameLengthBytes);
    }

    [Fact]
    public void TryParse_MonoChannel_UsesSmallerSideInfoSize()
    {
        var bytes = Mp3TestDataBuilder.BuildHeaderBytes(
            MpegVersion.Version1, MpegLayer.LayerIII, bitrateIndex: 9, sampleRateIndex: 0,
            padding: false, channelMode: ChannelMode.Mono);

        Mp3FrameHeader.TryParse(bytes, 0, out var header);

        Assert.Equal(17, header.SideInfoSize);
    }

    [Fact]
    public void TryParse_InvalidSyncByte_ReturnsFalse()
    {
        byte[] bytes = [0x00, 0xE0, 0x90, 0x00];

        var parsed = Mp3FrameHeader.TryParse(bytes, 0, out _);

        Assert.False(parsed);
    }

    [Fact]
    public void TryParse_ReservedBitrateIndex_ReturnsFalse()
    {
        var bytes = Mp3TestDataBuilder.BuildHeaderBytes(
            MpegVersion.Version1, MpegLayer.LayerIII, bitrateIndex: 15, sampleRateIndex: 0,
            padding: false, channelMode: ChannelMode.Stereo);

        var parsed = Mp3FrameHeader.TryParse(bytes, 0, out _);

        Assert.False(parsed);
    }

    [Fact]
    public void TryParse_FreeFormatBitrate_ReturnsFalse()
    {
        var bytes = Mp3TestDataBuilder.BuildHeaderBytes(
            MpegVersion.Version1, MpegLayer.LayerIII, bitrateIndex: 0, sampleRateIndex: 0,
            padding: false, channelMode: ChannelMode.Stereo);

        var parsed = Mp3FrameHeader.TryParse(bytes, 0, out _);

        Assert.False(parsed);
    }

    [Fact]
    public void TryParse_TooShortBuffer_ReturnsFalse()
    {
        byte[] bytes = [0xFF, 0xFB, 0x90];

        var parsed = Mp3FrameHeader.TryParse(bytes, 0, out _);

        Assert.False(parsed);
    }
}
