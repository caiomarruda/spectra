using AudioQualityAnalyzer.Analysis.Waveform;
using AudioQualityAnalyzer.Core.Decoding;
using Xunit;

namespace AudioQualityAnalyzer.Tests.Analysis;

public class WaveformAnalyzerTests
{
    private const int SampleRate = 44100;

    [Fact]
    public void Analyze_ConstantAmplitudeSignal_PeakAndRmsEqualAmplitude()
    {
        var samples = Enumerable.Repeat(0.5f, SampleRate).ToArray();
        var audio = CreateMono(samples);

        var result = WaveformAnalyzer.Analyze(audio);

        Assert.Equal(0.5f, result.PeakAmplitude, precision: 5);
        Assert.Equal(0.5f, result.RmsAmplitude, precision: 5);
    }

    [Fact]
    public void Analyze_SilenceThenSignal_DetectsLeadingSilenceDuration()
    {
        var samples = new float[SampleRate];
        var silentFrames = SampleRate / 2; // 0.5s
        for (var i = silentFrames; i < samples.Length; i++)
        {
            samples[i] = 0.8f;
        }
        var audio = CreateMono(samples);

        var result = WaveformAnalyzer.Analyze(audio);

        Assert.Equal(TimeSpan.FromSeconds(0.5), result.LeadingSilence);
    }

    [Fact]
    public void Analyze_SignalThenSilence_DetectsTrailingSilenceDuration()
    {
        var samples = new float[SampleRate];
        var soundFrames = SampleRate / 2;
        for (var i = 0; i < soundFrames; i++)
        {
            samples[i] = 0.8f;
        }
        var audio = CreateMono(samples);

        var result = WaveformAnalyzer.Analyze(audio);

        Assert.Equal(TimeSpan.FromSeconds(0.5), result.TrailingSilence);
    }

    [Fact]
    public void Analyze_TwoChannelsWithDifferentAmplitudes_ReportsPerChannelStats()
    {
        var left = Enumerable.Repeat(0.2f, 1000).ToArray();
        var right = Enumerable.Repeat(0.6f, 1000).ToArray();
        var audio = new DecodedAudio
        {
            SampleRateHz = SampleRate,
            ChannelCount = 2,
            Channels = [left, right],
            DecoderName = "Test",
            DecoderVersion = null,
            SourceSampleRateHz = SampleRate,
        };

        var result = WaveformAnalyzer.Analyze(audio);

        Assert.Equal(2, result.PerChannel.Count);
        Assert.Equal(0.2f, result.PerChannel[0].Peak, precision: 5);
        Assert.Equal(0.6f, result.PerChannel[1].Peak, precision: 5);
    }

    [Fact]
    public void Analyze_OneSecondAt100MsWindows_ProducesTenWindows()
    {
        var samples = new float[SampleRate];
        var audio = CreateMono(samples);

        var result = WaveformAnalyzer.Analyze(audio);

        Assert.Equal(10, result.RmsOverTime.Count);
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
