using System.Buffers.Binary;

namespace Spectra.Audio.Aiff;

/// <summary>Parsed "COMM" chunk plus a byte range pointing at the PCM data inside the "SSND" chunk.</summary>
internal readonly record struct AiffFileData
{
    public required int SampleRateHz { get; init; }
    public required int ChannelCount { get; init; }
    public required int BitsPerSample { get; init; }
    public required int SampleFrameCount { get; init; }
    public required byte[] FileBytes { get; init; }
    public required int DataOffset { get; init; }
    public required int DataLength { get; init; }
}

/// <summary>
/// Parses the FORM/AIFF container down to the "COMM" and "SSND" chunks. All fields are
/// big-endian, the opposite of WAV. Only reads the file — never opens it for writing.
/// </summary>
internal static class AiffFileReader
{
    public static AiffFileData Read(string path)
    {
        var data = File.ReadAllBytes(path);

        if (data.Length < 12 || data[0] != 'F' || data[1] != 'O' || data[2] != 'R' || data[3] != 'M')
        {
            throw new InvalidDataException($"'{path}' is not a valid FORM/AIFF file.");
        }

        var formType = System.Text.Encoding.ASCII.GetString(data, 8, 4);
        if (formType == "AIFC")
        {
            throw new InvalidDataException($"'{path}' is AIFC (compressed AIFF), which is not supported; only uncompressed AIFF is supported.");
        }
        if (formType != "AIFF")
        {
            throw new InvalidDataException($"'{path}' has FORM type '{formType}', not AIFF.");
        }

        int? sampleRateHz = null;
        int? channelCount = null;
        int? bitsPerSample = null;
        int? sampleFrameCount = null;
        int? dataOffset = null;
        int? dataLength = null;

        var offset = 12;
        while (offset + 8 <= data.Length)
        {
            var chunkId = System.Text.Encoding.ASCII.GetString(data, offset, 4);
            var chunkSize = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset + 4, 4));
            var chunkDataOffset = offset + 8;

            var available = (uint)Math.Max(0, data.Length - chunkDataOffset);
            var effectiveSize = Math.Min(chunkSize, available);

            if (chunkId == "COMM")
            {
                if (effectiveSize < 18)
                {
                    throw new InvalidDataException($"'{path}' has a truncated AIFF 'COMM' chunk.");
                }

                channelCount = BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(chunkDataOffset, 2));
                sampleFrameCount = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(chunkDataOffset + 2, 4));
                bitsPerSample = BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(chunkDataOffset + 6, 2));
                sampleRateHz = (int)Math.Round(ReadExtendedFloat80(data, chunkDataOffset + 8));
            }
            else if (chunkId == "SSND")
            {
                if (effectiveSize < 8)
                {
                    throw new InvalidDataException($"'{path}' has a truncated AIFF 'SSND' chunk.");
                }

                var soundDataOffset = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(chunkDataOffset, 4));
                dataOffset = chunkDataOffset + 8 + (int)soundDataOffset;
                dataLength = (int)effectiveSize - 8 - (int)soundDataOffset;
            }

            var advance = (int)chunkSize + (chunkSize % 2 == 1 ? 1 : 0); // chunks are word-aligned
            if (advance <= 0)
            {
                break;
            }
            offset = chunkDataOffset + advance;
        }

        if (sampleRateHz is null || channelCount is null || bitsPerSample is null || sampleFrameCount is null)
        {
            throw new InvalidDataException($"'{path}' has no 'COMM' chunk.");
        }
        if (dataOffset is null || dataLength is null)
        {
            throw new InvalidDataException($"'{path}' has no 'SSND' chunk.");
        }
        if (channelCount.Value is < 1 or > 2)
        {
            throw new InvalidDataException($"'{path}' has {channelCount.Value} channels; only mono and stereo files are supported.");
        }

        return new AiffFileData
        {
            SampleRateHz = sampleRateHz.Value,
            ChannelCount = channelCount.Value,
            BitsPerSample = bitsPerSample.Value,
            SampleFrameCount = sampleFrameCount.Value,
            FileBytes = data,
            DataOffset = dataOffset.Value,
            DataLength = Math.Max(0, dataLength.Value),
        };
    }

    /// <summary>
    /// Decodes the 80-bit IEEE 754 extended-precision float AIFF uses for its sample rate field
    /// (1 sign bit + 15 exponent bits, bias 16383, + 64-bit mantissa with an explicit integer bit
    /// — there is no built-in .NET type for this).
    /// </summary>
    private static double ReadExtendedFloat80(byte[] data, int offset)
    {
        var exponentAndSign = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset, 2));
        var mantissa = BinaryPrimitives.ReadUInt64BigEndian(data.AsSpan(offset + 2, 8));

        if (exponentAndSign == 0 && mantissa == 0)
        {
            return 0;
        }

        var negative = (exponentAndSign & 0x8000) != 0;
        var exponent = exponentAndSign & 0x7FFF;
        var value = mantissa * Math.Pow(2, exponent - 16383 - 63);
        return negative ? -value : value;
    }
}
