using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
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
using Spectra.Cli;
using Spectra.Core.Decoding;
using Spectra.Core.Models;
using Spectra.Reporting.ConsoleReport;
using Spectra.Reporting.Excel;
using Spectra.Reporting.Html;
using Microsoft.Extensions.Logging;

var options = CliOptions.Parse(args);
if (options is null)
{
    PrintUsage();
    return 1;
}

using var loggerFactory = LoggerFactory.Create(builder => builder
    .AddSimpleConsole(o => o.SingleLine = true)
    .SetMinimumLevel(options.Verbose ? LogLevel.Debug : LogLevel.Information));
var logger = loggerFactory.CreateLogger("Spectra");

var jsonOptions = new JsonSerializerOptions
{
    WriteIndented = true,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    Converters = { new JsonStringEnumConverter() },
    NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
};

return options.FolderPath is not null
    ? RunFolderMode(options, logger, jsonOptions)
    : RunSingleFileMode(options, logger, jsonOptions);

static int RunSingleFileMode(CliOptions options, ILogger logger, JsonSerializerOptions jsonOptions)
{
    var inputPath = options.InputPath!;

    if (!File.Exists(inputPath))
    {
        logger.LogError("File not found: {Path}", inputPath);
        return 1;
    }

    if (!IsSupportedExtension(inputPath))
    {
        logger.LogError("Unsupported file type '{Extension}'. Supported formats: {Supported}.", Path.GetExtension(inputPath), string.Join(", ", SupportedExtensions()));
        return 1;
    }

    try
    {
        var result = AnalyzeFile(inputPath);
        ConsoleReporter.Report(result, options.Verbose, Console.Out);

        if (options.Html)
        {
            TryExport("HTML", () => HtmlReporter.WriteToFile(result, BuildExportPath(inputPath, "html")));
        }
        if (options.Json)
        {
            TryExport("JSON", () => File.WriteAllText(BuildExportPath(inputPath, "json"), JsonSerializer.Serialize(result, jsonOptions)));
        }
        if (options.Sheet)
        {
            TryExport("Excel", () => ExcelReporter.WriteToFile(result, BuildExportPath(inputPath, "xlsx")));
        }

        return 0;
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Analysis failed.");
        return 1;
    }
}

static int RunFolderMode(CliOptions options, ILogger logger, JsonSerializerOptions jsonOptions)
{
    var folderPath = options.FolderPath!;

    if (!Directory.Exists(folderPath))
    {
        logger.LogError("Folder not found: {Path}", folderPath);
        return 1;
    }

    var audioFiles = Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories)
        .Where(IsSupportedExtension)
        .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
        .ToList();

    if (audioFiles.Count == 0)
    {
        Console.WriteLine($"No supported audio files ({string.Join(", ", SupportedExtensions())}) found under " + folderPath);
        return 0;
    }

    var threadCount = options.Threads ?? Environment.ProcessorCount;
    Console.WriteLine($"Found {audioFiles.Count} audio file(s) under {folderPath} — analyzing with {threadCount} thread(s)");
    Console.WriteLine();

    // Each file's analysis is independent (no shared mutable state across analyzers — every
    // analyzer is a pure static function, and NLayerAudioDecoder/LoudnessMeter are instantiated
    // fresh per call), so files are processed concurrently. Results are written into a
    // pre-sized, index-addressed array (one slot per thread, no contention) so the final
    // successes/failures lists stay in the original file-discovery order regardless of which
    // thread finished which file first — deterministic reports, non-deterministic scheduling.
    var slots = new (BatchTrackResult? Success, BatchTrackFailure? Failure)[audioFiles.Count];
    var completedCount = 0;
    var consoleLock = new object();

    Parallel.For(0, audioFiles.Count, new ParallelOptions { MaxDegreeOfParallelism = threadCount }, i =>
    {
        var path = audioFiles[i];
        var relativePath = Path.GetRelativePath(folderPath, path);

        string statusLine;
        AudioAnalysisResult? result = null;
        try
        {
            result = AnalyzeFile(path);
            slots[i] = (new BatchTrackResult { RelativePath = relativePath, Result = result }, null);
            statusLine = $"{result.OverallAssessment.Verdict} ({result.OverallAssessment.OverallQualityScore:F0}/100)";
        }
        catch (Exception ex)
        {
            slots[i] = (null, new BatchTrackFailure { RelativePath = relativePath, ErrorMessage = ex.Message });
            statusLine = $"FAILED ({ex.Message})";
        }

        var completed = Interlocked.Increment(ref completedCount);
        lock (consoleLock)
        {
            Console.WriteLine($"[{completed}/{audioFiles.Count}] {relativePath} ... {statusLine}");
            if (options.Verbose && result is not null)
            {
                ConsoleReporter.Report(result, verbose: true, Console.Out);
                Console.WriteLine();
            }
        }
    });

    var successes = slots.Where(s => s.Success is not null).Select(s => s.Success!).ToList();
    var failures = slots.Where(s => s.Failure is not null).Select(s => s.Failure!).ToList();

    Console.WriteLine();
    Console.WriteLine($"Analyzed {successes.Count} of {audioFiles.Count} file(s)" + (failures.Count > 0 ? $", {failures.Count} failed." : "."));

    // One consolidated file per format in the scanned folder's root, not one per track/subfolder.
    var folderName = new DirectoryInfo(folderPath).Name;
    if (options.Html)
    {
        TryExport("HTML", () => HtmlBatchReporter.WriteToFile(successes, failures, folderPath, Path.Combine(folderPath, $"{folderName}.batch-analysis.html")));
    }
    if (options.Json)
    {
        TryExport("JSON", () => File.WriteAllText(
            Path.Combine(folderPath, $"{folderName}.batch-analysis.json"),
            JsonSerializer.Serialize(new { Successes = successes, Failures = failures }, jsonOptions)));
    }
    if (options.Sheet)
    {
        TryExport("Excel", () => ExcelReporter.WriteBatchToFile(successes, failures, Path.Combine(folderPath, $"{folderName}.batch-analysis.xlsx")));
    }

    return successes.Count == 0 && failures.Count > 0 ? 1 : 0;
}

