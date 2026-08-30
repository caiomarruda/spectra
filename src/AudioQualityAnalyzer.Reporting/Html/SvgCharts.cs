using System.Globalization;
using System.Text;

namespace AudioQualityAnalyzer.Reporting.Html;

/// <summary>
/// Minimal self-contained SVG chart rendering — no JS charting library, so the generated HTML
/// report works fully offline with no CDN dependency (04-REPORTS.md gives no specific charting
/// technology, and this analyzer otherwise has zero external runtime dependencies).
/// </summary>
internal static class SvgCharts
{
    private const int LeftMargin = 48;
    private const int RightMargin = 12;
    private const int TopMargin = 12;
    private const int BottomMargin = 28;

    public static string LineChart(
        IReadOnlyList<(double X, double Y)> series, int width, int height, string color,
        string? yAxisSuffix = null, double? fixedYMin = null, double? fixedYMax = null,
        IReadOnlyList<(double X, double Y)>? series2 = null, string? color2 = null)
    {
        if (series.Count == 0)
        {
            return EmptyChart(width, height);
        }

        var allPoints = series2 is null ? series : series.Concat(series2).ToList();
        var xMin = allPoints.Min(p => p.X);
        var xMax = allPoints.Max(p => p.X);
        var yMin = fixedYMin ?? allPoints.Min(p => p.Y);
        var yMax = fixedYMax ?? allPoints.Max(p => p.Y);
        if (Math.Abs(yMax - yMin) < 1e-9)
        {
            yMax += 1;
            yMin -= 1;
        }
        if (Math.Abs(xMax - xMin) < 1e-9)
        {
            xMax += 1;
        }

        var sb = new StringBuilder();
        sb.Append(SvgHeader(width, height));
        AppendAxes(sb, width, height, yMin, yMax, yAxisSuffix);
        AppendPolyline(sb, series, width, height, xMin, xMax, yMin, yMax, color);
        if (series2 is not null)
        {
            AppendPolyline(sb, series2, width, height, xMin, xMax, yMin, yMax, color2 ?? "#888");
        }
        sb.Append("</svg>");
        return sb.ToString();
    }

    public static string BarChart(IReadOnlyList<(string Label, double Value)> bars, int width, int height, string color, double yMin, double yMax)
    {
        if (bars.Count == 0)
        {
            return EmptyChart(width, height);
        }

        var sb = new StringBuilder();
        sb.Append(SvgHeader(width, height));
        AppendAxes(sb, width, height, yMin, yMax, " dB");

        var plotWidth = width - LeftMargin - RightMargin;
        var plotHeight = height - TopMargin - BottomMargin;
        var barSlot = plotWidth / (double)bars.Count;
        var barWidth = barSlot * 0.7;

        for (var i = 0; i < bars.Count; i++)
        {
            var (label, value) = bars[i];
            var clamped = Math.Clamp(value, yMin, yMax);
            var y = MapY(clamped, yMin, yMax, plotHeight) + TopMargin;
            var zeroY = MapY(yMin, yMin, yMax, plotHeight) + TopMargin;
            var x = LeftMargin + (i * barSlot) + ((barSlot - barWidth) / 2);
            var barHeight = Math.Max(0, zeroY - y);
            sb.Append(CultureInfo.InvariantCulture, $"<rect x=\"{x:F1}\" y=\"{y:F1}\" width=\"{barWidth:F1}\" height=\"{barHeight:F1}\" fill=\"{color}\" />");
            sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{x + (barWidth / 2):F1}\" y=\"{height - 6}\" font-size=\"9\" text-anchor=\"middle\" fill=\"#666\">{System.Net.WebUtility.HtmlEncode(label)}</text>");
        }

