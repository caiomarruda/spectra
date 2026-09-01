using Spectra.Audio.Mp3;
using Spectra.Core.Enums;
using Xunit;

namespace Spectra.Tests.Mp3;

public class Mp3FrameParserTests
{
    [Fact]
    public void Parse_ConstantBitrateStream_FindsAllFramesWithSameBitrate()
    {
        const int frameLength = 417; // MPEG1 Layer III, 128 kbps, 44100 Hz, no padding.
        var data = ConcatFrames(Enumerable.Repeat(
            Mp3TestDataBuilder.BuildFrame(MpegVersion.Version1, MpegLayer.LayerIII, bitrateIndex: 9, sampleRateIndex: 0, frameLength),
            50));

        var result = Mp3FrameParser.Parse(data);

        Assert.Equal(50, result.Frames.Count);
        Assert.All(result.Frames, f => Assert.Equal(128, f.BitrateKbps));
        Assert.All(result.Frames, f => Assert.Equal(44100, f.SampleRateHz));
    }

    [Fact]
    public void Parse_SkipsId3v2TagBeforeLocatingFirstFrame()
    {
        const int frameLength = 417;
        var id3Tag = Mp3TestDataBuilder.BuildId3v2Tag(200);
        var frames = ConcatFrames(Enumerable.Repeat(
            Mp3TestDataBuilder.BuildFrame(MpegVersion.Version1, MpegLayer.LayerIII, bitrateIndex: 9, sampleRateIndex: 0, frameLength),
            10));
        var data = new byte[id3Tag.Length + frames.Length];
        id3Tag.CopyTo(data, 0);
        frames.CopyTo(data, id3Tag.Length);

        var result = Mp3FrameParser.Parse(data);

        Assert.Equal(10, result.Frames.Count);
        Assert.Equal(id3Tag.Length, result.AudioStartOffset);
    }

    [Fact]
    public void Parse_VariableBitrateStream_ReportsMinAndMaxBitrates()
    {
        const int frameLength128 = 417;
        const int frameLength320 = 1044; // 144 * 320000 / 44100

        var data = ConcatFrames(
        [
            Mp3TestDataBuilder.BuildFrame(MpegVersion.Version1, MpegLayer.LayerIII, bitrateIndex: 9, sampleRateIndex: 0, frameLength128),
            Mp3TestDataBuilder.BuildFrame(MpegVersion.Version1, MpegLayer.LayerIII, bitrateIndex: 14, sampleRateIndex: 0, frameLength320),
            Mp3TestDataBuilder.BuildFrame(MpegVersion.Version1, MpegLayer.LayerIII, bitrateIndex: 9, sampleRateIndex: 0, frameLength128),
        ]);

        var result = Mp3FrameParser.Parse(data);

        Assert.Equal(3, result.Frames.Count);
        Assert.Equal(128, result.Frames.Min(f => f.BitrateKbps));
        Assert.Equal(320, result.Frames.Max(f => f.BitrateKbps));
    }

    [Fact]
    public void Parse_Mpeg2LayerIII_UsesHalvedSamplesPerFrame()
    {
        // MPEG2 Layer III, 64 kbps (index 8 in the V2/2.5 L2/3 table), 22050 Hz.
        const int frameLength = 208; // 72 * 64000 / 22050
        var data = Mp3TestDataBuilder.BuildFrame(MpegVersion.Version2, MpegLayer.LayerIII, bitrateIndex: 8, sampleRateIndex: 0, frameLength);
        var padded = ConcatFrames(Enumerable.Repeat(data, 3));

        var result = Mp3FrameParser.Parse(padded);

        Assert.Equal(3, result.Frames.Count);
        Assert.All(result.Frames, f => Assert.Equal(576, f.SamplesPerFrame));
        Assert.All(result.Frames, f => Assert.Equal(22050, f.SampleRateHz));
    }

    [Fact]
    public void Parse_TruncatedFinalFrame_StopsWithoutThrowing()
    {
        const int frameLength = 417;
        var fullFrames = ConcatFrames(Enumerable.Repeat(
            Mp3TestDataBuilder.BuildFrame(MpegVersion.Version1, MpegLayer.LayerIII, bitrateIndex: 9, sampleRateIndex: 0, frameLength),
            5));
        var truncated = fullFrames[..^100];

        var result = Mp3FrameParser.Parse(truncated);

        Assert.Equal(4, result.Frames.Count);
    }

    private static byte[] ConcatFrames(IEnumerable<byte[]> frames)
    {
        using var stream = new MemoryStream();
        foreach (var frame in frames)
        {
            stream.Write(frame);
        }
        return stream.ToArray();
    }
}
