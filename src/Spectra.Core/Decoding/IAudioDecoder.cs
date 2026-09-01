namespace Spectra.Core.Decoding;

public interface IAudioDecoder
{
    DecodedAudio Decode(string path);
}
