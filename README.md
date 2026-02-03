# HVG2020B-Logger

Minimal CLI tool for logging pressure readings from Teledyne HVG-2020B vacuum gauge.
단순 그래프 / logging 만  , flux 계산 없음 
## Features

- USB Virtual COM and RS232 support
- CSV output with ISO timestamps
- Auto-reconnect on connection errors
- Unit conversion (mbar, Pa, atm → Torr)
- Clean shutdown with Ctrl+C
- 

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
timestamp_iso,pressure_torr
2026-01-22T04:16:05.0790000+00:00,766.6
2026-01-22T04:16:05.6230000+00:00,766.6
```

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
