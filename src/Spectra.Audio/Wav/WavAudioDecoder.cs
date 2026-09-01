using System.Buffers.Binary;
using Spectra.Core.Decoding;

namespace Spectra.Audio.Wav;

/// <summary>
/// Reads WAV PCM data directly into de-interleaved float samples. WAV audio is already
/// uncompressed, so this is a format conversion, not a codec — no external decoder needed.
/// </summary>
public sealed class WavAudioDecoder : IAudioDecoder
{
    public DecodedAudio Decode(string path)
    {
        var wav = WavFileReader.Read(path);
        var bytesPerSample = wav.BitsPerSample / 8;
        if (bytesPerSample * 8 != wav.BitsPerSample || bytesPerSample is not (1 or 2 or 3 or 4))
        {
            throw new InvalidDataException($"'{path}' uses {wav.BitsPerSample}-bit samples, which is not supported (supported: 8, 16, 24, 32-bit PCM or 32/64-bit float).");
        }

        var frameStride = wav.ChannelCount * bytesPerSample;
        var frameCount = WavFileReader.ComputeFrameCount(wav);

        var channels = new float[wav.ChannelCount][];
        for (var c = 0; c < wav.ChannelCount; c++)
        {
            channels[c] = new float[frameCount];
        }

        var span = wav.FileBytes.AsSpan(wav.DataOffset, wav.DataLength);
        for (var frame = 0; frame < frameCount; frame++)
        {
            var frameOffset = frame * frameStride;
            for (var c = 0; c < wav.ChannelCount; c++)
            {
                var sampleOffset = frameOffset + (c * bytesPerSample);
                channels[c][frame] = ReadSample(span.Slice(sampleOffset, bytesPerSample), wav.SampleFormat);
            }
        }

        return new DecodedAudio
        {
            SampleRateHz = wav.SampleRateHz,
            ChannelCount = wav.ChannelCount,
            Channels = channels,
            DecoderName = "WavAudioDecoder",
            DecoderVersion = null,
            SourceSampleRateHz = wav.SampleRateHz,
            PartialDecodeReason = null,
        };
    }

    private static float ReadSample(ReadOnlySpan<byte> sampleBytes, WavSampleFormat format)
    {
        if (format == WavSampleFormat.IeeeFloat)
        {
            return sampleBytes.Length switch
            {
                4 => BinaryPrimitives.ReadSingleLittleEndian(sampleBytes),
                8 => (float)BinaryPrimitives.ReadDoubleLittleEndian(sampleBytes),
                _ => throw new InvalidDataException($"Unsupported IEEE-float sample width: {sampleBytes.Length * 8}-bit."),
            };
        }

        return sampleBytes.Length switch
        {
            1 => (sampleBytes[0] - 128) / 128f, // WAV 8-bit PCM is unsigned; everything else is signed.
            2 => BinaryPrimitives.ReadInt16LittleEndian(sampleBytes) / 32768f,
            3 => Sign24(sampleBytes[0] | (sampleBytes[1] << 8) | (sampleBytes[2] << 16)) / 8388608f,
            4 => (float)(BinaryPrimitives.ReadInt32LittleEndian(sampleBytes) / 2147483648.0),
            _ => throw new InvalidDataException($"Unsupported PCM sample width: {sampleBytes.Length * 8}-bit."),
        };
    }

    private static int Sign24(int value) => (value & 0x800000) != 0 ? value | unchecked((int)0xFF000000) : value;
}
