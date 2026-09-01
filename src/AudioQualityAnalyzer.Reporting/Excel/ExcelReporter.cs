using ClosedXML.Excel;
using AudioQualityAnalyzer.Core.Enums;
using AudioQualityAnalyzer.Core.Models;

namespace AudioQualityAnalyzer.Reporting.Excel;

/// <summary>
/// Renders an <see cref="AudioAnalysisResult"/> to .xlsx via ClosedXML — a mature library, not a
/// hand-rolled OOXML writer (the format is exactly the kind of complex, easy-to-get-subtly-wrong
/// binary/zip format the spec's "usar biblioteca madura" principle is aimed at, same reasoning as
/// the MP3 decoder and the FFT).
/// </summary>
public static class ExcelReporter
{
    public static void WriteToFile(AudioAnalysisResult result, string path)
    {
        using var workbook = new XLWorkbook();

        WriteSummary(workbook.Worksheets.Add("Summary"), result);
        WriteFileInfo(workbook.Worksheets.Add("File Info"), result);
        WriteEncoding(workbook.Worksheets.Add("Encoding"), result);
        WriteSpectral(workbook.Worksheets.Add("Spectral"), result);
        WriteLoudness(workbook.Worksheets.Add("Loudness"), result);
        WriteDynamics(workbook.Worksheets.Add("Dynamics"), result);
        WriteClipping(workbook.Worksheets.Add("Clipping"), result);
        WriteStereo(workbook.Worksheets.Add("Stereo"), result);
        WriteTranscoding(workbook.Worksheets.Add("Transcoding"), result);
        WriteFindings(workbook.Worksheets.Add("Findings"), result.OverallAssessment.Findings);
        WriteRawMetrics(workbook.Worksheets.Add("Raw Metrics"), result);

        workbook.SaveAs(path);
    }

    /// <summary>One row per track (not the full 11-sheet detail — impractical for a whole folder scan), plus an aggregated findings sheet.</summary>
    public static void WriteBatchToFile(IReadOnlyList<BatchTrackResult> successes, IReadOnlyList<BatchTrackFailure> failures, string path)
    {
        using var workbook = new XLWorkbook();

        WriteBatchSummary(workbook.Worksheets.Add("Summary"), successes);
        WriteBatchFindings(workbook.Worksheets.Add("Findings"), successes);
        if (failures.Count > 0)
        {
            WriteBatchFailures(workbook.Worksheets.Add("Failures"), failures);
        }

        workbook.SaveAs(path);
    }

    private static void WriteBatchSummary(IXLWorksheet ws, IReadOnlyList<BatchTrackResult> successes)
    {
        var headers = new[]
        {
            "File", "Format", "Bitrate (kbps)", "Sample Rate (Hz)", "Duration", "Overall Score", "Encoding Score",
            "Spectral Score", "Technical Score", "Mastering Score", "Transcoding Probability (%)", "Transcoding Confidence", "Verdict", "Warnings",
        };
        for (var c = 0; c < headers.Length; c++)
        {
            ws.Cell(1, c + 1).Value = headers[c];
        }
        ws.Row(1).Style.Font.Bold = true;

        var row = 2;
        foreach (var track in successes.OrderBy(t => t.Result.OverallAssessment.OverallQualityScore))
        {
            var r = track.Result;
            var a = r.OverallAssessment;
            ws.Cell(row, 1).Value = track.RelativePath;
            ws.Cell(row, 2).Value = r.FormatInfo.Format;
            ws.Cell(row, 3).Value = r.EncodingAnalysis.DeclaredBitrateKbps;
            ws.Cell(row, 4).Value = r.FormatInfo.SampleRateHz;
            ws.Cell(row, 5).Value = r.FileInfo.Duration.ToString(@"hh\:mm\:ss");
            ws.Cell(row, 6).Value = a.OverallQualityScore;
            ws.Cell(row, 7).Value = a.EncodingQualityScore;
            ws.Cell(row, 8).Value = a.SpectralQualityScore;
            ws.Cell(row, 9).Value = a.TechnicalQualityScore;
            ws.Cell(row, 10).Value = a.MasteringQualityScore;
            ws.Cell(row, 11).Value = r.TranscodingAnalysis.Probability;
            ws.Cell(row, 12).Value = r.TranscodingAnalysis.Confidence.ToString();
            ws.Cell(row, 13).Value = a.Verdict;
            ws.Cell(row, 14).Value = string.Join(" | ", r.Warnings);
            row++;
        }
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();
    }

