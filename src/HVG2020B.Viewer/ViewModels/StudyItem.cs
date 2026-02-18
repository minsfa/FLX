using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using HVG2020B.Core;
using HVG2020B.Core.Models;

namespace HVG2020B.Viewer.ViewModels;

public partial class StudyItem : ObservableObject, IDisposable
{
    private StreamWriter? _csvWriter;
    private readonly Dictionary<string, int> _logTickCounters = new();
    private bool _disposed;

    public StudyItem(StudyMetadata metadata)
    {
        Metadata = metadata;
    }

    public StudyItem(StudyRecord record)
    {
        Metadata = record.Metadata;
        CsvFilePath = record.CsvFilePath;
        RecordedSampleCount = record.RecordedSampleCount;
        AnalysisResults = new ObservableCollection<FluxAnalysisResult>(
            record.AnalysisResults);

        // Restore StudyFolderPath from CsvFilePath
        if (!string.IsNullOrEmpty(record.CsvFilePath))
            StudyFolderPath = Path.GetDirectoryName(record.CsvFilePath);

        if (!string.IsNullOrEmpty(record.CsvFilePath) && File.Exists(record.CsvFilePath))
            State = StudyState.Done;
        else if (record.StudyState == "Done")
        {
            State = StudyState.Done;
            IsBroken = true; // CSV path recorded but file missing
        }
        else
            State = StudyState.Ready;
    }

    public StudyMetadata Metadata { get; }

    public string StudyId => Metadata.StudyId;

    public string Title => Metadata.Title;

    public string MeasurementId => Metadata.MeasurementId;

    public string DisplayName => string.IsNullOrEmpty(MeasurementId)
        ? Title
        : $"{MeasurementId} / {Title}";

    public string DevicesSummary => string.Join(", ", Metadata.DeviceIds);

    [ObservableProperty]
    private StudyState _state = StudyState.Ready;

    [ObservableProperty]
    private string? _csvFilePath;

    [ObservableProperty]
    private int _recordedSampleCount;

    [ObservableProperty]
    private bool _isBroken;

    public ObservableCollection<FluxAnalysisResult> AnalysisResults { get; } = new();

    public StudyRecord ToRecord()
    {
        return new StudyRecord
        {
            Metadata = Metadata,
            CsvFilePath = CsvFilePath,
            StudyState = State == StudyState.Recording ? "Done" : State.ToString(),
            RecordedSampleCount = RecordedSampleCount,
            AnalysisResults = AnalysisResults.ToList()
        };
    }

    public void AddAnalysisResult(FluxAnalysisResult result)
    {
        result.AnalysisId = Metadata.LatestAnalysisId + 1;
        Metadata.LatestAnalysisId = result.AnalysisId;
        AnalysisResults.Insert(0, result);
    }

    /// <summary>
    /// The per-study folder path: logs/{Title}/
    /// </summary>
    public string? StudyFolderPath { get; private set; }

    public void StartRecording(string logDir)
    {
        if (State != StudyState.Ready) return;

        // Create per-study folder: logs/{date}_{id}_{study}/ (Gasmon pattern)
        var date = DateTime.Now.ToString("yyyyMMdd");
        var parts = new[] { date, SanitizeFolderName(Metadata.MeasurementId), SanitizeFolderName(Metadata.Title) }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        var folderName = string.Join("_", parts);
        StudyFolderPath = Path.Combine(logDir, folderName);
        if (!Directory.Exists(StudyFolderPath))
            Directory.CreateDirectory(StudyFolderPath);

        CsvFilePath = Path.Combine(StudyFolderPath, $"{StudyId}.csv");
        _csvWriter = new StreamWriter(CsvFilePath, false, System.Text.Encoding.UTF8);
        _csvWriter.WriteLine("timestamp_iso,device_id,pressure_torr");

        Metadata.StartTime = DateTimeOffset.Now;
        RecordedSampleCount = 0;
        _logTickCounters.Clear();
        State = StudyState.Recording;
    }

    public void StopRecording()
    {
        if (State != StudyState.Recording) return;

        _csvWriter?.Flush();
        _csvWriter?.Close();
        _csvWriter?.Dispose();
        _csvWriter = null;

        Metadata.EndTime = DateTimeOffset.Now;
        State = StudyState.Done;
    }

    public bool TryWriteReading(string deviceId, GaugeReading reading, int tickThreshold)
    {
        if (State != StudyState.Recording || _csvWriter == null) return false;
        if (!Metadata.DeviceIds.Contains(deviceId)) return false;

        var counter = _logTickCounters.TryGetValue(deviceId, out var val) ? val + 1 : 1;
        if (counter < tickThreshold)
        {
            _logTickCounters[deviceId] = counter;
            return false;
        }
        _logTickCounters[deviceId] = 0;

        var timestampIso = reading.Timestamp.ToString("O", CultureInfo.InvariantCulture);
        var pressureStr = reading.PressureTorr.ToString("G", CultureInfo.InvariantCulture);
        _csvWriter.WriteLine($"{timestampIso},{deviceId},{pressureStr}");

        RecordedSampleCount++;

        if (RecordedSampleCount % 10 == 0)
            _csvWriter.Flush();

        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _csvWriter?.Flush();
        _csvWriter?.Dispose();
        _csvWriter = null;
    }

    private static string SanitizeFolderName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "Untitled" : sanitized.Trim();
    }
}

public enum StudyState
{
    Ready,
    Recording,
    Done
}
