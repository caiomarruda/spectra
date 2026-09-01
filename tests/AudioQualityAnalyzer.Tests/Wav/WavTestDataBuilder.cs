namespace AudioQualityAnalyzer.Tests.Wav;

/// <summary>Builds synthetic RIFF/WAVE byte streams for unit tests.</summary>
internal static class WavTestDataBuilder
{
    public static byte[] Build(int sampleRateHz, short channels, short bitsPerSample, short audioFormat, byte[] pcmData, byte[]? extraChunkBeforeFmt = null)
    {
        var blockAlign = (short)(channels * (bitsPerSample / 8));
        var byteRate = sampleRateHz * blockAlign;

        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);

        w.Write("RIFF"u8.ToArray());
        var riffSizePos = ms.Position;
        w.Write(0); // patched below

        w.Write("WAVE"u8.ToArray());
        if (extraChunkBeforeFmt is not null)
        {
            w.Write(extraChunkBeforeFmt);
        }

        w.Write("fmt "u8.ToArray());
        w.Write(16);
        w.Write(audioFormat);
        w.Write(channels);
        w.Write(sampleRateHz);
        w.Write(byteRate);
        w.Write(blockAlign);
        w.Write(bitsPerSample);

        w.Write("data"u8.ToArray());
        w.Write(pcmData.Length);
        w.Write(pcmData);
        if (pcmData.Length % 2 == 1)
        {
            w.Write((byte)0);
        }

        var end = ms.Position;
        ms.Position = riffSizePos;
        w.Write((int)(end - riffSizePos - 4));
        ms.Position = end;

        return ms.ToArray();
    }

    public static byte[] BuildListChunk(string text)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        var payload = System.Text.Encoding.ASCII.GetBytes("INFO" + text);
        w.Write("LIST"u8.ToArray());
        w.Write(payload.Length);
        w.Write(payload);
        if (payload.Length % 2 == 1)
        {
            w.Write((byte)0);
        }
        return ms.ToArray();
    }
}
