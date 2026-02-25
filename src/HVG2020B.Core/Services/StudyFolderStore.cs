using System.Text.Json;
using System.Text.Json.Serialization;
using HVG2020B.Core.Models;

namespace HVG2020B.Core.Services;

/// <summary>
/// Per-study folder storage. Each study has its own folder with a study.json file.
/// Layout: {logDir}/{StudyId}/study.json
/// </summary>
public class StudyFolderStore
{
    private readonly string _logDir;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public StudyFolderStore(string logDir)
    {
        if (!Directory.Exists(logDir))
            Directory.CreateDirectory(logDir);
        _logDir = logDir;
    }

    /// <summary>
    /// Scans logDir for subdirectories containing study.json, loads all.
    /// Returns studies ordered by CreatedAt descending (newest first).
    /// </summary>
    public List<StudyRecord> LoadAll()
    {
        var results = new List<StudyRecord>();
        if (!Directory.Exists(_logDir)) return results;

        foreach (var dir in Directory.GetDirectories(_logDir))
        {
            var studyJsonPath = Path.Combine(dir, "study.json");
            if (!File.Exists(studyJsonPath)) continue;

            try
            {
                var json = File.ReadAllText(studyJsonPath);
                var record = JsonSerializer.Deserialize<StudyRecord>(json, JsonOptions);
                if (record != null)
                    results.Add(record);
            }
            catch (JsonException)
            {
                var bakPath = studyJsonPath + $".bak.{DateTime.Now:yyyyMMddHHmmss}";
                try { File.Move(studyJsonPath, bakPath); } catch { /* best effort */ }
            }
        }

        return results.OrderByDescending(r => r.Metadata.CreatedAt).ToList();
    }

    /// <summary>
    /// Saves a single study's record to its folder's study.json.
    /// Creates the folder if it doesn't exist.
    /// </summary>
    public void Save(StudyRecord record, string studyFolderPath)
    {
        if (!Directory.Exists(studyFolderPath))
            Directory.CreateDirectory(studyFolderPath);

        var jsonPath = Path.Combine(studyFolderPath, "study.json");
        var json = JsonSerializer.Serialize(record, JsonOptions);
        var tmpPath = jsonPath + ".tmp";
        File.WriteAllText(tmpPath, json);
        File.Move(tmpPath, jsonPath, overwrite: true);
    }

    /// <summary>
    /// Deletes a study's folder entirely.
    /// </summary>
    public void DeleteFolder(string studyFolderPath)
    {
        if (Directory.Exists(studyFolderPath))
            Directory.Delete(studyFolderPath, recursive: true);
    }

    /// <summary>
    /// Returns the default folder path for a study: logs/{StudyId}/
    /// </summary>
    public string GetStudyFolderPath(string studyId)
    {
        return Path.Combine(_logDir, studyId);
    }

    /// <summary>
    /// One-time migration: reads old studies.json, creates per-study folders + study.json,
    /// renames old file to .bak.
    /// Returns the number of migrated studies.
    /// </summary>
    public int MigrateFromLegacy()
    {
        var legacyPath = Path.Combine(_logDir, "studies.json");
        if (!File.Exists(legacyPath)) return 0;

        var legacyStore = new StudyStore(_logDir);
        var oldRecords = legacyStore.LoadAll();
        int migrated = 0;

        foreach (var record in oldRecords)
        {
            // Create synthetic M001 measurement from legacy data
            if (record.Measurements.Count == 0 && !string.IsNullOrEmpty(record.CsvFilePath))
            {
                record.Measurements.Add(CreateSyntheticMeasurement(record));
            }

            // Determine study folder
            string studyFolder;
            if (!string.IsNullOrEmpty(record.CsvFilePath) &&
                Directory.Exists(Path.GetDirectoryName(record.CsvFilePath)))
            {
                studyFolder = Path.GetDirectoryName(record.CsvFilePath)!;
            }
            else
            {
                studyFolder = GetStudyFolderPath(record.Metadata.StudyId);
            }

            if (!Directory.Exists(studyFolder))
                Directory.CreateDirectory(studyFolder);

            // Map old state to new Status
            record.Metadata.Status = record.StudyState == "Ready" ? "Active" : "Closed";
            record.StudyState = record.Metadata.Status;

            // Write study.json
            Save(record, studyFolder);
            migrated++;
        }

        // Rename old studies.json to .bak
        var bakPath = legacyPath + $".migrated.{DateTime.Now:yyyyMMddHHmmss}.bak";
        try { File.Move(legacyPath, bakPath); } catch { /* best effort */ }

        return migrated;
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