/// <summary>Extensions this analyzer can read, ordered as shown in usage/error text.</summary>
static string[] SupportedExtensions() => [".mp3", ".wav", ".flac", ".aiff", ".aif"];

static bool IsSupportedExtension(string path) =>
    SupportedExtensions().Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

static AudioAnalysisResult AnalyzeFile(string path)
{
    var (fileInfo, formatInfo, encodingAnalysis, decoder) = ReadMetadata(path);
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

    var warnings = new List<string>();
    if (decoded.PartialDecodeReason is { } reason)
    {
        warnings.Add(reason + " — every metric below reflects only the decoded portion, not the full track.");
    }

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
        Warnings = warnings,
    };
}

static (AudioFileInfo FileInfo, FormatInfo FormatInfo, EncodingAnalysis EncodingAnalysis, IAudioDecoder Decoder) ReadMetadata(string path)
{
    var extension = Path.GetExtension(path);

    if (string.Equals(extension, ".mp3", StringComparison.OrdinalIgnoreCase))
    {
        var (fileInfo, formatInfo, encodingAnalysis) = Mp3MetadataReader.Read(path);
        return (fileInfo, formatInfo, encodingAnalysis, new NLayerAudioDecoder());
    }
    if (string.Equals(extension, ".wav", StringComparison.OrdinalIgnoreCase))
    {
        var (fileInfo, formatInfo, encodingAnalysis) = WavMetadataReader.Read(path);
        return (fileInfo, formatInfo, encodingAnalysis, new WavAudioDecoder());
    }
    if (string.Equals(extension, ".aiff", StringComparison.OrdinalIgnoreCase) || string.Equals(extension, ".aif", StringComparison.OrdinalIgnoreCase))
    {
        var (fileInfo, formatInfo, encodingAnalysis) = AiffMetadataReader.Read(path);
        return (fileInfo, formatInfo, encodingAnalysis, new AiffAudioDecoder());
    }
    if (string.Equals(extension, ".flac", StringComparison.OrdinalIgnoreCase))
    {
        var (fileInfo, formatInfo, encodingAnalysis) = FlacMetadataReader.Read(path);
        return (fileInfo, formatInfo, encodingAnalysis, new FlacAudioDecoder());
    }

    throw new NotSupportedException($"Unsupported file extension '{extension}'. Supported formats: {string.Join(", ", SupportedExtensions())}.");
}

// A failed export must not be reported as an analysis failure (04-REPORTS.md "Export Errors").
static void TryExport(string label, Action export)
{
    try
    {
        export();
        Console.WriteLine($"{label} export: SUCCESS");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"{label} export: FAILED");
        Console.WriteLine($"Reason: {ex.Message}");
    }
}

static string BuildExportPath(string inputPath, string extension)
{
    var directory = Path.GetDirectoryName(inputPath) ?? ".";
    var nameWithoutExtension = Path.GetFileNameWithoutExtension(inputPath);
    return Path.Combine(directory, $"{nameWithoutExtension}.analysis.{extension}");
}

static void PrintUsage()
{
    Console.WriteLine("""
        Usage:
          Spectra <path-to-file> [--html|--sheet|--json] [--verbose]
          Spectra --input <path-to-file> [--html|--sheet|--json] [--verbose]
          Spectra --folder <path-to-folder> --html|--sheet|--json

        Supported formats: .mp3, .wav, .flac, .aiff, .aif

        Single-file mode always prints the full report to the console, so --html/--sheet/--json
        are optional there. --folder mode has no per-track console output, so at least one of
        --html, --sheet, or --json is required — without one, nothing from the scan is kept
        anywhere once the console output scrolls away.

        Options:
          --folder    Recursively analyze every supported audio file under this folder (subfolders included)
          --threads   Parallel files to analyze at once in --folder mode (default: all CPU cores)
          --html      Export HTML report
                        single file: OriginalName.analysis.html
                        --folder:    <FolderName>.batch-analysis.html in the scanned folder's root
          --sheet     Export Excel report (.analysis.xlsx / .batch-analysis.xlsx, same rule as --html)
          --json      Export raw JSON data (.analysis.json / .batch-analysis.json, same rule as --html)
          --verbose   Show all measured metrics (single file only — files are analyzed concurrently
                        in --folder mode, so per-track verbose output would interleave illegibly)

        The input file is only ever read, never modified — no audio data or tags are changed.
        """);
}
