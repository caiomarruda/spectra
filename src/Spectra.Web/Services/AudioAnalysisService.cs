using Spectra.Analysis.Dynamics;
using Spectra.Analysis.Loudness;
using Spectra.Analysis.Noise;
using Spectra.Analysis.Scoring;
using Spectra.Analysis.Spectral;
using Spectra.Analysis.Stereo;
using Spectra.Analysis.Transcoding;
using Spectra.Analysis.Waveform;
using Spectra.Audio.Aiff;
using Spectra.Audio.Decoding;
using Spectra.Audio.Flac;
using Spectra.Audio.Mp3;
using Spectra.Audio.Wav;
using Spectra.Core.Decoding;
using Spectra.Core.Models;

namespace Spectra.Web.Services;

public interface IAudioAnalysisService
{
    Task<AudioAnalysisResult> AnalyzeAsync(byte[] data, string fileName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Browser-side counterpart to Spectra.Cli's Program.AnalyzeFile: same decoder/analyzer pipeline,
/// reused as-is from Spectra.Audio/Spectra.Analysis, but driven from an in-memory byte[] (an
/// IBrowserFile's contents) instead of a filesystem path — there is no disk to read from in
/// WebAssembly. Runs every analyzer sequentially rather than via CLI's Parallel.Invoke: Blazor
/// WebAssembly is single-threaded by default, so Parallel.* would not actually parallelize here.
/// </summary>
public sealed class AudioAnalysisService : IAudioAnalysisService
{
    public static IReadOnlyList<string> SupportedExtensions { get; } = [".mp3", ".wav", ".flac", ".aiff", ".aif"];

    public Task<AudioAnalysisResult> AnalyzeAsync(byte[] data, string fileName, CancellationToken cancellationToken = default)
    {
        var (fileInfo, formatInfo, encodingAnalysis, decoded) = ReadAndDecode(data, fileName);
        cancellationToken.ThrowIfCancellationRequested();

        var waveform = WaveformAnalyzer.Analyze(decoded);
        var spectral = SpectralAnalyzer.Analyze(decoded);
        var loudness = LoudnessAnalyzer.Analyze(decoded);
        var dynamicRange = DynamicRangeAnalyzer.Analyze(decoded, waveform);
        var clipping = ClippingAnalyzer.Analyze(decoded);
        var stereo = StereoAnalyzer.Analyze(decoded);
        var transcoding = TranscodingAnalyzer.Analyze(encodingAnalysis, spectral);
        var noise = NoiseAnalyzer.Analyze(decoded, waveform);
        cancellationToken.ThrowIfCancellationRequested();

        var overallAssessment = QualityScorer.Analyze(
            encodingAnalysis, spectral, dynamicRange, clipping, loudness, stereo, noise, transcoding);

        var warnings = new List<string>();
        if (decoded.PartialDecodeReason is { } reason)
        {
            warnings.Add(reason + " — every metric below reflects only the decoded portion, not the full track.");
        }

        var result = new AudioAnalysisResult
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
            Warnings = warnings,
        };

        return Task.FromResult(result);
    }

    private static (AudioFileInfo FileInfo, FormatInfo FormatInfo, EncodingAnalysis EncodingAnalysis, DecodedAudio Decoded) ReadAndDecode(byte[] data, string fileName)
    {
        var extension = Path.GetExtension(fileName);

        if (string.Equals(extension, ".mp3", StringComparison.OrdinalIgnoreCase))
        {
            var (fileInfo, formatInfo, encodingAnalysis) = Mp3MetadataReader.Read(data, fileName);
            return (fileInfo, formatInfo, encodingAnalysis, NLayerAudioDecoder.Decode(data, fileName));
        }
        if (string.Equals(extension, ".wav", StringComparison.OrdinalIgnoreCase))
        {
            var (fileInfo, formatInfo, encodingAnalysis) = WavMetadataReader.Read(data, fileName);
            return (fileInfo, formatInfo, encodingAnalysis, WavAudioDecoder.Decode(data, fileName));
        }
        if (string.Equals(extension, ".aiff", StringComparison.OrdinalIgnoreCase) || string.Equals(extension, ".aif", StringComparison.OrdinalIgnoreCase))
        {
            var (fileInfo, formatInfo, encodingAnalysis) = AiffMetadataReader.Read(data, fileName);
            return (fileInfo, formatInfo, encodingAnalysis, AiffAudioDecoder.Decode(data, fileName));
        }
        if (string.Equals(extension, ".flac", StringComparison.OrdinalIgnoreCase))
        {
            var (fileInfo, formatInfo, encodingAnalysis) = FlacMetadataReader.Read(data, fileName);
            return (fileInfo, formatInfo, encodingAnalysis, FlacAudioDecoder.Decode(data, fileName));
        }

        throw new NotSupportedException($"Unsupported file extension '{extension}'. Supported formats: {string.Join(", ", SupportedExtensions)}.");
    }
}
