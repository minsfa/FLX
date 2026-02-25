using System.Globalization;
using System.IO;
using ClosedXML.Excel;
using HVG2020B.Core.Models;
using HVG2020B.Viewer.ViewModels;

namespace HVG2020B.Viewer.Services;

public static class StudyExcelExporter
{
    /// <summary>
    /// Exports study data to an Excel file with tabs:
    /// Tab 1 = Summary (study info + measurements table)
    /// Tab 2..N = Per-measurement logging data
    /// Tab N+1 = Permeation Analysis Results (if any)
    /// Tab N+2 = Leak Rate Results (if any)
    /// </summary>
    public static void Export(string excelPath, StudyItem study, Core.Services.StudyFolderStore store)
    {
        using var workbook = new XLWorkbook();

        // Tab 1: Summary
        WriteSummarySheet(workbook, study);

        // Per-measurement logging data tabs
        foreach (var m in study.Measurements)
        {
            if (!string.IsNullOrEmpty(m.CsvFilePath) && File.Exists(m.CsvFilePath))
            {
                var sheetName = TruncateSheetName($"{m.MeasurementId} Data");
                WriteLoggingDataSheet(workbook, sheetName, m);
            }
        }

        // All analysis results across measurements
        var allResults = study.Measurements
            .SelectMany(m => m.AnalysisResults)
            .OrderByDescending(r => r.CalculatedAt)
            .ToList();

        var permeationResults = allResults
            .Where(r => r.Mode == CalculationMode.Permeation).ToList();
        if (permeationResults.Count > 0)
            WritePermeationSheet(workbook, permeationResults);

        var leakRateResults = allResults
            .Where(r => r.Mode == CalculationMode.LeakRate).ToList();
        if (leakRateResults.Count > 0)
            WriteLeakRateSheet(workbook, leakRateResults);

        // Fallback: if no results at all, still write an empty analysis sheet
        if (permeationResults.Count == 0 && leakRateResults.Count == 0)
            WritePermeationSheet(workbook, permeationResults);

        workbook.SaveAs(excelPath);
    }

    private static void WriteSummarySheet(XLWorkbook workbook, StudyItem study)
    {
        var ws = workbook.Worksheets.Add("Summary");
        var metadata = study.Metadata;

        ws.Cell(1, 1).Value = "Study";
        ws.Cell(1, 2).Value = metadata.Title;
        ws.Cell(2, 1).Value = "Tag";
        ws.Cell(2, 2).Value = metadata.UserTag;
        ws.Cell(3, 1).Value = "Study ID";
        ws.Cell(3, 2).Value = metadata.StudyId;
        ws.Cell(4, 1).Value = "Status";
        ws.Cell(4, 2).Value = metadata.Status;
        ws.Cell(5, 1).Value = "Created";
        ws.Cell(5, 2).Value = metadata.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
        ws.Cell(6, 1).Value = "End Time";
        ws.Cell(6, 2).Value = metadata.EndTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A";
        ws.Cell(7, 1).Value = "Devices";
        ws.Cell(7, 2).Value = string.Join(", ", metadata.DeviceIds);

        ws.Range(1, 1, 7, 1).Style.Font.Bold = true;

        // Measurements table
        int tableStart = 9;
        ws.Cell(tableStart, 1).Value = "Measurements";
        ws.Cell(tableStart, 1).Style.Font.Bold = true;

        var mHeaders = new[] { "ID", "Label", "Devices", "State", "Samples", "Start", "End" };
        for (int col = 0; col < mHeaders.Length; col++)
            ws.Cell(tableStart + 1, col + 1).Value = mHeaders[col];
        ws.Range(tableStart + 1, 1, tableStart + 1, mHeaders.Length).Style.Font.Bold = true;

        for (int i = 0; i < study.Measurements.Count; i++)
        {
            var m = study.Measurements[i];
            var row = tableStart + 2 + i;
            ws.Cell(row, 1).Value = m.MeasurementId;
            ws.Cell(row, 2).Value = m.Label;
            ws.Cell(row, 3).Value = m.DevicesSummary;
            ws.Cell(row, 4).Value = m.State.ToString();
            ws.Cell(row, 5).Value = m.RecordedSampleCount;
            ws.Cell(row, 6).Value = m.Record.StartTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";
            ws.Cell(row, 7).Value = m.Record.EndTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";
        }

        ws.Columns().AdjustToContents();
    }

