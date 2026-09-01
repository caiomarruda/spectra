using AudioQualityAnalyzer.Core.Decoding;

namespace AudioQualityAnalyzer.Audio.Flac;

/// <summary>
/// Decodes FLAC to PCM with a from-scratch, dependency-free decoder (RFC 9639) — chosen over an
/// available NuGet package specifically because that package had no clear license, and this
/// project is meant to be published with a clean one (see FlacFrameDecoder for the bitstream
/// implementation).
/// </summary>
public sealed class FlacAudioDecoder : IAudioDecoder
{
    public DecodedAudio Decode(string path)
    {
        var file = FlacContainerReader.Read(path);
        var channelCount = file.StreamInfo.ChannelCount;

        var channels = new List<float>[channelCount];
        for (var c = 0; c < channelCount; c++)
        {
            channels[c] = [];
        }

        var scale = 1f / (1L << (file.StreamInfo.BitsPerSample - 1));
        var offset = file.AudioStartOffset;
        string? partialDecodeReason = null;

        while (offset < file.FileBytes.Length)
        {
            FlacFrameResult result;
            try
            {
                result = FlacFrameDecoder.DecodeFrame(file.FileBytes, offset, file.FileBytes.Length, file.StreamInfo);
            }
            catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException or IndexOutOfRangeException or ArgumentException)
            {
                // Same recovery philosophy as NLayerAudioDecoder for MP3: a corrupted frame
                // partway through a real-world file must not discard everything decoded so far.
                partialDecodeReason = $"Decoding stopped after {TimeSpan.FromSeconds((double)channels[0].Count / file.StreamInfo.SampleRateHz):hh\\:mm\\:ss} due to a decoder error: {ex.Message}";
                break;
            }

            for (var c = 0; c < channelCount; c++)
            {
                var source = result.Channels[c];
                var target = channels[c];
                for (var i = 0; i < result.BlockSize; i++)
                {
                    target.Add(source[i] * scale);
                }
            }

            offset += result.FrameByteLength;
        }

        return new DecodedAudio
        {
            SampleRateHz = file.StreamInfo.SampleRateHz,
            ChannelCount = channelCount,
            Channels = channels.Select(c => c.ToArray()).ToArray(),
            DecoderName = "FlacAudioDecoder",
            DecoderVersion = null,
            SourceSampleRateHz = file.StreamInfo.SampleRateHz,
            PartialDecodeReason = partialDecodeReason,
        };
    }
}
