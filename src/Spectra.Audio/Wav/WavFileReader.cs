namespace Spectra.Audio.Wav;

/// <summary>Sample encoding declared in a WAV "fmt " chunk (WAVE_FORMAT_* tags this analyzer understands).</summary>
internal enum WavSampleFormat
{
    Pcm,
    IeeeFloat,
}

/// <summary>Parsed "fmt " chunk plus a byte range pointing at the "data" chunk within the file.</summary>
internal readonly record struct WavFileData
{
    public required int SampleRateHz { get; init; }
    public required int ChannelCount { get; init; }
    public required int BitsPerSample { get; init; }
    public required WavSampleFormat SampleFormat { get; init; }
    public required byte[] FileBytes { get; init; }
    public required int DataOffset { get; init; }
    public required int DataLength { get; init; }
}

/// <summary>
/// Parses the RIFF/WAVE container down to the "fmt " and "data" chunks. Only reads the file
/// (File.ReadAllBytes) — never opens it for writing, per the analyzer's read-only guarantee.
/// </summary>
internal static class WavFileReader
{
    public static WavFileData Read(string path)
    {
        var data = File.ReadAllBytes(path);

        if (data.Length < 12
            || data[0] != 'R' || data[1] != 'I' || data[2] != 'F' || data[3] != 'F'
            || data[8] != 'W' || data[9] != 'A' || data[10] != 'V' || data[11] != 'E')
        {
            throw new InvalidDataException($"'{path}' is not a valid RIFF/WAVE file.");
        }

        int? sampleRateHz = null;
        int? channelCount = null;
        int? bitsPerSample = null;
        WavSampleFormat? sampleFormat = null;
        int? dataOffset = null;
        int? dataLength = null;

        var offset = 12;
        while (offset + 8 <= data.Length)
        {
            var chunkId = System.Text.Encoding.ASCII.GetString(data, offset, 4);
            var chunkSize = BitConverter.ToUInt32(data, offset + 4);
            var chunkDataOffset = offset + 8;

            // A truncated or lying chunk size is clamped to what's actually left in the file
            // rather than throwing — real-world WAV files sometimes have an inaccurate trailing
            // chunk size, and the audio data chunk (found separately) is what actually matters.
            var available = (uint)Math.Max(0, data.Length - chunkDataOffset);
            var effectiveSize = Math.Min(chunkSize, available);

            if (chunkId == "fmt ")
            {
                if (effectiveSize < 16)
                {
                    throw new InvalidDataException($"'{path}' has a truncated WAV 'fmt ' chunk.");
                }

                var audioFormat = BitConverter.ToUInt16(data, chunkDataOffset);
                channelCount = BitConverter.ToUInt16(data, chunkDataOffset + 2);
                sampleRateHz = (int)BitConverter.ToUInt32(data, chunkDataOffset + 4);
                bitsPerSample = BitConverter.ToUInt16(data, chunkDataOffset + 14);

                if (audioFormat == 0xFFFE) // WAVE_FORMAT_EXTENSIBLE: real format code is the first two bytes of the sub-format GUID.
                {
                    if (effectiveSize < 40)
                    {
                        throw new InvalidDataException($"'{path}' has a truncated WAVE_FORMAT_EXTENSIBLE 'fmt ' chunk.");
                    }
                    audioFormat = BitConverter.ToUInt16(data, chunkDataOffset + 24);
                }

                sampleFormat = audioFormat switch
                {
                    1 => WavSampleFormat.Pcm,
                    3 => WavSampleFormat.IeeeFloat,
                    _ => throw new InvalidDataException($"'{path}' uses WAV audio format code {audioFormat}, which is not a supported PCM/IEEE-float encoding (compressed WAV variants such as A-law, mu-law, or ADPCM are not supported)."),
                };
            }
            else if (chunkId == "data")
            {
                dataOffset = chunkDataOffset;
                dataLength = (int)effectiveSize;
            }

            var advance = (int)chunkSize + (chunkSize % 2 == 1 ? 1 : 0); // chunks are word-aligned; padding byte is not counted in chunkSize
            if (advance <= 0)
            {
                break; // malformed zero/negative-size chunk — stop scanning rather than looping forever
            }
            offset = chunkDataOffset + advance;
        }

        if (sampleRateHz is null || channelCount is null || bitsPerSample is null || sampleFormat is null)
        {
            throw new InvalidDataException($"'{path}' has no 'fmt ' chunk.");
        }
        if (dataOffset is null || dataLength is null)
        {
            throw new InvalidDataException($"'{path}' has no 'data' chunk.");
        }

        if (channelCount.Value is < 1 or > 2)
        {
            throw new InvalidDataException($"'{path}' has {channelCount.Value} channels; only mono and stereo files are supported.");
        }

        return new WavFileData
        {
            SampleRateHz = sampleRateHz.Value,
            ChannelCount = channelCount.Value,
            BitsPerSample = bitsPerSample.Value,
            SampleFormat = sampleFormat.Value,
            FileBytes = data,
            DataOffset = dataOffset.Value,
            DataLength = dataLength.Value,
        };
    }

    /// <summary>Whole PCM frames (one sample per channel) available in the data chunk.</summary>
    public static int ComputeFrameCount(WavFileData wav)
    {
        var bytesPerSample = wav.BitsPerSample / 8;
        var frameStride = wav.ChannelCount * bytesPerSample;
        return frameStride > 0 ? wav.DataLength / frameStride : 0;
    }
}