        sb.Append("</svg>");
        return sb.ToString();
    }

    /// <summary>Coarse spectrogram — one row per analysis band, columns bucketed from the per-frame data. Not full FFT bin resolution.</summary>
    public static string Heatmap(IReadOnlyList<string> rowLabels, double[,] values, int width, int height, double minDb, double maxDb)
    {
        var rows = values.GetLength(0);
        var cols = values.GetLength(1);
        if (rows == 0 || cols == 0)
        {
            return EmptyChart(width, height);
        }

        var plotWidth = width - LeftMargin - RightMargin;
        var plotHeight = height - TopMargin - BottomMargin;
        var cellWidth = plotWidth / (double)cols;
        var cellHeight = plotHeight / (double)rows;

        var sb = new StringBuilder();
        sb.Append(SvgHeader(width, height));

        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                var fraction = Math.Clamp((values[r, c] - minDb) / (maxDb - minDb), 0, 1);
                var color = HeatColor(fraction);
                var x = LeftMargin + (c * cellWidth);
                var y = TopMargin + ((rows - 1 - r) * cellHeight);
                sb.Append(CultureInfo.InvariantCulture, $"<rect x=\"{x:F1}\" y=\"{y:F1}\" width=\"{cellWidth + 0.5:F1}\" height=\"{cellHeight + 0.5:F1}\" fill=\"{color}\" />");
            }
        }

        for (var r = 0; r < rows; r++)
        {
            var y = TopMargin + ((rows - 1 - r + 0.5) * cellHeight);
            sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{LeftMargin - 4}\" y=\"{y + 3:F1}\" font-size=\"8\" text-anchor=\"end\" fill=\"#666\">{System.Net.WebUtility.HtmlEncode(rowLabels[r])}</text>");
        }

        sb.Append("</svg>");
        return sb.ToString();
    }

    private static string SvgHeader(int width, int height) =>
        $"<svg viewBox=\"0 0 {width} {height}\" width=\"100%\" height=\"{height}\" xmlns=\"http://www.w3.org/2000/svg\" font-family=\"sans-serif\">";

    private static string EmptyChart(int width, int height) =>
        $"<svg viewBox=\"0 0 {width} {height}\" width=\"100%\" height=\"{height}\" xmlns=\"http://www.w3.org/2000/svg\">" +
        $"<text x=\"{width / 2}\" y=\"{height / 2}\" font-size=\"11\" text-anchor=\"middle\" fill=\"#999\">No data</text></svg>";

    private static void AppendAxes(StringBuilder sb, int width, int height, double yMin, double yMax, string? yAxisSuffix)
    {
        var plotHeight = height - TopMargin - BottomMargin;
        sb.Append(CultureInfo.InvariantCulture,
            $"<line x1=\"{LeftMargin}\" y1=\"{TopMargin}\" x2=\"{LeftMargin}\" y2=\"{TopMargin + plotHeight}\" stroke=\"#ccc\" />");
        sb.Append(CultureInfo.InvariantCulture,
            $"<line x1=\"{LeftMargin}\" y1=\"{TopMargin + plotHeight}\" x2=\"{width - RightMargin}\" y2=\"{TopMargin + plotHeight}\" stroke=\"#ccc\" />");

        foreach (var fraction in new[] { 0.0, 0.5, 1.0 })
        {
            var value = yMin + (fraction * (yMax - yMin));
            var y = TopMargin + plotHeight - (fraction * plotHeight);
            sb.Append(CultureInfo.InvariantCulture,
                $"<text x=\"{LeftMargin - 4}\" y=\"{y + 3:F1}\" font-size=\"9\" text-anchor=\"end\" fill=\"#666\">{value:F0}{yAxisSuffix}</text>");
        }
    }

    private static void AppendPolyline(
        StringBuilder sb, IReadOnlyList<(double X, double Y)> series, int width, int height,
        double xMin, double xMax, double yMin, double yMax, string color)
    {
        var plotWidth = width - LeftMargin - RightMargin;
        var plotHeight = height - TopMargin - BottomMargin;

        var points = new StringBuilder();
        foreach (var (x, y) in series)
        {
            var px = LeftMargin + ((x - xMin) / (xMax - xMin) * plotWidth);
            var py = TopMargin + MapY(y, yMin, yMax, plotHeight);
            points.Append(CultureInfo.InvariantCulture, $"{px:F1},{py:F1} ");
        }

        sb.Append(CultureInfo.InvariantCulture,
            $"<polyline points=\"{points}\" fill=\"none\" stroke=\"{color}\" stroke-width=\"1.5\" />");
    }

    private static double MapY(double value, double yMin, double yMax, double plotHeight)
    {
        var clamped = Math.Clamp(value, yMin, yMax);
        var fraction = (clamped - yMin) / (yMax - yMin);
        return plotHeight - (fraction * plotHeight);
    }

    /// <summary>Dark blue (quiet) -> yellow (loud), a simple two-stop gradient with no external palette dependency.</summary>
    private static string HeatColor(double fraction)
    {
        var r = (int)(30 + (fraction * 225));
        var g = (int)(30 + (fraction * 200));
        var b = (int)(90 - (fraction * 70));
        return $"rgb({Math.Clamp(r, 0, 255)},{Math.Clamp(g, 0, 255)},{Math.Clamp(b, 0, 255)})";
    }
}
