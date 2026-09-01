using System.Text;

namespace Spectra.Audio.Mp3;

/// <summary>
/// The Xing/Info VBR header and the LAME extension tag that most modern encoders embed in the
/// first MP3 frame. See the Hydrogenaudio "VBR Header" and "LAME Tag" specifications.
/// </summary>
internal sealed record XingLameTag
{
    public required bool IsVbrTag { get; init; }
    public int? TotalFrames { get; init; }
    public int? TotalBytes { get; init; }
    public string? EncoderVersion { get; init; }
    public int? EncoderDelaySamples { get; init; }
    public int? EncoderPaddingSamples { get; init; }

    public static XingLameTag? TryRead(ReadOnlySpan<byte> frame, in Mp3FrameHeader header)
    {
        var tagOffset = header.HeaderAndCrcSize + header.SideInfoSize;
        if (frame.Length < tagOffset + 8)
        {
            return null;
        }

        var id = Encoding.ASCII.GetString(frame.Slice(tagOffset, 4));
        var isVbrTag = id == "Xing";
        if (!isVbrTag && id != "Info")
        {
            return null;
        }

        var flags = ReadUInt32BigEndian(frame.Slice(tagOffset + 4, 4));
        var cursor = tagOffset + 8;

        int? totalFrames = null;
        int? totalBytes = null;

        if ((flags & 0x1) != 0 && frame.Length >= cursor + 4)
        {
            totalFrames = (int)ReadUInt32BigEndian(frame.Slice(cursor, 4));
            cursor += 4;
        }
        if ((flags & 0x2) != 0 && frame.Length >= cursor + 4)
        {
            totalBytes = (int)ReadUInt32BigEndian(frame.Slice(cursor, 4));
            cursor += 4;
        }
        if ((flags & 0x4) != 0)
        {
            cursor += 100; // TOC table, not currently used.
        }
        if ((flags & 0x8) != 0)
        {
            cursor += 4; // VBR quality indicator, not currently used.
        }

        string? encoderVersion = null;
        int? delay = null;
        int? padding = null;

        if (frame.Length >= cursor + 9 && frame[cursor] is (byte)'L' or (byte)'l')
        {
            var versionBytes = frame.Slice(cursor, 9);
            encoderVersion = Encoding.ASCII.GetString(versionBytes).TrimEnd('\0', ' ');

            // Encoder Delay and Padding is a fixed 3-byte field located 21 bytes past the
            // start of the LAME extension (version string[9] + info-tag byte[1] + lowpass[1]
            // + peak[4] + radio-gain[2] + audiophile-gain[2] + encflags[1] + bitrate[1] = 21).
            var delayPaddingOffset = cursor + 21;
            if (frame.Length >= delayPaddingOffset + 3)
            {
                var b0 = frame[delayPaddingOffset];
                var b1 = frame[delayPaddingOffset + 1];
                var b2 = frame[delayPaddingOffset + 2];
                delay = (b0 << 4) | (b1 >> 4);
                padding = ((b1 & 0x0F) << 8) | b2;
            }
        }

        return new XingLameTag
        {
            IsVbrTag = isVbrTag,
            TotalFrames = totalFrames,
            TotalBytes = totalBytes,
            EncoderVersion = encoderVersion,
            EncoderDelaySamples = delay,
            EncoderPaddingSamples = padding,
        };
    }

    private static uint ReadUInt32BigEndian(ReadOnlySpan<byte> bytes) =>
        ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
}
