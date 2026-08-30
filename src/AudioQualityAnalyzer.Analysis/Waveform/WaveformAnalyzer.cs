using AudioQualityAnalyzer.Core.Decoding;
using AudioQualityAnalyzer.Core.Models;

namespace AudioQualityAnalyzer.Analysis.Waveform;

public static class WaveformAnalyzer
{
    private const double RmsWindowDurationSeconds = 0.1;
    private const float SilenceAmplitudeThreshold = 0.001f; // ~ -60 dBFS

    public static WaveformAnalysis Analyze(DecodedAudio audio)
    {
        var channelStats = new List<ChannelWaveformStats>(audio.ChannelCount);
        float overallPeak = 0f;
        float overallMin = 0f;
        float overallMax = 0f;
        double sumSquares = 0;
        long totalSamples = 0;

        for (var c = 0; c < audio.ChannelCount; c++)
        {
            var samples = audio.Channels[c];
            var peak = 0f;
            var min = 0f;
            var max = 0f;
            double channelSumSquares = 0;

            for (var i = 0; i < samples.Length; i++)
            {
                var sample = samples[i];
                var abs = MathF.Abs(sample);
                if (abs > peak)
                {
                    peak = abs;
                }
                if (sample < min)
                {
                    min = sample;
                }
                if (sample > max)
                {
                    max = sample;
                }
                channelSumSquares += (double)sample * sample;
            }

            var rms = samples.Length > 0 ? (float)Math.Sqrt(channelSumSquares / samples.Length) : 0f;
            channelStats.Add(new ChannelWaveformStats
            {
                ChannelIndex = c,
                Peak = peak,
                Rms = rms,
                MinSample = min,
                MaxSample = max,
            });

            overallPeak = Math.Max(overallPeak, peak);
            overallMin = Math.Min(overallMin, min);
            overallMax = Math.Max(overallMax, max);
            sumSquares += channelSumSquares;
            totalSamples += samples.Length;
        }

        var overallRms = totalSamples > 0 ? (float)Math.Sqrt(sumSquares / totalSamples) : 0f;

        return new WaveformAnalysis
        {
            PeakAmplitude = overallPeak,
            RmsAmplitude = overallRms,
            MinSample = overallMin,
            MaxSample = overallMax,
            LeadingSilence = ComputeLeadingSilence(audio),
            TrailingSilence = ComputeTrailingSilence(audio),
            PerChannel = channelStats,
            RmsOverTime = ComputeRmsWindows(audio),
        };
    }

    private static List<RmsWindow> ComputeRmsWindows(DecodedAudio audio)
    {
        var windows = new List<RmsWindow>();
        var frameCount = audio.ChannelCount > 0 ? audio.Channels[0].Length : 0;
        var windowSizeInFrames = (int)(RmsWindowDurationSeconds * audio.SampleRateHz);
        if (windowSizeInFrames <= 0 || frameCount == 0)
        {
            return windows;
        }

        for (var start = 0; start < frameCount; start += windowSizeInFrames)
        {
            var length = Math.Min(windowSizeInFrames, frameCount - start);
            double sumSquares = 0;
            var peak = 0f;

            for (var i = start; i < start + length; i++)
            {
                var mixed = MixToMono(audio, i);
                sumSquares += (double)mixed * mixed;
                var abs = MathF.Abs(mixed);
                if (abs > peak)
                {
                    peak = abs;
                }
            }

            windows.Add(new RmsWindow
            {
                StartTime = TimeSpan.FromSeconds((double)start / audio.SampleRateHz),
                Rms = (float)Math.Sqrt(sumSquares / length),
                Peak = peak,
            });
        }

        return windows;
    }

    private static TimeSpan ComputeLeadingSilence(DecodedAudio audio)
    {
        var frameCount = audio.ChannelCount > 0 ? audio.Channels[0].Length : 0;
        for (var i = 0; i < frameCount; i++)
        {
            if (IsAboveSilenceThreshold(audio, i))
            {
                return TimeSpan.FromSeconds((double)i / audio.SampleRateHz);
            }
        }

        return TimeSpan.FromSeconds((double)frameCount / audio.SampleRateHz);
    }

    private static TimeSpan ComputeTrailingSilence(DecodedAudio audio)
    {
        var frameCount = audio.ChannelCount > 0 ? audio.Channels[0].Length : 0;
        for (var i = frameCount - 1; i >= 0; i--)
        {
            if (IsAboveSilenceThreshold(audio, i))
            {
                return TimeSpan.FromSeconds((double)(frameCount - 1 - i) / audio.SampleRateHz);
            }
        }

        return TimeSpan.FromSeconds((double)frameCount / audio.SampleRateHz);
    }

    private static bool IsAboveSilenceThreshold(DecodedAudio audio, int frameIndex)
    {
        for (var c = 0; c < audio.ChannelCount; c++)
        {
            if (MathF.Abs(audio.Channels[c][frameIndex]) > SilenceAmplitudeThreshold)
            {
                return true;
            }
        }

        return false;
    }

    private static float MixToMono(DecodedAudio audio, int frameIndex)
    {
        float sum = 0;
        for (var c = 0; c < audio.ChannelCount; c++)
        {
            sum += audio.Channels[c][frameIndex];
        }

        return sum / audio.ChannelCount;
    }
}
