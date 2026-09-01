namespace AudioQualityAnalyzer.Audio.Flac;

/// <summary>
/// Walks every frame in a FLAC stream without keeping the decoded samples — used by
/// <see cref="FlacMetadataReader"/>, which needs an accurate frame count and (as a fallback when
/// STREAMINFO's total-sample count is 0/unknown) total sample count, but not the audio itself.
/// A frame's byte length is only known after fully parsing it (there is no length field, unlike
/// MP3), so this does the same parsing work as <see cref="FlacAudioDecoder"/> — it just discards
/// the result.
/// </summary>
internal static class FlacFrameWalker
{
    public static (int FrameCount, long TotalSamples) CountFrames(FlacFileData file)
    {
        var offset = file.AudioStartOffset;
        var frameCount = 0;
        long totalSamples = 0;

        while (offset < file.FileBytes.Length)
        {
            FlacFrameResult result;
            try
            {
                result = FlacFrameDecoder.DecodeFrame(file.FileBytes, offset, file.FileBytes.Length, file.StreamInfo);
            }
            catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException or IndexOutOfRangeException or ArgumentException)
            {
                break;
            }

            frameCount++;
            totalSamples += result.BlockSize;
            offset += result.FrameByteLength;
        }

        return (frameCount, totalSamples);
    }
}
