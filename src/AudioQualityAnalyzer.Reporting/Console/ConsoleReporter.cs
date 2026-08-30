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

        writer.WriteLine("-- Spectral --");
        writer.WriteLine($"Effective Bandwidth:   {result.SpectralAnalysis.EffectiveBandwidthHz / 1000.0:F2} kHz (confidence: {result.SpectralAnalysis.BandwidthConfidence})");
        writer.WriteLine($"Spectral Centroid:     {result.SpectralAnalysis.SpectralCentroidHz / 1000.0:F2} kHz");
        writer.WriteLine($"Spectral Rolloff (85%):{result.SpectralAnalysis.SpectralRolloffHz / 1000.0:F2} kHz");
        writer.WriteLine($"Cutoff Frequency:      {result.SpectralAnalysis.CutoffFrequencyHz / 1000.0:F2} kHz");
        writer.WriteLine($"Cutoff Sharpness:      {result.SpectralAnalysis.CutoffSharpnessDbPerOctave.ToString("F1", culture)} dB/octave");
        writer.WriteLine($"Cutoff Consistency:    {result.SpectralAnalysis.CutoffConsistency:P0}");
        if (verbose)
        {
            writer.WriteLine($"Spectral Bandwidth:    {result.SpectralAnalysis.SpectralBandwidthHz / 1000.0:F2} kHz");
            writer.WriteLine($"Spectral Flatness:     {result.SpectralAnalysis.SpectralFlatness.ToString("F4", culture)}");
            writer.WriteLine($"Spectral Flux (avg):   {result.SpectralAnalysis.SpectralFluxAverage.ToString("E2", culture)}");
            writer.WriteLine($"Spectral Contrast:     {result.SpectralAnalysis.SpectralContrast.ToString("F1", culture)} dB");
            writer.WriteLine("  Band energies:");
            foreach (var band in result.SpectralAnalysis.BandEnergies)
            {
                writer.WriteLine($"    {band.Label,-14} {band.AverageEnergyDb.ToString("F1", culture)} dB");
            }
        }
        writer.WriteLine();

        writer.WriteLine("-- Loudness --");
        writer.WriteLine($"Integrated Loudness:   {FormatLufs(result.LoudnessAnalysis.IntegratedLufs, culture)}");
        writer.WriteLine($"Loudness Range:        {result.LoudnessAnalysis.LoudnessRangeLu.ToString("F1", culture)} LU");
        writer.WriteLine($"Sample Peak:           {result.LoudnessAnalysis.SamplePeakDbfs.ToString("F2", culture)} dBFS");
        writer.WriteLine($"True Peak:             {result.LoudnessAnalysis.TruePeakDbfs.ToString("F2", culture)} dBTP");
        if (verbose)
        {
            writer.WriteLine($"Momentary Max:         {FormatLufs(result.LoudnessAnalysis.MomentaryMaxLufs, culture)}");
            writer.WriteLine($"Short-Term Max:        {FormatLufs(result.LoudnessAnalysis.ShortTermMaxLufs, culture)}");
        }
        writer.WriteLine();

        writer.WriteLine("-- Dynamic Range --");
        writer.WriteLine($"Crest Factor:          {result.DynamicRangeAnalysis.CrestFactorDb.ToString("F1", culture)} dB");
        writer.WriteLine($"Near Full Scale:       {result.DynamicRangeAnalysis.PercentSamplesNearFullScale.ToString("F3", culture)}% of samples");
        if (verbose)
        {
            writer.WriteLine($"RMS Window Range:      {result.DynamicRangeAnalysis.RmsWindowMinDb.ToString("F1", culture)} to {result.DynamicRangeAnalysis.RmsWindowMaxDb.ToString("F1", culture)} dB (median {result.DynamicRangeAnalysis.RmsWindowMedianDb.ToString("F1", culture)}, stddev {result.DynamicRangeAnalysis.RmsWindowStdDevDb.ToString("F1", culture)})");
        }
        writer.WriteLine();

        writer.WriteLine("-- Clipping --");
        writer.WriteLine($"Clipped Samples:       {result.ClippingAnalysis.TotalClippedSamples} ({result.ClippingAnalysis.ClippedPercentage.ToString("F4", culture)}%)");
        writer.WriteLine($"Clip Events:           {result.ClippingAnalysis.ClipEventCount}");
        writer.WriteLine($"Longest Clip:          {result.ClippingAnalysis.LongestClipDuration.TotalMilliseconds.ToString("F1", culture)} ms{(result.ClippingAnalysis.IsSevere ? " (SEVERE)" : "")}");
        writer.WriteLine();

        if (result.StereoAnalysis is { } stereo)
        {
            writer.WriteLine("-- Stereo --");
            writer.WriteLine($"Correlation:           {stereo.CorrelationCoefficient.ToString("F2", culture)}");
            writer.WriteLine($"Balance:               {stereo.ChannelBalanceDb.ToString("+0.00;-0.00", culture)} dB");
            writer.WriteLine($"Mono Compatibility:    {stereo.MonoCompatibilityRatio.ToString("F2", culture)}");
            if (verbose)
            {
                writer.WriteLine($"Mid/Side Energy:       {stereo.MidEnergyDb.ToString("F1", culture)} / {stereo.SideEnergyDb.ToString("F1", culture)} dB (side-to-mid {stereo.SideToMidRatioDb.ToString("+0.0;-0.0", culture)})");
            }
            var flags = new List<string>();
            if (stereo.IsChannelEffectivelyMissing) flags.Add("channel effectively missing");
            if (stereo.IsSeverelyImbalanced) flags.Add("severe imbalance");
            if (stereo.IsMonoDisguisedAsStereo) flags.Add("mono disguised as stereo");
            if (stereo.HasPhaseProblems) flags.Add("phase problems");
            if (stereo.HasPolarityInversion) flags.Add("polarity inversion");
            if (stereo.HasExcessiveSideContent) flags.Add("excessive side content");
            if (flags.Count > 0)
            {
                writer.WriteLine($"Flags:                 {string.Join(", ", flags)}");
            }
            writer.WriteLine();
        }

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

    private static string FormatLufs(double lufs, CultureInfo culture) =>
        double.IsNegativeInfinity(lufs) ? "-inf LUFS (silent)" : $"{lufs.ToString("F1", culture)} LUFS";

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
