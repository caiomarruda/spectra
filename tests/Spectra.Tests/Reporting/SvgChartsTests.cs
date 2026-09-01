using Spectra.Reporting.Html;
using Xunit;

namespace Spectra.Tests.Reporting;

public class SvgChartsTests
{
    [Fact]
    public void LineChart_EmptySeries_ReturnsPlaceholderWithoutThrowing()
    {
        var svg = SvgCharts.LineChart([], 400, 100, "#000");

        Assert.Contains("No data", svg);
    }

    [Fact]
    public void LineChart_ConstantValue_DoesNotDivideByZero()
    {
        var series = Enumerable.Range(0, 5).Select(i => ((double)i, 5.0)).ToList();

        var svg = SvgCharts.LineChart(series, 400, 100, "#000");

        Assert.DoesNotContain("NaN", svg);
        Assert.DoesNotContain("Infinity", svg);
    }

    [Fact]
    public void BarChart_EmptyBars_ReturnsPlaceholderWithoutThrowing()
    {
        var svg = SvgCharts.BarChart([], 400, 100, "#000", -100, 0);

        Assert.Contains("No data", svg);
    }

    [Fact]
    public void BarChart_ValuesOutsideRange_AreClampedNotThrown()
    {
        var bars = new List<(string, double)> { ("A", -500), ("B", 500) };

        var svg = SvgCharts.BarChart(bars, 400, 100, "#000", -100, 0);

        Assert.DoesNotContain("NaN", svg);
    }

    [Fact]
    public void Heatmap_EmptyMatrix_ReturnsPlaceholderWithoutThrowing()
    {
        var svg = SvgCharts.Heatmap([], new double[0, 0], 400, 100, -100, 0);

        Assert.Contains("No data", svg);
    }

    [Fact]
    public void Heatmap_NonEmptyMatrix_ProducesOneRectPerCell()
    {
        var values = new double[2, 3];
        var svg = SvgCharts.Heatmap(["Row0", "Row1"], values, 400, 100, -100, 0);

        var rectCount = svg.Split("<rect").Length - 1;
        Assert.Equal(6, rectCount);
    }
}
