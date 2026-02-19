using System.Globalization;

namespace HVG2020B.Core;

/// <summary>
/// Parses study CSV files produced by StudyItem recording.
/// CSV format: timestamp_iso,device_id,pressure_torr
/// </summary>
public static class StudyCsvParser
{
    public record struct CsvRow(DateTimeOffset Timestamp, string DeviceId, double PressureTorr);

    public record ParsedStudyData(List<string> DeviceIds, Dictionary<string, List<CsvRow>> RowsByDevice);

    /// <summary>
    /// Parses a study CSV and groups rows by device.
    /// </summary>
    public static ParsedStudyData Parse(string csvFilePath)
    {
        var rowsByDevice = new Dictionary<string, List<CsvRow>>();
        var deviceOrder = new List<string>();

        foreach (var line in File.ReadLines(csvFilePath).Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = line.Split(',', 3);
            if (parts.Length < 3) continue;

            if (!DateTimeOffset.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp))
                continue;
            if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var pressure))
                continue;

            var deviceId = parts[1];

            if (!rowsByDevice.TryGetValue(deviceId, out var list))
            {
                list = new List<CsvRow>();
                rowsByDevice[deviceId] = list;
                deviceOrder.Add(deviceId);
            }

            list.Add(new CsvRow(timestamp, deviceId, pressure));
        }

        return new ParsedStudyData(deviceOrder, rowsByDevice);
    }

    /// <summary>
    /// Extracts time (seconds from start) and pressure (Torr) arrays for a single device.
    /// </summary>
    public static (double[] TimeSeconds, double[] PressureTorr) ExtractDeviceData(List<CsvRow> rows)
    {
        if (rows.Count == 0)
            return (Array.Empty<double>(), Array.Empty<double>());

        var sorted = rows.OrderBy(r => r.Timestamp).ToList();
        var startTime = sorted[0].Timestamp;

        var timeSeconds = new double[sorted.Count];
        var pressureTorr = new double[sorted.Count];

        for (var i = 0; i < sorted.Count; i++)
        {
            timeSeconds[i] = (sorted[i].Timestamp - startTime).TotalSeconds;
            pressureTorr[i] = sorted[i].PressureTorr;
        }

        return (timeSeconds, pressureTorr);
    }
}
