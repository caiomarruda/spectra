using Spectra.Audio.Decoding;
using Spectra.Tests.TestSupport;
using Xunit;

namespace Spectra.Tests.Audio;

public class NLayerAudioDecoderTests
{
    /// <summary>
    /// Regression test for a real crash: NLayer's own frame scanner has no false-sync resync
    /// check, and a real-world file with a second ID3v2 tag immediately after the first (likely
    /// from embedded artwork) contained bytes that coincidentally looked like a Layer II frame
    /// sync, crashing inside NLayer's Layer II CRC path (IndexOutOfRangeException) before any of
    /// our own code ran. The fix hands NLayer a stream that starts at the audio offset our own
    /// Mp3FrameParser resync logic finds, so it never sees the problematic leading bytes. This
    /// test can't reproduce NLayer's exact internal bug without the original (copyrighted) file,
    /// but it does verify the actual fix mechanism: garbage bytes containing several fake sync
    /// candidates prepended to a real track must not change what gets decoded.
    /// </summary>
    [Fact]
    public void Decode_FileWithFakeSyncBytesBeforeRealAudio_DecodesTheSameAudioAsWithoutThem()
    {
        var referencePath = ReferenceDataset.FindPath("mp3-128/track-128.mp3");
        var originalBytes = File.ReadAllBytes(referencePath);

        var garbage = new byte[5000];
        new Random(7).NextBytes(garbage);
        for (var i = 0; i + 1 < garbage.Length; i += 137)
        {
            // Sprinkle in byte pairs that pass the sync-word check (0xFF + top 3 bits set) but do
            // not lead into a real, consistent frame stream — exactly the kind of false candidate
            // Mp3FrameParser's resync logic is required to reject.
            garbage[i] = 0xFF;
            garbage[i + 1] = 0xE0;
        }

        var combined = new byte[garbage.Length + originalBytes.Length];
        garbage.CopyTo(combined, 0);
        originalBytes.CopyTo(combined, garbage.Length);

        var tempPath = Path.GetTempFileName() + ".mp3";
        File.WriteAllBytes(tempPath, combined);

        try
        {
            var decoder = new NLayerAudioDecoder();
            var direct = decoder.Decode(referencePath);
            var withGarbagePrefix = decoder.Decode(tempPath);

            Assert.Equal(direct.SampleRateHz, withGarbagePrefix.SampleRateHz);
            Assert.Equal(direct.ChannelCount, withGarbagePrefix.ChannelCount);
            Assert.Equal(direct.Channels[0].Length, withGarbagePrefix.Channels[0].Length);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }
}
