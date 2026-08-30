namespace AudioQualityAnalyzer.Cli;

public sealed record CliOptions
{
    public required string InputPath { get; init; }
    public bool Html { get; init; }
    public bool Excel { get; init; }
    public bool Json { get; init; }
    public bool Verbose { get; init; }

    public static CliOptions? Parse(string[] args)
    {
        string? inputPath = null;
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

        return inputPath is null
            ? null
            : new CliOptions { InputPath = inputPath, Html = html, Excel = excel, Json = json, Verbose = verbose };
    }
}
