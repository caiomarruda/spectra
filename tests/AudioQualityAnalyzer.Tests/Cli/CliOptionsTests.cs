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

    [Fact]
    public void Parse_FolderFlag_SetsFolderPath()
    {
        var options = CliOptions.Parse(["--folder", "/some/dir"]);

        Assert.NotNull(options);
        Assert.Equal("/some/dir", options!.FolderPath);
        Assert.Null(options.InputPath);
    }

    [Fact]
    public void Parse_FolderFlagMissingValue_ReturnsNull()
    {
        var options = CliOptions.Parse(["--folder"]);

        Assert.Null(options);
    }

    [Fact]
    public void Parse_BothInputAndFolder_ReturnsNull()
    {
        var options = CliOptions.Parse(["song.mp3", "--folder", "/some/dir"]);

        Assert.Null(options);
    }

    [Fact]
    public void Parse_FolderWithExportFlags_AreRecognized()
    {
        var options = CliOptions.Parse(["--folder", "/some/dir", "--html", "--excel", "--json", "--verbose"]);

        Assert.NotNull(options);
        Assert.True(options!.Html);
        Assert.True(options.Excel);
        Assert.True(options.Json);
        Assert.True(options.Verbose);
    }
}
