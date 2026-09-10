using Spectra.Audio.Aiff;
using Spectra.Audio.Decoding;
using Spectra.Audio.Flac;
using Spectra.Audio.Mp3;
using Spectra.Audio.Wav;
using Spectra.Core.Decoding;
using Spectra.Tests.Aiff;
using Spectra.Tests.Flac;
using Spectra.Tests.TestSupport;
using Spectra.Tests.Wav;
using Xunit;

namespace Spectra.Tests.Web;

/// <summary>
/// Spectra.Web calls the byte[]-based Read/Decode overloads added to every reader/decoder (see
/// Spectra.Audio) so it never touches File.ReadAllBytes, which doesn't exist in a browser. These
/// tests lock in that the byte[] overload produces byte-identical output to the existing
/// path-based overload it was split out of, for one representative fixture per format.
/// </summary>
public class ByteOverloadParityTests
{
    [Fact]
    public void Mp3_ByteOverloads_MatchPathOverloads()
    {
        var path = ReferenceDataset.FindPath("mp3-320/track-320.mp3");
        var data = File.ReadAllBytes(path);

        var (fileInfoFromPath, formatInfoFromPath, encodingFromPath) = Mp3MetadataReader.Read(path);
        var (fileInfoFromBytes, formatInfoFromBytes, encodingFromBytes) = Mp3MetadataReader.Read(data, path);

        Assert.Equal(fileInfoFromPath with { FullPath = fileInfoFromBytes.FullPath }, fileInfoFromBytes);
        Assert.Equal(formatInfoFromPath, formatInfoFromBytes);
        Assert.Equal(encodingFromPath, encodingFromBytes);

        AssertDecodedAudioEqual(new NLayerAudioDecoder().Decode(path), NLayerAudioDecoder.Decode(data, path));
    }

    [Fact]
    public void Wav_ByteOverloads_MatchPathOverloads()
    {
        var pcm = new byte[]
        {
            0x00, 0x00, 0x00, 0x40,
            0x00, 0xC0, 0xFF, 0x7F,
            0x00, 0x80, 0x01, 0x00,
        };
        var data = WavTestDataBuilder.Build(44100, channels: 2, bitsPerSample: 16, audioFormat: 1, pcm);
        var path = WriteTemp(data, ".wav");

        try
        {
            var (fileInfoFromPath, formatInfoFromPath, encodingFromPath) = WavMetadataReader.Read(path);
            var (fileInfoFromBytes, formatInfoFromBytes, encodingFromBytes) = WavMetadataReader.Read(data, path);

            Assert.Equal(fileInfoFromPath with { FullPath = fileInfoFromBytes.FullPath }, fileInfoFromBytes);
            Assert.Equal(formatInfoFromPath, formatInfoFromBytes);
            Assert.Equal(encodingFromPath, encodingFromBytes);

            AssertDecodedAudioEqual(new WavAudioDecoder().Decode(path), WavAudioDecoder.Decode(data, path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Aiff_ByteOverloads_MatchPathOverloads()
    {
        var pcm = new byte[]
        {
            0x00, 0x00, 0x00, 0x40,
            0x00, 0xC0, 0x7F, 0xFF,
        };
        var data = AiffTestDataBuilder.Build(44100, channels: 2, bitsPerSample: 16, sampleFrameCount: 2, pcm);
        var path = WriteTemp(data, ".aiff");

        try
        {
            var (fileInfoFromPath, formatInfoFromPath, encodingFromPath) = AiffMetadataReader.Read(path);
            var (fileInfoFromBytes, formatInfoFromBytes, encodingFromBytes) = AiffMetadataReader.Read(data, path);

            Assert.Equal(fileInfoFromPath with { FullPath = fileInfoFromBytes.FullPath }, fileInfoFromBytes);
            Assert.Equal(formatInfoFromPath, formatInfoFromBytes);
            Assert.Equal(encodingFromPath, encodingFromBytes);

            AssertDecodedAudioEqual(new AiffAudioDecoder().Decode(path), AiffAudioDecoder.Decode(data, path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Flac_ByteOverloads_MatchPathOverloads()
    {
        var path = FlacTestData.FindPath("stereo-16bit-cl0.flac");
        var data = File.ReadAllBytes(path);

        var (fileInfoFromPath, formatInfoFromPath, encodingFromPath) = FlacMetadataReader.Read(path);
        var (fileInfoFromBytes, formatInfoFromBytes, encodingFromBytes) = FlacMetadataReader.Read(data, path);

        Assert.Equal(fileInfoFromPath with { FullPath = fileInfoFromBytes.FullPath }, fileInfoFromBytes);
        Assert.Equal(formatInfoFromPath, formatInfoFromBytes);
        Assert.Equal(encodingFromPath, encodingFromBytes);

        AssertDecodedAudioEqual(new FlacAudioDecoder().Decode(path), FlacAudioDecoder.Decode(data, path));
    }

    // DecodedAudio is a record, but Channels (IReadOnlyList<float[]>) uses default reference
    // equality for each array under the record's generated Equals — two structurally-identical
    // arrays from separate decode calls would wrongly compare unequal via Assert.Equal(a, b) on
    // the whole record. Compare field-by-field instead, using Assert.Equal directly on each
    // float[] pair, which does the element-wise comparison actually wanted here.
    private static void AssertDecodedAudioEqual(DecodedAudio expected, DecodedAudio actual)
    {
        Assert.Equal(expected.SampleRateHz, actual.SampleRateHz);
        Assert.Equal(expected.ChannelCount, actual.ChannelCount);
        Assert.Equal(expected.DecoderName, actual.DecoderName);
        Assert.Equal(expected.DecoderVersion, actual.DecoderVersion);
        Assert.Equal(expected.SourceSampleRateHz, actual.SourceSampleRateHz);
        Assert.Equal(expected.PartialDecodeReason, actual.PartialDecodeReason);
        Assert.Equal(expected.Channels.Count, actual.Channels.Count);
        for (var c = 0; c < expected.Channels.Count; c++)
        {
            Assert.Equal(expected.Channels[c], actual.Channels[c]);
        }
    }

    private static string WriteTemp(byte[] bytes, string extension)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{extension}");
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
