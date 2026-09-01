using Spectra.Audio.Flac;
using Xunit;

namespace Spectra.Tests.Flac;

public class FlacContainerReaderTests
{
    [Fact]
    public void Read_ThreeChannelStreamInfo_ThrowsInvalidDataException()
    {
        var bytes = FlacTestDataBuilder.BuildStreamInfoOnly(44100, channels: 3, bitsPerSample: 16, totalSamples: 0);
        var path = WriteTemp(bytes);

        try
        {
            Assert.Throws<InvalidDataException>(() => FlacContainerReader.Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_ValidStreamInfoNoFrames_ParsesFieldsAndLeavesAudioStartAtEndOfFile()
    {
        var bytes = FlacTestDataBuilder.BuildStreamInfoOnly(48000, channels: 2, bitsPerSample: 24, totalSamples: 12345);
        var path = WriteTemp(bytes);

        try
        {
            var file = FlacContainerReader.Read(path);

            Assert.Equal(48000, file.StreamInfo.SampleRateHz);
            Assert.Equal(2, file.StreamInfo.ChannelCount);
            Assert.Equal(24, file.StreamInfo.BitsPerSample);
            Assert.Equal(12345, file.StreamInfo.TotalSamples);
            Assert.Equal(bytes.Length, file.AudioStartOffset);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_MissingStreamMarker_ThrowsInvalidDataException()
    {
        var path = WriteTemp(new byte[100]);

        try
        {
            Assert.Throws<InvalidDataException>(() => FlacContainerReader.Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteTemp(byte[] bytes)
    {
        var path = Path.GetTempFileName() + ".flac";
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
