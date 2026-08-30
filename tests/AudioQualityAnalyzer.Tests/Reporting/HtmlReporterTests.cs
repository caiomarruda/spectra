using AudioQualityAnalyzer.Reporting.Html;
using Xunit;

namespace AudioQualityAnalyzer.Tests.Reporting;

public class HtmlReporterTests
{
    [Fact]
    public void Generate_FullResult_ContainsAllRequiredSections()
    {
        var result = TestResultBuilder.Build();

        var html = HtmlReporter.Generate(result);

        Assert.Contains("Executive Summary", html);
        Assert.Contains("File Information", html);
        Assert.Contains("Spectral Analysis", html);
        Assert.Contains("Loudness", html);
        Assert.Contains("Dynamic Range", html);
        Assert.Contains("Stereo", html);
        Assert.Contains("Findings", html);
        Assert.Contains("Technical Details", html);
        Assert.Contains("GOOD 128 KBPS", html);
    }

    [Fact]
    public void Generate_MonoFile_DoesNotThrowAndNotesNoStereoImage()
    {
        var result = TestResultBuilder.Build(includeStereo: false);

        var html = HtmlReporter.Generate(result);

        Assert.Contains("no stereo image", html);
    }

    [Fact]
    public void Generate_FileNameWithHtmlSpecialCharacters_IsEscaped()
    {
        var result = TestResultBuilder.Build();
        var withSpecialChars = result with
        {
            FileInfo = result.FileInfo with { FileName = "<script>alert(1)</script>.mp3" },
        };

        var html = HtmlReporter.Generate(withSpecialChars);

        Assert.DoesNotContain("<script>alert", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public void Generate_OutputIsWellFormedEnoughToParse()
    {
        var result = TestResultBuilder.Build();

        var html = HtmlReporter.Generate(result);

        Assert.Equal(html.Split("<svg").Length - 1, html.Split("</svg>").Length - 1);
        Assert.Contains("<html>", html);
        Assert.Contains("</html>", html);
    }
}
