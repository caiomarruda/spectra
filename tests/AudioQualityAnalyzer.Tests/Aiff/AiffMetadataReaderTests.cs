using AudioQualityAnalyzer.Audio.Aiff;
using AudioQualityAnalyzer.Core.Enums;
using Xunit;

namespace AudioQualityAnalyzer.Tests.Aiff;

public class AiffMetadataReaderTests
{
    [Fact]
    public void Read_16BitStereoFile_ReportsNominalConstantBitrate()
    {
        const int frameCount = 44100;
        var pcm = new byte[frameCount * 2 * 2]; // 1 second, 16-bit stereo
        var bytes = AiffTestDataBuilder.Build(44100, channels: 2, bitsPerSample: 16, sampleFrameCount: frameCount, pcm);
        var path = Path.GetTempFileName() + ".aiff";
        File.WriteAllBytes(path, bytes);

        try
        {
            var (fileInfo, formatInfo, encoding) = AiffMetadataReader.Read(path);

            Assert.Equal("AIFF", formatInfo.Format);
            Assert.Equal(44100, formatInfo.SampleRateHz);
            Assert.Equal(2, formatInfo.Channels);
            Assert.Equal(ChannelMode.Stereo, formatInfo.ChannelMode);
            Assert.Equal(16, formatInfo.BitsPerSample);
            Assert.Equal(1411, encoding.DeclaredBitrateKbps);
            Assert.Equal(BitrateMode.ConstantBitRate, encoding.BitrateMode);
            Assert.Equal(1.0, fileInfo.Duration.TotalSeconds, precision: 3);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_NotAFormFile_ThrowsInvalidDataException()
    {
        var path = Path.GetTempFileName() + ".aiff";
        File.WriteAllBytes(path, new byte[100]);

        try
        {
            Assert.Throws<InvalidDataException>(() => AiffMetadataReader.Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
