using AudioQualityAnalyzer.Analysis.Spectral;
using AudioQualityAnalyzer.Analysis.Waveform;
using AudioQualityAnalyzer.Audio.Decoding;
using AudioQualityAnalyzer.Audio.Mp3;
using AudioQualityAnalyzer.Cli;
using AudioQualityAnalyzer.Core.Decoding;
using AudioQualityAnalyzer.Core.Models;
using AudioQualityAnalyzer.Reporting.ConsoleReport;
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

    var result = new AudioAnalysisResult
    {
        FileInfo = fileInfo,
        FormatInfo = formatInfo,
        EncodingAnalysis = encodingAnalysis,
        WaveformAnalysis = waveform,
        SpectralAnalysis = spectral,
    };

    ConsoleReporter.Report(result, options.Verbose, Console.Out);

    if (options.Html || options.Excel || options.Json)
    {
        logger.LogWarning("--html/--excel/--json exporters are not implemented yet in this version.");
    }
}
catch (Exception ex)
{
    logger.LogError(ex, "Analysis failed.");
    return 1;
}

return 0;

static void PrintUsage()
{
    Console.WriteLine("""
        Usage:
          AudioQualityAnalyzer <path-to.mp3>
          AudioQualityAnalyzer --input <path-to.mp3>

        Options:
          --html      Export HTML report (not yet implemented)
          --excel     Export Excel report (not yet implemented)
          --json      Export raw JSON data (not yet implemented)
          --verbose   Show all measured metrics
        """);
}
