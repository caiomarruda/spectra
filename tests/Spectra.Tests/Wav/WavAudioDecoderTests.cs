using Spectra.Audio.Wav;
using Xunit;

namespace Spectra.Tests.Wav;

public class WavAudioDecoderTests
{
    [Fact]
    public void Decode_16BitStereoPcm_ProducesExactNormalizedSamples()
    {
        // Interleaved stereo frames: (0, 16384), (-16384, 32767), (-32768, 1)
        var pcm = new byte[]
        {
            0x00, 0x00, 0x00, 0x40,
            0x00, 0xC0, 0xFF, 0x7F,
            0x00, 0x80, 0x01, 0x00,
        };
        var bytes = WavTestDataBuilder.Build(44100, channels: 2, bitsPerSample: 16, audioFormat: 1, pcm);
        var path = WriteTemp(bytes, ".wav");

        try
        {
            var decoded = new WavAudioDecoder().Decode(path);

            Assert.Equal(44100, decoded.SampleRateHz);
            Assert.Equal(2, decoded.ChannelCount);
            Assert.Equal(3, decoded.Channels[0].Length);
            Assert.Equal(0f, decoded.Channels[0][0]);
            Assert.Equal(16384 / 32768f, decoded.Channels[1][0]);
            Assert.Equal(-16384 / 32768f, decoded.Channels[0][1]);
            Assert.Equal(32767 / 32768f, decoded.Channels[1][1]);
            Assert.Equal(-32768 / 32768f, decoded.Channels[0][2]);
            Assert.Equal(1 / 32768f, decoded.Channels[1][2]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Decode_8BitPcm_IsTreatedAsUnsigned()
    {
        // 8-bit WAV PCM is unsigned; 0 => -1.0, 128 => 0.0, 255 => ~+1.0.
        var pcm = new byte[] { 0, 128, 255 };
        var bytes = WavTestDataBuilder.Build(8000, channels: 1, bitsPerSample: 8, audioFormat: 1, pcm);
        var path = WriteTemp(bytes, ".wav");

        try
        {
            var decoded = new WavAudioDecoder().Decode(path);

            Assert.Equal(-1f, decoded.Channels[0][0]);
            Assert.Equal(0f, decoded.Channels[0][1]);
            Assert.Equal(127 / 128f, decoded.Channels[0][2]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Decode_32BitFloatPcm_PassesValuesThrough()
    {
        var pcm = new byte[8];
        BitConverter.GetBytes(0.5f).CopyTo(pcm, 0);
        BitConverter.GetBytes(-0.25f).CopyTo(pcm, 4);
        var bytes = WavTestDataBuilder.Build(48000, channels: 1, bitsPerSample: 32, audioFormat: 3, pcm);
        var path = WriteTemp(bytes, ".wav");

        try
        {
            var decoded = new WavAudioDecoder().Decode(path);

            Assert.Equal(0.5f, decoded.Channels[0][0]);
            Assert.Equal(-0.25f, decoded.Channels[0][1]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Decode_ChunksBeforeFmt_AreSkippedCorrectly()
    {
        var pcm = new byte[] { 0x00, 0x40 }; // one mono 16-bit sample
        var listChunk = WavTestDataBuilder.BuildListChunk("hello world");
        var bytes = WavTestDataBuilder.Build(44100, channels: 1, bitsPerSample: 16, audioFormat: 1, pcm, extraChunkBeforeFmt: listChunk);
        var path = WriteTemp(bytes, ".wav");

        try
        {
            var decoded = new WavAudioDecoder().Decode(path);

            Assert.Single(decoded.Channels[0]);
            Assert.Equal(16384 / 32768f, decoded.Channels[0][0]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Decode_UnsupportedAudioFormatCode_ThrowsInvalidDataException()
    {
        var bytes = WavTestDataBuilder.Build(44100, channels: 1, bitsPerSample: 16, audioFormat: 6 /* A-law */, [0, 0]);
        var path = WriteTemp(bytes, ".wav");

        try
        {
            Assert.Throws<InvalidDataException>(() => new WavAudioDecoder().Decode(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Decode_ThreeChannels_ThrowsInvalidDataException()
    {
        var bytes = WavTestDataBuilder.Build(44100, channels: 3, bitsPerSample: 16, audioFormat: 1, new byte[6]);
        var path = WriteTemp(bytes, ".wav");

        try
        {
            Assert.Throws<InvalidDataException>(() => new WavAudioDecoder().Decode(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteTemp(byte[] bytes, string extension)
    {
        var path = Path.GetTempFileName() + extension;
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
