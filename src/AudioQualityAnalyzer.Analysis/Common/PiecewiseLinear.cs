namespace AudioQualityAnalyzer.Analysis.Common;

/// <summary>Linear interpolation over a sorted set of (Input, Output) points, clamped at the ends.</summary>
public static class PiecewiseLinear
{
    public static double Interpolate(IReadOnlyList<(double Input, double Output)> points, double input)
    {
        if (input <= points[0].Input)
        {
            return points[0].Output;
        }
        if (input >= points[^1].Input)
        {
            return points[^1].Output;
        }

        for (var i = 0; i < points.Count - 1; i++)
        {
            var (x0, y0) = points[i];
            var (x1, y1) = points[i + 1];
            if (input < x0 || input > x1)
            {
                continue;
            }

            var fraction = (input - x0) / (x1 - x0);
            return y0 + (fraction * (y1 - y0));
        }

        return points[^1].Output;
    }
}
