using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using HVG2020B.Core;
using HVG2020B.Core.Models;

namespace HVG2020B.Viewer.ViewModels;

public partial class MeasurementItem : ObservableObject, IDisposable
{
    private StreamWriter? _csvWriter;
    private readonly Dictionary<string, int> _logTickCounters = new();
    private bool _disposed;
    private readonly string _studyFolderPath;

    public MeasurementItem(MeasurementRecord record, string studyFolderPath)
    {
        Record = record;
        _studyFolderPath = studyFolderPath;
        RecordedSampleCount = record.RecordedSampleCount;

        State = Enum.TryParse<MeasurementState>(record.State, out var s)
            ? s : MeasurementState.Ready;

        if (State == MeasurementState.Done && !string.IsNullOrEmpty(record.CsvFileName))
        {
            var fullPath = Path.Combine(studyFolderPath, record.CsvFileName);
            if (!File.Exists(fullPath))
                IsBroken = true;
        }
    }

    public MeasurementRecord Record { get; }

    public string MeasurementId => Record.MeasurementId;
    public string Label => Record.Label;
    public string DevicesSummary => string.Join(", ", Record.DeviceIds);

    public string? CsvFilePath => string.IsNullOrEmpty(Record.CsvFileName)
        ? null
        : Path.Combine(_studyFolderPath, Record.CsvFileName);

    [ObservableProperty]
    private MeasurementState _state = MeasurementState.Ready;

    [ObservableProperty]
    private int _recordedSampleCount;

    [ObservableProperty]
    private bool _isBroken;

    public ObservableCollection<FluxAnalysisResult> AnalysisResults { get; } = new();

    public void StartRecording()
    {
        if (State != MeasurementState.Ready) return;

        if (!Directory.Exists(_studyFolderPath))
            Directory.CreateDirectory(_studyFolderPath);

        Record.CsvFileName = $"{MeasurementId}.csv";
        var csvPath = Path.Combine(_studyFolderPath, Record.CsvFileName);
        _csvWriter = new StreamWriter(csvPath, false, Encoding.UTF8);
        _csvWriter.WriteLine("timestamp_iso,device_id,pressure_torr");

        Record.StartTime = DateTimeOffset.Now;
        RecordedSampleCount = 0;
        _logTickCounters.Clear();
        State = MeasurementState.Recording;
    }

    public void StopRecording()
    {
        if (State != MeasurementState.Recording) return;

        _csvWriter?.Flush();
        _csvWriter?.Close();
        _csvWriter?.Dispose();
        _csvWriter = null;

        Record.EndTime = DateTimeOffset.Now;
        Record.RecordedSampleCount = RecordedSampleCount;
        Record.State = "Done";
        State = MeasurementState.Done;
    }

    public bool TryWriteReading(string deviceId, GaugeReading reading, int tickThreshold)
    {
        if (State != MeasurementState.Recording || _csvWriter == null) return false;
        if (!Record.DeviceIds.Contains(deviceId)) return false;

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

    public void AddAnalysisResult(FluxAnalysisResult result)
    {
        result.AnalysisId = Record.LatestAnalysisId + 1;
        Record.LatestAnalysisId = result.AnalysisId;
        result.MeasurementRecordId = MeasurementId;
        AnalysisResults.Insert(0, result);
    }

    public void SyncToRecord()
    {
        Record.State = State == MeasurementState.Recording ? "Done" : State.ToString();
        Record.RecordedSampleCount = RecordedSampleCount;
        Record.AnalysisResults = AnalysisResults.ToList();
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
