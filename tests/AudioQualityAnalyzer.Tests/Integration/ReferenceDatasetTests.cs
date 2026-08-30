using AudioQualityAnalyzer.Analysis.Dynamics;
using AudioQualityAnalyzer.Analysis.Loudness;
using AudioQualityAnalyzer.Analysis.Noise;
using AudioQualityAnalyzer.Analysis.Scoring;
using AudioQualityAnalyzer.Analysis.Spectral;
using AudioQualityAnalyzer.Analysis.Stereo;
using AudioQualityAnalyzer.Analysis.Transcoding;
using AudioQualityAnalyzer.Analysis.Waveform;
using AudioQualityAnalyzer.Audio.Decoding;
using AudioQualityAnalyzer.Audio.Mp3;
using AudioQualityAnalyzer.Core.Decoding;
using AudioQualityAnalyzer.Core.Models;
using Xunit;

namespace AudioQualityAnalyzer.Tests.Integration;

/// <summary>
/// End-to-end regression tests against the Phase 13 reference dataset (reference/, regenerated
/// by scripts/generate-reference-dataset.sh). Each of these locks in a real bug found while
/// building that dataset and running the full pipeline against it — see the commit history for
/// 05-IMPLEMENTATION-PLAN.md Phase 13/14. Ranges are deliberately loose
/// (05-IMPLEMENTATION-PLAN.md Phase 15: "Evitar expectativas excessivamente rígidas; validar
/// faixas razoáveis") since this is one synthetic dataset, not the broad real-world corpus real
/// calibration would need.
/// </summary>
public class ReferenceDatasetTests
{
    private static readonly string[] BitrateLadderPaths =
    [
        "mp3-128/track-128.mp3", "mp3-192/track-192.mp3", "mp3-256/track-256.mp3", "mp3-320/track-320.mp3",
    ];


    [Theory]
    [InlineData("mp3-320/track-320.mp3", "GOOD 320 KBPS")]
    [InlineData("mp3-256/track-256.mp3", "GOOD 256 KBPS")]
    [InlineData("mp3-192/track-192.mp3", "GOOD 192 KBPS")]
    [InlineData("mp3-128/track-128.mp3", "GOOD 128 KBPS")]
    public void Analyze_GenuineEncode_IsGoodWithLowTranscodeProbability(string relativePath, string expectedVerdict)
    {
        var result = AnalyzeReferenceFile(relativePath);

        Assert.Equal(expectedVerdict, result.OverallAssessment.Verdict);
        Assert.True(
            result.TranscodingAnalysis.Probability < 30,
            $"Expected a genuine encode to score low transcoding probability, got {result.TranscodingAnalysis.Probability}%.");
    }

    [Fact]
    public void Analyze_128To320Transcode_IsFlaggedAsPossibleTranscode()
    {
        var result = AnalyzeReferenceFile("transcoded-128-to-320/track-128-transcoded-320.mp3");

        Assert.Equal("320 KBPS / POSSIBLE TRANSCODE", result.OverallAssessment.Verdict);
        Assert.True(
            result.TranscodingAnalysis.Probability > 50,
            $"Expected a clear-cut 128->320 transcode to score above 50%, got {result.TranscodingAnalysis.Probability}%.");
    }

    [Fact]
    public void Analyze_128To320Transcode_ScoresHigherThanGenuine128Encode()
    {
        // The core claim of TranscodingAnalyzer: re-declaring the same underlying (128kbps-limited)
        // content as 320kbps must score meaningfully higher than the honest 128kbps encode of the
        // same source, even if the absolute number doesn't cross into "Likely".
        var genuine = AnalyzeReferenceFile("mp3-128/track-128.mp3");
        var transcoded = AnalyzeReferenceFile("transcoded-128-to-320/track-128-transcoded-320.mp3");

        Assert.True(transcoded.TranscodingAnalysis.Probability > genuine.TranscodingAnalysis.Probability + 20);
    }