    private static void WriteLoggingDataSheet(XLWorkbook workbook, string sheetName, MeasurementItem measurement)
    {
        var ws = workbook.Worksheets.Add(sheetName);

        // Measurement header info
        ws.Cell(1, 1).Value = "Measurement";
        ws.Cell(1, 2).Value = measurement.MeasurementId;
        ws.Cell(2, 1).Value = "Label";
        ws.Cell(2, 2).Value = measurement.Label;
        ws.Cell(3, 1).Value = "Devices";
        ws.Cell(3, 2).Value = measurement.DevicesSummary;
        ws.Cell(4, 1).Value = "Samples";
        ws.Cell(4, 2).Value = measurement.RecordedSampleCount;

        ws.Range(1, 1, 4, 1).Style.Font.Bold = true;

        // CSV data
        int dataStartRow = 6;
        ws.Cell(dataStartRow, 1).Value = "Timestamp";
        ws.Cell(dataStartRow, 2).Value = "Device ID";
        ws.Cell(dataStartRow, 3).Value = "Pressure (Torr)";
        ws.Range(dataStartRow, 1, dataStartRow, 3).Style.Font.Bold = true;

        var csvPath = measurement.CsvFilePath;
        if (csvPath != null && File.Exists(csvPath))
        {
            var lines = File.ReadAllLines(csvPath);
            for (int i = 1; i < lines.Length; i++) // Skip header
            {
                var parts = lines[i].Split(',');
                if (parts.Length >= 3)
                {
                    var row = dataStartRow + i;
                    ws.Cell(row, 1).Value = parts[0];
                    ws.Cell(row, 2).Value = parts[1];
                    if (double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var pressure))
                        ws.Cell(row, 3).Value = pressure;
                    else
                        ws.Cell(row, 3).Value = parts[2];
                }
            }
        }

