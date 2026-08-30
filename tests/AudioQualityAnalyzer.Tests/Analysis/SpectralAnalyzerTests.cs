using AudioQualityAnalyzer.Analysis.Spectral;
using AudioQualityAnalyzer.Core.Decoding;
using Xunit;

namespace AudioQualityAnalyzer.Tests.Analysis;

public class SpectralAnalyzerTests
{
    private const int SampleRate = 44100;

    [Fact]
    public void Analyze_SignalBandLimitedTo3Khz_DetectsCutoffNearThatFrequency_NotNyquist()
    {
        var samples = GenerateMultiToneSignal(SampleRate * 3, [440, 1000, 2800]);
        var audio = CreateMono(samples);

        var result = SpectralAnalyzer.Analyze(audio);

        Assert.True(
            result.EffectiveBandwidthHz < 8000,
            $"Expected a cutoff well below Nyquist for a signal with no content above 2.8 kHz, but got {result.EffectiveBandwidthHz} Hz.");
    }

    [Fact]
    public void Analyze_FullBandwidthWhiteNoise_ReportsBandwidthNearNyquist()
    {
        var random = new Random(42);
        var samples = new float[SampleRate * 2];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (float)(random.NextDouble() * 2 - 1) * 0.5f;
        }
        var audio = CreateMono(samples);

        var result = SpectralAnalyzer.Analyze(audio);

        Assert.True(
            result.EffectiveBandwidthHz > 18000,
            $"Expected white noise to show energy near Nyquist, but effective bandwidth was {result.EffectiveBandwidthHz} Hz.");
    }

    [Fact]
    public void Analyze_Silence_DoesNotThrowAndReportsLowConfidence()
    {
        var audio = CreateMono(new float[SampleRate]);

        var result = SpectralAnalyzer.Analyze(audio);

        Assert.Equal(Core.Enums.ConfidenceLevel.Low, result.BandwidthConfidence);
    }

    private static float[] GenerateMultiToneSignal(int sampleCount, double[] frequenciesHz)
    {
        var samples = new float[sampleCount];
        for (var i = 0; i < sampleCount; i++)
        {
            double sum = 0;
            foreach (var freq in frequenciesHz)
            {
                sum += Math.Sin(2 * Math.PI * freq * i / SampleRate);
            }
            samples[i] = (float)(sum / frequenciesHz.Length) * 0.8f;
        }
        return samples;
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
