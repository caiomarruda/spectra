using System.Globalization;
using System.Net;
using System.Text;
using AudioQualityAnalyzer.Core.Models;

namespace AudioQualityAnalyzer.Reporting.Html;

/// <summary>
/// Renders an <see cref="AudioAnalysisResult"/> to a single self-contained HTML file (no external
/// CDN/JS dependency — see SvgCharts) covering the sections 04-REPORTS.md specifies: Header,
/// Executive Summary, File Information, Spectral, Loudness, Dynamic Range, Stereo, Findings,
/// Technical Details.
/// </summary>
public static class HtmlReporter
{
    private const int ChartWidth = 760;
    private const int ChartHeight = 180;
    private const int MaxChartPoints = 400;

    public static string Generate(AudioAnalysisResult result)
    {
        var culture = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();

        sb.Append("<!doctype html><html><head><meta charset=\"utf-8\">");
        sb.Append($"<title>{Html(result.FileInfo.FileName)} — Audio Quality Analysis</title>");
        sb.Append(Styles());
        sb.Append("</head><body>");

        AppendHeader(sb, result);
        AppendExecutiveSummary(sb, result, culture);
        AppendFileInformation(sb, result, culture);
        AppendSpectral(sb, result, culture);
        AppendLoudness(sb, result, culture);
        AppendDynamicRange(sb, result, culture);
        AppendStereo(sb, result, culture);
        AppendFindings(sb, result);
        AppendTechnicalDetails(sb, result, culture);

        sb.Append("</body></html>");
        return sb.ToString();
    }

    public static void WriteToFile(AudioAnalysisResult result, string path) => File.WriteAllText(path, Generate(result));

    private static void AppendHeader(StringBuilder sb, AudioAnalysisResult result)
    {
        sb.Append("<header><h1>Audio Quality Analysis</h1>");
        sb.Append($"<p class=\"file\">{Html(result.FileInfo.FileName)}</p>");
        sb.Append($"<p class=\"muted\">Generated {DateTime.Now:yyyy-MM-dd HH:mm}</p></header>");
    }

    private static void AppendExecutiveSummary(StringBuilder sb, AudioAnalysisResult result, CultureInfo culture)
    {
        var a = result.OverallAssessment;
        sb.Append("<section><h2>Executive Summary</h2>");
        sb.Append($"<p class=\"verdict\">{Html(a.Verdict)}</p>");
        sb.Append("<div class=\"scores\">");
        AppendScoreCard(sb, "Overall", a.OverallQualityScore, culture, primary: true);
        AppendScoreCard(sb, "Encoding", a.EncodingQualityScore, culture);
        AppendScoreCard(sb, "Spectral", a.SpectralQualityScore, culture);
        AppendScoreCard(sb, "Technical", a.TechnicalQualityScore, culture);
        AppendScoreCard(sb, "Mastering", a.MasteringQualityScore, culture);
        sb.Append("</div>");
        sb.Append("<table class=\"kv\">");
        AppendRow(sb, "Transcoding Probability", $"{result.TranscodingAnalysis.Probability.ToString("F0", culture)}% ({result.TranscodingAnalysis.Label})");
        AppendRow(sb, "Transcoding Confidence", result.TranscodingAnalysis.Confidence.ToString());
        sb.Append("</table></section>");
    }

    private static void AppendScoreCard(StringBuilder sb, string label, double score, CultureInfo culture, bool primary = false)
    {
        var cls = ScoreClass(score);
        sb.Append($"<div class=\"score-card {cls}{(primary ? " primary" : "")}\">");
        sb.Append($"<div class=\"score-value\">{score.ToString("F0", culture)}</div>");
        sb.Append($"<div class=\"score-label\">{Html(label)}</div></div>");
    }

    private static string ScoreClass(double score) => score switch
    {
        >= 80 => "good",
        >= 60 => "fair",
        _ => "poor",
    };

