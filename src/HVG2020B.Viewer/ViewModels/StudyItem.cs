using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using HVG2020B.Core.Models;

namespace HVG2020B.Viewer.ViewModels;

public partial class StudyItem : ObservableObject, IDisposable
{
    private bool _disposed;
    private int _nextMeasurementNumber = 1;

    /// <summary>Create a brand-new study (no measurements yet)</summary>
    public StudyItem(StudyMetadata metadata, string studyFolderPath)
    {
        Metadata = metadata;
        StudyFolderPath = studyFolderPath;
        IsActive = true;
    }

    /// <summary>Load from a persisted StudyRecord (including legacy)</summary>
    public StudyItem(StudyRecord record, string studyFolderPath)
    {
        Metadata = record.Metadata;
        StudyFolderPath = studyFolderPath;
        IsActive = record.Metadata.Status != "Closed";

        // Legacy compat: synthesize M001 if no measurements but has CsvFilePath
        if (record.Measurements.Count == 0 && !string.IsNullOrEmpty(record.CsvFilePath))
        {
            record.Measurements.Add(CreateSyntheticMeasurement(record));
        }

        foreach (var mRec in record.Measurements)
        {
            var mItem = new MeasurementItem(mRec, studyFolderPath);
            foreach (var ar in mRec.AnalysisResults)
                mItem.AnalysisResults.Add(ar);
            Measurements.Add(mItem);
        }

        _nextMeasurementNumber = record.Measurements.Count + 1;
        IsBroken = Measurements.Any(m => m.IsBroken);
    }

    public StudyMetadata Metadata { get; }

    public string StudyId => Metadata.StudyId;
    public string Title => Metadata.Title;
    public string UserTag => Metadata.UserTag;
    public string StudyFolderPath { get; }

    public string DisplayName => string.IsNullOrEmpty(UserTag)
        ? Title
        : $"{UserTag} / {Title}";

    public string DevicesSummary => string.Join(", ", Metadata.DeviceIds);

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    private bool _isBroken;

    [ObservableProperty]
    private bool _isExpanded;

    public ObservableCollection<MeasurementItem> Measurements { get; } = new();

    public bool HasRecordingMeasurement =>
        Measurements.Any(m => m.State == MeasurementState.Recording);

    /// <summary>Call after a measurement starts or stops recording to refresh UI bindings.</summary>
    public void NotifyRecordingChanged() =>
        OnPropertyChanged(nameof(HasRecordingMeasurement));

    public int TotalSampleCount => Measurements.Sum(m => m.RecordedSampleCount);

    public int DoneMeasurementCount =>
        Measurements.Count(m => m.State == MeasurementState.Done);

    public string MeasurementSummary
    {
        get
        {
            if (Measurements.Count == 0) return "No measurements";
            var recording = Measurements.Count(m => m.State == MeasurementState.Recording);
            var done = DoneMeasurementCount;
            if (recording > 0) return $"{Measurements.Count} measurements ({recording} recording)";
            return $"{Measurements.Count} measurements ({done} done)";
        }
    }

    public MeasurementItem AddMeasurement(string label, List<string> deviceIds)
    {
        if (!IsActive) throw new InvalidOperationException("Cannot add measurement to a Closed study");

        var mId = $"M{_nextMeasurementNumber:D3}";
        _nextMeasurementNumber++;

        var mRecord = new MeasurementRecord
        {
            MeasurementId = mId,
            Label = label,
            DeviceIds = deviceIds,
            State = "Ready"
        };

        var mItem = new MeasurementItem(mRecord, StudyFolderPath);
        Measurements.Add(mItem);
        OnPropertyChanged(nameof(MeasurementSummary));
        return mItem;
    }

    public void CloseStudy()
    {
        foreach (var m in Measurements.Where(m => m.State == MeasurementState.Recording))
            m.StopRecording();

        Metadata.EndTime = DateTimeOffset.Now;
        Metadata.Status = "Closed";
        IsActive = false;
        OnPropertyChanged(nameof(MeasurementSummary));
    }

    public StudyRecord ToRecord()
    {
        foreach (var m in Measurements)
            m.SyncToRecord();

        return new StudyRecord
        {
            Metadata = Metadata,
            Measurements = Measurements.Select(m => m.Record).ToList(),
            StudyState = IsActive ? "Active" : "Closed",
            CsvFilePath = null,
            RecordedSampleCount = TotalSampleCount,
            AnalysisResults = new()
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var m in Measurements)
            m.Dispose();
    }

    private static MeasurementRecord CreateSyntheticMeasurement(StudyRecord record)
    {
        return new MeasurementRecord
        {
            MeasurementId = "M001",
            Label = "(migrated)",
            DeviceIds = new List<string>(record.Metadata.DeviceIds),
            CsvFileName = Path.GetFileName(record.CsvFilePath!),
            StartTime = record.Metadata.StartTime ?? record.Metadata.CreatedAt,
            EndTime = record.Metadata.EndTime,
            RecordedSampleCount = record.RecordedSampleCount,
            State = (record.StudyState == "Done" || record.StudyState == "Closed") ? "Done" : "Ready",
            AnalysisResults = new List<FluxAnalysisResult>(record.AnalysisResults),
            LatestAnalysisId = record.Metadata.LatestAnalysisId
        };
    }
}
