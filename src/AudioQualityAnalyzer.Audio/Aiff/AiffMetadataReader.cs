using AudioQualityAnalyzer.Core.Enums;
using AudioQualityAnalyzer.Core.Models;

namespace AudioQualityAnalyzer.Audio.Aiff;

/// <summary>Same reasoning as WavMetadataReader: AIFF is uncompressed PCM, so "bitrate" is a nominal, always-constant figure, not a measured one.</summary>
public static class AiffMetadataReader
{
    public static (AudioFileInfo FileInfo, FormatInfo FormatInfo, EncodingAnalysis EncodingAnalysis) Read(string path)
    {
        var aiff = AiffFileReader.Read(path);
        var duration = aiff.SampleRateHz > 0 ? TimeSpan.FromSeconds((double)aiff.SampleFrameCount / aiff.SampleRateHz) : TimeSpan.Zero;

        var nominalBitrateKbps = (int)Math.Round(aiff.SampleRateHz * aiff.BitsPerSample * aiff.ChannelCount / 1000.0);

        var fileInfo = new AudioFileInfo
        {
            FullPath = Path.GetFullPath(path),
            FileName = Path.GetFileName(path),
            Extension = Path.GetExtension(path),
            SizeInBytes = aiff.FileBytes.LongLength,
            Duration = duration,
        };

        var formatInfo = new FormatInfo
        {
            Format = "AIFF",
            MpegVersion = MpegVersion.Unknown,
            MpegLayer = MpegLayer.Unknown,
            SampleRateHz = aiff.SampleRateHz,
            Channels = aiff.ChannelCount,
            ChannelMode = aiff.ChannelCount == 1 ? ChannelMode.Mono : ChannelMode.Stereo,
            BitsPerSample = aiff.BitsPerSample,
        };

        var encodingAnalysis = new EncodingAnalysis
        {
            DeclaredBitrateKbps = nominalBitrateKbps,
            AverageBitrateKbps = nominalBitrateKbps,
            MinimumBitrateKbps = nominalBitrateKbps,
            MaximumBitrateKbps = nominalBitrateKbps,
            BitrateMode = BitrateMode.ConstantBitRate,
            FrameCount = aiff.SampleFrameCount,
            HasXingHeader = false,
            HasLameTag = false,
        };

        return (fileInfo, formatInfo, encodingAnalysis);
    }
}
