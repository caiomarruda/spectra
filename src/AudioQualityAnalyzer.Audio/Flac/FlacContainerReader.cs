namespace AudioQualityAnalyzer.Audio.Flac;

internal readonly record struct FlacStreamInfo
{
    public required int SampleRateHz { get; init; }
    public required int ChannelCount { get; init; }
    public required int BitsPerSample { get; init; }

    /// <summary>Total samples per channel from STREAMINFO. Can legitimately be 0 if the encoder didn't know the length up front.</summary>
    public required long TotalSamples { get; init; }
}

internal readonly record struct FlacFileData
{
    public required byte[] FileBytes { get; init; }
    public required int AudioStartOffset { get; init; }
    public required FlacStreamInfo StreamInfo { get; init; }
}

/// <summary>
/// Parses the "fLaC" stream marker and metadata blocks down to STREAMINFO, and locates where the
/// audio frames start. Only reads the file — never opens it for writing.
/// </summary>
internal static class FlacContainerReader
{
    public static FlacFileData Read(string path)
    {
        var data = File.ReadAllBytes(path);

        if (data.Length < 4 || data[0] != 'f' || data[1] != 'L' || data[2] != 'a' || data[3] != 'C')
        {
            throw new InvalidDataException($"'{path}' is not a valid FLAC file (missing 'fLaC' marker).");
        }

        FlacStreamInfo? streamInfo = null;
        var offset = 4;

        while (offset + 4 <= data.Length)
        {
            var headerByte = data[offset];
            var isLast = (headerByte & 0x80) != 0;
            var blockType = headerByte & 0x7F;
            var length = (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
            var blockDataOffset = offset + 4;

            if (blockDataOffset + length > data.Length)
            {
                throw new InvalidDataException($"'{path}' has a truncated metadata block.");
            }

            if (blockType == 0) // STREAMINFO
            {
                streamInfo = ParseStreamInfo(data, blockDataOffset, length, path);
            }
            // All other block types (VORBIS_COMMENT, SEEKTABLE, PADDING, APPLICATION, CUESHEET,
            // PICTURE, and any reserved/unknown type) are skipped by length, not interpreted.

            offset = blockDataOffset + length;
            if (isLast)
            {
                break;
            }
        }

        if (streamInfo is null)
        {
            throw new InvalidDataException($"'{path}' has no STREAMINFO metadata block.");
        }
        if (streamInfo.Value.ChannelCount is < 1 or > 2)
        {
            throw new InvalidDataException($"'{path}' has {streamInfo.Value.ChannelCount} channels; only mono and stereo files are supported.");
        }

        return new FlacFileData { FileBytes = data, AudioStartOffset = offset, StreamInfo = streamInfo.Value };
    }

    private static FlacStreamInfo ParseStreamInfo(byte[] data, int offset, int length, string path)
    {
        if (length < 34)
        {
            throw new InvalidDataException($"'{path}' has a truncated STREAMINFO block.");
        }

        var reader = new FlacBitReader(data, offset, offset + length);
        reader.ReadBits(16); // min block size — unused, real per-frame block size is read from each frame header
        reader.ReadBits(16); // max block size — unused
        reader.ReadBits(24); // min frame size — unused
        reader.ReadBits(24); // max frame size — unused
        var sampleRateHz = (int)reader.ReadBits(20);
        var channelCount = (int)reader.ReadBits(3) + 1;
        var bitsPerSample = (int)reader.ReadBits(5) + 1;
        var totalSamples = reader.ReadBits(36);
        // 128-bit MD5 signature follows — not verified; corruption is instead caught via per-frame CRCs.

        if (sampleRateHz <= 0)
        {
            throw new InvalidDataException($"'{path}' has an invalid sample rate in STREAMINFO.");
        }

        return new FlacStreamInfo
        {
            SampleRateHz = sampleRateHz,
            ChannelCount = channelCount,
            BitsPerSample = bitsPerSample,
            TotalSamples = totalSamples,
        };
    }
}
