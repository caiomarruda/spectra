using Spectra.Core.Enums;
using Spectra.Core.Models;

namespace Spectra.Audio.Aiff;

/// <summary>Same reasoning as WavMetadataReader: AIFF is uncompressed PCM, so "bitrate" is a nominal, always-constant figure, not a measured one.</summary>
public static class AiffMetadataReader
{
    public static (AudioFileInfo FileInfo, FormatInfo FormatInfo, EncodingAnalysis EncodingAnalysis) Read(string path)
    {
        var (fileInfo, formatInfo, encodingAnalysis) = Read(File.ReadAllBytes(path), path);
        return (fileInfo with { FullPath = Path.GetFullPath(path) }, formatInfo, encodingAnalysis);
    }

    /// <summary>
    /// <see cref="AudioFileInfo.FullPath"/> is set to <paramref name="fileName"/> verbatim here —
    /// there is no real filesystem path when called from a byte source with no disk file (e.g. the
    /// browser). The <see cref="Read(string)"/> overload above resolves the true full path itself.
    /// </summary>
    public static (AudioFileInfo FileInfo, FormatInfo FormatInfo, EncodingAnalysis EncodingAnalysis) Read(byte[] data, string fileName)
    {
        var aiff = AiffFileReader.Read(data, fileName);
        var duration = aiff.SampleRateHz > 0 ? TimeSpan.FromSeconds((double)aiff.SampleFrameCount / aiff.SampleRateHz) : TimeSpan.Zero;

        var nominalBitrateKbps = (int)Math.Round(aiff.SampleRateHz * aiff.BitsPerSample * aiff.ChannelCount / 1000.0);

        var fileInfo = new AudioFileInfo
        {
            FullPath = fileName,
            FileName = Path.GetFileName(fileName),
            Extension = Path.GetExtension(fileName),
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
