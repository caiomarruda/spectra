using Spectra.Analysis.Common;
using Spectra.Core.Enums;
using Spectra.Core.Models;

namespace Spectra.Analysis.Scoring;

/// <summary>
/// The interpretation layer on top of every other analysis. Facts come first — this only
/// combines them into scores and a short verdict, and every deduction is traceable to a metric
/// already reported elsewhere in AudioAnalysisResult (03-QUALITY-DETECTION.md: "scoring não deve
/// esconder as métricas originais").
/// </summary>
public static class QualityScorer
{
    public static OverallAssessment Analyze(
        EncodingAnalysis encoding,
        SpectralAnalysis spectral,
        DynamicRangeAnalysis dynamics,
        ClippingAnalysis clipping,
        LoudnessAnalysis loudness,
        StereoAnalysis? stereo,
        NoiseAnalysis noise,
        TranscodingAnalysis transcoding)
    {
        var findings = new List<AnalysisFinding>();

        var encodingScore = ScoreEncoding(encoding, out var encodingFindings);
        findings.AddRange(encodingFindings);

        var spectralScore = ScoreSpectral(spectral);

        var technicalScore = ScoreTechnical(clipping, stereo, noise, out var technicalFindings);
        findings.AddRange(technicalFindings);

        var masteringScore = ScoreMastering(clipping, dynamics, loudness, out var masteringFindings);
        findings.AddRange(masteringFindings);

        findings.AddRange(transcoding.Findings);

        var overallScore =
            (encodingScore * ScoringSettings.EncodingWeight) +
            (spectralScore * ScoringSettings.SpectralWeight) +
            (technicalScore * ScoringSettings.TechnicalWeight) +
            (masteringScore * ScoringSettings.MasteringWeight);

        var referenceBitrateKbps = encoding.BitrateMode == BitrateMode.ConstantBitRate
            ? encoding.DeclaredBitrateKbps
            : (int)Math.Round(encoding.AverageBitrateKbps);

        var verdict = BuildVerdict(referenceBitrateKbps, transcoding, spectralScore, technicalScore, masteringScore, loudness);

        return new OverallAssessment
        {
            EncodingQualityScore = encodingScore,
            SpectralQualityScore = spectralScore,
            TechnicalQualityScore = technicalScore,
            MasteringQualityScore = masteringScore,
            OverallQualityScore = overallScore,
            Verdict = verdict,
            Findings = findings.OrderByDescending(f => f.Severity).ToList(),
        };
    }

    private static double ScoreEncoding(EncodingAnalysis encoding, out List<AnalysisFinding> findings)
    {
        findings = [];
        var referenceBitrateKbps = encoding.BitrateMode == BitrateMode.ConstantBitRate
            ? encoding.DeclaredBitrateKbps
            : (int)Math.Round(encoding.AverageBitrateKbps);

        var baseScore = PiecewiseLinear.Interpolate(ScoringSettings.BitrateQualityCurve, referenceBitrateKbps);

        // The bitrate-mismatch fact itself is already surfaced as a finding by TranscodingAnalyzer
        // (appended separately in Analyze) — only apply the score penalty here, not a duplicate finding.
        var mismatch = encoding.BitrateMode == BitrateMode.ConstantBitRate
            && Math.Abs(encoding.AverageBitrateKbps - encoding.DeclaredBitrateKbps) > 2.0;
        var score = Math.Clamp(baseScore - (mismatch ? ScoringSettings.BitrateMismatchPenalty : 0), 0, 100);

        if (referenceBitrateKbps < 96)
        {
            findings.Add(new AnalysisFinding
            {
                Code = "ENCODING_LOW_BITRATE",
                Title = "Low encoding bitrate",
                Severity = Severity.Warning,
                Confidence = ConfidenceLevel.High,
                Description = $"A {referenceBitrateKbps} kbps encode has a low technical ceiling regardless of source quality.",
                Evidence = [$"Bitrate: {referenceBitrateKbps} kbps"],
                Metrics = new Dictionary<string, double> { ["BitrateKbps"] = referenceBitrateKbps },
            });
        }

        return score;
    }

    private static double ScoreSpectral(SpectralAnalysis spectral)
    {
        // Deliberately a smooth curve, not a hard pass/fail threshold
        // (02-AUDIO-ANALYSIS-SPEC.md: "Não usar regra simples como bandwidth < 18 kHz = bad").
        // Flatness/contrast are reported as data but not scored: without a calibration dataset
        // (Phase 13/14) there's no defensible "good" direction for them yet.
        return Math.Clamp(spectral.EffectiveBandwidthHz / ScoringSettings.FullBandwidthReferenceHz * 100.0, 0, 100);
    }

