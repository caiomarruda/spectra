using Spectra.Audio.Aiff;
using Xunit;

namespace Spectra.Tests.Aiff;

public class AiffAudioDecoderTests
{
    [Fact]
    public void Decode_16BitStereoPcm_ProducesExactNormalizedSamples()
    {
        // Big-endian interleaved stereo frames: (0, 16384), (-16384, 32767)
        var pcm = new byte[]
        {
            0x00, 0x00, 0x40, 0x00,
            0xC0, 0x00, 0x7F, 0xFF,
        };
        var bytes = AiffTestDataBuilder.Build(44100, channels: 2, bitsPerSample: 16, sampleFrameCount: 2, pcm);
        var path = WriteTemp(bytes);

        try
        {
            var decoded = new AiffAudioDecoder().Decode(path);

            Assert.Equal(44100, decoded.SampleRateHz);
            Assert.Equal(2, decoded.ChannelCount);
            Assert.Equal(2, decoded.Channels[0].Length);
            Assert.Equal(0f, decoded.Channels[0][0]);
            Assert.Equal(16384 / 32768f, decoded.Channels[1][0]);
            Assert.Equal(-16384 / 32768f, decoded.Channels[0][1]);
            Assert.Equal(32767 / 32768f, decoded.Channels[1][1]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Decode_8BitPcm_IsTreatedAsSigned()
    {
        // AIFF 8-bit PCM is signed, unlike WAV's unsigned convention.
        var pcm = new byte[] { 0x80, 0x00, 0x7F };
        var bytes = AiffTestDataBuilder.Build(8000, channels: 1, bitsPerSample: 8, sampleFrameCount: 3, pcm);
        var path = WriteTemp(bytes);

        try
        {
            var decoded = new AiffAudioDecoder().Decode(path);

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
    public void Decode_ThreeChannels_ThrowsInvalidDataException()
    {
        var bytes = AiffTestDataBuilder.Build(44100, channels: 3, bitsPerSample: 16, sampleFrameCount: 1, new byte[6]);
        var path = WriteTemp(bytes);

        try
        {
            Assert.Throws<InvalidDataException>(() => new AiffAudioDecoder().Decode(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Decode_AifcFormType_ThrowsInvalidDataException()
    {
        var bytes = AiffTestDataBuilder.Build(44100, channels: 1, bitsPerSample: 16, sampleFrameCount: 1, new byte[2], formType: "AIFC");
        var path = WriteTemp(bytes);

        try
        {
            Assert.Throws<InvalidDataException>(() => new AiffAudioDecoder().Decode(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteTemp(byte[] bytes)
    {
        var path = Path.GetTempFileName() + ".aiff";
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
