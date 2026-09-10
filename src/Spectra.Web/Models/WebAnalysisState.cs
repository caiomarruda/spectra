namespace Spectra.Web.Models;

/// <summary>Drives which part of <see cref="Pages.Home"/> is shown. Loading/Decoding/Analyzing
/// are cosmetic stage labels around one <see cref="Services.IAudioAnalysisService.AnalyzeAsync"/>
/// call, not real progress — the analysis engine has no incremental progress to report.</summary>
public enum WebAnalysisState
{
    Idle,
    Loading,
    Decoding,
    Analyzing,
    Completed,
    Error,
}