    private static void WriteBatchFindings(IXLWorksheet ws, IReadOnlyList<BatchTrackResult> successes)
    {
        var headers = new[] { "File", "Code", "Title", "Severity", "Confidence", "Description", "Evidence" };
        for (var c = 0; c < headers.Length; c++)
        {
            ws.Cell(1, c + 1).Value = headers[c];
        }
        ws.Row(1).Style.Font.Bold = true;

        var row = 2;
        foreach (var track in successes)
        {
            foreach (var finding in track.Result.OverallAssessment.Findings.Where(f => f.Severity != Severity.Info))
            {
                ws.Cell(row, 1).Value = track.RelativePath;
                ws.Cell(row, 2).Value = finding.Code;
                ws.Cell(row, 3).Value = finding.Title;
                ws.Cell(row, 4).Value = finding.Severity.ToString();
                ws.Cell(row, 5).Value = finding.Confidence.ToString();
                ws.Cell(row, 6).Value = finding.Description;
                ws.Cell(row, 7).Value = string.Join(" | ", finding.Evidence);
                row++;
            }
        }
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();
    }

    private static void WriteBatchFailures(IXLWorksheet ws, IReadOnlyList<BatchTrackFailure> failures)
    {
        ws.Cell(1, 1).Value = "File";
        ws.Cell(1, 2).Value = "Error";
        ws.Row(1).Style.Font.Bold = true;

        var row = 2;
        foreach (var failure in failures)
        {
            ws.Cell(row, 1).Value = failure.RelativePath;
            ws.Cell(row, 2).Value = failure.ErrorMessage;
            row++;
        }
        ws.Columns().AdjustToContents();
    }

    private static void WriteSummary(IXLWorksheet ws, AudioAnalysisResult result)
    {
        var a = result.OverallAssessment;
        var row = Header(ws, "Metric", "Value");
        row = WriteText(ws, row, "Track", result.FileInfo.FileName);
        row = WriteText(ws, row, "Format", result.FormatInfo.Format);
        row = WriteNumber(ws, row, "Bitrate (kbps)", result.EncodingAnalysis.DeclaredBitrateKbps);
        row = WriteNumber(ws, row, "Sample Rate (Hz)", result.FormatInfo.SampleRateHz);
        row = WriteText(ws, row, "Duration", result.FileInfo.Duration.ToString(@"hh\:mm\:ss\.ff"));
        row = WriteNumber(ws, row, "Overall Score", a.OverallQualityScore);
        row = WriteNumber(ws, row, "Encoding Score", a.EncodingQualityScore);
        row = WriteNumber(ws, row, "Spectral Score", a.SpectralQualityScore);
        row = WriteNumber(ws, row, "Technical Score", a.TechnicalQualityScore);
        row = WriteNumber(ws, row, "Mastering Score", a.MasteringQualityScore);
        row = WriteNumber(ws, row, "Transcoding Probability (%)", result.TranscodingAnalysis.Probability);
        row = WriteText(ws, row, "Transcoding Confidence", result.TranscodingAnalysis.Confidence.ToString());
        row = WriteText(ws, row, "Verdict", a.Verdict);
        if (result.Warnings.Count > 0)
        {
            WriteText(ws, row, "Warnings", string.Join(" | ", result.Warnings));
        }
        ws.Columns().AdjustToContents();
    }

    private static void WriteFileInfo(IXLWorksheet ws, AudioAnalysisResult result)
    {
        var row = Header(ws, "Field", "Value");
        row = WriteText(ws, row, "File Name", result.FileInfo.FileName);
        row = WriteText(ws, row, "Full Path", result.FileInfo.FullPath);
        row = WriteText(ws, row, "Extension", result.FileInfo.Extension);
        row = WriteNumber(ws, row, "Size (bytes)", result.FileInfo.SizeInBytes);
        row = WriteText(ws, row, "Duration", result.FileInfo.Duration.ToString(@"hh\:mm\:ss\.ff"));
        if (result.FormatInfo.Format == "MP3")
        {
            row = WriteText(ws, row, "MPEG Version", result.FormatInfo.MpegVersion.ToString());
            row = WriteText(ws, row, "MPEG Layer", result.FormatInfo.MpegLayer.ToString());
        }
        row = WriteNumber(ws, row, "Sample Rate (Hz)", result.FormatInfo.SampleRateHz);
        row = WriteNumber(ws, row, "Channels", result.FormatInfo.Channels);
        row = WriteText(ws, row, "Channel Mode", result.FormatInfo.ChannelMode.ToString());
        if (result.FormatInfo.BitsPerSample is { } bitsPerSample)
        {
            row = WriteNumber(ws, row, "Bit Depth", bitsPerSample);
        }
        row = WriteText(ws, row, "Encoder", result.FormatInfo.Encoder ?? "unknown");
        if (result.FormatInfo.EncoderDelaySamples is { } delay)
        {
            row = WriteNumber(ws, row, "Encoder Delay (samples)", delay);
        }
        if (result.FormatInfo.PaddingSamples is { } padding)
        {
            row = WriteNumber(ws, row, "Encoder Padding (samples)", padding);
        }
        ws.Columns().AdjustToContents();
    }

