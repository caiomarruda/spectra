using AudioQualityAnalyzer.Core.Decoding;
using NLayer;

namespace AudioQualityAnalyzer.Audio.Decoding;

/// <summary>
/// Decodes MP3 to PCM using NLayer, a managed MPEG-1/2/2.5 Layer III decoder. Chosen over an
/// FFmpeg-process backend so the analyzer has no external binary dependency; swap in another
/// <see cref="IAudioDecoder"/> implementation if an FFmpeg backend becomes preferable later.
/// </summary>
public sealed class NLayerAudioDecoder : IAudioDecoder
{
    private const int ReadChunkSizeInFrames = 4096;

    public DecodedAudio Decode(string path)
    {
        using var stream = File.OpenRead(path);
        using var mpegFile = new MpegFile(stream);

        var channelCount = mpegFile.Channels;
        var sampleRateHz = mpegFile.SampleRate;

        var channels = new List<float>[channelCount];
        for (var i = 0; i < channelCount; i++)
        {
            channels[i] = [];
        }

        var interleavedBuffer = new float[ReadChunkSizeInFrames * channelCount];
        int samplesRead;
        while ((samplesRead = mpegFile.ReadSamples(interleavedBuffer, 0, interleavedBuffer.Length)) > 0)
        {
            var framesRead = samplesRead / channelCount;
            for (var frame = 0; frame < framesRead; frame++)
            {
                for (var channel = 0; channel < channelCount; channel++)
                {
                    channels[channel].Add(interleavedBuffer[(frame * channelCount) + channel]);
                }
            }
        }

        return new DecodedAudio
        {
            SampleRateHz = sampleRateHz,
            ChannelCount = channelCount,
            Channels = channels.Select(c => c.ToArray()).ToArray(),
            DecoderName = "NLayer",
            DecoderVersion = typeof(MpegFile).Assembly.GetName().Version?.ToString(),
            SourceSampleRateHz = sampleRateHz,
        };
    }
}
