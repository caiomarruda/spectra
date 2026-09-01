using Spectra.Analysis.Stereo;
using Spectra.Core.Decoding;
using Xunit;

namespace Spectra.Tests.Analysis;

public class StereoAnalyzerTests
{
    private const int SampleRate = 44100;

    [Fact]
    public void Analyze_MonoAudio_ReturnsNull()
    {
        var audio = new DecodedAudio
        {
            SampleRateHz = SampleRate,
            ChannelCount = 1,
            Channels = [new float[1000]],
            DecoderName = "Test",
            DecoderVersion = null,
            SourceSampleRateHz = SampleRate,
        };

        var result = StereoAnalyzer.Analyze(audio);

        Assert.Null(result);
    }

    [Fact]
    public void Analyze_IdenticalChannels_DetectsMonoDisguisedAsStereo()
    {
        var samples = GenerateSine(1000, 440);
        var audio = CreateStereo(samples, samples);

        var result = StereoAnalyzer.Analyze(audio);

        Assert.NotNull(result);
        Assert.InRange(result!.CorrelationCoefficient, 0.99, 1.01);
        Assert.True(result.IsMonoDisguisedAsStereo);
        Assert.False(result.HasPolarityInversion);
    }

    [Fact]
    public void Analyze_InvertedRightChannel_DetectsPolarityInversion()
    {
        var left = GenerateSine(1000, 440);
        var right = left.Select(s => -s).ToArray();
        var audio = CreateStereo(left, right);

        var result = StereoAnalyzer.Analyze(audio);

        Assert.NotNull(result);
        Assert.InRange(result!.CorrelationCoefficient, -1.01, -0.99);
        Assert.True(result.HasPolarityInversion);
        Assert.True(result.HasPhaseProblems);
    }

    [Fact]
    public void Analyze_SilentRightChannel_DetectsMissingChannel()
    {
        var left = GenerateSine(1000, 440);
        var right = new float[1000];
        var audio = CreateStereo(left, right);

        var result = StereoAnalyzer.Analyze(audio);

        Assert.NotNull(result);
        Assert.True(result!.IsChannelEffectivelyMissing);
        Assert.True(result.IsSeverelyImbalanced);
    }

    [Fact]
    public void Analyze_InvertedChannel_HasPoorMonoCompatibility()
    {
        var left = GenerateSine(1000, 440);
        var right = left.Select(s => -s).ToArray();
        var audio = CreateStereo(left, right);

        var result = StereoAnalyzer.Analyze(audio);

        Assert.NotNull(result);
        Assert.InRange(result!.MonoCompatibilityRatio, 0.0, 0.01);
    }

    private static float[] GenerateSine(int sampleCount, double frequencyHz)
    {
        var samples = new float[sampleCount];
        for (var i = 0; i < sampleCount; i++)
        {
            samples[i] = (float)Math.Sin(2 * Math.PI * frequencyHz * i / SampleRate);
        }
        return samples;
    }

    private static DecodedAudio CreateStereo(float[] left, float[] right) => new()
    {
        SampleRateHz = SampleRate,
        ChannelCount = 2,
        Channels = [left, right],
        DecoderName = "Test",
        DecoderVersion = null,
        SourceSampleRateHz = SampleRate,
    };
}