    private static void WriteEncoding(IXLWorksheet ws, AudioAnalysisResult result)
    {
        var e = result.EncodingAnalysis;
        var row = Header(ws, "Field", "Value");
        row = WriteNumber(ws, row, "Declared Bitrate (kbps)", e.DeclaredBitrateKbps);
        row = WriteNumber(ws, row, "Average Bitrate (kbps)", e.AverageBitrateKbps);
        row = WriteNumber(ws, row, "Minimum Bitrate (kbps)", e.MinimumBitrateKbps);
        row = WriteNumber(ws, row, "Maximum Bitrate (kbps)", e.MaximumBitrateKbps);
        row = WriteText(ws, row, "Bitrate Mode", e.BitrateMode.ToString());
        row = WriteNumber(ws, row, "Frame Count", e.FrameCount);
        if (result.FormatInfo.Format == "MP3")
        {
            row = WriteText(ws, row, "Has Xing Header", e.HasXingHeader.ToString());
            WriteText(ws, row, "Has LAME Tag", e.HasLameTag.ToString());
        }
        ws.Columns().AdjustToContents();
    }

    private static void WriteSpectral(IXLWorksheet ws, AudioAnalysisResult result)
    {
        var s = result.SpectralAnalysis;
        var row = Header(ws, "Field", "Value");
        row = WriteNumber(ws, row, "Spectral Centroid (Hz)", s.SpectralCentroidHz);
        row = WriteNumber(ws, row, "Spectral Bandwidth (Hz)", s.SpectralBandwidthHz);
        row = WriteNumber(ws, row, "Spectral Rolloff 85% (Hz)", s.SpectralRolloffHz);
        row = WriteNumber(ws, row, "Spectral Flatness", s.SpectralFlatness);
        row = WriteNumber(ws, row, "Spectral Flux (avg)", s.SpectralFluxAverage);
        row = WriteNumber(ws, row, "Spectral Contrast (dB)", s.SpectralContrast);
        row = WriteNumber(ws, row, "Effective Bandwidth (Hz)", s.EffectiveBandwidthHz);
        row = WriteText(ws, row, "Bandwidth Confidence", s.BandwidthConfidence.ToString());
        row = WriteNumber(ws, row, "Cutoff Frequency (Hz)", s.CutoffFrequencyHz);
        row = WriteNumber(ws, row, "Cutoff Sharpness (dB/octave)", s.CutoffSharpnessDbPerOctave);
        row = WriteNumber(ws, row, "Cutoff Consistency", s.CutoffConsistency);

        row += 1;
        ws.Cell(row, 1).Value = "Band";
        ws.Cell(row, 2).Value = "Low (Hz)";
        ws.Cell(row, 3).Value = "High (Hz)";
        ws.Cell(row, 4).Value = "Avg Energy (dB)";
        row++;
        foreach (var band in s.BandEnergies)
        {
            ws.Cell(row, 1).Value = band.Label;
            ws.Cell(row, 2).Value = band.LowHz;
            ws.Cell(row, 3).Value = band.HighHz;
            ws.Cell(row, 4).Value = band.AverageEnergyDb;
            row++;
        }
        ws.Columns().AdjustToContents();
    }

