using System.Globalization;
using System.IO;
using ClosedXML.Excel;
using HVG2020B.Core;
using HVG2020B.Core.Models;

namespace HVG2020B.Viewer.Services;

public static class StudyExcelExporter
{
    /// <summary>
    /// Exports study data to an Excel file with two tabs:
    /// Tab 1 = Logging Data (raw CSV data), Tab 2 = Analysis Results.
    /// </summary>
    public static void Export(string excelPath, string csvFilePath,
        IReadOnlyList<FluxAnalysisResult> analysisResults, StudyMetadata metadata)
    {
        using var workbook = new XLWorkbook();

        // Tab 1: Logging Data
        WriteLoggingDataSheet(workbook, csvFilePath, metadata);

        // Tab 2: Analysis Results
        WriteAnalysisSheet(workbook, analysisResults, metadata);

        workbook.SaveAs(excelPath);
    }

    private static void WriteLoggingDataSheet(XLWorkbook workbook, string csvFilePath, StudyMetadata metadata)
    {
        var ws = workbook.Worksheets.Add("Logging Data");

        // Header info
        ws.Cell(1, 1).Value = "Study";
        ws.Cell(1, 2).Value = metadata.Title;
        ws.Cell(2, 1).Value = "Id";
        ws.Cell(2, 2).Value = metadata.MeasurementId;
        ws.Cell(3, 1).Value = "Study ID";
        ws.Cell(3, 2).Value = metadata.StudyId;
        ws.Cell(4, 1).Value = "Start Time";
        ws.Cell(4, 2).Value = metadata.StartTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A";
        ws.Cell(5, 1).Value = "End Time";
        ws.Cell(5, 2).Value = metadata.EndTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A";
        ws.Cell(6, 1).Value = "Devices";
        ws.Cell(6, 2).Value = string.Join(", ", metadata.DeviceIds);

        ws.Range(1, 1, 6, 1).Style.Font.Bold = true;

        // CSV data
        int dataStartRow = 7;
        ws.Cell(dataStartRow, 1).Value = "Timestamp";
        ws.Cell(dataStartRow, 2).Value = "Device ID";
        ws.Cell(dataStartRow, 3).Value = "Pressure (Torr)";
        ws.Range(dataStartRow, 1, dataStartRow, 3).Style.Font.Bold = true;

        if (File.Exists(csvFilePath))
        {
            var lines = File.ReadAllLines(csvFilePath);
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

    private static void WriteAnalysisSheet(XLWorkbook workbook,
        IReadOnlyList<FluxAnalysisResult> results, StudyMetadata metadata)
    {
        var ws = workbook.Worksheets.Add("Analysis Results");

        if (results.Count == 0)
        {
            ws.Cell(1, 1).Value = "No analysis results available";
            return;
        }

        // Headers
        var headers = new[]
        {
            "Analysis #", "Date", "Device", "Membrane Area (m²)", "Temperature (K)",
            "Feed Pressure (Pa)", "Chamber Vol (m³)", "Start Time (s)", "End Time (s)",
            "Flux (mol/m²·s)", "Permeance (mol/m²·Pa·s)", "Permeance (GPU)",
            "Pressure Rate (Pa/s)", "R²", "Data Points"
        };

        for (int col = 0; col < headers.Length; col++)
        {
            ws.Cell(1, col + 1).Value = headers[col];
        }
        ws.Range(1, 1, 1, headers.Length).Style.Font.Bold = true;

        // Data rows
        for (int i = 0; i < results.Count; i++)
        {
            var r = results[i];
            var row = i + 2;
            ws.Cell(row, 1).Value = r.AnalysisId;
            ws.Cell(row, 2).Value = r.CalculatedAt.ToString("yyyy-MM-dd HH:mm:ss");
            ws.Cell(row, 3).Value = r.DeviceId;
            ws.Cell(row, 4).Value = r.MembraneArea;
            ws.Cell(row, 5).Value = r.Temperature;
            ws.Cell(row, 6).Value = r.FeedSidePressure;
            ws.Cell(row, 7).Value = r.ChamberVolume;
            ws.Cell(row, 8).Value = r.StartTime;
            ws.Cell(row, 9).Value = r.EndTime;
            ws.Cell(row, 10).Value = r.Flux;
            ws.Cell(row, 11).Value = r.Permeance;
            ws.Cell(row, 12).Value = r.PermeanceGpu;
            ws.Cell(row, 13).Value = r.PressureChangeRate;
            ws.Cell(row, 14).Value = r.RSquared ?? 0;
            ws.Cell(row, 15).Value = r.DataPointCount;

            // Scientific notation for small values
            ws.Cell(row, 4).Style.NumberFormat.Format = "0.00E+00";
            ws.Cell(row, 7).Style.NumberFormat.Format = "0.00E+00";
            ws.Cell(row, 10).Style.NumberFormat.Format = "0.0000E+00";
            ws.Cell(row, 11).Style.NumberFormat.Format = "0.0000E+00";
            ws.Cell(row, 12).Style.NumberFormat.Format = "0.00";
            ws.Cell(row, 14).Style.NumberFormat.Format = "0.0000";
        }

        ws.Columns().AdjustToContents();
    }
}
