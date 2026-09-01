using Spectra.Core.Enums;
using Spectra.Core.Models;

namespace Spectra.Audio.Flac;

/// <summary>
/// FLAC is lossless but compressed, unlike WAV/AIFF's always-exactly-constant PCM bitrate.
/// <see cref="EncodingAnalysis.DeclaredBitrateKbps"/> is the nominal *uncompressed* PCM-equivalent
/// bitrate (sample rate × bit depth × channels) — deliberately not the compressed rate, so
/// TranscodingAnalyzer's expected-bandwidth lookup (clamped at its 320 kbps top tier either way)
/// correctly treats this as "expect full lossless bandwidth" and flags any spectral cutoff as
/// evidence of a prior lossy encode, which is the most useful check for a lossless file.
/// <see cref="EncodingAnalysis.AverageBitrateKbps"/> is the actual measured compressed bitrate.
/// BitrateMode is deliberately VariableBitRate, not ConstantBitRate — those two figures normally
/// differ by a lot (compression), and ConstantBitRate would make TranscodingAnalyzer's
/// declared-vs-average mismatch check fire on every single FLAC file for no real reason.
/// </summary>
public static class FlacMetadataReader
{
    public static (AudioFileInfo FileInfo, FormatInfo FormatInfo, EncodingAnalysis EncodingAnalysis) Read(string path)
    {
        var file = FlacContainerReader.Read(path);
        var (frameCount, totalSamplesWalked) = FlacFrameWalker.CountFrames(file);

        var totalSamples = file.StreamInfo.TotalSamples > 0 ? file.StreamInfo.TotalSamples : totalSamplesWalked;
        var duration = file.StreamInfo.SampleRateHz > 0
            ? TimeSpan.FromSeconds((double)totalSamples / file.StreamInfo.SampleRateHz)
            : TimeSpan.Zero;

        var nominalBitrateKbps = (int)Math.Round(file.StreamInfo.SampleRateHz * file.StreamInfo.BitsPerSample * file.StreamInfo.ChannelCount / 1000.0);
        var averageBitrateKbps = duration.TotalSeconds > 0
            ? file.FileBytes.LongLength * 8 / duration.TotalSeconds / 1000.0
            : 0;

        var fileInfo = new AudioFileInfo
        {
            FullPath = Path.GetFullPath(path),
            FileName = Path.GetFileName(path),
            Extension = Path.GetExtension(path),
            SizeInBytes = file.FileBytes.LongLength,
            Duration = duration,
        };

        var formatInfo = new FormatInfo
        {
            Format = "FLAC",
            MpegVersion = MpegVersion.Unknown,
            MpegLayer = MpegLayer.Unknown,
            SampleRateHz = file.StreamInfo.SampleRateHz,
            Channels = file.StreamInfo.ChannelCount,
            ChannelMode = file.StreamInfo.ChannelCount == 1 ? ChannelMode.Mono : ChannelMode.Stereo,
            BitsPerSample = file.StreamInfo.BitsPerSample,
        };

        var encodingAnalysis = new EncodingAnalysis
        {
            DeclaredBitrateKbps = nominalBitrateKbps,
            AverageBitrateKbps = averageBitrateKbps,
            MinimumBitrateKbps = (int)Math.Round(averageBitrateKbps),
            MaximumBitrateKbps = (int)Math.Round(averageBitrateKbps),
            BitrateMode = BitrateMode.VariableBitRate,
            FrameCount = frameCount,
            HasXingHeader = false,
            HasLameTag = false,
        };

        return (fileInfo, formatInfo, encodingAnalysis);
    }
}