    private static void AppendFileInformation(StringBuilder sb, AudioAnalysisResult result, CultureInfo culture)
    {
        sb.Append("<section><h2>File Information</h2><table class=\"kv\">");
        AppendRow(sb, "Filename", result.FileInfo.FileName);
        AppendRow(sb, "Duration", result.FileInfo.Duration.ToString(@"hh\:mm\:ss\.ff"));
        AppendRow(sb, "Size", $"{result.FileInfo.SizeInBytes / 1024.0 / 1024.0:F2} MB");
        AppendRow(sb, "Format", $"{result.FormatInfo.Format} (MPEG {DescribeVersion(result.FormatInfo.MpegVersion)} Layer {DescribeLayer(result.FormatInfo.MpegLayer)})");
        AppendRow(sb, "Sample Rate", $"{result.FormatInfo.SampleRateHz} Hz");
        AppendRow(sb, "Channels", $"{result.FormatInfo.Channels} ({result.FormatInfo.ChannelMode})");
        if (result.FormatInfo.Encoder is not null)
        {
            AppendRow(sb, "Encoder", result.FormatInfo.Encoder);
        }
        AppendRow(sb, "Declared Bitrate", $"{result.EncodingAnalysis.DeclaredBitrateKbps} kbps");
        AppendRow(sb, "Measured Average Bitrate", $"{result.EncodingAnalysis.AverageBitrateKbps.ToString("F1", culture)} kbps");
        AppendRow(sb, "Bitrate Mode", result.EncodingAnalysis.BitrateMode.ToString());
        sb.Append("</table></section>");
    }

    private static void AppendSpectral(StringBuilder sb, AudioAnalysisResult result, CultureInfo culture)
    {
        var s = result.SpectralAnalysis;
        sb.Append("<section><h2>Spectral Analysis</h2><table class=\"kv\">");
        AppendRow(sb, "Effective Bandwidth", $"{s.EffectiveBandwidthHz / 1000.0:F2} kHz (confidence: {s.BandwidthConfidence})");
        AppendRow(sb, "Cutoff Sharpness", $"{s.CutoffSharpnessDbPerOctave.ToString("F1", culture)} dB/octave");
        AppendRow(sb, "Cutoff Consistency", s.CutoffConsistency.ToString("P0", culture));
        AppendRow(sb, "Spectral Centroid", $"{s.SpectralCentroidHz / 1000.0:F2} kHz");
        AppendRow(sb, "Spectral Rolloff (85%)", $"{s.SpectralRolloffHz / 1000.0:F2} kHz");
        sb.Append("</table>");

        sb.Append("<h3>Band Energies</h3>");
        var bars = s.BandEnergies.Select(b => (b.Label, b.AverageEnergyDb)).ToList();
        sb.Append(SvgCharts.BarChart(bars, ChartWidth, ChartHeight, "#4a7cc7", -100, 0));

        sb.Append("<h3>Average Spectrum</h3>");
        var spectrum = Downsample(s.AverageSpectrumDb
            .Select((db, bin) => ((double)bin * result.FormatInfo.SampleRateHz / 2 / s.AverageSpectrumDb.Count, db))
            .ToList(), MaxChartPoints);
        sb.Append(SvgCharts.LineChart(spectrum, ChartWidth, ChartHeight, "#4a7cc7", " dB", -100, 0));

        sb.Append("<h3>Spectrogram (14-band resolution)</h3>");
        AppendSpectrogram(sb, s.FramesOverTime, s.BandEnergies.Select(b => b.Label).ToList());

        sb.Append("</section>");
    }

    private static void AppendSpectrogram(StringBuilder sb, IReadOnlyList<SpectralFrameSummary> frames, IReadOnlyList<string> bandLabels)
    {
        if (frames.Count == 0)
        {
            sb.Append(SvgCharts.Heatmap([], new double[0, 0], ChartWidth, ChartHeight, -100, 0));
            return;
        }

        const int maxColumns = 200;
        var bandCount = frames[0].BandEnergiesDb.Count;
        var bucketSize = Math.Max(1, (int)Math.Ceiling(frames.Count / (double)maxColumns));
        var columns = (int)Math.Ceiling(frames.Count / (double)bucketSize);
        var matrix = new double[bandCount, columns];

        for (var col = 0; col < columns; col++)
        {
            var start = col * bucketSize;
            var end = Math.Min(frames.Count, start + bucketSize);
            for (var band = 0; band < bandCount; band++)
            {
                double sum = 0;
                for (var f = start; f < end; f++)
                {
                    sum += frames[f].BandEnergiesDb[band];
                }
                matrix[band, col] = sum / (end - start);
            }
        }

        sb.Append(SvgCharts.Heatmap(bandLabels, matrix, ChartWidth, 260, -100, 0));
    }

