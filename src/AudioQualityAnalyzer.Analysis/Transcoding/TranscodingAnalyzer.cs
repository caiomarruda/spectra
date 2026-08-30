using AudioQualityAnalyzer.Analysis.Common;
using AudioQualityAnalyzer.Core.Enums;
using AudioQualityAnalyzer.Core.Models;

namespace AudioQualityAnalyzer.Analysis.Transcoding;

/// <summary>
/// Combines several independent, individually weak signals into a single probability rather
/// than one frequency rule (05-IMPLEMENTATION-PLAN.md Phase 9), and never claims certainty about
/// historical origin — only a probability, a confidence, and the evidence behind them.
/// </summary>
public static class TranscodingAnalyzer
{
    public static TranscodingAnalysis Analyze(EncodingAnalysis encoding, SpectralAnalysis spectral)
    {
        var referenceBitrateKbps = encoding.BitrateMode == BitrateMode.ConstantBitRate
            ? encoding.DeclaredBitrateKbps
            : (int)Math.Round(encoding.AverageBitrateKbps);

        var expectedBandwidthHz = PiecewiseLinear.Interpolate(TranscodingSettings.ExpectedBandwidthTable, referenceBitrateKbps);
        var deficitHz = expectedBandwidthHz - spectral.EffectiveBandwidthHz;

        var bandwidthScore = ComputeBandwidthScore(deficitHz);
        var confidenceWeight = spectral.BandwidthConfidence switch
        {
            ConfidenceLevel.High => 1.0,
            ConfidenceLevel.Medium => 0.7,
            _ => 0.3,
        };
        var weightedBandwidthScore = bandwidthScore * confidenceWeight;

        var sharpnessBonus = spectral.CutoffSharpnessDbPerOctave > TranscodingSettings.SharpCutoffDbPerOctaveThreshold
            ? Math.Min(TranscodingSettings.MaxSharpnessBonusPoints, spectral.CutoffSharpnessDbPerOctave - TranscodingSettings.SharpCutoffDbPerOctaveThreshold)
            : 0;

        var bitrateMismatchDetected = encoding.BitrateMode == BitrateMode.ConstantBitRate
            && Math.Abs(encoding.AverageBitrateKbps - encoding.DeclaredBitrateKbps) > TranscodingSettings.CbrBitrateMismatchToleranceKbps;
        var bitrateMismatchBonus = bitrateMismatchDetected ? TranscodingSettings.BitrateMismatchBonusPoints : 0;

        var probability = Math.Clamp(weightedBandwidthScore + sharpnessBonus + bitrateMismatchBonus, 0, 100);

        var findings = new List<AnalysisFinding>();
        if (deficitHz > TranscodingSettings.NoDeficitToleranceHz)
        {
            findings.Add(BuildCutoffFinding(spectral, referenceBitrateKbps, expectedBandwidthHz, probability));
        }
        else
        {
            findings.Add(BuildNoCutoffEvidenceFinding(spectral, referenceBitrateKbps, expectedBandwidthHz));
        }

        if (bitrateMismatchDetected)
        {
            findings.Add(BuildBitrateMismatchFinding(encoding));
        }

        return new TranscodingAnalysis
        {
            Probability = probability,
            Label = ToLabel(probability),
            Confidence = spectral.BandwidthConfidence,
            Findings = findings,
        };
    }

    private static double ComputeBandwidthScore(double deficitHz)
    {
        if (deficitHz <= TranscodingSettings.NoDeficitToleranceHz)
        {
            return 0;
        }

        var effectiveDeficit = deficitHz - TranscodingSettings.NoDeficitToleranceHz;
        var range = TranscodingSettings.MaxDeficitForFullScoreHz - TranscodingSettings.NoDeficitToleranceHz;
        return Math.Clamp(effectiveDeficit / range * 100.0, 0, 100);
    }

    private static TranscodingProbabilityLabel ToLabel(double probability) => probability switch
    {
        <= 20 => TranscodingProbabilityLabel.VeryUnlikely,
        <= 40 => TranscodingProbabilityLabel.Unlikely,
        <= 60 => TranscodingProbabilityLabel.Uncertain,
        <= 80 => TranscodingProbabilityLabel.Likely,
        _ => TranscodingProbabilityLabel.HighlyLikely,
    };

