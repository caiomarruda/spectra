namespace Spectra.Audio.Flac;

/// <summary>
/// MSB-first bit reader — FLAC's bitstream packs fields big-endian at the bit level within each
/// byte, unlike WAV/AIFF which are byte-oriented. Bounded by an explicit end offset so reads past
/// the frame (or file) throw cleanly instead of running into unrelated bytes.
/// </summary>
internal sealed class FlacBitReader
{
    private readonly byte[] _data;
    private readonly int _end;
    private int _bytePos;
    private int _bitPos;

    public FlacBitReader(byte[] data, int startOffset, int endOffsetExclusive)
    {
        _data = data;
        _bytePos = startOffset;
        _end = endOffsetExclusive;
        _bitPos = 0;
    }

    public int BytePosition => _bytePos;
    public bool IsByteAligned => _bitPos == 0;

    public int ReadBit()
    {
        if (_bytePos >= _end)
        {
            throw new EndOfStreamException("Ran out of frame data while reading a bit.");
        }
        var bit = (_data[_bytePos] >> (7 - _bitPos)) & 1;
        _bitPos++;
        if (_bitPos == 8)
        {
            _bitPos = 0;
            _bytePos++;
        }
        return bit;
    }

    public long ReadBits(int count)
    {
        long value = 0;
        for (var i = 0; i < count; i++)
        {
            value = (value << 1) | (uint)ReadBit();
        }
        return value;
    }

    /// <summary>Reads a two's-complement signed value of the given bit width (1-64).</summary>
    public long ReadSignedBits(int count)
    {
        var value = ReadBits(count);
        var signBit = 1L << (count - 1);
        return (value ^ signBit) - signBit;
    }

    /// <summary>Unary code: counts consecutive 0 bits, consuming the terminating 1 bit.</summary>
    public int ReadUnary()
    {
        var count = 0;
        while (ReadBit() == 0)
        {
            count++;
            if (count > 2_000_000)
            {
                throw new InvalidDataException("Unary-coded value exceeded a sane length — the frame is corrupt.");
            }
        }
        return count;
    }

    public void AlignToByte()
    {
        if (_bitPos != 0)
        {
            _bitPos = 0;
            _bytePos++;
        }
    }

    public byte ReadAlignedByte()
    {
        if (!IsByteAligned)
        {
            throw new InvalidOperationException("Reader is not byte-aligned.");
        }
        if (_bytePos >= _end)
        {
            throw new EndOfStreamException("Ran out of frame data while reading a byte.");
        }
        return _data[_bytePos++];
    }
}
