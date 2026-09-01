namespace Spectra.Tests.Flac;

/// <summary>Builds minimal synthetic FLAC byte streams (stream marker + STREAMINFO, no audio frames) for container-level unit tests.</summary>
internal static class FlacTestDataBuilder
{
    public static byte[] BuildStreamInfoOnly(int sampleRateHz, int channels, int bitsPerSample, long totalSamples)
    {
        var streamInfo = new byte[34];
        var writer = new TestBitWriter(streamInfo);
        writer.WriteBits(4096, 16); // min block size
        writer.WriteBits(4096, 16); // max block size
        writer.WriteBits(0, 24); // min frame size
        writer.WriteBits(0, 24); // max frame size
        writer.WriteBits((ulong)sampleRateHz, 20);
        writer.WriteBits((ulong)(channels - 1), 3);
        writer.WriteBits((ulong)(bitsPerSample - 1), 5);
        writer.WriteBits((ulong)totalSamples, 36);
        // 128-bit MD5 left as zeros

        using var ms = new MemoryStream();
        ms.Write("fLaC"u8.ToArray());
        ms.WriteByte(0x80); // block type 0 (STREAMINFO), last-block flag set
        ms.WriteByte((byte)((streamInfo.Length >> 16) & 0xFF));
        ms.WriteByte((byte)((streamInfo.Length >> 8) & 0xFF));
        ms.WriteByte((byte)(streamInfo.Length & 0xFF));
        ms.Write(streamInfo);
        return ms.ToArray();
    }

    /// <summary>MSB-first bit packer — the inverse of FlacBitReader, used only to build test fixtures.</summary>
    private sealed class TestBitWriter(byte[] buffer)
    {
        private int _bytePos;
        private int _bitPos;

        public void WriteBits(ulong value, int count)
        {
            for (var i = count - 1; i >= 0; i--)
            {
                var bit = (int)((value >> i) & 1);
                buffer[_bytePos] |= (byte)(bit << (7 - _bitPos));
                _bitPos++;
                if (_bitPos == 8)
                {
                    _bitPos = 0;
                    _bytePos++;
                }
            }
        }
    }
}
