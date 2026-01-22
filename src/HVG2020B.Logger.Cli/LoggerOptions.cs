using HVG2020B.Driver;

namespace HVG2020B.Logger.Cli;

/// <summary>
/// Command line options for the HVG logger.
/// </summary>
public sealed class LoggerOptions
{
    /// <summary>
    /// COM port name (e.g., "COM3"). Required.
    /// </summary>
    public string Port { get; set; } = string.Empty;

    /// <summary>
    /// Polling interval in milliseconds. Default: 500.
    /// </summary>
    public int IntervalMs { get; set; } = 500;

    /// <summary>
    /// Output CSV file path. Default: ./logs/hvg_2020b_{timestamp}.csv
    /// </summary>
    public string? OutputPath { get; set; }

    /// <summary>
    /// Connection mode: usb or rs232. Default: usb.
    /// </summary>
    public HVGConnectionMode Mode { get; set; } = HVGConnectionMode.Usb;

    /// <summary>
    /// Baud rate for RS232 mode. Default: 19200.
    /// </summary>
    public int BaudRate { get; set; } = 19200;

    /// <summary>
    /// Read timeout in milliseconds. Default: 1500.
    /// </summary>
    public int TimeoutMs { get; set; } = 1500;

    /// <summary>
    /// Whether to automatically reconnect on IO errors.
    /// </summary>
    public bool Reconnect { get; set; } = false;

    /// <summary>
    /// Gets the effective output path, generating default if not specified.
    /// </summary>
    public string GetEffectiveOutputPath()
    {
        if (!string.IsNullOrWhiteSpace(OutputPath))
            return OutputPath;

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        return Path.Combine("logs", $"hvg_2020b_{timestamp}.csv");
    }

    /// <summary>
    /// Creates HVGSerialSettings from these options.
    /// </summary>
    public HVGSerialSettings ToSerialSettings() => new()
    {
        Mode = Mode,
        BaudRate = BaudRate,
        ReadTimeoutMs = TimeoutMs
    };

    /// <summary>
    /// Parses command line arguments.
    /// </summary>
    public static LoggerOptions Parse(string[] args)
    {
        var options = new LoggerOptions();

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i].ToLowerInvariant();

            switch (arg)
            {
                case "--port":
                    options.Port = GetNextArg(args, ref i, "--port");
                    break;

                case "--interval-ms":
                    options.IntervalMs = ParseInt(GetNextArg(args, ref i, "--interval-ms"), "--interval-ms");
                    break;

                case "--out":
                    options.OutputPath = GetNextArg(args, ref i, "--out");
                    break;

                case "--mode":
                    var modeStr = GetNextArg(args, ref i, "--mode").ToLowerInvariant();
                    options.Mode = modeStr switch
                    {
                        "usb" => HVGConnectionMode.Usb,
                        "rs232" => HVGConnectionMode.Rs232,
                        _ => throw new ArgumentException($"Invalid mode: '{modeStr}'. Use 'usb' or 'rs232'.")
                    };
                    break;

                case "--baud":
                    options.BaudRate = ParseInt(GetNextArg(args, ref i, "--baud"), "--baud");
                    break;

                case "--timeout-ms":
                    options.TimeoutMs = ParseInt(GetNextArg(args, ref i, "--timeout-ms"), "--timeout-ms");
                    break;

                case "--reconnect":
                    options.Reconnect = true;
                    break;

                case "--help":
                case "-h":
                    PrintUsage();
                    Environment.Exit(0);
                    break;

                case "--list-ports":
                    ListPorts();
                    Environment.Exit(0);
                    break;

                default:
                    throw new ArgumentException($"Unknown argument: '{args[i]}'");
            }
        }

        return options;
    }

    /// <summary>
    /// Validates options and throws if invalid.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Port))
            throw new ArgumentException("--port is required. Use --list-ports to see available ports.");

        if (IntervalMs < 10)
            throw new ArgumentException("--interval-ms must be at least 10.");

        if (TimeoutMs < 100)
            throw new ArgumentException("--timeout-ms must be at least 100.");

        if (BaudRate <= 0)
            throw new ArgumentException("--baud must be positive.");
    }

    private static string GetNextArg(string[] args, ref int index, string argName)
    {
        if (index + 1 >= args.Length)
            throw new ArgumentException($"{argName} requires a value.");
        return args[++index];
    }

    private static int ParseInt(string value, string argName)
    {
        if (!int.TryParse(value, out var result))
            throw new ArgumentException($"{argName} must be an integer.");
        return result;
    }

    private static void ListPorts()
    {
        var ports = HVG2020BClient.GetAvailablePorts();
        if (ports.Length == 0)
        {
            Console.WriteLine("No COM ports found.");
        }
        else
        {
            Console.WriteLine("Available COM ports:");
            foreach (var port in ports.OrderBy(p => p))
            {
                Console.WriteLine($"  {port}");
            }
        }
    }

    public static void PrintUsage()
    {
        Console.WriteLine("""
            HVG-2020B Logger - Minimal vacuum gauge data logger

            Usage:
              hvglogger --port <COM_PORT> [options]

            Required:
              --port <name>       COM port name (e.g., COM3)

            Options:
              --interval-ms <ms>  Polling interval (default: 500)
              --out <path>        Output CSV path (default: ./logs/hvg_2020b_<timestamp>.csv)
              --mode <usb|rs232>  Connection mode (default: usb)
              --baud <rate>       Baud rate for RS232 (default: 19200)
              --timeout-ms <ms>   Read timeout (default: 1500)
              --reconnect         Auto-reconnect on connection errors
              --list-ports        List available COM ports and exit
              --help, -h          Show this help

            Examples:
              hvglogger --port COM3
              hvglogger --port COM3 --interval-ms 1000 --out data.csv
              hvglogger --port COM4 --mode rs232 --baud 19200 --reconnect

            Output CSV format:
              timestamp_iso,pressure_torr
              2024-01-15T10:30:00.123Z,1.23E-05
            """);
    }
}
