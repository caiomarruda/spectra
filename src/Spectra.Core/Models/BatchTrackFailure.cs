namespace Spectra.Core.Models;

public sealed record BatchTrackFailure
{
    public required string RelativePath { get; init; }
    public required string ErrorMessage { get; init; }
}
