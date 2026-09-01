using Spectra.Core.Models;
using Spectra.Reporting.Excel;
using ClosedXML.Excel;
using Xunit;

namespace Spectra.Tests.Reporting;

public class ExcelReporterTests
{
    [Fact]
    public void WriteToFile_FullResult_CreatesAllRequiredWorksheets()
    {
        var result = TestResultBuilder.Build();
        var path = Path.GetTempFileName() + ".xlsx";

        try
        {
            ExcelReporter.WriteToFile(result, path);

            using var workbook = new XLWorkbook(path);
            var sheetNames = workbook.Worksheets.Select(w => w.Name).ToList();

            Assert.Contains("Summary", sheetNames);
            Assert.Contains("File Info", sheetNames);
            Assert.Contains("Encoding", sheetNames);
            Assert.Contains("Spectral", sheetNames);
            Assert.Contains("Loudness", sheetNames);
            Assert.Contains("Dynamics", sheetNames);
            Assert.Contains("Clipping", sheetNames);
            Assert.Contains("Stereo", sheetNames);
            Assert.Contains("Transcoding", sheetNames);
            Assert.Contains("Findings", sheetNames);
            Assert.Contains("Raw Metrics", sheetNames);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void WriteToFile_FullResult_SummarySheetContainsVerdictAndScores()
    {
        var result = TestResultBuilder.Build();
        var path = Path.GetTempFileName() + ".xlsx";

        try
        {
            ExcelReporter.WriteToFile(result, path);

            using var workbook = new XLWorkbook(path);
            var summary = workbook.Worksheet("Summary");
            var cells = summary.CellsUsed().Select(c => c.GetString()).ToList();

            Assert.Contains("GOOD 128 KBPS", cells);
            Assert.Contains("test.mp3", cells);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void WriteToFile_MonoFile_DoesNotThrowAndStereoSheetNotesMonoFile()
    {
        var result = TestResultBuilder.Build(includeStereo: false);
        var path = Path.GetTempFileName() + ".xlsx";

        try
        {
            ExcelReporter.WriteToFile(result, path);

            using var workbook = new XLWorkbook(path);
            var stereo = workbook.Worksheet("Stereo");
            var cells = stereo.CellsUsed().Select(c => c.GetString());

            Assert.Contains(cells, c => c.Contains("Mono file", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void WriteBatchToFile_MultipleTracks_CreatesSummaryAndFindingsSheets()
    {
        var successes = new List<BatchTrackResult>
        {
            new() { RelativePath = "album1/track1.mp3", Result = TestResultBuilder.Build() },
            new() { RelativePath = "album2/track2.mp3", Result = TestResultBuilder.Build() },
        };
        var path = Path.GetTempFileName() + ".xlsx";

        try
        {
            ExcelReporter.WriteBatchToFile(successes, [], path);

            using var workbook = new XLWorkbook(path);
            var sheetNames = workbook.Worksheets.Select(w => w.Name).ToList();
            Assert.Contains("Summary", sheetNames);
            Assert.Contains("Findings", sheetNames);
            Assert.DoesNotContain("Failures", sheetNames);

            var summaryCells = workbook.Worksheet("Summary").CellsUsed().Select(c => c.GetString()).ToList();
            Assert.Contains("album1/track1.mp3", summaryCells);
            Assert.Contains("album2/track2.mp3", summaryCells);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void WriteBatchToFile_WithFailures_AddsFailuresSheet()
    {
        var failures = new List<BatchTrackFailure>
        {
            new() { RelativePath = "corrupt.mp3", ErrorMessage = "No valid MPEG audio frames found." },
        };
        var path = Path.GetTempFileName() + ".xlsx";

        try
        {
            ExcelReporter.WriteBatchToFile([], failures, path);

            using var workbook = new XLWorkbook(path);
            Assert.Contains("Failures", workbook.Worksheets.Select(w => w.Name));

            var cells = workbook.Worksheet("Failures").CellsUsed().Select(c => c.GetString());
            Assert.Contains(cells, c => c.Contains("corrupt.mp3"));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
