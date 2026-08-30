using AudioQualityAnalyzer.Core.Decoding;
using AudioQualityAnalyzer.Core.Models;

namespace AudioQualityAnalyzer.Analysis.Stereo;

public static class StereoAnalyzer
{
    private const double Epsilon = 1e-30;

    /// <summary>Returns null for mono audio — there is no stereo image to analyze.</summary>
    public static StereoAnalysis? Analyze(DecodedAudio audio)
    {
        if (audio.ChannelCount < 2)
        {
            return null;
        }

        var left = audio.Channels[0];
        var right = audio.Channels[1];
        var sampleCount = Math.Min(left.Length, right.Length);

        double sumLR = 0, sumLL = 0, sumRR = 0, sumMidSq = 0, sumSideSq = 0;
        for (var i = 0; i < sampleCount; i++)
        {
            var l = (double)left[i];
            var r = (double)right[i];
            sumLR += l * r;
            sumLL += l * l;
            sumRR += r * r;

            var mid = (l + r) / 2.0;
            var side = (l - r) / 2.0;
            sumMidSq += mid * mid;
            sumSideSq += side * side;
        }

        var correlation = sumLL > Epsilon && sumRR > Epsilon
            ? sumLR / Math.Sqrt(sumLL * sumRR)
            : 0.0;

        var leftRmsDb = ToDb(Math.Sqrt(sumLL / sampleCount));
        var rightRmsDb = ToDb(Math.Sqrt(sumRR / sampleCount));
        var balanceDb = rightRmsDb - leftRmsDb;

        var midRmsDb = ToDb(Math.Sqrt(sumMidSq / sampleCount));
        var sideRmsDb = ToDb(Math.Sqrt(sumSideSq / sampleCount));
        var sideToMidDb = sideRmsDb - midRmsDb;

        var monoRms = Math.Sqrt(sumMidSq / sampleCount);
        var averageChannelRms = (Math.Sqrt(sumLL / sampleCount) + Math.Sqrt(sumRR / sampleCount)) / 2.0;
        var monoCompatibility = averageChannelRms > Epsilon ? monoRms / averageChannelRms : 1.0;

        return new StereoAnalysis
        {
            CorrelationCoefficient = correlation,
            ChannelBalanceDb = balanceDb,
            MonoCompatibilityRatio = monoCompatibility,
            MidEnergyDb = midRmsDb,
            SideEnergyDb = sideRmsDb,
            SideToMidRatioDb = sideToMidDb,
            IsChannelEffectivelyMissing = Math.Abs(balanceDb) >= StereoSettings.MissingChannelDb,
            IsSeverelyImbalanced = Math.Abs(balanceDb) >= StereoSettings.SevereImbalanceDb,
            IsMonoDisguisedAsStereo = correlation >= StereoSettings.MonoDisguiseCorrelationThreshold
                && sideToMidDb <= StereoSettings.MonoDisguiseSideToMidDb,
            HasPhaseProblems = correlation <= StereoSettings.PhaseProblemCorrelationThreshold,
            HasPolarityInversion = correlation <= StereoSettings.PolarityInversionCorrelationThreshold,
            HasExcessiveSideContent = sideToMidDb >= StereoSettings.ExcessiveSideContentDb,
        };
    }

    private static double ToDb(double linear) => linear > 0 ? 20.0 * Math.Log10(linear) : double.NegativeInfinity;
}
