using Spectra.Analysis.Dynamics;
using Spectra.Analysis.Waveform;
using Spectra.Core.Decoding;
using Xunit;

namespace Spectra.Tests.Analysis;

public class DynamicRangeAnalyzerTests
{
    private const int SampleRate = 44100;

    [Fact]
    public void Analyze_ConstantAmplitudeSignal_HasNearZeroCrestFactor()
    {
        var audio = CreateMono(Enumerable.Repeat(0.5f, SampleRate).ToArray());
        var waveform = WaveformAnalyzer.Analyze(audio);

        var result = DynamicRangeAnalyzer.Analyze(audio, waveform);

        Assert.InRange(result.CrestFactorDb, -0.1, 0.1);
    }

    [Fact]
    public void Analyze_SineWave_HasCrestFactorNearThreeDb()
    {
        var samples = new float[SampleRate];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (float)Math.Sin(2 * Math.PI * 1000 * i / SampleRate);
        }
        var audio = CreateMono(samples);
        var waveform = WaveformAnalyzer.Analyze(audio);

        var result = DynamicRangeAnalyzer.Analyze(audio, waveform);

        Assert.InRange(result.CrestFactorDb, 2.5, 3.5);
    }

    [Fact]
    public void Analyze_SamplesAtFullScale_CountsTowardNearFullScalePercentage()
    {
        var samples = new float[1000];
        for (var i = 0; i < 100; i++)
        {
            samples[i] = 1.0f;
        }
        var audio = CreateMono(samples);
        var waveform = WaveformAnalyzer.Analyze(audio);

        var result = DynamicRangeAnalyzer.Analyze(audio, waveform);

        Assert.Equal(10.0, result.PercentSamplesNearFullScale, precision: 1);
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
