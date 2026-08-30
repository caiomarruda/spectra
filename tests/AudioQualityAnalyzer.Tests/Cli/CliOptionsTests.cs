using AudioQualityAnalyzer.Cli;
using Xunit;

namespace AudioQualityAnalyzer.Tests.Cli;

public class CliOptionsTests
{
    [Fact]
    public void Parse_PositionalPath_SetsInputPath()
    {
        var options = CliOptions.Parse(["song.mp3"]);

        Assert.NotNull(options);
        Assert.Equal("song.mp3", options!.InputPath);
    }

    [Fact]
    public void Parse_InputFlag_SetsInputPath()
    {
        var options = CliOptions.Parse(["--input", "song.mp3"]);

        Assert.NotNull(options);
        Assert.Equal("song.mp3", options!.InputPath);
    }

    [Fact]
    public void Parse_AllExportFlags_AreRecognized()
    {
        var options = CliOptions.Parse(["song.mp3", "--html", "--excel", "--json", "--verbose"]);

        Assert.NotNull(options);
        Assert.True(options!.Html);
        Assert.True(options.Excel);
        Assert.True(options.Json);
        Assert.True(options.Verbose);
    }

    [Fact]
    public void Parse_NoArguments_ReturnsNull()
    {
        var options = CliOptions.Parse([]);

        Assert.Null(options);
    }

    [Fact]
    public void Parse_UnknownFlag_ReturnsNull()
    {
        var options = CliOptions.Parse(["song.mp3", "--unknown"]);

        Assert.Null(options);
    }

    [Fact]
    public void Parse_InputFlagMissingValue_ReturnsNull()
    {
        var options = CliOptions.Parse(["--input"]);

        Assert.Null(options);
    }
}
