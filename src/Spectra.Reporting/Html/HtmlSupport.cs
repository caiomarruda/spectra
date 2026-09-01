using System.Net;

namespace Spectra.Reporting.Html;

/// <summary>Shared HTML-encoding and CSS for both the single-file (<see cref="HtmlReporter"/>) and batch (<see cref="HtmlBatchReporter"/>) reports, so they stay visually consistent.</summary>
internal static class HtmlSupport
{
    public static string Html(string value) => WebUtility.HtmlEncode(value);

    public static string Styles() => """
        <style>
        body { font-family: -apple-system, Segoe UI, Roboto, sans-serif; margin: 0; background: #f6f7f9; color: #1f2430; }
        header { background: #1f2430; color: #fff; padding: 24px 32px; }
        header h1 { margin: 0 0 4px; font-size: 22px; }
        header .file { font-size: 15px; opacity: .9; margin: 0; }
        header .muted { font-size: 12px; opacity: .6; margin: 4px 0 0; }
        section { max-width: 960px; margin: 20px auto; background: #fff; border-radius: 8px; padding: 20px 28px; box-shadow: 0 1px 3px rgba(0,0,0,.08); }
        h2 { font-size: 17px; margin-top: 0; border-bottom: 1px solid #eee; padding-bottom: 8px; }
        h3 { font-size: 14px; color: #444; margin-bottom: 6px; }
        h4 { margin: 0 0 4px; font-size: 13px; }
        table.kv { width: 100%; border-collapse: collapse; font-size: 13px; }
        table.kv th { text-align: left; color: #666; font-weight: 500; padding: 5px 8px 5px 0; width: 40%; vertical-align: top; }
        table.kv td { padding: 5px 0; }
        .warning-banner { max-width: 960px; margin: 16px auto 0; background: #fdf1dc; border: 1px solid #eecb8f; border-radius: 6px; padding: 10px 16px; }
        .warning-banner p { margin: 4px 0; font-size: 13px; color: #7a5310; }
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
        table.list { width: 100%; border-collapse: collapse; font-size: 12px; }
        table.list th { text-align: left; background: #f0f1f4; padding: 6px 8px; white-space: nowrap; }
        table.list td { padding: 6px 8px; border-bottom: 1px solid #eee; white-space: nowrap; }
        table.list tr:hover { background: #f8f9fb; }
        .badge { display: inline-block; padding: 2px 8px; border-radius: 10px; font-size: 11px; font-weight: 600; }
        .badge.good { background: #e3f5ea; color: #1f7a44; }
        .badge.fair { background: #fbeed7; color: #966018; }
        .badge.poor { background: #fbe4e4; color: #a13a3a; }
        .stat-row { display: flex; gap: 10px; flex-wrap: wrap; margin-bottom: 16px; }
        .stat { flex: 1; min-width: 110px; background: #f0f1f4; border-radius: 6px; padding: 12px; text-align: center; }
        .stat .n { font-size: 22px; font-weight: 700; }
        .stat .l { font-size: 11px; color: #666; text-transform: uppercase; letter-spacing: .04em; }
        </style>
        """;
}
