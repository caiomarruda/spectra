using Spectra.Core.Models;
using Spectra.Reporting.Html;
using Xunit;

namespace Spectra.Tests.Reporting;

public class HtmlBatchReporterTests
{
    [Fact]
    public void Generate_MultipleTracks_ContainsOneRowPerTrack()
    {
        var successes = new List<BatchTrackResult>
        {
            new() { RelativePath = "album1/track1.mp3", Result = TestResultBuilder.Build() },
            new() { RelativePath = "album2/track2.mp3", Result = TestResultBuilder.Build() },
        };

        var html = HtmlBatchReporter.Generate(successes, [], "/music/library");

        Assert.Contains("album1/track1.mp3", html);
        Assert.Contains("album2/track2.mp3", html);
        Assert.Contains("Batch Audio Quality Analysis", html);
    }

    [Fact]
    public void Generate_NoTracks_DoesNotThrow()
    {
        var html = HtmlBatchReporter.Generate([], [], "/music/library");

        Assert.Contains("No tracks analyzed", html);
    }

    [Fact]
    public void Generate_WithFailures_ListsFailedFiles()
    {
        var failures = new List<BatchTrackFailure>
        {
            new() { RelativePath = "corrupt.mp3", ErrorMessage = "No valid MPEG audio frames found." },
        };

        var html = HtmlBatchReporter.Generate([], failures, "/music/library");

        Assert.Contains("corrupt.mp3", html);
        Assert.Contains("No valid MPEG audio frames found", html);
    }

    [Fact]
    public void Generate_PathWithHtmlSpecialCharacters_IsEscaped()
    {
        var successes = new List<BatchTrackResult>
        {
            new() { RelativePath = "<script>alert(1)</script>.mp3", Result = TestResultBuilder.Build() },
        };

        var html = HtmlBatchReporter.Generate(successes, [], "/music/library");

        Assert.DoesNotContain("<script>alert", html);
        Assert.Contains("&lt;script&gt;", html);
    }
}
