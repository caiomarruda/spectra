using Spectra.Audio.Flac;
using Spectra.Core.Enums;
using Xunit;

namespace Spectra.Tests.Flac;

public class FlacMetadataReaderTests
{
    [Fact]
    public void Read_Stereo16BitFile_ReportsNominalAndMeasuredBitratesSeparately()
    {
        var (fileInfo, formatInfo, encoding) = FlacMetadataReader.Read(FlacTestData.FindPath("stereo-16bit-cl8.flac"));

        Assert.Equal("FLAC", formatInfo.Format);
        Assert.Equal(44100, formatInfo.SampleRateHz);
        Assert.Equal(2, formatInfo.Channels);
        Assert.Equal(ChannelMode.Stereo, formatInfo.ChannelMode);
        Assert.Equal(16, formatInfo.BitsPerSample);
        Assert.Equal(1.0, fileInfo.Duration.TotalSeconds, precision: 2);

        // Declared = nominal uncompressed PCM bitrate (44100 * 16 * 2 / 1000), never the
        // compressed rate — see FlacMetadataReader's doc comment for why this distinction matters
        // for TranscodingAnalyzer.
        Assert.Equal(1411, encoding.DeclaredBitrateKbps);
        Assert.True(encoding.AverageBitrateKbps < encoding.DeclaredBitrateKbps, "Measured (compressed) bitrate should be well below the nominal PCM rate.");
        Assert.Equal(BitrateMode.VariableBitRate, encoding.BitrateMode);
        Assert.True(encoding.FrameCount > 0);
        Assert.False(encoding.HasXingHeader);
    }

    [Fact]
    public void Read_MonoFile_ReportsMonoChannelMode()
    {
        var (_, formatInfo, _) = FlacMetadataReader.Read(FlacTestData.FindPath("mono-16bit-cl8.flac"));

        Assert.Equal(1, formatInfo.Channels);
        Assert.Equal(ChannelMode.Mono, formatInfo.ChannelMode);
    }

    [Fact]
    public void Read_24BitFile_ReportsBitDepth()
    {
        var (_, formatInfo, _) = FlacMetadataReader.Read(FlacTestData.FindPath("stereo-24bit-cl8.flac"));

        Assert.Equal(24, formatInfo.BitsPerSample);
    }

    [Fact]
    public void Read_NotAFlacFile_ThrowsInvalidDataException()
    {
        var path = Path.GetTempFileName() + ".flac";
        File.WriteAllBytes(path, new byte[100]);

        try
        {
            Assert.Throws<InvalidDataException>(() => FlacMetadataReader.Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
