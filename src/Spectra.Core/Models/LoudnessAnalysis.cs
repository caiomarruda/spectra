namespace Spectra.Core.Models;

/// <summary>
/// LUFS values follow ITU-R BS.1770 / EBU R128. A value of <see cref="double.NegativeInfinity"/>
/// for <see cref="IntegratedLufs"/> means the material is silent or entirely below the -70 LUFS
/// absolute gate — report it as such rather than a misleading number.
/// </summary>
public sealed record LoudnessAnalysis
{
    public required double IntegratedLufs { get; init; }
    public required double MomentaryMaxLufs { get; init; }
    public required double ShortTermMaxLufs { get; init; }
    public required double LoudnessRangeLu { get; init; }
    public required double SamplePeakDbfs { get; init; }
    public required double TruePeakDbfs { get; init; }
    public required IReadOnlyList<double> SamplePeakPerChannelDbfs { get; init; }
    public required IReadOnlyList<double> TruePeakPerChannelDbfs { get; init; }
    public required IReadOnlyList<LoudnessTimePoint> LoudnessOverTime { get; init; }
}