    private static double ScoreTechnical(ClippingAnalysis clipping, StereoAnalysis? stereo, NoiseAnalysis noise, out List<AnalysisFinding> findings)
    {
        findings = [];
        double penalty = 0;

        if (clipping.IsSevere)
        {
            penalty += ScoringSettings.SeverClippingPenalty;
            findings.Add(BuildClippingFinding(clipping, severe: true));
        }
        else if (clipping.TotalClippedSamples > 0)
        {
            penalty += Math.Min(ScoringSettings.MaxMinorClippingPenalty, clipping.ClippedPercentage * ScoringSettings.ClippingPenaltyPerPercent);
        }

        if (stereo is not null)
        {
            if (stereo.HasPolarityInversion)
            {
                penalty += ScoringSettings.PolarityInversionPenalty;
                findings.Add(BuildStereoFinding("TECHNICAL_POLARITY_INVERSION", "Polarity inversion between channels", stereo));
            }
            else if (stereo.HasPhaseProblems)
            {
                penalty += ScoringSettings.PhaseProblemPenalty;
                findings.Add(BuildStereoFinding("TECHNICAL_PHASE_PROBLEM", "Out-of-phase content between channels", stereo));
            }

            if (stereo.IsSeverelyImbalanced)
            {
                penalty += ScoringSettings.SevereImbalancePenalty;
                findings.Add(BuildStereoFinding("TECHNICAL_CHANNEL_IMBALANCE", "Severe channel imbalance", stereo));
            }
        }

        if (noise.HasSignificantDcOffset)
        {
            penalty += ScoringSettings.DcOffsetPenalty;
            findings.Add(new AnalysisFinding
            {
                Code = "TECHNICAL_DC_OFFSET",
                Title = "Significant DC offset",
                Severity = Severity.Warning,
                Confidence = ConfidenceLevel.High,
                Description = "One or more channels have a non-zero average sample value, indicating a DC bias.",
                Evidence = noise.DcOffsetPerChannel.Select((v, i) => $"Channel {i} DC offset: {v:E2}").ToList(),
                Metrics = noise.DcOffsetPerChannel
                    .Select((v, i) => (Key: $"DcOffsetChannel{i}", v))
                    .ToDictionary(x => x.Key, x => x.v),
            });
        }

        if (noise.HasExcessiveInternalSilence)
        {
            penalty += ScoringSettings.ExcessiveSilencePenalty;
            findings.Add(new AnalysisFinding
            {
                Code = "TECHNICAL_EXCESSIVE_SILENCE",
                Title = "Unusually long internal silence",
                Severity = Severity.Info,
                Confidence = ConfidenceLevel.Medium,
                Description = "A silent gap of several seconds was found in the middle of the track, away from the leading/trailing edges.",
                Evidence = ["Internal silence detected: true"],
                Metrics = new Dictionary<string, double>(),
            });
        }

        return Math.Clamp(100 - penalty, 0, 100);
    }

    private static double ScoreMastering(ClippingAnalysis clipping, DynamicRangeAnalysis dynamics, LoudnessAnalysis loudness, out List<AnalysisFinding> findings)
    {
        findings = [];
        double penalty = 0;

        if (clipping.IsSevere)
        {
            penalty += ScoringSettings.SeverClippingPenalty;
        }
        else if (clipping.TotalClippedSamples > 0)
        {
            penalty += Math.Min(ScoringSettings.MaxMinorClippingPenalty, clipping.ClippedPercentage * ScoringSettings.ClippingPenaltyPerPercent);
        }

        if (dynamics.CrestFactorDb < ScoringSettings.HeavyLimitingCrestFactorDb)
        {
            penalty += ScoringSettings.HeavyLimitingPenalty;
            findings.Add(BuildLimitingFinding(dynamics, severe: true));
        }
        else if (dynamics.CrestFactorDb < ScoringSettings.ModerateLimitingCrestFactorDb)
        {
            penalty += ScoringSettings.ModerateLimitingPenalty;
            findings.Add(BuildLimitingFinding(dynamics, severe: false));
        }

        if (loudness.TruePeakDbfs > 0)
        {
            penalty += ScoringSettings.TruePeakOverPenalty;
            findings.Add(new AnalysisFinding
            {
                Code = "MASTERING_TRUE_PEAK_OVER",
                Title = "True peak exceeds 0 dBTP",
                Severity = Severity.Warning,
                Confidence = ConfidenceLevel.High,
                Description = "Inter-sample reconstruction peaks above digital full scale, which can cause audible distortion or clipping on some playback systems.",
                Evidence = [$"True peak: {loudness.TruePeakDbfs:F2} dBTP"],
                Metrics = new Dictionary<string, double> { ["TruePeakDbfs"] = loudness.TruePeakDbfs },
            });
        }

        if (!double.IsNegativeInfinity(loudness.IntegratedLufs) && loudness.LoudnessRangeLu < ScoringSettings.LowLoudnessRangeLu)
        {
            penalty += ScoringSettings.LowLoudnessRangePenalty;
            findings.Add(new AnalysisFinding
            {
                Code = "MASTERING_LOW_DYNAMIC_RANGE",
                Title = "Low loudness range",
                Severity = Severity.Info,
                Confidence = ConfidenceLevel.Medium,
                Description = "The track has very little variation in loudness over time, consistent with heavy compression/limiting.",
                Evidence = [$"Loudness range: {loudness.LoudnessRangeLu:F1} LU"],
                Metrics = new Dictionary<string, double> { ["LoudnessRangeLu"] = loudness.LoudnessRangeLu },
            });
        }

        // Low loudness is never penalized by itself (02-AUDIO-ANALYSIS-SPEC.md section 8,
        // 03-QUALITY-DETECTION.md: "Loudness baixo não é defeito por si só").
        return Math.Clamp(100 - penalty, 0, 100);
    }

