using Spectra.Core.Decoding;
using Spectra.Core.Models;

namespace Spectra.Analysis.Noise;

public static class NoiseAnalyzer
{
    private const double DcOffsetLinearThreshold = 0.005; // ~ -46 dBFS.
    private static readonly TimeSpan ExcessiveInternalSilenceThreshold = TimeSpan.FromSeconds(5);

    public static NoiseAnalysis Analyze(DecodedAudio audio, WaveformAnalysis waveform)
    {
        var dcOffsetPerChannel = new double[audio.ChannelCount];
        for (var c = 0; c < audio.ChannelCount; c++)
        {
            var samples = audio.Channels[c];
            double sum = 0;
            foreach (var sample in samples)
            {
                sum += sample;
            }
            dcOffsetPerChannel[c] = samples.Length > 0 ? sum / samples.Length : 0;
        }

        var nonSilentWindowDb = waveform.RmsOverTime
            .Where(w => w.Rms > 0)
            .Select(w => 20.0 * Math.Log10(w.Rms))
            .OrderBy(db => db)
            .ToList();
        var noiseFloorDb = nonSilentWindowDb.Count > 0
            ? nonSilentWindowDb[(int)(nonSilentWindowDb.Count * 0.10)]
            : double.NegativeInfinity;

        return new NoiseAnalysis
        {
            NoiseFloorDb = noiseFloorDb,
            DcOffsetPerChannel = dcOffsetPerChannel,
            HasSignificantDcOffset = dcOffsetPerChannel.Any(offset => Math.Abs(offset) >= DcOffsetLinearThreshold),
            HasExcessiveInternalSilence = HasExcessiveInternalSilence(waveform),
        };
    }

    private static bool HasExcessiveInternalSilence(WaveformAnalysis waveform)
    {
        var windows = waveform.RmsOverTime;
        if (windows.Count < 2)
        {
            return false;
        }

        var silenceThreshold = 0.001f; // ~ -60 dBFS, matches WaveformAnalyzer's own silence threshold.
        var runStart = -1;
        var longestInternalRun = TimeSpan.Zero;

        for (var i = 0; i < windows.Count; i++)
        {
            var isSilent = windows[i].Rms <= silenceThreshold;
            if (isSilent && runStart < 0)
            {
                runStart = i;
            }
            else if (!isSilent && runStart >= 0)
            {
                if (runStart > 0 && i < windows.Count) // strictly internal, not touching the leading edge.
                {
                    var duration = windows[i].StartTime - windows[runStart].StartTime;
                    if (duration > longestInternalRun)
                    {
                        longestInternalRun = duration;
                    }
                }
                runStart = -1;
            }
        }

        return longestInternalRun >= ExcessiveInternalSilenceThreshold;
    }
}