    private static void WriteLoudness(IXLWorksheet ws, AudioAnalysisResult result)
    {
        var l = result.LoudnessAnalysis;
        var row = Header(ws, "Field", "Value");
        row = WriteNumber(ws, row, "Integrated Loudness (LUFS)", l.IntegratedLufs);
        row = WriteNumber(ws, row, "Momentary Max (LUFS)", l.MomentaryMaxLufs);
        row = WriteNumber(ws, row, "Short-Term Max (LUFS)", l.ShortTermMaxLufs);
        row = WriteNumber(ws, row, "Loudness Range (LU)", l.LoudnessRangeLu);
        row = WriteNumber(ws, row, "Sample Peak (dBFS)", l.SamplePeakDbfs);
        row = WriteNumber(ws, row, "True Peak (dBTP)", l.TruePeakDbfs);
        for (var c = 0; c < l.SamplePeakPerChannelDbfs.Count; c++)
        {
            row = WriteNumber(ws, row, $"Channel {c} Sample Peak (dBFS)", l.SamplePeakPerChannelDbfs[c]);
            row = WriteNumber(ws, row, $"Channel {c} True Peak (dBTP)", l.TruePeakPerChannelDbfs[c]);
        }
        ws.Columns().AdjustToContents();
    }

    private static void WriteDynamics(IXLWorksheet ws, AudioAnalysisResult result)
    {
        var d = result.DynamicRangeAnalysis;
        var row = Header(ws, "Field", "Value");
        row = WriteNumber(ws, row, "Crest Factor (dB)", d.CrestFactorDb);
        row = WriteNumber(ws, row, "RMS Window Min (dB)", d.RmsWindowMinDb);
        row = WriteNumber(ws, row, "RMS Window Max (dB)", d.RmsWindowMaxDb);
        row = WriteNumber(ws, row, "RMS Window Median (dB)", d.RmsWindowMedianDb);
        row = WriteNumber(ws, row, "RMS Window StdDev (dB)", d.RmsWindowStdDevDb);
        WriteNumber(ws, row, "Samples Near Full Scale (%)", d.PercentSamplesNearFullScale);
        ws.Columns().AdjustToContents();
    }

    private static void WriteClipping(IXLWorksheet ws, AudioAnalysisResult result)
    {
        var c = result.ClippingAnalysis;
        var row = Header(ws, "Field", "Value");
        row = WriteNumber(ws, row, "Total Clipped Samples", c.TotalClippedSamples);
        row = WriteNumber(ws, row, "Clipped Percentage", c.ClippedPercentage);
        row = WriteNumber(ws, row, "Clip Event Count", c.ClipEventCount);
        row = WriteNumber(ws, row, "Longest Clip (ms)", c.LongestClipDuration.TotalMilliseconds);
        row = WriteText(ws, row, "Is Severe", c.IsSevere.ToString());
        for (var i = 0; i < c.ClippedSamplesPerChannel.Count; i++)
        {
            row = WriteNumber(ws, row, $"Channel {i} Clipped Samples", c.ClippedSamplesPerChannel[i]);
        }
        ws.Columns().AdjustToContents();
    }

    private static void WriteStereo(IXLWorksheet ws, AudioAnalysisResult result)
    {
        var row = Header(ws, "Field", "Value");
        if (result.StereoAnalysis is not { } s)
        {
            WriteText(ws, row, "Note", "Mono file — no stereo image to describe.");
            ws.Columns().AdjustToContents();
            return;
        }

        row = WriteNumber(ws, row, "Correlation", s.CorrelationCoefficient);
        row = WriteNumber(ws, row, "Channel Balance (dB)", s.ChannelBalanceDb);
        row = WriteNumber(ws, row, "Mono Compatibility Ratio", s.MonoCompatibilityRatio);
        row = WriteNumber(ws, row, "Mid Energy (dB)", s.MidEnergyDb);
        row = WriteNumber(ws, row, "Side Energy (dB)", s.SideEnergyDb);
        row = WriteNumber(ws, row, "Side-to-Mid Ratio (dB)", s.SideToMidRatioDb);
        row = WriteText(ws, row, "Channel Effectively Missing", s.IsChannelEffectivelyMissing.ToString());
        row = WriteText(ws, row, "Severely Imbalanced", s.IsSeverelyImbalanced.ToString());
        row = WriteText(ws, row, "Mono Disguised As Stereo", s.IsMonoDisguisedAsStereo.ToString());
        row = WriteText(ws, row, "Has Phase Problems", s.HasPhaseProblems.ToString());
        row = WriteText(ws, row, "Has Polarity Inversion", s.HasPolarityInversion.ToString());
        WriteText(ws, row, "Has Excessive Side Content", s.HasExcessiveSideContent.ToString());
        ws.Columns().AdjustToContents();
    }