    private static void AppendLoudness(StringBuilder sb, AudioAnalysisResult result, CultureInfo culture)
    {
        var l = result.LoudnessAnalysis;
        sb.Append("<section><h2>Loudness</h2><table class=\"kv\">");
        AppendRow(sb, "Integrated Loudness", FormatLufs(l.IntegratedLufs, culture));
        AppendRow(sb, "Loudness Range", $"{l.LoudnessRangeLu.ToString("F1", culture)} LU");
        AppendRow(sb, "Sample Peak", $"{l.SamplePeakDbfs.ToString("F2", culture)} dBFS");
        AppendRow(sb, "True Peak", $"{l.TruePeakDbfs.ToString("F2", culture)} dBTP");
        sb.Append("</table>");

        sb.Append("<h3>Loudness Over Time</h3>");
        var momentary = Downsample(l.LoudnessOverTime
            .Where(p => !double.IsNegativeInfinity(p.MomentaryLufs))
            .Select(p => (p.Time.TotalSeconds, p.MomentaryLufs)).ToList(), MaxChartPoints);
        var shortTerm = Downsample(l.LoudnessOverTime
            .Where(p => !double.IsNegativeInfinity(p.ShortTermLufs))
            .Select(p => (p.Time.TotalSeconds, p.ShortTermLufs)).ToList(), MaxChartPoints);
        sb.Append(SvgCharts.LineChart(momentary, ChartWidth, ChartHeight, "#c76b4a", " LUFS", -60, 0, shortTerm, "#4a7cc7"));
        sb.Append("<p class=\"muted\">Orange: momentary (400ms). Blue: short-term (3s).</p>");
        sb.Append("</section>");
    }

    private static void AppendDynamicRange(StringBuilder sb, AudioAnalysisResult result, CultureInfo culture)
    {
        var d = result.DynamicRangeAnalysis;
        sb.Append("<section><h2>Dynamic Range</h2><table class=\"kv\">");
        AppendRow(sb, "Crest Factor", $"{d.CrestFactorDb.ToString("F1", culture)} dB");
        AppendRow(sb, "Near Full Scale", $"{d.PercentSamplesNearFullScale.ToString("F3", culture)}% of samples");
        AppendRow(sb, "RMS Window Range", $"{d.RmsWindowMinDb.ToString("F1", culture)} to {d.RmsWindowMaxDb.ToString("F1", culture)} dB");
        sb.Append("</table>");

        sb.Append("<h3>RMS / Peak Over Time</h3>");
        var rms = Downsample(result.WaveformAnalysis.RmsOverTime
            .Select(w => (w.StartTime.TotalSeconds, (double)ToDb(w.Rms))).ToList(), MaxChartPoints);
        var peak = Downsample(result.WaveformAnalysis.RmsOverTime
            .Select(w => (w.StartTime.TotalSeconds, (double)ToDb(w.Peak))).ToList(), MaxChartPoints);
        sb.Append(SvgCharts.LineChart(rms, ChartWidth, ChartHeight, "#4a7cc7", " dB", -80, 6, peak, "#c76b4a"));
        sb.Append("<p class=\"muted\">Blue: RMS. Orange: peak (per 100ms window).</p>");
        sb.Append("</section>");
    }

