using System.Buffers.Binary;

namespace AudioQualityAnalyzer.Tests.Aiff;

/// <summary>Builds synthetic FORM/AIFF byte streams for unit tests.</summary>
internal static class AiffTestDataBuilder
{
    public static byte[] Build(int sampleRateHz, short channels, short bitsPerSample, int sampleFrameCount, byte[] pcmData, string formType = "AIFF")
    {
        using var ms = new MemoryStream();

        var comm = BuildCommChunk(channels, sampleFrameCount, bitsPerSample, sampleRateHz);
        var ssnd = BuildSsndChunk(pcmData);

        WriteAscii(ms, "FORM");
        WriteBE(ms, 4 + comm.Length + ssnd.Length); // "AIFF" + chunks
        WriteAscii(ms, formType);
        ms.Write(comm);
        ms.Write(ssnd);

        return ms.ToArray();
    }

    private static byte[] BuildCommChunk(short channels, int sampleFrameCount, short bitsPerSample, int sampleRateHz)
    {
        using var ms = new MemoryStream();
        WriteAscii(ms, "COMM");
        WriteBE(ms, 18);
        WriteBE(ms, channels);
        WriteBE(ms, sampleFrameCount);
        WriteBE(ms, bitsPerSample);
        ms.Write(EncodeExtended80(sampleRateHz));
        return ms.ToArray();
    }

    private static byte[] BuildSsndChunk(byte[] pcmData)
    {
        using var ms = new MemoryStream();
        WriteAscii(ms, "SSND");
        WriteBE(ms, pcmData.Length + 8);
        WriteBE(ms, 0); // offset
        WriteBE(ms, 0); // blockSize
        ms.Write(pcmData);
        if (pcmData.Length % 2 == 1)
        {
            ms.WriteByte(0);
        }
        return ms.ToArray();
    }

    private static void WriteAscii(Stream s, string text) => s.Write(System.Text.Encoding.ASCII.GetBytes(text));

    private static void WriteBE(Stream s, int value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, value);
        s.Write(buffer);
    }

    private static void WriteBE(Stream s, short value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteInt16BigEndian(buffer, value);
        s.Write(buffer);
    }

    /// <summary>Encodes a positive integer sample rate as an 80-bit IEEE 754 extended-precision float (inverse of AiffFileReader.ReadExtendedFloat80).</summary>
    private static byte[] EncodeExtended80(double value)
    {
        var exponent = (int)Math.Floor(Math.Log2(value));
        var mantissaFraction = value / Math.Pow(2, exponent);
        var mantissa = (ulong)Math.Round(mantissaFraction * Math.Pow(2, 63));
        var exponentField = (ushort)(exponent + 16383);

        var bytes = new byte[10];
        bytes[0] = (byte)(exponentField >> 8);
        bytes[1] = (byte)(exponentField & 0xFF);
        for (var i = 0; i < 8; i++)
        {
            bytes[2 + i] = (byte)((mantissa >> (56 - (8 * i))) & 0xFF);
        }
        return bytes;
    }
}