        ws.Columns().AdjustToContents();
    }

    private static void WritePermeationSheet(XLWorkbook workbook,
        IReadOnlyList<FluxAnalysisResult> results)
    {
        var ws = workbook.Worksheets.Add("Analysis Results");

        if (results.Count == 0)
        {
            ws.Cell(1, 1).Value = "No analysis results available";
            return;
        }

        var headers = new[]
        {
            "Analysis #", "Measurement", "Date", "Device", "Membrane Area (m²)", "Temperature (K)",
            "Feed Pressure (Pa)", "Chamber Vol (m³)", "Start Time (s)", "End Time (s)",
            "Flux (mol/m²·s)", "Permeance (mol/m²·Pa·s)", "Permeance (GPU)",
            "Pressure Rate (Pa/s)", "R²", "Data Points"
        };

        for (int col = 0; col < headers.Length; col++)
            ws.Cell(1, col + 1).Value = headers[col];
        ws.Range(1, 1, 1, headers.Length).Style.Font.Bold = true;

        for (int i = 0; i < results.Count; i++)
        {
            var r = results[i];
            var row = i + 2;
            ws.Cell(row, 1).Value = r.AnalysisId;
            ws.Cell(row, 2).Value = r.MeasurementRecordId ?? "";
            ws.Cell(row, 3).Value = r.CalculatedAt.ToString("yyyy-MM-dd HH:mm:ss");
            ws.Cell(row, 4).Value = r.DeviceId;
            ws.Cell(row, 5).Value = r.MembraneArea;
            ws.Cell(row, 6).Value = r.Temperature;
            ws.Cell(row, 7).Value = r.FeedSidePressure;
            ws.Cell(row, 8).Value = r.ChamberVolume;
            ws.Cell(row, 9).Value = r.StartTime;
            ws.Cell(row, 10).Value = r.EndTime;
            ws.Cell(row, 11).Value = r.Flux;
            ws.Cell(row, 12).Value = r.Permeance;
            ws.Cell(row, 13).Value = r.PermeanceGpu;
            ws.Cell(row, 14).Value = r.PressureChangeRate;
            ws.Cell(row, 15).Value = r.RSquared ?? 0;
            ws.Cell(row, 16).Value = r.DataPointCount;

            ws.Cell(row, 5).Style.NumberFormat.Format = "0.00E+00";
            ws.Cell(row, 8).Style.NumberFormat.Format = "0.00E+00";
            ws.Cell(row, 11).Style.NumberFormat.Format = "0.0000E+00";
            ws.Cell(row, 12).Style.NumberFormat.Format = "0.0000E+00";
            ws.Cell(row, 13).Style.NumberFormat.Format = "0.00";
            ws.Cell(row, 15).Style.NumberFormat.Format = "0.0000";
        }

        ws.Columns().AdjustToContents();
    }

    private static void WriteLeakRateSheet(XLWorkbook workbook,
        IReadOnlyList<FluxAnalysisResult> results)
    {
        var ws = workbook.Worksheets.Add("Leak Rate Results");

        var headers = new[]
        {
            "Analysis #", "Measurement", "Date", "Device", "Config",
            "Chamber Vol (m³)", "Start Time (s)", "End Time (s)",
            "Q (Torr·L/s)", "Q (Pa·m³/s)", "Q (mbar·L/s)",
            "Pressure Rate (Pa/s)", "R²", "Data Points"
        };

        for (int col = 0; col < headers.Length; col++)
            ws.Cell(1, col + 1).Value = headers[col];
        ws.Range(1, 1, 1, headers.Length).Style.Font.Bold = true;

        for (int i = 0; i < results.Count; i++)
        {
            var r = results[i];
            var row = i + 2;
            ws.Cell(row, 1).Value = r.AnalysisId;
            ws.Cell(row, 2).Value = r.MeasurementRecordId ?? "";
            ws.Cell(row, 3).Value = r.CalculatedAt.ToString("yyyy-MM-dd HH:mm:ss");
            ws.Cell(row, 4).Value = r.DeviceId;
            ws.Cell(row, 5).Value = r.ConfigMemo;
            ws.Cell(row, 6).Value = r.ChamberVolume;
            ws.Cell(row, 7).Value = r.StartTime;
            ws.Cell(row, 8).Value = r.EndTime;
            ws.Cell(row, 9).Value = r.LeakRateTorrLps;
            ws.Cell(row, 10).Value = r.LeakRatePaM3ps;
            ws.Cell(row, 11).Value = r.LeakRateMbarLps;
            ws.Cell(row, 12).Value = r.PressureChangeRate;
            ws.Cell(row, 13).Value = r.RSquared ?? 0;
            ws.Cell(row, 14).Value = r.DataPointCount;

            ws.Cell(row, 6).Style.NumberFormat.Format = "0.00E+00";
            ws.Cell(row, 9).Style.NumberFormat.Format = "0.00E+00";
            ws.Cell(row, 10).Style.NumberFormat.Format = "0.00E+00";
            ws.Cell(row, 11).Style.NumberFormat.Format = "0.00E+00";
            ws.Cell(row, 12).Style.NumberFormat.Format = "0.00E+00";
            ws.Cell(row, 13).Style.NumberFormat.Format = "0.0000";
        }

        ws.Columns().AdjustToContents();
    }

    private static string TruncateSheetName(string name)
    {
        // Excel sheet names max 31 characters
        return name.Length <= 31 ? name : name[..31];
    }
}