    private static void WriteTranscoding(IXLWorksheet ws, AudioAnalysisResult result)
    {
        var t = result.TranscodingAnalysis;
        var row = Header(ws, "Field", "Value");
        row = WriteNumber(ws, row, "Probability (%)", t.Probability);
        row = WriteText(ws, row, "Label", t.Label.ToString());
        row = WriteText(ws, row, "Confidence", t.Confidence.ToString());
        row += 1;
        WriteFindingsTable(ws, row, t.Findings);
        ws.Columns().AdjustToContents();
    }

    private static void WriteFindings(IXLWorksheet ws, IReadOnlyList<AnalysisFinding> findings)
    {
        WriteFindingsTable(ws, 1, findings);
        ws.Columns().AdjustToContents();
    }

    private static void WriteFindingsTable(IXLWorksheet ws, int startRow, IReadOnlyList<AnalysisFinding> findings)
    {
        var row = startRow;
        ws.Cell(row, 1).Value = "Code";
        ws.Cell(row, 2).Value = "Title";
        ws.Cell(row, 3).Value = "Severity";
        ws.Cell(row, 4).Value = "Confidence";
        ws.Cell(row, 5).Value = "Description";
        ws.Cell(row, 6).Value = "Evidence";
        row++;

        foreach (var finding in findings)
        {
            ws.Cell(row, 1).Value = finding.Code;
            ws.Cell(row, 2).Value = finding.Title;
            ws.Cell(row, 3).Value = finding.Severity.ToString();
            ws.Cell(row, 4).Value = finding.Confidence.ToString();
            ws.Cell(row, 5).Value = finding.Description;
            ws.Cell(row, 6).Value = string.Join(" | ", finding.Evidence);
            row++;
        }
    }

