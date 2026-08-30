using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
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
using AudioQualityAnalyzer.Cli;
using AudioQualityAnalyzer.Core.Decoding;
using AudioQualityAnalyzer.Core.Models;
using AudioQualityAnalyzer.Reporting.ConsoleReport;
using AudioQualityAnalyzer.Reporting.Excel;
using AudioQualityAnalyzer.Reporting.Html;
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
var logger = loggerFactory.CreateLogger("AudioQualityAnalyzer");

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

    if (!string.Equals(Path.GetExtension(inputPath), ".mp3", StringComparison.OrdinalIgnoreCase))
    {
        logger.LogError("Only MP3 files are supported in this version.");
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
        if (options.Excel)
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

    var mp3Files = Directory.EnumerateFiles(folderPath, "*.mp3", SearchOption.AllDirectories)
        .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
        .ToList();

    if (mp3Files.Count == 0)
    {
        Console.WriteLine("No .mp3 files found under " + folderPath);
        return 0;
    }

    Console.WriteLine($"Found {mp3Files.Count} MP3 file(s) under {folderPath}");
    Console.WriteLine();

    var successes = new List<BatchTrackResult>();
    var failures = new List<BatchTrackFailure>();

    for (var i = 0; i < mp3Files.Count; i++)
    {
        var path = mp3Files[i];
        var relativePath = Path.GetRelativePath(folderPath, path);
        Console.Write($"[{i + 1}/{mp3Files.Count}] {relativePath} ... ");

        try
        {
            var result = AnalyzeFile(path);
            successes.Add(new BatchTrackResult { RelativePath = relativePath, Result = result });
            Console.WriteLine($"{result.OverallAssessment.Verdict} ({result.OverallAssessment.OverallQualityScore:F0}/100)");

            if (options.Verbose)
            {
                ConsoleReporter.Report(result, verbose: true, Console.Out);
                Console.WriteLine();
            }
        }
        catch (Exception ex)
        {
            failures.Add(new BatchTrackFailure { RelativePath = relativePath, ErrorMessage = ex.Message });
            Console.WriteLine($"FAILED ({ex.Message})");
        }
    }

    Console.WriteLine();
    Console.WriteLine($"Analyzed {successes.Count} of {mp3Files.Count} file(s)" + (failures.Count > 0 ? $", {failures.Count} failed." : "."));

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
    if (options.Excel)
    {
        TryExport("Excel", () => ExcelReporter.WriteBatchToFile(successes, failures, Path.Combine(folderPath, $"{folderName}.batch-analysis.xlsx")));
    }

    return successes.Count == 0 && failures.Count > 0 ? 1 : 0;
}

static AudioAnalysisResult AnalyzeFile(string path)
{
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
          AudioQualityAnalyzer <path-to.mp3>
          AudioQualityAnalyzer --input <path-to.mp3>
          AudioQualityAnalyzer --folder <path-to-folder>

        Options:
          --folder    Recursively analyze every .mp3 under this folder (subfolders included)
          --html      Export HTML report
                        single file: OriginalName.analysis.html
                        --folder:    <FolderName>.batch-analysis.html in the scanned folder's root
          --excel     Export Excel report (.analysis.xlsx / .batch-analysis.xlsx, same rule as --html)
          --json      Export raw JSON data (.analysis.json / .batch-analysis.json, same rule as --html)
          --verbose   Show all measured metrics (single file: always; --folder: per-track detail too)
        """);
}
