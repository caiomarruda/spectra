using AudioQualityAnalyzer.Core.Decoding;
using AudioQualityAnalyzer.Core.Models;

namespace AudioQualityAnalyzer.Analysis.Dynamics;

public static class ClippingAnalyzer
{
    /// <summary>~ -0.0087 dBFS: essentially full scale. Deliberately tighter than DynamicRangeAnalyzer's "near full scale" (-1 dBFS) statistic, which flags hot masters rather than actual clipping.</summary>
    private const float ClippingLinearThreshold = 0.999f;

    /// <summary>A run shorter than this is a single click, usually inaudible; longer runs are the sustained, audibly distorting kind the spec asks to distinguish.</summary>
    private static readonly TimeSpan SevereClipDurationThreshold = TimeSpan.FromMilliseconds(2);

    public static ClippingAnalysis Analyze(DecodedAudio audio)
    {
        var clippedPerChannel = new long[audio.ChannelCount];
        long totalClipped = 0;
        long totalSamples = 0;
        var clipEventCount = 0;
        var longestRunSamples = 0;

        for (var c = 0; c < audio.ChannelCount; c++)
        {
            var samples = audio.Channels[c];
            totalSamples += samples.Length;
            var currentRun = 0;

            foreach (var sample in samples)
            {
                if (MathF.Abs(sample) >= ClippingLinearThreshold)
                {
                    if (currentRun == 0)
                    {
                        clipEventCount++;
                    }
                    currentRun++;
                    clippedPerChannel[c]++;
                    totalClipped++;
                    if (currentRun > longestRunSamples)
                    {
                        longestRunSamples = currentRun;
                    }
                }
                else
                {
                    currentRun = 0;
                }
            }
        }

        var longestRunDuration = audio.SampleRateHz > 0
            ? TimeSpan.FromSeconds((double)longestRunSamples / audio.SampleRateHz)
            : TimeSpan.Zero;

        return new ClippingAnalysis
        {
            TotalClippedSamples = totalClipped,
            ClippedPercentage = totalSamples > 0 ? 100.0 * totalClipped / totalSamples : 0,
            ClipEventCount = clipEventCount,
            LongestClipDuration = longestRunDuration,
            ClippedSamplesPerChannel = clippedPerChannel,
            IsSevere = longestRunDuration >= SevereClipDurationThreshold,
        };
    }
}
