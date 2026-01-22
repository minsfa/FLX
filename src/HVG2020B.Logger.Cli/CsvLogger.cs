using System.Globalization;
using HVG2020B.Driver;

namespace HVG2020B.Logger.Cli;

/// <summary>
/// Handles CSV file logging for pressure readings.
/// </summary>
public sealed class CsvLogger : IDisposable
{
    private const string Header = "timestamp_iso,pressure_torr";

    private readonly StreamWriter _writer;
    private readonly string _filePath;
    private bool _disposed;

    public string FilePath => _filePath;

    private CsvLogger(StreamWriter writer, string filePath)
    {
        _writer = writer;
        _filePath = filePath;
    }

    /// <summary>
    /// Creates a new CSV logger, creating the output directory if needed.
    /// </summary>
    public static CsvLogger Create(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var writer = new StreamWriter(filePath, append: false, encoding: System.Text.Encoding.UTF8)
        {
            AutoFlush = false // We'll flush manually for better control
        };

        // Write header
        writer.WriteLine(Header);
        writer.Flush();

        return new CsvLogger(writer, filePath);
    }

    /// <summary>
    /// Logs a pressure reading.
    /// </summary>
    public void Log(DateTimeOffset timestamp, PressureReading reading)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var timestampIso = timestamp.ToString("O", CultureInfo.InvariantCulture);
        var pressureStr = reading.PressureTorr.ToString("G", CultureInfo.InvariantCulture);

        _writer.WriteLine($"{timestampIso},{pressureStr}");
    }

    /// <summary>
    /// Flushes pending writes to disk.
    /// </summary>
    public void Flush()
    {
        if (!_disposed)
        {
            _writer.Flush();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            _writer.Flush();
            _writer.Dispose();
        }
        catch
        {
            // Ignore dispose errors
        }
    }
}
