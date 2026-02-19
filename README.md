LOGGING AND  MULTI FULX Calculator

Minimal CLI tool for logging pressure readings from Teledyne HVG-2020B vacuum gauge.

## Features

- USB Virtual COM and RS232 support
- CSV output with ISO timestamps
- Auto-reconnect on connection errors
- Unit conversion (mbar, Pa, atm → Torr)
- Clean shutdown with Ctrl+C

## Quick Start

```bash
# Build
dotnet build HVG2020B.Logger.sln

# List available COM ports
dotnet run --project src/HVG2020B.Logger.Cli -- --list-ports

# Start logging
dotnet run --project src/HVG2020B.Logger.Cli -- --port COM3
```

## Output Format

```csv
timestamp_iso,study_id,device_id,pressure_torr
2026-01-22T04:16:05.0790000+00:00,STD-20260122-041605,HVG-01,766.6
2026-01-22T04:16:05.6230000+00:00,STD-20260122-041605,HVG-02,766.6
```

## Connection Notes (Auto-Baud RS232)

If RS232 auto-baud detection reports "connected" but no values appear, the root cause
was an auto-scan loop that disconnected the serial port even after a successful
baud detection. This made the UI show "Connected" while the port was already closed.

Fix applied:
- Keep the port open after a successful auto-baud detection
- Increase scan read timeout and add short settle delay + retry per baud

If you still see scan failures on some adapters, ensure the port is not in use and
try again after Refresh Ports.

## CLI Options

| Option | Default | Description |
|--------|---------|-------------|
| `--port` | (required) | COM port name |
| `--interval-ms` | 500 | Polling interval |
| `--out` | `./logs/hvg_2020b_<timestamp>.csv` | Output file |
| `--mode` | usb | `usb` or `rs232` |
| `--baud` | 19200 | Baud rate (RS232 only) |
| `--timeout-ms` | 1500 | Read timeout |
| `--reconnect` | off | Auto-reconnect on errors |

## Documentation

See [docs/RUNBOOK.md](docs/RUNBOOK.md) for detailed usage and troubleshooting.

## Requirements

- .NET 8.0 SDK
- Windows (tested)
- Teledyne HVG-2020B vacuum gauge

## License

MIT
