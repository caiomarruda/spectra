using System.Globalization;

namespace Spectra.Web.Components;

/// <summary>Shared value-formatting for report components — mirrors Spectra.Reporting.Html.HtmlReporter's
/// formatting conventions so the browser report reads the same way as the --html report.</summary>
internal static class Format
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    public static string Number(double value, string format = "F1") => value.ToString(format, Culture);

    public static string Db(double value, string format = "F1") => $"{Number(value, format)} dB";

    public static string Percent(double value, string format = "F0") => $"{Number(value, format)}%";

    public static string Lufs(double lufs) =>
        double.IsNegativeInfinity(lufs) ? "-inf LUFS (silent)" : $"{Number(lufs)} LUFS";

    public static string Duration(TimeSpan value) => value.ToString(@"hh\:mm\:ss\.ff", Culture);

    public static string ScoreClass(double score) => score switch
    {
        >= 80 => "good",
        >= 60 => "fair",
        _ => "poor",
    };

    public static string Bytes(long sizeInBytes) => $"{sizeInBytes / 1024.0 / 1024.0:F2} MB";
}
