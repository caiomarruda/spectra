namespace Spectra.Audio.Flac;

/// <summary>
/// The two CRCs FLAC uses to detect a corrupt frame: CRC-8 (poly 0x07) over the frame header,
/// CRC-16 (poly 0x8005) over the whole frame. Both are unreflected, zero-initialized — the same
/// bitwise variant used throughout the FLAC format.
/// </summary>
internal static class FlacCrc
{
    public static byte ComputeCrc8(ReadOnlySpan<byte> data)
    {
        byte crc = 0;
        foreach (var b in data)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
            {
                crc = (byte)((crc & 0x80) != 0 ? (crc << 1) ^ 0x07 : crc << 1);
            }
        }
        return crc;
    }

    public static ushort ComputeCrc16(ReadOnlySpan<byte> data)
    {
        ushort crc = 0;
        foreach (var b in data)
        {
            crc ^= (ushort)(b << 8);
            for (var i = 0; i < 8; i++)
            {
                crc = (ushort)((crc & 0x8000) != 0 ? (crc << 1) ^ 0x8005 : crc << 1);
            }
        }
        return crc;
    }
}
