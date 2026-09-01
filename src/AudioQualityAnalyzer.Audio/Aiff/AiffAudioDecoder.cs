using System.Buffers.Binary;
using AudioQualityAnalyzer.Core.Decoding;

namespace AudioQualityAnalyzer.Audio.Aiff;

/// <summary>
/// Reads AIFF PCM data directly into de-interleaved float samples. AIFF samples are always
/// signed and big-endian, regardless of bit depth — unlike WAV's unsigned 8-bit quirk.
/// </summary>
public sealed class AiffAudioDecoder : IAudioDecoder
{
    public DecodedAudio Decode(string path)
    {
        var aiff = AiffFileReader.Read(path);
        var bytesPerSample = aiff.BitsPerSample / 8;
        if (bytesPerSample * 8 != aiff.BitsPerSample || bytesPerSample is not (1 or 2 or 3 or 4))
        {
            throw new InvalidDataException($"'{path}' uses {aiff.BitsPerSample}-bit samples, which is not supported (supported: 8, 16, 24, 32-bit PCM).");
        }

        var frameStride = aiff.ChannelCount * bytesPerSample;
        var frameCount = frameStride > 0 ? Math.Min(aiff.SampleFrameCount, aiff.DataLength / frameStride) : 0;

        var channels = new float[aiff.ChannelCount][];
        for (var c = 0; c < aiff.ChannelCount; c++)
        {
            channels[c] = new float[frameCount];
        }

        var span = aiff.FileBytes.AsSpan(aiff.DataOffset, aiff.DataLength);
        for (var frame = 0; frame < frameCount; frame++)
        {
            var frameOffset = frame * frameStride;
            for (var c = 0; c < aiff.ChannelCount; c++)
            {
                var sampleOffset = frameOffset + (c * bytesPerSample);
                channels[c][frame] = ReadSample(span.Slice(sampleOffset, bytesPerSample));
            }
        }

        return new DecodedAudio
        {
            SampleRateHz = aiff.SampleRateHz,
            ChannelCount = aiff.ChannelCount,
            Channels = channels,
            DecoderName = "AiffAudioDecoder",
            DecoderVersion = null,
            SourceSampleRateHz = aiff.SampleRateHz,
            PartialDecodeReason = null,
        };
    }

    private static float ReadSample(ReadOnlySpan<byte> sampleBytes) => sampleBytes.Length switch
    {
        1 => unchecked((sbyte)sampleBytes[0]) / 128f,
        2 => BinaryPrimitives.ReadInt16BigEndian(sampleBytes) / 32768f,
        3 => Sign24((sampleBytes[0] << 16) | (sampleBytes[1] << 8) | sampleBytes[2]) / 8388608f,
        4 => (float)(BinaryPrimitives.ReadInt32BigEndian(sampleBytes) / 2147483648.0),
        _ => throw new InvalidDataException($"Unsupported PCM sample width: {sampleBytes.Length * 8}-bit."),
    };

    private static int Sign24(int value) => (value & 0x800000) != 0 ? value | unchecked((int)0xFF000000) : value;
}