    private static void AppendStereo(StringBuilder sb, AudioAnalysisResult result, CultureInfo culture)
    {
        sb.Append("<section><h2>Stereo</h2>");
        if (result.StereoAnalysis is not { } stereo)
        {
            sb.Append("<p class=\"muted\">Mono file — no stereo image to describe.</p></section>");
            return;
        }

        sb.Append("<table class=\"kv\">");
        AppendRow(sb, "Correlation", stereo.CorrelationCoefficient.ToString("F2", culture));
        AppendRow(sb, "Balance", $"{stereo.ChannelBalanceDb.ToString("+0.00;-0.00", culture)} dB");
        AppendRow(sb, "Mono Compatibility", stereo.MonoCompatibilityRatio.ToString("F2", culture));
        AppendRow(sb, "Mid / Side Energy", $"{stereo.MidEnergyDb.ToString("F1", culture)} / {stereo.SideEnergyDb.ToString("F1", culture)} dB");
        sb.Append("</table>");

        var flags = new List<string>();
        if (stereo.IsChannelEffectivelyMissing) flags.Add("channel effectively missing");
        if (stereo.IsSeverelyImbalanced) flags.Add("severe imbalance");
        if (stereo.IsMonoDisguisedAsStereo) flags.Add("mono disguised as stereo");
        if (stereo.HasPhaseProblems) flags.Add("phase problems");
        if (stereo.HasPolarityInversion) flags.Add("polarity inversion");
        if (stereo.HasExcessiveSideContent) flags.Add("excessive side content");
        if (flags.Count > 0)
        {
            sb.Append($"<p class=\"warn\">Flags: {Html(string.Join(", ", flags))}</p>");
        }

        sb.Append("<h3>Correlation Over Time</h3>");
        var correlation = Downsample(stereo.CorrelationOverTime.Select(p => (p.Time.TotalSeconds, p.Correlation)).ToList(), MaxChartPoints);
        sb.Append(SvgCharts.LineChart(correlation, ChartWidth, ChartHeight, "#4a7cc7", "", -1, 1));
        sb.Append("</section>");
    }

    private static void AppendFindings(StringBuilder sb, AudioAnalysisResult result)
    {
        sb.Append("<section><h2>Findings</h2>");
        if (result.OverallAssessment.Findings.Count == 0)
        {
            sb.Append("<p class=\"muted\">No findings.</p></section>");
            return;
        }

        foreach (var finding in result.OverallAssessment.Findings)
        {
            sb.Append($"<div class=\"finding {finding.Severity.ToString().ToLowerInvariant()}\">");
            sb.Append($"<h4>[{finding.Severity}] {Html(finding.Title)} <span class=\"muted\">({finding.Code})</span></h4>");
            sb.Append($"<p>{Html(finding.Description)}</p>");
            sb.Append($"<p class=\"muted\">Confidence: {finding.Confidence}</p>");
            sb.Append("<ul>");
            foreach (var evidence in finding.Evidence)
            {
                sb.Append($"<li>{Html(evidence)}</li>");
            }
            sb.Append("</ul></div>");
        }
        sb.Append("</section>");
    }

    private static void AppendTechnicalDetails(StringBuilder sb, AudioAnalysisResult result, CultureInfo culture)
    {
        sb.Append("<section><h2>Technical Details</h2><table class=\"kv\">");
        AppendRow(sb, "Clipped Samples", $"{result.ClippingAnalysis.TotalClippedSamples} ({result.ClippingAnalysis.ClippedPercentage.ToString("F4", culture)}%)");
        AppendRow(sb, "Clip Events", result.ClippingAnalysis.ClipEventCount.ToString(culture));
        AppendRow(sb, "Longest Clip", $"{result.ClippingAnalysis.LongestClipDuration.TotalMilliseconds.ToString("F1", culture)} ms{(result.ClippingAnalysis.IsSevere ? " (SEVERE)" : "")}");
        AppendRow(sb, "Noise Floor", $"{result.NoiseAnalysis.NoiseFloorDb.ToString("F1", culture)} dB");
        AppendRow(sb, "DC Offset", result.NoiseAnalysis.HasSignificantDcOffset ? "significant" : "negligible");
        AppendRow(sb, "Internal Silence", result.NoiseAnalysis.HasExcessiveInternalSilence ? "excessive gap detected" : "none");
        AppendRow(sb, "Leading Silence", result.WaveformAnalysis.LeadingSilence.ToString(@"hh\:mm\:ss\.ff"));
        AppendRow(sb, "Trailing Silence", result.WaveformAnalysis.TrailingSilence.ToString(@"hh\:mm\:ss\.ff"));
        AppendRow(sb, "Decoder", $"{result.FormatInfo.Encoder ?? "unknown"}");
        sb.Append("</table></section>");
    }

