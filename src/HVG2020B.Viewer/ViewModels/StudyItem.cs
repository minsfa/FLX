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

    public StudyMetadata Metadata { get; }

    public string StudyId => Metadata.StudyId;

    public string Title => Metadata.Title;

    public string DevicesSummary => string.Join(", ", Metadata.DeviceIds);

    [ObservableProperty]
    private StudyState _state = StudyState.Ready;

    [ObservableProperty]
    private string? _csvFilePath;

    [ObservableProperty]
    private int _recordedSampleCount;

    public void StartRecording(string logDir)
    {
        if (State != StudyState.Ready) return;

        if (!Directory.Exists(logDir))
            Directory.CreateDirectory(logDir);

        CsvFilePath = Path.Combine(logDir, $"{StudyId}.csv");
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
}

public enum StudyState
{
    Ready,
    Recording,
    Done
}
