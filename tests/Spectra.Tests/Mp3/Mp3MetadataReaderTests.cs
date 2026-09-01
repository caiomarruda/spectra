using Spectra.Audio.Mp3;
using Spectra.Core.Enums;
using Xunit;

namespace Spectra.Tests.Mp3;

public class Mp3MetadataReaderTests
{
    [Fact]
    public void Read_ConstantBitrateFile_ReportsMatchingDeclaredAndAverageBitrate()
    {
        const int frameLength = 417; // 128 kbps, 44100 Hz.
        const int frameCount = 100;
        var frame = Mp3TestDataBuilder.BuildFrame(MpegVersion.Version1, MpegLayer.LayerIII, bitrateIndex: 9, sampleRateIndex: 0, frameLength);

        var path = Path.GetTempFileName();
        try
        {
            using (var stream = File.OpenWrite(path))
            {
                for (var i = 0; i < frameCount; i++)
                {
                    stream.Write(frame);
                }
            }

            var (fileInfo, formatInfo, encoding) = Mp3MetadataReader.Read(path);

            Assert.Equal(frameCount, encoding.FrameCount);
            Assert.Equal(128, encoding.DeclaredBitrateKbps);
            Assert.Equal(128, encoding.AverageBitrateKbps, precision: 0);
            Assert.Equal(BitrateMode.ConstantBitRate, encoding.BitrateMode);
            Assert.Equal(44100, formatInfo.SampleRateHz);
            Assert.Equal(2, formatInfo.Channels);
            Assert.Equal((long)frameCount * frameLength, fileInfo.SizeInBytes);

            var expectedDuration = TimeSpan.FromSeconds((double)frameCount * 1152 / 44100);
            Assert.Equal(expectedDuration.TotalSeconds, fileInfo.Duration.TotalSeconds, precision: 3);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_FileWithNoValidFrames_ThrowsInvalidDataException()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, new byte[100]);

            Assert.Throws<InvalidDataException>(() => Mp3MetadataReader.Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
