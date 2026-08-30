using AudioQualityAnalyzer.Analysis.Loudness;
using AudioQualityAnalyzer.Core.Decoding;
using Xunit;

namespace AudioQualityAnalyzer.Tests.Analysis;

public class LoudnessAnalyzerTests
{
    private const int SampleRate = 44100;

    [Fact]
    public void Analyze_Silence_ReportsNegativeInfinityIntegratedLoudness()
    {
        var audio = CreateMono(new float[SampleRate * 2]);

        var result = LoudnessAnalyzer.Analyze(audio);

        Assert.True(double.IsNegativeInfinity(result.IntegratedLufs));
    }

    [Fact]
    public void Analyze_FullScale1KHzSineWave_ReportsPlausibleLoudnessAndPeak()
    {
        var samples = new float[SampleRate * 3];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (float)Math.Sin(2 * Math.PI * 1000 * i / SampleRate);
        }
        var audio = CreateMono(samples);

        var result = LoudnessAnalyzer.Analyze(audio);

        // A full-scale 1 kHz tone is a well-known BS.1770 reference point: K-weighting is
        // near-unity around 1 kHz, so integrated loudness should land close to -3 LUFS (the
        // sine's own RMS-to-peak ratio), not at some arbitrary value.
        Assert.InRange(result.IntegratedLufs, -6.0, -1.0);
        Assert.InRange(result.SamplePeakDbfs, -0.5, 0.5);
    }

    [Fact]
    public void Analyze_LouderPassage_ReportsHigherMomentaryThanQuietPassage()
    {
        var samples = new float[SampleRate * 4];
        for (var i = 0; i < samples.Length; i++)
        {
            var amplitude = i < samples.Length / 2 ? 0.05f : 0.8f;
            samples[i] = amplitude * (float)Math.Sin(2 * Math.PI * 1000 * i / SampleRate);
        }
        var audio = CreateMono(samples);

        var result = LoudnessAnalyzer.Analyze(audio);

        var firstHalf = result.LoudnessOverTime.Where(p => p.Time.TotalSeconds < 2).ToList();
        var secondHalf = result.LoudnessOverTime.Where(p => p.Time.TotalSeconds >= 2.5).ToList();

        Assert.True(secondHalf.Max(p => p.MomentaryLufs) > firstHalf.Max(p => p.MomentaryLufs));
    }

    private static DecodedAudio CreateMono(float[] samples) => new()
    {
        SampleRateHz = SampleRate,
        ChannelCount = 1,
        Channels = [samples],
        DecoderName = "Test",
        DecoderVersion = null,
        SourceSampleRateHz = SampleRate,
    };
}
