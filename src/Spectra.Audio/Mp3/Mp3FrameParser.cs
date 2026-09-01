namespace Spectra.Audio.Mp3;

internal sealed record Mp3ParseResult
{
    public required IReadOnlyList<Mp3FrameHeader> Frames { get; init; }
    public required XingLameTag? VbrTag { get; init; }
    public required long AudioStartOffset { get; init; }
}

internal static class Mp3FrameParser
{
    private const int MaxResyncSearchBytes = 1 << 20; // 1 MiB

    public static Mp3ParseResult Parse(byte[] data)
    {
        var offset = SkipId3v2Tag(data);
        var startOffset = FindFirstFrameSync(data, offset);

        var frames = new List<Mp3FrameHeader>();
        XingLameTag? vbrTag = null;

        var position = startOffset;
        while (position >= 0 && position + 4 <= data.Length)
        {
            if (!Mp3FrameHeader.TryParse(data.AsSpan((int)position), position, out var header))
            {
                break;
            }

            if (position + header.FrameLengthBytes > data.Length)
            {
                break;
            }

            if (frames.Count == 0)
            {
                vbrTag = XingLameTag.TryRead(data.AsSpan((int)position, header.FrameLengthBytes), header);
            }

            frames.Add(header);
            position += header.FrameLengthBytes;
        }

        return new Mp3ParseResult
        {
            Frames = frames,
            VbrTag = vbrTag,
            AudioStartOffset = startOffset,
        };
    }

    private static long SkipId3v2Tag(byte[] data)
    {
        if (data.Length < 10 || data[0] != 'I' || data[1] != 'D' || data[2] != '3')
        {
            return 0;
        }

        var flags = data[5];
        var hasFooter = (flags & 0x10) != 0;
        var size = ((data[6] & 0x7F) << 21) | ((data[7] & 0x7F) << 14) | ((data[8] & 0x7F) << 7) | (data[9] & 0x7F);

        var tagLength = 10 + size + (hasFooter ? 10 : 0);
        return Math.Min(tagLength, data.Length);
    }

    /// <summary>
    /// Confirms sync by requiring that the frame following the candidate also parses as a
    /// valid header, which avoids locking onto a false 0xFF byte inside tag padding or audio data.
    /// </summary>
    private static long FindFirstFrameSync(byte[] data, long searchStart)
    {
        var limit = Math.Min(data.Length, searchStart + MaxResyncSearchBytes);
        for (var i = searchStart; i + 4 <= limit; i++)
        {
            if (!Mp3FrameHeader.TryParse(data.AsSpan((int)i), i, out var header))
            {
                continue;
            }

            var nextOffset = i + header.FrameLengthBytes;
            if (nextOffset + 4 > data.Length)
            {
                return i; // Last frame in file; accept without a second confirmation.
            }

            if (Mp3FrameHeader.TryParse(data.AsSpan((int)nextOffset), nextOffset, out _))
            {
                return i;
            }
        }

        return -1;
    }
}
