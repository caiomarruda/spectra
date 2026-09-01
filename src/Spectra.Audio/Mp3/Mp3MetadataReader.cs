using Spectra.Core.Enums;
using Spectra.Core.Models;

namespace Spectra.Audio.Mp3;

public static class Mp3MetadataReader
{
    public static (AudioFileInfo FileInfo, FormatInfo FormatInfo, EncodingAnalysis EncodingAnalysis) Read(string path)
    {
        var data = File.ReadAllBytes(path);
        var parseResult = Mp3FrameParser.Parse(data);
        var frames = parseResult.Frames;

        if (frames.Count == 0)
        {
            throw new InvalidDataException($"No valid MPEG audio frames found in '{path}'.");
        }

        var first = frames[0];
        var totalSamples = frames.Sum(f => (long)f.SamplesPerFrame);
        var duration = TimeSpan.FromSeconds((double)totalSamples / first.SampleRateHz);

        var totalAudioBits = frames.Sum(f => (long)f.FrameLengthBytes) * 8L;
        var averageBitrateKbps = duration.TotalSeconds > 0
            ? totalAudioBits / duration.TotalSeconds / 1000.0
            : 0;

        var minBitrate = frames.Min(f => f.BitrateKbps);
        var maxBitrate = frames.Max(f => f.BitrateKbps);

        var vbrTag = parseResult.VbrTag;
        var isLame = vbrTag?.EncoderVersion?.StartsWith("LAME", StringComparison.OrdinalIgnoreCase) == true;

        var bitrateMode = DetermineBitrateMode(minBitrate, maxBitrate, vbrTag, isLame);

        var fileInfo = new AudioFileInfo
        {
            FullPath = Path.GetFullPath(path),
            FileName = Path.GetFileName(path),
            Extension = Path.GetExtension(path),
            SizeInBytes = data.LongLength,
            Duration = duration,
        };

        var formatInfo = new FormatInfo
        {
            Format = "MP3",
            MpegVersion = first.Version,
            MpegLayer = first.Layer,
            SampleRateHz = first.SampleRateHz,
            Channels = first.ChannelMode == ChannelMode.Mono ? 1 : 2,
            ChannelMode = first.ChannelMode,
            Encoder = vbrTag?.EncoderVersion,
            EncoderDelaySamples = vbrTag?.EncoderDelaySamples,
            PaddingSamples = vbrTag?.EncoderPaddingSamples,
        };

        var encodingAnalysis = new EncodingAnalysis
        {
            DeclaredBitrateKbps = first.BitrateKbps,
            AverageBitrateKbps = averageBitrateKbps,
            MinimumBitrateKbps = minBitrate,
            MaximumBitrateKbps = maxBitrate,
            BitrateMode = bitrateMode,
            FrameCount = frames.Count,
            HasXingHeader = vbrTag is { IsVbrTag: true },
            HasLameTag = isLame,
        };

        return (fileInfo, formatInfo, encodingAnalysis);
    }

    private static BitrateMode DetermineBitrateMode(int minBitrate, int maxBitrate, XingLameTag? vbrTag, bool isLame)
    {
        if (minBitrate == maxBitrate)
        {
            return BitrateMode.ConstantBitRate;
        }

        // LAME writes an "Info" tag ID (not "Xing") for CBR/ABR files even though frame-level
        // bitrate can still fluctuate slightly at stream boundaries.
        if (isLame && vbrTag is { IsVbrTag: false })
        {
            return BitrateMode.AverageBitRate;
        }

        return BitrateMode.VariableBitRate;
    }
}
