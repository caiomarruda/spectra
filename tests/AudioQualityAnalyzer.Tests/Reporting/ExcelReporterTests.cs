using AudioQualityAnalyzer.Reporting.Excel;
using ClosedXML.Excel;
using Xunit;

namespace AudioQualityAnalyzer.Tests.Reporting;

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
}