    private static string BuildVerdict(
        int referenceBitrateKbps, TranscodingAnalysis transcoding,
        double spectralScore, double technicalScore, double masteringScore,
        LoudnessAnalysis loudness)
    {
        if (transcoding.Label is TranscodingProbabilityLabel.Likely or TranscodingProbabilityLabel.HighlyLikely)
        {
            return $"{referenceBitrateKbps} KBPS / POSSIBLE TRANSCODE";
        }

        if (masteringScore < ScoringSettings.PoorMasteringScoreThreshold)
        {
            return $"VALID {referenceBitrateKbps} KBPS / POOR MASTERING";
        }

        var isSpectrallyAndTechnicallyClean = spectralScore >= ScoringSettings.GoodComponentScoreThreshold
            && technicalScore >= ScoringSettings.GoodComponentScoreThreshold;

        if (!double.IsNegativeInfinity(loudness.IntegratedLufs)
            && loudness.IntegratedLufs < ScoringSettings.LowLoudnessLufsThreshold
            && isSpectrallyAndTechnicallyClean)
        {
            return $"VALID {referenceBitrateKbps} KBPS / LOW LOUDNESS";
        }

        if (isSpectrallyAndTechnicallyClean && masteringScore >= ScoringSettings.GoodComponentScoreThreshold)
        {
            return $"GOOD {referenceBitrateKbps} KBPS";
        }

        return $"{referenceBitrateKbps} KBPS / MIXED QUALITY";
    }

    private static AnalysisFinding BuildClippingFinding(ClippingAnalysis clipping, bool severe) => new()
    {
        Code = "MASTERING_CLIPPING",
        Title = severe ? "Sustained clipping detected" : "Minor clipping detected",
        Severity = severe ? Severity.Critical : Severity.Warning,
        Confidence = ConfidenceLevel.High,
        Description = severe
            ? "Clipping runs are long enough to be audibly distorting, not just isolated single-sample events."
            : "A small number of samples reach full scale.",
        Evidence =
        [
            $"Clipped samples: {clipping.TotalClippedSamples} ({clipping.ClippedPercentage:F4}%)",
            $"Longest clip: {clipping.LongestClipDuration.TotalMilliseconds:F1} ms",
        ],
        Metrics = new Dictionary<string, double>
        {
            ["ClippedPercentage"] = clipping.ClippedPercentage,
            ["LongestClipMs"] = clipping.LongestClipDuration.TotalMilliseconds,
        },
    };

    private static AnalysisFinding BuildLimitingFinding(DynamicRangeAnalysis dynamics, bool severe) => new()
    {
        Code = "MASTERING_HEAVY_LIMITING",
        Title = severe ? "Heavily limited dynamics" : "Moderately limited dynamics",
        Severity = severe ? Severity.Warning : Severity.Info,
        Confidence = ConfidenceLevel.Medium,
        Description = "A low crest factor indicates aggressive limiting/compression ('loudness war' style mastering) rather than a natural dynamic performance.",
        Evidence = [$"Crest factor: {dynamics.CrestFactorDb:F1} dB"],
        Metrics = new Dictionary<string, double> { ["CrestFactorDb"] = dynamics.CrestFactorDb },
    };

    private static AnalysisFinding BuildStereoFinding(string code, string title, StereoAnalysis stereo) => new()
    {
        Code = code,
        Title = title,
        Severity = Severity.Warning,
        Confidence = ConfidenceLevel.High,
        Description = "See the Stereo analysis section for the underlying correlation and balance measurements.",
        Evidence =
        [
            $"Correlation: {stereo.CorrelationCoefficient:F2}",
            $"Balance: {stereo.ChannelBalanceDb:F2} dB",
        ],
        Metrics = new Dictionary<string, double>
        {
            ["Correlation"] = stereo.CorrelationCoefficient,
            ["ChannelBalanceDb"] = stereo.ChannelBalanceDb,
        },
    };
}