    [Fact]
    public void Analyze_192To320Transcode_DoesNotFalselyFlagAsLikely()
    {
        // A 192->320 transcode has much less spectral evidence than 128->320 (192kbps already
        // reaches close to 320's expected bandwidth) — the algorithm should not overreach into a
        // confident "Likely/Highly likely" verdict on weak evidence (low-false-positive goal).
        var result = AnalyzeReferenceFile("transcoded-192-to-320/track-192-transcoded-320.mp3");

        Assert.True(
            result.TranscodingAnalysis.Probability < 61,
            $"Expected the harder-to-detect 192->320 case to stay below the 'Likely' threshold, got {result.TranscodingAnalysis.Probability}%.");
    }

    [Fact]
    public void Analyze_ProblematicMastering_IsFlaggedDespiteCleanEncoding()
    {
        var result = AnalyzeReferenceFile("problematic-mastering/track-mastering-issues-320.mp3");

        Assert.Equal("VALID 320 KBPS / POOR MASTERING", result.OverallAssessment.Verdict);
        Assert.True(result.OverallAssessment.EncodingQualityScore > 90, "Encoding itself is clean 320kbps CBR.");
        Assert.True(result.OverallAssessment.MasteringQualityScore < 60, "Mastering should be flagged for the induced clipping.");
    }

    [Fact]
    public void Analyze_LowLoudness_IsNotPenalizedAsPoorMastering()
    {
        // 03-QUALITY-DETECTION.md: "Loudness baixo não é defeito por si só".
        var result = AnalyzeReferenceFile("low-loudness/track-low-loudness-320.mp3");

        Assert.Equal("VALID 320 KBPS / LOW LOUDNESS", result.OverallAssessment.Verdict);
        Assert.True(result.OverallAssessment.MasteringQualityScore >= 80);
    }

    [Fact]
    public void Analyze_BitrateLadder_EffectiveBandwidthIncreasesWithBitrate()
    {
        var bandwidths = BitrateLadderPaths
            .Select(path => AnalyzeReferenceFile(path).SpectralAnalysis.EffectiveBandwidthHz)
            .ToList();

        for (var i = 1; i < bandwidths.Count; i++)
        {
            Assert.True(
                bandwidths[i] >= bandwidths[i - 1],
                $"Expected non-decreasing effective bandwidth as bitrate increases, got {string.Join(", ", bandwidths)}.");
        }
    }

    private static AudioAnalysisResult AnalyzeReferenceFile(string relativePath)
    {
        var path = FindReferenceDatasetPath(relativePath);

        var (fileInfo, formatInfo, encodingAnalysis) = Mp3MetadataReader.Read(path);
        IAudioDecoder decoder = new NLayerAudioDecoder();
        var decoded = decoder.Decode(path);

        var waveform = WaveformAnalyzer.Analyze(decoded);
        var spectral = SpectralAnalyzer.Analyze(decoded);
        var loudness = LoudnessAnalyzer.Analyze(decoded);
        var dynamicRange = DynamicRangeAnalyzer.Analyze(decoded, waveform);
        var clipping = ClippingAnalyzer.Analyze(decoded);
        var stereo = StereoAnalyzer.Analyze(decoded);
        var transcoding = TranscodingAnalyzer.Analyze(encodingAnalysis, spectral);
        var noise = NoiseAnalyzer.Analyze(decoded, waveform);
        var overallAssessment = QualityScorer.Analyze(
            encodingAnalysis, spectral, dynamicRange, clipping, loudness, stereo, noise, transcoding);

        return new AudioAnalysisResult
        {
            FileInfo = fileInfo,
            FormatInfo = formatInfo,
            EncodingAnalysis = encodingAnalysis,
            WaveformAnalysis = waveform,
            SpectralAnalysis = spectral,
            LoudnessAnalysis = loudness,
            DynamicRangeAnalysis = dynamicRange,
            ClippingAnalysis = clipping,
            StereoAnalysis = stereo,
            TranscodingAnalysis = transcoding,
            NoiseAnalysis = noise,
            OverallAssessment = overallAssessment,
        };
    }

    private static string FindReferenceDatasetPath(string relativePath)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "reference")))
        {
            dir = Directory.GetParent(dir)?.FullName;
        }

        if (dir is null)
        {
            throw new DirectoryNotFoundException(
                "Could not locate the reference/ dataset directory by walking up from the test output directory. " +
                "Run scripts/generate-reference-dataset.sh from the repo root first.");
        }

        return Path.Combine(dir, "reference", relativePath);
    }
}
