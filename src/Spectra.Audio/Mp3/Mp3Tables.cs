using Spectra.Core.Enums;

namespace Spectra.Audio.Mp3;

/// <summary>
/// Lookup tables from the MPEG-1/2/2.5 Audio frame header specification (ISO/IEC 11172-3, 13818-3).
/// </summary>
internal static class Mp3Tables
{
    private static readonly int[] BitrateV1L1 = { 0, 32, 64, 96, 128, 160, 192, 224, 256, 288, 320, 352, 384, 416, 448, -1 };
    private static readonly int[] BitrateV1L2 = { 0, 32, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 384, -1 };
    private static readonly int[] BitrateV1L3 = { 0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, -1 };
    private static readonly int[] BitrateV2L1 = { 0, 32, 48, 56, 64, 80, 96, 112, 128, 144, 160, 176, 192, 224, 256, -1 };
    private static readonly int[] BitrateV2L23 = { 0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160, -1 };

    private static readonly int[] SampleRateV1 = { 44100, 48000, 32000, -1 };
    private static readonly int[] SampleRateV2 = { 22050, 24000, 16000, -1 };
    private static readonly int[] SampleRateV2_5 = { 11025, 12000, 8000, -1 };

    /// <returns>Bitrate in kbps, 0 for free format, -1 for invalid/reserved.</returns>
    public static int GetBitrateKbps(MpegVersion version, MpegLayer layer, int bitrateIndex)
    {
        if (bitrateIndex is < 0 or > 15)
        {
            return -1;
        }

        var table = version == MpegVersion.Version1
            ? layer switch
            {
                MpegLayer.LayerI => BitrateV1L1,
                MpegLayer.LayerII => BitrateV1L2,
                MpegLayer.LayerIII => BitrateV1L3,
                _ => null,
            }
            : layer switch
            {
                MpegLayer.LayerI => BitrateV2L1,
                MpegLayer.LayerII or MpegLayer.LayerIII => BitrateV2L23,
                _ => null,
            };

        return table?[bitrateIndex] ?? -1;
    }

    public static int GetSampleRateHz(MpegVersion version, int sampleRateIndex)
    {
        if (sampleRateIndex is < 0 or > 3)
        {
            return -1;
        }

        return version switch
        {
            MpegVersion.Version1 => SampleRateV1[sampleRateIndex],
            MpegVersion.Version2 => SampleRateV2[sampleRateIndex],
            MpegVersion.Version2_5 => SampleRateV2_5[sampleRateIndex],
            _ => -1,
        };
    }

    public static int GetSamplesPerFrame(MpegVersion version, MpegLayer layer) => layer switch
    {
        MpegLayer.LayerI => 384,
        MpegLayer.LayerII => 1152,
        MpegLayer.LayerIII => version == MpegVersion.Version1 ? 1152 : 576,
        _ => 0,
    };

    public static int GetSideInfoSize(MpegVersion version, ChannelMode channelMode)
    {
        var isMono = channelMode == ChannelMode.Mono;
        return version == MpegVersion.Version1
            ? isMono ? 17 : 32
            : isMono ? 9 : 17;
    }

    public static int GetFrameLengthBytes(MpegVersion version, MpegLayer layer, int bitrateKbps, int sampleRateHz, bool padding)
    {
        if (bitrateKbps <= 0 || sampleRateHz <= 0)
        {
            return -1;
        }

        var paddingSlots = padding ? 1 : 0;

        if (layer == MpegLayer.LayerI)
        {
            return (12 * bitrateKbps * 1000 / sampleRateHz + paddingSlots) * 4;
        }

        var coefficient = layer == MpegLayer.LayerIII && version != MpegVersion.Version1 ? 72 : 144;
        return coefficient * bitrateKbps * 1000 / sampleRateHz + paddingSlots;
    }
}
