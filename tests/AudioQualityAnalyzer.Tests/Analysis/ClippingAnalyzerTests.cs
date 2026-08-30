using AudioQualityAnalyzer.Analysis.Dynamics;
using AudioQualityAnalyzer.Core.Decoding;
using Xunit;

namespace AudioQualityAnalyzer.Tests.Analysis;

public class ClippingAnalyzerTests
{
    private const int SampleRate = 44100;

    [Fact]
    public void Analyze_NoClipping_ReportsZero()
    {
        var audio = CreateMono(Enumerable.Repeat(0.5f, 1000).ToArray());

        var result = ClippingAnalyzer.Analyze(audio);

        Assert.Equal(0, result.TotalClippedSamples);
        Assert.Equal(0, result.ClipEventCount);
        Assert.False(result.IsSevere);
    }

    [Fact]
    public void Analyze_SingleSampleClip_IsNotSevere()
    {
        var samples = new float[1000];
        samples[500] = 1.0f;
        var audio = CreateMono(samples);

        var result = ClippingAnalyzer.Analyze(audio);

        Assert.Equal(1, result.TotalClippedSamples);
        Assert.Equal(1, result.ClipEventCount);
        Assert.False(result.IsSevere);
    }

    [Fact]
    public void Analyze_SustainedClipping_IsSevere()
    {
        // ~4.5 ms at 44100 Hz — comfortably over the 2 ms severity threshold.
        var samples = new float[2000];
        for (var i = 500; i < 700; i++)
        {
            samples[i] = 1.0f;
        }
        var audio = CreateMono(samples);

        var result = ClippingAnalyzer.Analyze(audio);

        Assert.Equal(200, result.TotalClippedSamples);
        Assert.Equal(1, result.ClipEventCount);
        Assert.True(result.IsSevere);
    }

    [Fact]
    public void Analyze_TwoSeparateClipRuns_CountsTwoEvents()
    {
        var samples = new float[1000];
        samples[100] = 1.0f;
        samples[101] = 1.0f;
        samples[500] = 1.0f;
        var audio = CreateMono(samples);

        var result = ClippingAnalyzer.Analyze(audio);

        Assert.Equal(2, result.ClipEventCount);
        Assert.Equal(3, result.TotalClippedSamples);
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
