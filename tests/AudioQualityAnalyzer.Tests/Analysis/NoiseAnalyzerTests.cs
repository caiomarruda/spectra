using AudioQualityAnalyzer.Analysis.Noise;
using AudioQualityAnalyzer.Analysis.Waveform;
using AudioQualityAnalyzer.Core.Decoding;
using Xunit;

namespace AudioQualityAnalyzer.Tests.Analysis;

public class NoiseAnalyzerTests
{
    private const int SampleRate = 44100;

    [Fact]
    public void Analyze_SamplesWithConstantOffset_DetectsSignificantDcOffset()
    {
        var samples = new float[SampleRate];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = 0.02f + (0.1f * MathF.Sin(2 * MathF.PI * 440 * i / SampleRate));
        }
        var audio = CreateMono(samples);
        var waveform = WaveformAnalyzer.Analyze(audio);

        var result = NoiseAnalyzer.Analyze(audio, waveform);

        Assert.True(result.HasSignificantDcOffset);
    }

    [Fact]
    public void Analyze_ZeroMeanSignal_NoDcOffset()
    {
        var samples = new float[SampleRate];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = 0.5f * MathF.Sin(2 * MathF.PI * 440 * i / SampleRate);
        }
        var audio = CreateMono(samples);
        var waveform = WaveformAnalyzer.Analyze(audio);

        var result = NoiseAnalyzer.Analyze(audio, waveform);

        Assert.False(result.HasSignificantDcOffset);
    }

    [Fact]
    public void Analyze_LongInternalSilenceGap_DetectsExcessiveSilence()
    {
        var samples = new float[SampleRate * 20];
        for (var i = 0; i < samples.Length; i++)
        {
            var inGap = i > SampleRate * 5 && i < SampleRate * 13; // 8s internal gap.
            samples[i] = inGap ? 0f : 0.5f * MathF.Sin(2 * MathF.PI * 440 * i / SampleRate);
        }
        var audio = CreateMono(samples);
        var waveform = WaveformAnalyzer.Analyze(audio);

        var result = NoiseAnalyzer.Analyze(audio, waveform);

        Assert.True(result.HasExcessiveInternalSilence);
    }

    [Fact]
    public void Analyze_OnlyLeadingAndTrailingSilence_DoesNotCountAsInternal()
    {
        var samples = new float[SampleRate * 10];
        for (var i = 0; i < samples.Length; i++)
        {
            var inEdge = i < SampleRate * 3 || i > samples.Length - (SampleRate * 3);
            samples[i] = inEdge ? 0f : 0.5f * MathF.Sin(2 * MathF.PI * 440 * i / SampleRate);
        }
        var audio = CreateMono(samples);
        var waveform = WaveformAnalyzer.Analyze(audio);

        var result = NoiseAnalyzer.Analyze(audio, waveform);

        Assert.False(result.HasExcessiveInternalSilence);
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
