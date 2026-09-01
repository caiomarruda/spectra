using System.Globalization;
using System.Text;
using Spectra.Core.Enums;
using Spectra.Core.Models;

namespace Spectra.Reporting.Html;

/// <summary>
/// A single summarized HTML report for a whole folder scan (--folder): one row per track rather
/// than the full per-track deep dive HtmlReporter produces — hundreds of tracks' worth of charts
/// on one page would be impractical to render and not what a library scan is for.
/// </summary>
public static class HtmlBatchReporter
{
    public static string Generate(IReadOnlyList<BatchTrackResult> successes, IReadOnlyList<BatchTrackFailure> failures, string folderPath)
    {
        var culture = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();

        sb.Append("<!doctype html><html><head><meta charset=\"utf-8\">");
        sb.Append($"<title>Batch Audio Quality Analysis — {HtmlSupport.Html(Path.GetFileName(folderPath))}</title>");
        sb.Append(HtmlSupport.Styles());
        sb.Append("</head><body>");

        sb.Append("<header><h1>Batch Audio Quality Analysis</h1>");
        sb.Append($"<p class=\"file\">{HtmlSupport.Html(folderPath)}</p>");
        sb.Append($"<p class=\"muted\">Generated {DateTime.Now:yyyy-MM-dd HH:mm} — {successes.Count} file(s) analyzed{(failures.Count > 0 ? $", {failures.Count} failed" : "")}</p></header>");

        AppendStats(sb, successes, culture);
        AppendTrackTable(sb, successes, culture);
        AppendFlaggedFindings(sb, successes);
        if (failures.Count > 0)
        {
            AppendFailures(sb, failures);
        }

        sb.Append("</body></html>");
        return sb.ToString();
    }

    public static void WriteToFile(IReadOnlyList<BatchTrackResult> successes, IReadOnlyList<BatchTrackFailure> failures, string folderPath, string outputPath) =>
        File.WriteAllText(outputPath, Generate(successes, failures, folderPath));

    private static void AppendStats(StringBuilder sb, IReadOnlyList<BatchTrackResult> successes, CultureInfo culture)
    {
        sb.Append("<section>");
        sb.Append("<div class=\"stat-row\">");
        AppendStat(sb, successes.Count.ToString(culture), "Tracks");
        AppendStat(sb, successes.Count > 0 ? successes.Average(t => t.Result.OverallAssessment.OverallQualityScore).ToString("F0", culture) : "-", "Avg Score");
        AppendStat(sb, successes.Count(t => t.Result.TranscodingAnalysis.Label is TranscodingProbabilityLabel.Likely or TranscodingProbabilityLabel.HighlyLikely).ToString(culture), "Possible Transcodes");
        AppendStat(sb, successes.Count(t => t.Result.OverallAssessment.Findings.Any(f => f.Severity == Severity.Critical)).ToString(culture), "Critical Findings");
        sb.Append("</div></section>");
    }

    private static void AppendStat(StringBuilder sb, string value, string label) =>
        sb.Append($"<div class=\"stat\"><div class=\"n\">{HtmlSupport.Html(value)}</div><div class=\"l\">{HtmlSupport.Html(label)}</div></div>");

    private static void AppendTrackTable(StringBuilder sb, IReadOnlyList<BatchTrackResult> successes, CultureInfo culture)
    {
        sb.Append("<section><h2>Tracks (worst overall score first)</h2>");
        if (successes.Count == 0)
        {
            sb.Append("<p class=\"muted\">No tracks analyzed.</p></section>");
            return;
        }

        sb.Append("<div style=\"overflow-x:auto\"><table class=\"list\"><thead><tr>");
        foreach (var header in new[] { "File", "Verdict", "Overall", "Encoding", "Spectral", "Technical", "Mastering", "Transcode", "Bitrate", "Warnings" })
        {
            sb.Append($"<th>{HtmlSupport.Html(header)}</th>");
        }
        sb.Append("</tr></thead><tbody>");

        foreach (var track in successes.OrderBy(t => t.Result.OverallAssessment.OverallQualityScore))
        {
            var a = track.Result.OverallAssessment;
            var t = track.Result.TranscodingAnalysis;
            sb.Append("<tr>");
            sb.Append($"<td>{HtmlSupport.Html(track.RelativePath)}</td>");
            sb.Append($"<td>{ScoreBadge(a.OverallQualityScore, a.Verdict)}</td>");
            sb.Append($"<td>{a.OverallQualityScore.ToString("F0", culture)}</td>");
            sb.Append($"<td>{a.EncodingQualityScore.ToString("F0", culture)}</td>");
            sb.Append($"<td>{a.SpectralQualityScore.ToString("F0", culture)}</td>");
            sb.Append($"<td>{a.TechnicalQualityScore.ToString("F0", culture)}</td>");
            sb.Append($"<td>{a.MasteringQualityScore.ToString("F0", culture)}</td>");
            sb.Append($"<td>{t.Probability.ToString("F0", culture)}% ({t.Label})</td>");
            sb.Append($"<td>{track.Result.EncodingAnalysis.DeclaredBitrateKbps} kbps</td>");
            sb.Append($"<td>{HtmlSupport.Html(string.Join("; ", track.Result.Warnings))}</td>");
            sb.Append("</tr>");
        }

        sb.Append("</tbody></table></div></section>");
    }

    private static string ScoreBadge(double score, string verdict)
    {
        var cls = score switch { >= 80 => "good", >= 60 => "fair", _ => "poor" };
        return $"<span class=\"badge {cls}\">{HtmlSupport.Html(verdict)}</span>";
    }

    private static void AppendFlaggedFindings(StringBuilder sb, IReadOnlyList<BatchTrackResult> successes)
    {
        var flagged = successes
            .Select(t => (t.RelativePath, Findings: t.Result.OverallAssessment.Findings.Where(f => f.Severity != Severity.Info).ToList()))
            .Where(t => t.Findings.Count > 0)
            .ToList();

        sb.Append("<section><h2>Flagged Tracks</h2>");
        if (flagged.Count == 0)
        {
            sb.Append("<p class=\"muted\">No warnings or critical findings.</p></section>");
            return;
        }

        foreach (var (relativePath, findings) in flagged)
        {
            sb.Append($"<h4>{HtmlSupport.Html(relativePath)}</h4><ul>");
            foreach (var finding in findings)
            {
                sb.Append($"<li>[{finding.Severity}] {HtmlSupport.Html(finding.Title)}</li>");
            }
            sb.Append("</ul>");
        }
        sb.Append("</section>");
    }

    private static void AppendFailures(StringBuilder sb, IReadOnlyList<BatchTrackFailure> failures)
    {
        sb.Append("<section><h2>Failed to Analyze</h2><ul>");
        foreach (var failure in failures)
        {
            sb.Append($"<li>{HtmlSupport.Html(failure.RelativePath)} — {HtmlSupport.Html(failure.ErrorMessage)}</li>");
        }
        sb.Append("</ul></section>");
    }
}
