using Spectra.Core.Decoding;

namespace Spectra.Audio.Flac;

/// <summary>
/// Decodes FLAC to PCM with a from-scratch, dependency-free decoder (RFC 9639) — chosen over an
/// available NuGet package specifically because that package had no clear license, and this
/// project is meant to be published with a clean one (see FlacFrameDecoder for the bitstream
/// implementation).
/// </summary>
public sealed class FlacAudioDecoder : IAudioDecoder
{
    public DecodedAudio Decode(string path) => Decode(File.ReadAllBytes(path), path);

    public static DecodedAudio Decode(byte[] data, string fileName)
    {
        var file = FlacContainerReader.Read(data, fileName);
        var channelCount = file.StreamInfo.ChannelCount;

        // STREAMINFO's total-sample count (0 if the encoder didn't know it up front) lets each
        // channel list pre-allocate its backing array once instead of growing — and repeatedly
        // copying — it frame by frame across a from-scratch decode of the whole file.
        var estimatedSampleCount = file.StreamInfo.TotalSamples > 0 ? (int)Math.Min(file.StreamInfo.TotalSamples, int.MaxValue) : 0;
        var channels = new List<float>[channelCount];
        for (var c = 0; c < channelCount; c++)
        {
            channels[c] = new List<float>(estimatedSampleCount);
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
