using Spectra.Cli;
using Xunit;

namespace Spectra.Tests.Cli;

public class CliOptionsTests
{
    [Fact]
    public void Parse_PositionalPath_SetsInputPath()
    {
        var options = CliOptions.Parse(["song.mp3", "--json"]);

        Assert.NotNull(options);
        Assert.Equal("song.mp3", options!.InputPath);
    }

    [Fact]
    public void Parse_InputFlag_SetsInputPath()
    {
        var options = CliOptions.Parse(["--input", "song.mp3", "--json"]);

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
        var options = CliOptions.Parse(["--folder", "/some/dir", "--json"]);

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

    [Fact]
    public void Parse_ThreadsFlag_SetsThreadCount()
    {
        var options = CliOptions.Parse(["--folder", "/some/dir", "--threads", "4", "--json"]);

        Assert.NotNull(options);
        Assert.Equal(4, options!.Threads);
    }

    [Fact]
    public void Parse_NoThreadsFlag_LeavesThreadsNull()
    {
        var options = CliOptions.Parse(["--folder", "/some/dir", "--json"]);

        Assert.NotNull(options);
        Assert.Null(options!.Threads);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("notanumber")]
    public void Parse_InvalidThreadsValue_ReturnsNull(string value)
    {
        var options = CliOptions.Parse(["--folder", "/some/dir", "--threads", value, "--json"]);

        Assert.Null(options);
    }

    [Fact]
    public void Parse_ThreadsFlagMissingValue_ReturnsNull()
    {
        var options = CliOptions.Parse(["--folder", "/some/dir", "--threads"]);

        Assert.Null(options);
    }

    [Theory]
    [InlineData("song.mp3")]
    [InlineData("--input", "song.mp3")]
    [InlineData("--folder", "/some/dir")]
    public void Parse_NoExportFlag_ReturnsNull(params string[] args)
    {
        var options = CliOptions.Parse(args);

        Assert.Null(options);
    }

    [Fact]
    public void Parse_VerboseOnlyNoExportFlag_ReturnsNull()
    {
        var options = CliOptions.Parse(["song.mp3", "--verbose"]);

        Assert.Null(options);
    }
}
