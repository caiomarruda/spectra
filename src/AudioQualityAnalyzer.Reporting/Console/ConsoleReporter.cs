using System.Globalization;
using AudioQualityAnalyzer.Core.Models;

namespace AudioQualityAnalyzer.Reporting.ConsoleReport;

/// <summary>
/// Renders an <see cref="AudioAnalysisResult"/> to a text writer. All exporters (HTML, Excel)
/// read from the same result object — none of them re-derive analysis logic.
/// </summary>
public static class ConsoleReporter
{
    public static void Report(AudioAnalysisResult result, bool verbose, TextWriter writer)
    {
        var culture = CultureInfo.InvariantCulture;

        writer.WriteLine("=== Audio Quality Analyzer ===");
        writer.WriteLine();
        writer.WriteLine($"File:       {result.FileInfo.FileName}");
        writer.WriteLine($"Duration:   {result.FileInfo.Duration:hh\\:mm\\:ss\\.ff}");
        writer.WriteLine($"Size:       {result.FileInfo.SizeInBytes / 1024.0 / 1024.0:F2} MB");
        writer.WriteLine();

        writer.WriteLine("-- Format --");
        writer.WriteLine($"Format:         {result.FormatInfo.Format} ({DescribeMpeg(result.FormatInfo.MpegVersion, result.FormatInfo.MpegLayer)})");
        writer.WriteLine($"Sample Rate:    {result.FormatInfo.SampleRateHz} Hz");
        writer.WriteLine($"Channels:       {result.FormatInfo.Channels} ({result.FormatInfo.ChannelMode})");
        if (result.FormatInfo.Encoder is not null)
        {
            writer.WriteLine($"Encoder:        {result.FormatInfo.Encoder}");
        }
        writer.WriteLine();

        writer.WriteLine("-- Encoding --");
        writer.WriteLine($"Declared Bitrate:   {result.EncodingAnalysis.DeclaredBitrateKbps} kbps");
        writer.WriteLine($"Average Bitrate:    {result.EncodingAnalysis.AverageBitrateKbps.ToString("F1", culture)} kbps (measured)");
        writer.WriteLine($"Bitrate Range:      {result.EncodingAnalysis.MinimumBitrateKbps}-{result.EncodingAnalysis.MaximumBitrateKbps} kbps");
        writer.WriteLine($"Bitrate Mode:       {result.EncodingAnalysis.BitrateMode}");
        if (verbose)
        {
            writer.WriteLine($"Frame Count:        {result.EncodingAnalysis.FrameCount}");
            writer.WriteLine($"Has Xing Header:    {result.EncodingAnalysis.HasXingHeader}");
            writer.WriteLine($"Has LAME Tag:       {result.EncodingAnalysis.HasLameTag}");
            if (result.FormatInfo.EncoderDelaySamples is { } delay)
            {
                writer.WriteLine($"Encoder Delay:      {delay} samples");
            }
            if (result.FormatInfo.PaddingSamples is { } padding)
            {
                writer.WriteLine($"Encoder Padding:    {padding} samples");
            }
        }
        writer.WriteLine();

        writer.WriteLine("-- Waveform --");
        writer.WriteLine($"Peak:               {ToDbfs(result.WaveformAnalysis.PeakAmplitude)}");
        writer.WriteLine($"RMS:                {ToDbfs(result.WaveformAnalysis.RmsAmplitude)}");
        writer.WriteLine($"Leading Silence:    {result.WaveformAnalysis.LeadingSilence:hh\\:mm\\:ss\\.ff}");
        writer.WriteLine($"Trailing Silence:   {result.WaveformAnalysis.TrailingSilence:hh\\:mm\\:ss\\.ff}");
        if (verbose)
        {
            foreach (var channel in result.WaveformAnalysis.PerChannel)
            {
                writer.WriteLine($"  Channel {channel.ChannelIndex}: Peak {ToDbfs(channel.Peak)}, RMS {ToDbfs(channel.Rms)}");
            }
            writer.WriteLine($"  RMS windows captured: {result.WaveformAnalysis.RmsOverTime.Count}");
        }
    }

    private static string DescribeMpeg(Core.Enums.MpegVersion version, Core.Enums.MpegLayer layer) =>
        $"MPEG {version switch { Core.Enums.MpegVersion.Version1 => "1", Core.Enums.MpegVersion.Version2 => "2", Core.Enums.MpegVersion.Version2_5 => "2.5", _ => "?" }} Layer {layer switch { Core.Enums.MpegLayer.LayerI => "I", Core.Enums.MpegLayer.LayerII => "II", Core.Enums.MpegLayer.LayerIII => "III", _ => "?" }}";

    private static string ToDbfs(float linearAmplitude)
    {
        if (linearAmplitude <= 0f)
        {
            return "-inf dBFS";
        }

        var db = 20.0 * Math.Log10(linearAmplitude);
        return $"{db.ToString("F2", CultureInfo.InvariantCulture)} dBFS";
    }
}