    private static void WriteRawMetrics(IXLWorksheet ws, AudioAnalysisResult result)
    {
        var row = Header(ws, "Metric", "Value");
        var w = result.WaveformAnalysis;
        row = WriteNumber(ws, row, "Waveform.PeakAmplitude", w.PeakAmplitude);
        row = WriteNumber(ws, row, "Waveform.RmsAmplitude", w.RmsAmplitude);
        row = WriteNumber(ws, row, "Waveform.MinSample", w.MinSample);
        row = WriteNumber(ws, row, "Waveform.MaxSample", w.MaxSample);
        row = WriteNumber(ws, row, "Waveform.LeadingSilenceSeconds", w.LeadingSilence.TotalSeconds);
        row = WriteNumber(ws, row, "Waveform.TrailingSilenceSeconds", w.TrailingSilence.TotalSeconds);
        foreach (var channel in w.PerChannel)
        {
            row = WriteNumber(ws, row, $"Waveform.Channel{channel.ChannelIndex}.Peak", channel.Peak);
            row = WriteNumber(ws, row, $"Waveform.Channel{channel.ChannelIndex}.Rms", channel.Rms);
        }

        var s = result.SpectralAnalysis;
        row = WriteNumber(ws, row, "Spectral.CentroidHz", s.SpectralCentroidHz);
        row = WriteNumber(ws, row, "Spectral.BandwidthHz", s.SpectralBandwidthHz);
        row = WriteNumber(ws, row, "Spectral.RolloffHz", s.SpectralRolloffHz);
        row = WriteNumber(ws, row, "Spectral.Flatness", s.SpectralFlatness);
        row = WriteNumber(ws, row, "Spectral.FluxAverage", s.SpectralFluxAverage);
        row = WriteNumber(ws, row, "Spectral.Contrast", s.SpectralContrast);
        row = WriteNumber(ws, row, "Spectral.EffectiveBandwidthHz", s.EffectiveBandwidthHz);
        row = WriteNumber(ws, row, "Spectral.CutoffFrequencyHz", s.CutoffFrequencyHz);
        row = WriteNumber(ws, row, "Spectral.CutoffSharpnessDbPerOctave", s.CutoffSharpnessDbPerOctave);
        row = WriteNumber(ws, row, "Spectral.CutoffConsistency", s.CutoffConsistency);

        var l = result.LoudnessAnalysis;
        row = WriteNumber(ws, row, "Loudness.IntegratedLufs", l.IntegratedLufs);
        row = WriteNumber(ws, row, "Loudness.MomentaryMaxLufs", l.MomentaryMaxLufs);
        row = WriteNumber(ws, row, "Loudness.ShortTermMaxLufs", l.ShortTermMaxLufs);
        row = WriteNumber(ws, row, "Loudness.LoudnessRangeLu", l.LoudnessRangeLu);
        row = WriteNumber(ws, row, "Loudness.SamplePeakDbfs", l.SamplePeakDbfs);
        row = WriteNumber(ws, row, "Loudness.TruePeakDbfs", l.TruePeakDbfs);

        var d = result.DynamicRangeAnalysis;
        row = WriteNumber(ws, row, "Dynamics.CrestFactorDb", d.CrestFactorDb);
        row = WriteNumber(ws, row, "Dynamics.RmsWindowMinDb", d.RmsWindowMinDb);
        row = WriteNumber(ws, row, "Dynamics.RmsWindowMaxDb", d.RmsWindowMaxDb);
        row = WriteNumber(ws, row, "Dynamics.RmsWindowMedianDb", d.RmsWindowMedianDb);
        row = WriteNumber(ws, row, "Dynamics.RmsWindowStdDevDb", d.RmsWindowStdDevDb);
        row = WriteNumber(ws, row, "Dynamics.PercentSamplesNearFullScale", d.PercentSamplesNearFullScale);

        var c = result.ClippingAnalysis;
        row = WriteNumber(ws, row, "Clipping.TotalClippedSamples", c.TotalClippedSamples);
        row = WriteNumber(ws, row, "Clipping.ClippedPercentage", c.ClippedPercentage);
        row = WriteNumber(ws, row, "Clipping.ClipEventCount", c.ClipEventCount);
        row = WriteNumber(ws, row, "Clipping.LongestClipMs", c.LongestClipDuration.TotalMilliseconds);

        if (result.StereoAnalysis is { } st)
        {
            row = WriteNumber(ws, row, "Stereo.Correlation", st.CorrelationCoefficient);
            row = WriteNumber(ws, row, "Stereo.ChannelBalanceDb", st.ChannelBalanceDb);
            row = WriteNumber(ws, row, "Stereo.MonoCompatibilityRatio", st.MonoCompatibilityRatio);
            row = WriteNumber(ws, row, "Stereo.MidEnergyDb", st.MidEnergyDb);
            row = WriteNumber(ws, row, "Stereo.SideEnergyDb", st.SideEnergyDb);
        }

        var n = result.NoiseAnalysis;
        row = WriteNumber(ws, row, "Noise.NoiseFloorDb", n.NoiseFloorDb);
        for (var i = 0; i < n.DcOffsetPerChannel.Count; i++)
        {
            row = WriteNumber(ws, row, $"Noise.Channel{i}.DcOffset", n.DcOffsetPerChannel[i]);
        }

        var t = result.TranscodingAnalysis;
        row = WriteNumber(ws, row, "Transcoding.Probability", t.Probability);

        var a = result.OverallAssessment;
        row = WriteNumber(ws, row, "Assessment.EncodingQualityScore", a.EncodingQualityScore);
        row = WriteNumber(ws, row, "Assessment.SpectralQualityScore", a.SpectralQualityScore);
        row = WriteNumber(ws, row, "Assessment.TechnicalQualityScore", a.TechnicalQualityScore);
        row = WriteNumber(ws, row, "Assessment.MasteringQualityScore", a.MasteringQualityScore);
        WriteNumber(ws, row, "Assessment.OverallQualityScore", a.OverallQualityScore);

        ws.Columns().AdjustToContents();
    }

    private static int Header(IXLWorksheet ws, string key, string value)
    {
        ws.Cell(1, 1).Value = key;
        ws.Cell(1, 2).Value = value;
        ws.Row(1).Style.Font.Bold = true;
        return 2;
    }

    private static int WriteText(IXLWorksheet ws, int row, string key, string value)
    {
        ws.Cell(row, 1).Value = key;
        ws.Cell(row, 2).Value = value;
        return row + 1;
    }

    private static int WriteNumber(IXLWorksheet ws, int row, string key, double value)
    {
        ws.Cell(row, 1).Value = key;
        if (double.IsFinite(value))
        {
            ws.Cell(row, 2).Value = value;
        }
        else
        {
            ws.Cell(row, 2).Value = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        return row + 1;
    }
}