    private static List<(double X, double Y)> Downsample(List<(double X, double Y)> points, int maxPoints)
    {
        if (points.Count <= maxPoints)
        {
            return points;
        }

        var bucketSize = (int)Math.Ceiling(points.Count / (double)maxPoints);
        var result = new List<(double, double)>();
        for (var i = 0; i < points.Count; i += bucketSize)
        {
            var bucket = points.Skip(i).Take(bucketSize).ToList();
            result.Add((bucket.Average(p => p.X), bucket.Average(p => p.Y)));
        }
        return result;
    }

    private static void AppendRow(StringBuilder sb, string key, string value) =>
        sb.Append($"<tr><th>{Html(key)}</th><td>{Html(value)}</td></tr>");

    private static string Html(string value) => WebUtility.HtmlEncode(value);

    private static string FormatLufs(double lufs, CultureInfo culture) =>
        double.IsNegativeInfinity(lufs) ? "-inf LUFS (silent)" : $"{lufs.ToString("F1", culture)} LUFS";

    private static float ToDb(float linear) => linear > 0 ? 20f * MathF.Log10(linear) : -120f;

    private static string DescribeVersion(Core.Enums.MpegVersion version) => version switch
    {
        Core.Enums.MpegVersion.Version1 => "1",
        Core.Enums.MpegVersion.Version2 => "2",
        Core.Enums.MpegVersion.Version2_5 => "2.5",
        _ => "?",
    };

    private static string DescribeLayer(Core.Enums.MpegLayer layer) => layer switch
    {
        Core.Enums.MpegLayer.LayerI => "I",
        Core.Enums.MpegLayer.LayerII => "II",
        Core.Enums.MpegLayer.LayerIII => "III",
        _ => "?",
    };

    private static string Styles() => """
        <style>
        body { font-family: -apple-system, Segoe UI, Roboto, sans-serif; margin: 0; background: #f6f7f9; color: #1f2430; }
        header { background: #1f2430; color: #fff; padding: 24px 32px; }
        header h1 { margin: 0 0 4px; font-size: 22px; }
        header .file { font-size: 15px; opacity: .9; margin: 0; }
        header .muted { font-size: 12px; opacity: .6; margin: 4px 0 0; }
        section { max-width: 880px; margin: 20px auto; background: #fff; border-radius: 8px; padding: 20px 28px; box-shadow: 0 1px 3px rgba(0,0,0,.08); }
        h2 { font-size: 17px; margin-top: 0; border-bottom: 1px solid #eee; padding-bottom: 8px; }
        h3 { font-size: 14px; color: #444; margin-bottom: 6px; }
        h4 { margin: 0 0 4px; font-size: 13px; }
        table.kv { width: 100%; border-collapse: collapse; font-size: 13px; }
        table.kv th { text-align: left; color: #666; font-weight: 500; padding: 5px 8px 5px 0; width: 40%; vertical-align: top; }
        table.kv td { padding: 5px 0; }
        .verdict { font-size: 16px; font-weight: 600; margin: 0 0 14px; }
        .scores { display: flex; gap: 10px; margin-bottom: 16px; flex-wrap: wrap; }
        .score-card { flex: 1; min-width: 90px; text-align: center; padding: 12px 8px; border-radius: 6px; background: #f0f1f4; }
        .score-card.primary { background: #1f2430; }
        .score-card.primary .score-value, .score-card.primary .score-label { color: #fff; }
        .score-card.good .score-value { color: #2a9d5c; }
        .score-card.fair .score-value { color: #c78a2a; }
        .score-card.poor .score-value { color: #c74a4a; }
        .score-value { font-size: 24px; font-weight: 700; }
        .score-label { font-size: 11px; color: #666; text-transform: uppercase; letter-spacing: .04em; }
        .muted { color: #888; font-size: 12px; }
        .warn { color: #a8621f; font-size: 13px; }
        .finding { border-left: 3px solid #ccc; padding: 8px 12px; margin-bottom: 10px; background: #fafafa; }
        .finding.critical { border-color: #c74a4a; }
        .finding.warning { border-color: #c78a2a; }
        .finding.info { border-color: #4a7cc7; }
        .finding ul { margin: 6px 0 0; padding-left: 18px; font-size: 12px; color: #555; }
        svg { display: block; margin-bottom: 10px; }
        </style>
        """;
}
