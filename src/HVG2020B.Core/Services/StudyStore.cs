using System.Text.Json;
using System.Text.Json.Serialization;
using HVG2020B.Core.Models;

namespace HVG2020B.Core.Services;

public class StudyStore
{
    private readonly string _jsonFilePath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public StudyStore(string logDir)
    {
        if (!Directory.Exists(logDir))
            Directory.CreateDirectory(logDir);
        _jsonFilePath = Path.Combine(logDir, "studies.json");
    }

    public List<StudyRecord> LoadAll()
    {
        if (!File.Exists(_jsonFilePath))
            return new List<StudyRecord>();

        try
        {
            var json = File.ReadAllText(_jsonFilePath);
            return JsonSerializer.Deserialize<List<StudyRecord>>(json, JsonOptions)
                   ?? new List<StudyRecord>();
        }
        catch (JsonException)
        {
            var bakPath = _jsonFilePath + $".bak.{DateTime.Now:yyyyMMddHHmmss}";
            File.Move(_jsonFilePath, bakPath);
            return new List<StudyRecord>();
        }
    }

    public void SaveAll(IEnumerable<StudyRecord> records)
    {
        var json = JsonSerializer.Serialize(records.ToList(), JsonOptions);
        var tmpPath = _jsonFilePath + ".tmp";
        File.WriteAllText(tmpPath, json);
        File.Move(tmpPath, _jsonFilePath, overwrite: true);
    }
}
