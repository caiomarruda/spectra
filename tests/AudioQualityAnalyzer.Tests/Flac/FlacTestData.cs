namespace AudioQualityAnalyzer.Tests.Flac;

/// <summary>Locates the committed FLAC test fixtures from a test's runtime output directory (same approach as TestSupport.ReferenceDataset).</summary>
internal static class FlacTestData
{
    public static string FindPath(string fileName)
    {
        var dir = AppContext.BaseDirectory;
        var relative = Path.Combine("tests", "AudioQualityAnalyzer.Tests", "Flac", "TestData");
        while (dir is not null && !Directory.Exists(Path.Combine(dir, relative)))
        {
            dir = Directory.GetParent(dir)?.FullName;
        }

        if (dir is null)
        {
            throw new DirectoryNotFoundException("Could not locate the Flac/TestData directory by walking up from the test output directory.");
        }

        return Path.Combine(dir, relative, fileName);
    }
}
