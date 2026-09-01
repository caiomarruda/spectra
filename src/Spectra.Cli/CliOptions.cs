namespace Spectra.Cli;

public sealed record CliOptions
{
    /// <summary>Single-file mode. Mutually exclusive with <see cref="FolderPath"/>; exactly one of the two is set.</summary>
    public string? InputPath { get; init; }

    /// <summary>Folder (batch) mode: recursively analyzes every .mp3 under this path.</summary>
    public string? FolderPath { get; init; }

    /// <summary>Degree of parallelism for --folder mode. Null means "use all available cores".</summary>
    public int? Threads { get; init; }

    public bool Html { get; init; }
    public bool Sheet { get; init; }
    public bool Json { get; init; }
    public bool Verbose { get; init; }

    public static CliOptions? Parse(string[] args)
    {
        string? inputPath = null;
        string? folderPath = null;
        int? threads = null;
        var html = false;
        var sheet = false;
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
                case "--threads":
                    if (i + 1 >= args.Length || !int.TryParse(args[i + 1], out var parsedThreads) || parsedThreads < 1)
                    {
                        return null;
                    }
                    threads = parsedThreads;
                    i++;
                    break;
                case "--html":
                    html = true;
                    break;
                case "--sheet":
                    sheet = true;
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

        if (inputPath is null && folderPath is null)
        {
            return null;
        }

        if (folderPath is not null)
        {
            if (!html && !sheet && !json)
            {
                // --folder mode has no per-track console output at all without one of these —
                // without an export, a scan would produce nothing anyone can look at afterward.
                // Require the caller to say what they want kept.
                return null;
            }

            if (verbose)
            {
                // Per-track verbose detail is written from multiple threads at once in --folder
                // mode, so it interleaves into unreadable console output. Single-file mode has no
                // such issue and always shows the full report, verbose or not.
                return null;
            }
        }

        return new CliOptions { InputPath = inputPath, FolderPath = folderPath, Threads = threads, Html = html, Sheet = sheet, Json = json, Verbose = verbose };
    }
}
