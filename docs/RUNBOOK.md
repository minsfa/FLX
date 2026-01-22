# HVG-2020B Logger Runbook

Minimal CLI tool for logging pressure readings from Teledyne HVG-2020B vacuum gauge.

## Hardware Connection

### Recommended: USB Virtual COM

1. Connect HVG-2020B to PC via USB cable
2. Windows will automatically install drivers and create a virtual COM port
3. Baud rate is ignored by the device in USB mode

### Alternative: RS232

1. Connect HVG-2020B to PC via RS232 cable (DB9)
2. May require USB-to-RS232 adapter
3. Default settings: **19200 baud, 8 data bits, no parity, 1 stop bit, no flow control**

## Identifying COM Port on Windows

### Method 1: Device Manager
1. Press `Win + X` → Device Manager
2. Expand "Ports (COM & LPT)"
3. Look for "USB Serial Port" or similar entry
4. Note the COM port number (e.g., COM3)

### Method 2: Using hvglogger
```bash
hvglogger --list-ports
```

### Method 3: PowerShell
```powershell
Get-WmiObject Win32_SerialPort | Select-Object DeviceID, Description
```

## Installation

### Build from source
```bash
cd src/HVG2020B.Logger.Cli
dotnet build -c Release
```

The executable will be at: `bin/Release/net8.0/hvglogger.exe`

### Run directly
```bash
dotnet run --project src/HVG2020B.Logger.Cli -- --port COM3
```

## Usage

### Basic Usage (USB mode)
```bash
hvglogger --port COM3
```

### With custom interval
```bash
hvglogger --port COM3 --interval-ms 1000
```

### RS232 mode
```bash
hvglogger --port COM4 --mode rs232 --baud 19200
```

### Custom output file
```bash
hvglogger --port COM3 --out C:\data\pressure_log.csv
```

### With auto-reconnect
```bash
hvglogger --port COM3 --reconnect
```

### Full example
```bash
hvglogger --port COM3 --mode usb --interval-ms 500 --timeout-ms 2000 --reconnect --out ./logs/mylog.csv
```

## Command Line Options

| Option | Required | Default | Description |
|--------|----------|---------|-------------|
| `--port <name>` | Yes | - | COM port name (e.g., COM3) |
| `--interval-ms <ms>` | No | 500 | Polling interval in milliseconds |
| `--out <path>` | No | `./logs/hvg_2020b_<timestamp>.csv` | Output CSV file path |
| `--mode <usb\|rs232>` | No | usb | Connection mode |
| `--baud <rate>` | No | 19200 | Baud rate (RS232 only) |
| `--timeout-ms <ms>` | No | 1500 | Read timeout in milliseconds |
| `--reconnect` | No | disabled | Auto-reconnect on connection errors |
| `--list-ports` | No | - | List available COM ports and exit |
| `--help` | No | - | Show help |

## Output Format

CSV file with header:
```
timestamp_iso,pressure_torr
2024-01-15T10:30:00.1234567Z,1.23E-05
2024-01-15T10:30:00.6234567Z,1.24E-05
```

- `timestamp_iso`: UTC timestamp in ISO 8601 format
- `pressure_torr`: Pressure value in Torr (scientific notation)

## Troubleshooting

### No data / Timeout errors

1. **Check cable connection**: Ensure USB/RS232 cable is securely connected
2. **Verify COM port**: Use `--list-ports` to see available ports
3. **Check device power**: Ensure gauge is powered on and displaying readings
4. **Try longer timeout**: Use `--timeout-ms 3000` or higher
5. **Check terminator**: Protocol uses `\r` (carriage return, 0x0D) as command terminator
6. **Verify prompt**: Device should respond with data followed by `>` prompt

### Wrong unit / Unexpected values

1. **Check gauge configuration**: Ensure gauge is configured to display Torr
2. **Unit tokens**: The logger recognizes these units and converts to Torr:
   - TORR, mTORR (milliTorr)
   - mbar, bar
   - Pa, hPa, kPa
   - atm
   - PSI
3. **Unknown units**: If unit is not recognized, value is logged as-is (assumed Torr)

### Port access denied

1. **Close other applications**: Another program may be using the port
2. **Check permissions**: Run as Administrator if needed
3. **Unplug and replug**: Reset the USB connection

### Connection keeps dropping

1. **Use `--reconnect` flag**: Enables automatic reconnection with exponential backoff
2. **Check cable quality**: Try a different USB/RS232 cable
3. **Check USB hub**: Connect directly to PC if using a hub
4. **Reduce polling rate**: Use `--interval-ms 1000` or higher

### Parse errors

1. **Check raw response**: Error message includes the raw data received
2. **Verify device model**: This tool is designed for HVG-2020B specifically
3. **Check for interference**: Electrical noise can corrupt serial data

## Protocol Reference

- **Command**: `P\r` (P followed by carriage return)
- **Response**: `<value> [unit]>` (e.g., `1.23E-5 TORR>`)
- **Terminator**: Carriage return (`\r`, 0x0D)
- **Prompt**: `>` indicates end of response

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Clean shutdown (Ctrl+C) |
| 1 | Invalid arguments |
| 2 | Connection failed (no reconnect) |
| 3 | Timeout (no reconnect) |
| 4 | Communication error (no reconnect) |

## Known Limitations / TODO

- **Unit conversion**: If device returns an unrecognized unit, value is logged as-is
- **No real-time graphing**: PR-1 is logging only; graphing planned for later
- **Single device**: Does not support multiple gauges simultaneously
- **Windows only**: Tested on Windows; Linux/macOS may require different port names (e.g., `/dev/ttyUSB0`)

## Support

For issues, check:
1. This runbook's troubleshooting section
2. HVG-2020B device manual
3. Project repository issues
