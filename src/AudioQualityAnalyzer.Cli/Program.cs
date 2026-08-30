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
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
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

if (!File.Exists(options.InputPath))
{
    logger.LogError("File not found: {Path}", options.InputPath);
    return 1;
}

if (!string.Equals(Path.GetExtension(options.InputPath), ".mp3", StringComparison.OrdinalIgnoreCase))
{
    logger.LogError("Only MP3 files are supported in this version.");
    return 1;
}

var jsonOptions = new JsonSerializerOptions
{
    WriteIndented = true,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    Converters = { new JsonStringEnumConverter() },
    NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
};

try
{
    var (fileInfo, formatInfo, encodingAnalysis) = Mp3MetadataReader.Read(options.InputPath);
    logger.LogDebug("Parsed {FrameCount} MPEG frames.", encodingAnalysis.FrameCount);

    IAudioDecoder decoder = new NLayerAudioDecoder();
    var decoded = decoder.Decode(options.InputPath);
    logger.LogDebug(
        "Decoded {Channels}ch @ {Rate} Hz via {Decoder} {Version}.",
        decoded.ChannelCount, decoded.SampleRateHz, decoded.DecoderName, decoded.DecoderVersion);

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
    };

    ConsoleReporter.Report(result, options.Verbose, Console.Out);

    if (options.Html)
    {
        TryExport("HTML", () => HtmlReporter.WriteToFile(result, BuildExportPath(options.InputPath, "html")));
    }
    if (options.Json)
    {
        TryExport("JSON", () => File.WriteAllText(BuildExportPath(options.InputPath, "json"), JsonSerializer.Serialize(result, jsonOptions)));
    }
    if (options.Excel)
    {
        TryExport("Excel", () => ExcelReporter.WriteToFile(result, BuildExportPath(options.InputPath, "xlsx")));
    }
}
catch (Exception ex)
{
    logger.LogError(ex, "Analysis failed.");
    return 1;
}

return 0;

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

        Options:
          --html      Export HTML report (OriginalName.analysis.html)
          --excel     Export Excel report (OriginalName.analysis.xlsx)
          --json      Export raw JSON data (OriginalName.analysis.json)
          --verbose   Show all measured metrics
        """);
}
