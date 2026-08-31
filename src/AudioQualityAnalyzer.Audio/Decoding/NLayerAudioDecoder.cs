using AudioQualityAnalyzer.Audio.Mp3;
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
        var data = File.ReadAllBytes(path);
        var audioStartOffset = FindAudioStartOffset(data);

        using var stream = new MemoryStream(data, audioStartOffset, data.Length - audioStartOffset, writable: false);
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

    /// <summary>
    /// NLayer's own frame scanner has no false-sync resync check and can throw (a bug in its
    /// Layer II CRC path — real MP3s are Layer III, but an unusual leading tag, such as a second
    /// ID3v2 tag or embedded artwork, can contain bytes that coincidentally look like a Layer II
    /// sync) if handed the raw file including leading metadata. Mp3FrameParser's own resync logic
    /// (requires two consecutive frames to parse before accepting a sync candidate) reliably finds
    /// the true first audio frame for the encoding-metadata pass, so reuse it here and hand NLayer
    /// a stream that already starts at real audio data instead of the whole file.
    /// </summary>
    private static int FindAudioStartOffset(byte[] data)
    {
        var parseResult = Mp3FrameParser.Parse(data);
        return parseResult.AudioStartOffset >= 0 ? (int)parseResult.AudioStartOffset : 0;
    }
}