    private static AnalysisFinding BuildCutoffFinding(SpectralAnalysis spectral, int referenceBitrateKbps, double expectedBandwidthHz, double probability)
    {
        var isAbrupt = spectral.CutoffSharpnessDbPerOctave > TranscodingSettings.SharpCutoffDbPerOctaveThreshold;
        return new AnalysisFinding
        {
            Code = "TRANSCODING_SPECTRAL_CUTOFF",
            Title = "Possible previous lossy encoding",
            Severity = probability > 60 ? Severity.Warning : Severity.Info,
            Confidence = spectral.BandwidthConfidence,
            Description = $"Effective bandwidth ({spectral.EffectiveBandwidthHz / 1000.0:F1} kHz) is narrower than typical for a {referenceBitrateKbps} kbps encode (~{expectedBandwidthHz / 1000.0:F1} kHz expected), which is consistent with a prior lossy encode at a lower bitrate.",
            Evidence =
            [
                $"Effective bandwidth: {spectral.EffectiveBandwidthHz / 1000.0:F1} kHz",
                $"Expected bandwidth for {referenceBitrateKbps} kbps: ~{expectedBandwidthHz / 1000.0:F1} kHz",
                $"Cutoff sharpness: {spectral.CutoffSharpnessDbPerOctave:F1} dB/octave ({(isAbrupt ? "abrupt" : "gradual")})",
                $"Cutoff consistency over time: {spectral.CutoffConsistency:P0}",
            ],
            Metrics = new Dictionary<string, double>
            {
                ["EffectiveBandwidthHz"] = spectral.EffectiveBandwidthHz,
                ["ExpectedBandwidthHz"] = expectedBandwidthHz,
                ["CutoffSharpnessDbPerOctave"] = spectral.CutoffSharpnessDbPerOctave,
                ["CutoffConsistency"] = spectral.CutoffConsistency,
            },
        };
    }

    private static AnalysisFinding BuildNoCutoffEvidenceFinding(SpectralAnalysis spectral, int referenceBitrateKbps, double expectedBandwidthHz) => new()
    {
        Code = "TRANSCODING_BANDWIDTH_CONSISTENT",
        Title = "No spectral evidence of prior lossy encoding",
        Severity = Severity.Info,
        Confidence = spectral.BandwidthConfidence,
        Description = $"Effective bandwidth ({spectral.EffectiveBandwidthHz / 1000.0:F1} kHz) matches or exceeds what is typical for a {referenceBitrateKbps} kbps encode.",
        Evidence =
        [
            $"Effective bandwidth: {spectral.EffectiveBandwidthHz / 1000.0:F1} kHz",
            $"Expected bandwidth for {referenceBitrateKbps} kbps: ~{expectedBandwidthHz / 1000.0:F1} kHz",
        ],
        Metrics = new Dictionary<string, double>
        {
            ["EffectiveBandwidthHz"] = spectral.EffectiveBandwidthHz,
            ["ExpectedBandwidthHz"] = expectedBandwidthHz,
        },
    };

    private static AnalysisFinding BuildBitrateMismatchFinding(EncodingAnalysis encoding) => new()
    {
        Code = "ENCODING_BITRATE_MISMATCH",
        Title = "Declared bitrate does not match the measured average",
        Severity = Severity.Warning,
        Confidence = ConfidenceLevel.High,
        Description = "The file declares a constant bitrate, but the measured average bitrate across all frames differs from it — unusual for a genuine single-generation CBR encode.",
        Evidence =
        [
            $"Declared bitrate: {encoding.DeclaredBitrateKbps} kbps",
            $"Measured average bitrate: {encoding.AverageBitrateKbps:F1} kbps",
        ],
        Metrics = new Dictionary<string, double>
        {
            ["DeclaredBitrateKbps"] = encoding.DeclaredBitrateKbps,
            ["AverageBitrateKbps"] = encoding.AverageBitrateKbps,
        },
    };
}
