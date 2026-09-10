using Spectra.Analysis.Dynamics;
using Spectra.Analysis.Loudness;
using Spectra.Analysis.Noise;
using Spectra.Analysis.Scoring;
using Spectra.Analysis.Spectral;
using Spectra.Analysis.Stereo;
using Spectra.Analysis.Transcoding;
using Spectra.Analysis.Waveform;
using Spectra.Audio.Decoding;
using Spectra.Audio.Mp3;
using Spectra.Core.Models;
using Spectra.Tests.TestSupport;
using Spectra.Web.Services;
using Xunit;

namespace Spectra.Tests.Web;

/// <summary>
/// Spectra Web's browser pipeline (AudioAnalysisService, byte[]-based) must produce the same
/// analysis as the CLI's path-based pipeline (Spectra.Cli/Program.cs's AnalyzeFile) for the same
/// input — the master plan's CLI-vs-WASM regression requirement. Both pipelines are deterministic
/// (no randomness, no I/O timing dependence) given the same bytes, so equality is exact, not
/// approximate.
/// </summary>
public class AudioAnalysisServiceTests
{
    [Fact]
    public async Task AnalyzeAsync_MatchesCliPipeline_ForReferenceFile()
    {
        var path = ReferenceDataset.FindPath("mp3-320/track-320.mp3");
        var data = File.ReadAllBytes(path);

        var expected = AnalyzeViaCliPipeline(path);
        var actual = await new AudioAnalysisService().AnalyzeAsync(data, path);

        Assert.Equal(expected.FileInfo with { FullPath = actual.FileInfo.FullPath }, actual.FileInfo);
        Assert.Equal(expected.FormatInfo, actual.FormatInfo);
        Assert.Equal(expected.EncodingAnalysis, actual.EncodingAnalysis);

        Assert.Equal(expected.OverallAssessment.Verdict, actual.OverallAssessment.Verdict);
        Assert.Equal(expected.OverallAssessment.OverallQualityScore, actual.OverallAssessment.OverallQualityScore);
        Assert.Equal(expected.OverallAssessment.EncodingQualityScore, actual.OverallAssessment.EncodingQualityScore);
        Assert.Equal(expected.OverallAssessment.SpectralQualityScore, actual.OverallAssessment.SpectralQualityScore);
        Assert.Equal(expected.OverallAssessment.TechnicalQualityScore, actual.OverallAssessment.TechnicalQualityScore);
        Assert.Equal(expected.OverallAssessment.MasteringQualityScore, actual.OverallAssessment.MasteringQualityScore);
        Assert.Equal(expected.OverallAssessment.Findings.Count, actual.OverallAssessment.Findings.Count);

        Assert.Equal(expected.SpectralAnalysis.EffectiveBandwidthHz, actual.SpectralAnalysis.EffectiveBandwidthHz);
        Assert.Equal(expected.SpectralAnalysis.SpectralCentroidHz, actual.SpectralAnalysis.SpectralCentroidHz);
        Assert.Equal(expected.SpectralAnalysis.SpectralRolloffHz, actual.SpectralAnalysis.SpectralRolloffHz);
        Assert.Equal(expected.SpectralAnalysis.CutoffFrequencyHz, actual.SpectralAnalysis.CutoffFrequencyHz);

        Assert.Equal(expected.LoudnessAnalysis.IntegratedLufs, actual.LoudnessAnalysis.IntegratedLufs);
        Assert.Equal(expected.LoudnessAnalysis.SamplePeakDbfs, actual.LoudnessAnalysis.SamplePeakDbfs);
        Assert.Equal(expected.LoudnessAnalysis.TruePeakDbfs, actual.LoudnessAnalysis.TruePeakDbfs);

        Assert.Equal(expected.DynamicRangeAnalysis.CrestFactorDb, actual.DynamicRangeAnalysis.CrestFactorDb);
        Assert.Equal(expected.ClippingAnalysis.TotalClippedSamples, actual.ClippingAnalysis.TotalClippedSamples);
        Assert.Equal(expected.ClippingAnalysis.ClippedPercentage, actual.ClippingAnalysis.ClippedPercentage);

        Assert.Equal(expected.StereoAnalysis is not null, actual.StereoAnalysis is not null);
        if (expected.StereoAnalysis is not null && actual.StereoAnalysis is not null)
        {
            Assert.Equal(expected.StereoAnalysis.CorrelationCoefficient, actual.StereoAnalysis.CorrelationCoefficient);
        }

        Assert.Equal(expected.NoiseAnalysis.NoiseFloorDb, actual.NoiseAnalysis.NoiseFloorDb);

        Assert.Equal(expected.TranscodingAnalysis.Probability, actual.TranscodingAnalysis.Probability);
        Assert.Equal(expected.TranscodingAnalysis.Label, actual.TranscodingAnalysis.Label);
        Assert.Equal(expected.TranscodingAnalysis.Confidence, actual.TranscodingAnalysis.Confidence);
    }

    /// <summary>Same pipeline as Spectra.Cli/Program.cs's AnalyzeFile (path-based, sequential) —
    /// duplicated here rather than reused because Program.cs's top-level local functions aren't
    /// callable from a test project.</summary>
    private static AudioAnalysisResult AnalyzeViaCliPipeline(string path)
    {
        var (fileInfo, formatInfo, encodingAnalysis) = Mp3MetadataReader.Read(path);
        var decoded = new NLayerAudioDecoder().Decode(path);

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
            Warnings = [],
        };
    }
}
