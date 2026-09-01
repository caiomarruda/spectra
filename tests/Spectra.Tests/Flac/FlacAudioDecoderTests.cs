using Spectra.Audio.Flac;
using Xunit;

namespace Spectra.Tests.Flac;

/// <summary>
/// Expected sample values below were captured once from ffmpeg's own FLAC decode of these exact
/// fixtures (`ffmpeg -i fixture.flac -f s16le/s32le ...`) during development, confirming this
/// from-scratch decoder is bit-exact — see FlacAudioDecoder's doc comment for why a from-scratch
/// implementation exists instead of a NuGet package. These tests don't shell out to ffmpeg
/// themselves; the fixtures are small (~80KB), 1-second, committed files.
/// </summary>
public class FlacAudioDecoderTests
{
    [Fact]
    public void Decode_Stereo16BitCompressionLevel0_MatchesKnownGoodSamples()
    {
        var decoded = new FlacAudioDecoder().Decode(FlacTestData.FindPath("stereo-16bit-cl0.flac"));

        Assert.Equal(44100, decoded.SampleRateHz);
        Assert.Equal(2, decoded.ChannelCount);
        Assert.Equal(44100, decoded.Channels[0].Length);
        Assert.Null(decoded.PartialDecodeReason);
        AssertFirstAndLastFrames(decoded);
    }

    [Fact]
    public void Decode_Stereo16BitCompressionLevel8_MatchesSameSamplesAsLevel0()
    {
        // Compression level only changes how the same audio is encoded (predictor order,
        // partitioning), never the decoded result — this exercises different LPC/partition code
        // paths than level 0 while expecting byte-identical output.
        var decoded = new FlacAudioDecoder().Decode(FlacTestData.FindPath("stereo-16bit-cl8.flac"));

        Assert.Equal(44100, decoded.Channels[0].Length);
        Assert.Null(decoded.PartialDecodeReason);
        AssertFirstAndLastFrames(decoded);
    }

    private static void AssertFirstAndLastFrames(Spectra.Core.Decoding.DecodedAudio decoded)
    {
        const float scale = 1 / 32768f;
        int[] expectedFirst = [-2424, -957, -1866, 885];
        int[] expectedLast = [1336, -692, 3036, 2360];

        for (var i = 0; i < expectedFirst.Length; i++)
        {
            Assert.Equal(expectedFirst[i] * scale, decoded.Channels[0][i], precision: 5);
            Assert.Equal(expectedFirst[i] * scale, decoded.Channels[1][i], precision: 5);
        }

        var n = decoded.Channels[0].Length;
        for (var i = 0; i < expectedLast.Length; i++)
        {
            var index = n - expectedLast.Length + i;
            Assert.Equal(expectedLast[i] * scale, decoded.Channels[0][index], precision: 5);
            Assert.Equal(expectedLast[i] * scale, decoded.Channels[1][index], precision: 5);
        }
    }

    [Fact]
    public void Decode_Mono16Bit_MatchesKnownGoodSamples()
    {
        var decoded = new FlacAudioDecoder().Decode(FlacTestData.FindPath("mono-16bit-cl8.flac"));

        Assert.Equal(44100, decoded.SampleRateHz);
        Assert.Equal(1, decoded.ChannelCount);
        Assert.Equal(44100, decoded.Channels[0].Length);
        Assert.Null(decoded.PartialDecodeReason);

        const float scale = 1 / 32768f;
        int[] expectedFirst = [-2424, -957, -1866, 885, 4519, 6412, 4610, 1592];
        int[] expectedLast = [-2192, -3475, -4813, -2248, 1336, -692, 3036, 2360];

        for (var i = 0; i < expectedFirst.Length; i++)
        {
            Assert.Equal(expectedFirst[i] * scale, decoded.Channels[0][i], precision: 5);
        }

        var n = decoded.Channels[0].Length;
        for (var i = 0; i < expectedLast.Length; i++)
        {
            Assert.Equal(expectedLast[i] * scale, decoded.Channels[0][n - expectedLast.Length + i], precision: 5);
        }
    }

    [Fact]
    public void Decode_Stereo24Bit_MatchesKnownGoodSamples()
    {
        var decoded = new FlacAudioDecoder().Decode(FlacTestData.FindPath("stereo-24bit-cl8.flac"));

        Assert.Equal(44100, decoded.SampleRateHz);
        Assert.Equal(2, decoded.ChannelCount);
        Assert.Null(decoded.PartialDecodeReason);

        const float scale = 1 / 8388608f;
        int[] expectedFirst = [-620544, -244992, -477696, 226560];
        int[] expectedLast = [342016, -177152, 777216, 604160];

        for (var i = 0; i < expectedFirst.Length; i++)
        {
            Assert.Equal(expectedFirst[i] * scale, decoded.Channels[0][i], precision: 5);
            Assert.Equal(expectedFirst[i] * scale, decoded.Channels[1][i], precision: 5);
        }

        var n = decoded.Channels[0].Length;
        for (var i = 0; i < expectedLast.Length; i++)
        {
            var index = n - expectedLast.Length + i;
            Assert.Equal(expectedLast[i] * scale, decoded.Channels[0][index], precision: 5);
            Assert.Equal(expectedLast[i] * scale, decoded.Channels[1][index], precision: 5);
        }
    }

    [Fact]
    public void Decode_TruncatedFile_ReturnsPartialDecodeWithReasonInsteadOfThrowing()
    {
        var originalBytes = File.ReadAllBytes(FlacTestData.FindPath("stereo-16bit-cl8.flac"));
        var truncated = originalBytes[..(originalBytes.Length * 3 / 5)];
        var path = Path.GetTempFileName() + ".flac";
        File.WriteAllBytes(path, truncated);

        try
        {
            var decoded = new FlacAudioDecoder().Decode(path);

            Assert.NotNull(decoded.PartialDecodeReason);
            Assert.True(decoded.Channels[0].Length > 0);
            Assert.True(decoded.Channels[0].Length < 44100);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Decode_NotAFlacFile_ThrowsInvalidDataException()
    {
        var path = Path.GetTempFileName() + ".flac";
        File.WriteAllBytes(path, new byte[100]);

        try
        {
            Assert.Throws<InvalidDataException>(() => new FlacAudioDecoder().Decode(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
