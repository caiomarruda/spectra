using AudioQualityAnalyzer.Core.Decoding;
using AudioQualityAnalyzer.Core.Models;
using R128Net;

namespace AudioQualityAnalyzer.Analysis.Loudness;

/// <summary>
/// ITU-R BS.1770 / EBU R128 loudness via R128Net, a bit-exact-verified port of libebur128 (the
/// reference implementation most loudness tools, including ffmpeg's loudnorm, are built on) —
/// hand-deriving K-weighting filter coefficients was judged too easy to get subtly wrong for a
/// metric this central to the app's conclusions.
/// </summary>
public static class LoudnessAnalyzer
{
    private const double ChunkDurationSeconds = 0.1;

    public static LoudnessAnalysis Analyze(DecodedAudio audio)
    {
        using var meter = new LoudnessMeter(audio.ChannelCount, audio.SampleRateHz, LoudnessModes.All);

        var frameCount = audio.ChannelCount > 0 ? audio.Channels[0].Length : 0;
        var chunkFrames = Math.Max(1, (int)(ChunkDurationSeconds * audio.SampleRateHz));
        var interleaved = new float[chunkFrames * audio.ChannelCount];

        var timeline = new List<LoudnessTimePoint>();
        var momentaryMax = double.NegativeInfinity;
        var shortTermMax = double.NegativeInfinity;

        for (var start = 0; start < frameCount; start += chunkFrames)
        {
            var framesInChunk = Math.Min(chunkFrames, frameCount - start);
            for (var frame = 0; frame < framesInChunk; frame++)
            {
                for (var channel = 0; channel < audio.ChannelCount; channel++)
                {
                    interleaved[(frame * audio.ChannelCount) + channel] = audio.Channels[channel][start + frame];
                }
            }

            meter.AddFrames(interleaved.AsSpan(0, framesInChunk * audio.ChannelCount));

            var momentary = meter.MomentaryLoudness;
            var shortTerm = meter.ShortTermLoudness;
            timeline.Add(new LoudnessTimePoint
            {
                Time = TimeSpan.FromSeconds((double)start / audio.SampleRateHz),
                MomentaryLufs = momentary,
                ShortTermLufs = shortTerm,
            });

            if (momentary > momentaryMax)
            {
                momentaryMax = momentary;
            }
            if (shortTerm > shortTermMax)
            {
                shortTermMax = shortTerm;
            }
        }

        var samplePeakPerChannel = new double[audio.ChannelCount];
        var truePeakPerChannel = new double[audio.ChannelCount];
        for (var c = 0; c < audio.ChannelCount; c++)
        {
            samplePeakPerChannel[c] = LinearToDb(meter.GetSamplePeak(c));
            truePeakPerChannel[c] = LinearToDb(meter.GetTruePeak(c));
        }

        return new LoudnessAnalysis
        {
            IntegratedLufs = meter.IntegratedLoudness,
            MomentaryMaxLufs = momentaryMax,
            ShortTermMaxLufs = shortTermMax,
            LoudnessRangeLu = meter.LoudnessRange,
            SamplePeakDbfs = samplePeakPerChannel.Length > 0 ? samplePeakPerChannel.Max() : double.NegativeInfinity,
            TruePeakDbfs = truePeakPerChannel.Length > 0 ? truePeakPerChannel.Max() : double.NegativeInfinity,
            SamplePeakPerChannelDbfs = samplePeakPerChannel,
            TruePeakPerChannelDbfs = truePeakPerChannel,
            LoudnessOverTime = timeline,
        };
    }

    private static double LinearToDb(double linear) => linear > 0 ? 20.0 * Math.Log10(linear) : double.NegativeInfinity;
}
