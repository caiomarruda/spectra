namespace AudioQualityAnalyzer.Cli;

public sealed record CliOptions
{
    /// <summary>Single-file mode. Mutually exclusive with <see cref="FolderPath"/>; exactly one of the two is set.</summary>
    public string? InputPath { get; init; }

    /// <summary>Folder (batch) mode: recursively analyzes every .mp3 under this path.</summary>
    public string? FolderPath { get; init; }

    public bool Html { get; init; }
    public bool Excel { get; init; }
    public bool Json { get; init; }
    public bool Verbose { get; init; }

    public static CliOptions? Parse(string[] args)
    {
        string? inputPath = null;
        string? folderPath = null;
        var html = false;
        var excel = false;
        var json = false;
        var verbose = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--input":
                    if (i + 1 >= args.Length)
                    {
                        return null;
                    }
                    inputPath = args[++i];
                    break;
                case "--folder":
                    if (i + 1 >= args.Length)
                    {
                        return null;
                    }
                    folderPath = args[++i];
                    break;
                case "--html":
                    html = true;
                    break;
                case "--excel":
                    excel = true;
                    break;
                case "--json":
                    json = true;
                    break;
                case "--verbose":
                    verbose = true;
                    break;
                default:
                    if (args[i].StartsWith("--", StringComparison.Ordinal))
                    {
                        return null;
                    }
                    inputPath ??= args[i];
                    break;
            }
        }

        if (inputPath is not null && folderPath is not null)
        {
            return null; // Mutually exclusive: a single file or a folder scan, not both.
        }

        return inputPath is null && folderPath is null
            ? null
            : new CliOptions { InputPath = inputPath, FolderPath = folderPath, Html = html, Excel = excel, Json = json, Verbose = verbose };
    }
}
