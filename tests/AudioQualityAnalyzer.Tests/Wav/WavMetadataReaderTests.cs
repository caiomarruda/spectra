using AudioQualityAnalyzer.Audio.Wav;
using AudioQualityAnalyzer.Core.Enums;
using Xunit;

namespace AudioQualityAnalyzer.Tests.Wav;

public class WavMetadataReaderTests
{
    [Fact]
    public void Read_16BitStereoFile_ReportsNominalConstantBitrate()
    {
        var pcm = new byte[44100 * 2 * 2]; // 1 second, 16-bit stereo
        var bytes = WavTestDataBuilder.Build(44100, channels: 2, bitsPerSample: 16, audioFormat: 1, pcm);
        var path = Path.GetTempFileName() + ".wav";
        File.WriteAllBytes(path, bytes);

        try
        {
            var (fileInfo, formatInfo, encoding) = WavMetadataReader.Read(path);

            Assert.Equal("WAV", formatInfo.Format);
            Assert.Equal(44100, formatInfo.SampleRateHz);
            Assert.Equal(2, formatInfo.Channels);
            Assert.Equal(ChannelMode.Stereo, formatInfo.ChannelMode);
            Assert.Equal(16, formatInfo.BitsPerSample);
            Assert.Equal(1411, encoding.DeclaredBitrateKbps);
            Assert.Equal(1411, encoding.AverageBitrateKbps, precision: 0);
            Assert.Equal(BitrateMode.ConstantBitRate, encoding.BitrateMode);
            Assert.False(encoding.HasXingHeader);
            Assert.Equal(1.0, fileInfo.Duration.TotalSeconds, precision: 3);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_MonoFile_ReportsMonoChannelMode()
    {
        var bytes = WavTestDataBuilder.Build(8000, channels: 1, bitsPerSample: 16, audioFormat: 1, new byte[16]);
        var path = Path.GetTempFileName() + ".wav";
        File.WriteAllBytes(path, bytes);

        try
        {
            var (_, formatInfo, _) = WavMetadataReader.Read(path);

            Assert.Equal(1, formatInfo.Channels);
            Assert.Equal(ChannelMode.Mono, formatInfo.ChannelMode);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_NotARiffFile_ThrowsInvalidDataException()
    {
        var path = Path.GetTempFileName() + ".wav";
        File.WriteAllBytes(path, new byte[100]);

        try
        {
            Assert.Throws<InvalidDataException>(() => WavMetadataReader.Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
