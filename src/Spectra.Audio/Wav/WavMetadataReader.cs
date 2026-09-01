using Spectra.Core.Enums;
using Spectra.Core.Models;

namespace Spectra.Audio.Wav;

/// <summary>
/// WAV is uncompressed PCM: there is no declared-vs-actual bitrate question the way there is for
/// MP3. The "bitrate" reported here is the nominal rate implied by sample rate × bit depth ×
/// channels, and is always constant across the file.
/// </summary>
public static class WavMetadataReader
{
    public static (AudioFileInfo FileInfo, FormatInfo FormatInfo, EncodingAnalysis EncodingAnalysis) Read(string path)
    {
        var wav = WavFileReader.Read(path);
        var frameCount = WavFileReader.ComputeFrameCount(wav);
        var duration = wav.SampleRateHz > 0 ? TimeSpan.FromSeconds((double)frameCount / wav.SampleRateHz) : TimeSpan.Zero;

        var nominalBitrateKbps = (int)Math.Round(wav.SampleRateHz * wav.BitsPerSample * wav.ChannelCount / 1000.0);

        var fileInfo = new AudioFileInfo
        {
            FullPath = Path.GetFullPath(path),
            FileName = Path.GetFileName(path),
            Extension = Path.GetExtension(path),
            SizeInBytes = wav.FileBytes.LongLength,
            Duration = duration,
        };

        var formatInfo = new FormatInfo
        {
            Format = "WAV",
            MpegVersion = MpegVersion.Unknown,
            MpegLayer = MpegLayer.Unknown,
            SampleRateHz = wav.SampleRateHz,
            Channels = wav.ChannelCount,
            ChannelMode = wav.ChannelCount == 1 ? ChannelMode.Mono : ChannelMode.Stereo,
            BitsPerSample = wav.BitsPerSample,
        };

        var encodingAnalysis = new EncodingAnalysis
        {
            DeclaredBitrateKbps = nominalBitrateKbps,
            AverageBitrateKbps = nominalBitrateKbps,
            MinimumBitrateKbps = nominalBitrateKbps,
            MaximumBitrateKbps = nominalBitrateKbps,
            BitrateMode = BitrateMode.ConstantBitRate,
            FrameCount = frameCount,
            HasXingHeader = false,
            HasLameTag = false,
        };

        return (fileInfo, formatInfo, encodingAnalysis);
    }
}
