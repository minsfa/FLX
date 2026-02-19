using System.IO.Ports;
using System.Text;
using HVG2020B.Core;

namespace HVG2020B.Driver;

/// <summary>
/// Client for communicating with HVG-2020B vacuum gauge.
/// Implements IGaugeDevice for multi-sensor support.
/// </summary>
public sealed class HVG2020BClient : IGaugeDevice
{
    private const char CommandTerminator = '\r';
    private const char ResponsePrompt = '>';
    private const string PressureCommand = "P";

    /// <summary>
    /// Common baud rates for RS232 connection (ordered by frequency of use).
    /// </summary>
    public static readonly int[] CommonBaudRates = { 19200, 9600, 38400, 57600, 115200 };

    private SerialPort? _serialPort;
    private readonly object _lock = new();
    private bool _disposed;
    private HVGSerialSettings _settings = HVGSerialSettings.ForUsb();
    private int _detectedBaudRate;

    /// <summary>
    /// Creates a new HVG-2020B client with auto-generated device ID.
    /// </summary>
    public HVG2020BClient() : this(Guid.NewGuid().ToString("N")[..8])
    {
    }

    /// <summary>
    /// Creates a new HVG-2020B client with specified device ID.
    /// </summary>
    public HVG2020BClient(string deviceId)
    {
        DeviceId = deviceId;
        DisplayName = $"HVG-2020B ({deviceId})";
    }

    #region IGaugeDevice Implementation

    /// <inheritdoc />
    public string DeviceId { get; }

    /// <inheritdoc />
    public string DeviceType => "HVG-2020B";

    /// <inheritdoc />
    public string DisplayName { get; set; }

    /// <inheritdoc />
    public bool IsConnected => _serialPort?.IsOpen ?? false;

    /// <inheritdoc />
    public string? PortName => _serialPort?.PortName;

    /// <inheritdoc />
    public event EventHandler<GaugeReading>? ReadingReceived;

    /// <inheritdoc />
    public event EventHandler<Exception>? ConnectionLost;

    /// <summary>
    /// Gets the detected baud rate after auto-scan connection.
    /// </summary>
    public int DetectedBaudRate => _detectedBaudRate;

    /// <inheritdoc />
    public Task ConnectAsync(string portName, CancellationToken cancellationToken = default)
    {
        return ConnectAsync(portName, _settings, cancellationToken);
    }

    /// <summary>
    /// Connects to the HVG-2020B gauge with automatic baud rate detection for RS232.
    /// </summary>
    /// <param name="portName">COM port name (e.g., "COM3")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Detected baud rate, or 0 if USB mode</returns>
    public async Task<int> ConnectWithAutoScanAsync(string portName, CancellationToken cancellationToken = default)
    {
        return await ConnectWithAutoScanAsync(portName, CommonBaudRates, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Connects to the HVG-2020B gauge with automatic baud rate detection for RS232.
    /// </summary>
    /// <param name="portName">COM port name (e.g., "COM3")</param>
    /// <param name="baudRatesToTry">Baud rates to try in order</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Detected baud rate</returns>
    public async Task<int> ConnectWithAutoScanAsync(string portName, int[] baudRatesToTry, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(portName))
            throw new ArgumentException("Port name cannot be empty", nameof(portName));

        ArgumentNullException.ThrowIfNull(baudRatesToTry);

        if (baudRatesToTry.Length == 0)
            throw new ArgumentException("At least one baud rate must be provided", nameof(baudRatesToTry));

        const int scanTimeoutMs = 2000;
        const int scanRetriesPerBaud = 2;
        const int settleDelayMs = 150;
        const int retryDelayMs = 250;

        foreach (var baudRate in baudRatesToTry)
        {
            for (var attempt = 1; attempt <= scanRetriesPerBaud; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var testSettings = HVGSerialSettings.ForRs232(baudRate, readTimeoutMs: scanTimeoutMs);
                    await ConnectAsync(portName, testSettings, cancellationToken).ConfigureAwait(false);

                    // Give the adapter/device a brief settle time before testing.
                    await Task.Delay(settleDelayMs, cancellationToken).ConfigureAwait(false);

                    // Test connection with pressure command
                    if (await TestConnectionAsync(cancellationToken).ConfigureAwait(false))
                    {
                        // Success! Update settings with normal timeout
                        _settings = HVGSerialSettings.ForRs232(baudRate);
                        _detectedBaudRate = baudRate;

                        if (_serialPort != null)
                        {
                            _serialPort.ReadTimeout = _settings.ReadTimeoutMs;
                        }

                        return baudRate;
                    }

                    // Test failed, disconnect and try next attempt/baud
                    Disconnect();
                }
                catch
                {
                    // Connection failed, try next attempt/baud
                    Disconnect();
                }

                await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
            }
        }

        throw new HVGProtocolException(
            $"Failed to connect: No valid baud rate found on {portName}. Tried: {string.Join(", ", baudRatesToTry)}");
    }

    /// <summary>
    /// Tests if the current connection is valid by sending a command and checking response.
    /// </summary>
    private async Task<bool> TestConnectionAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await SendCommandInternalAsync(PressureCommand, cancellationToken).ConfigureAwait(false);
            return IsValidResponse(response);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Validates response by checking ASCII printable character ratio.
    /// </summary>
    public static bool IsValidResponse(string? response)
    {
        if (string.IsNullOrEmpty(response))
            return false;

        if (response.Length < 1)
            return false;

        // Count ASCII printable characters (0x20~0x7E) and common control chars
        int printableCount = response.Count(c =>
            (c >= 0x20 && c <= 0x7E) || c == '\r' || c == '\n');

        double ratio = (double)printableCount / response.Length;

        // 80% or more printable characters indicates valid response
        return ratio >= 0.8;
    }

    /// <inheritdoc />
    public async Task<GaugeReading> ReadOnceAsync(CancellationToken cancellationToken = default)
    {
        var pressureReading = await ReadPressureOnceAsync(cancellationToken).ConfigureAwait(false);

        var reading = new GaugeReading
        {
            DeviceId = DeviceId,
            Timestamp = DateTimeOffset.UtcNow,
            PressureTorr = pressureReading.PressureTorr,
            RawUnit = pressureReading.UnitRaw,
            RawResponse = pressureReading.RawLine,
            WasConverted = pressureReading.WasConverted,
            Status = GaugeStatus.Ok
        };

        ReadingReceived?.Invoke(this, reading);
        return reading;
    }

    #endregion

    /// <summary>
    /// Gets or sets the serial settings for this device.
    /// </summary>
    public HVGSerialSettings Settings
    {
        get => _settings;
        set => _settings = value ?? HVGSerialSettings.ForUsb();
    }

    /// <summary>
    /// Connects to the HVG-2020B gauge with specific settings.
    /// </summary>
    /// <param name="portName">COM port name (e.g., "COM3")</param>
    /// <param name="settings">Serial port settings</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public Task ConnectAsync(string portName, HVGSerialSettings settings, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(portName))
            throw new ArgumentException("Port name cannot be empty", nameof(portName));

        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;

        lock (_lock)
        {
            if (_serialPort?.IsOpen == true)
            {
                _serialPort.Close();
                _serialPort.Dispose();
            }

            _serialPort = new SerialPort
            {
                PortName = portName,
                BaudRate = settings.Mode == HVGConnectionMode.Rs232 ? settings.BaudRate : 9600, // USB ignores this
                DataBits = settings.DataBits,
                Parity = settings.Parity,
                StopBits = settings.StopBits,
                Handshake = settings.Handshake,
                ReadTimeout = settings.ReadTimeoutMs,
                WriteTimeout = settings.WriteTimeoutMs,
                NewLine = CommandTerminator.ToString(),
                Encoding = Encoding.ASCII
            };

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                _serialPort.Open();
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or InvalidOperationException)
            {
                throw new HVGProtocolException($"Failed to open port {portName}: {ex.Message}", ex);
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Disconnect()
    {
        lock (_lock)
        {
            if (_serialPort != null)
            {
                try
                {
                    // Discard buffers before closing to unblock pending reads
                    if (_serialPort.IsOpen)
                    {
                        _serialPort.DiscardInBuffer();
                        _serialPort.DiscardOutBuffer();
                        _serialPort.BaseStream.Close();
                        _serialPort.Close();
                    }
                }
                catch
                {
                    // Ignore close errors
                }
                finally
                {
                    try { _serialPort.Dispose(); } catch { }
                    _serialPort = null;
                }
            }
        }
    }

    /// <summary>
    /// Reads pressure once from the gauge (legacy method).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Pressure reading</returns>
    public async Task<PressureReading> ReadPressureOnceAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var rawResponse = await SendCommandAsync(PressureCommand, cancellationToken).ConfigureAwait(false);
        return PressureParser.Parse(rawResponse);
    }

    /// <summary>
    /// Sends a command and reads response until '>' prompt.
    /// </summary>
    private async Task<string> SendCommandAsync(string command, CancellationToken cancellationToken)
    {
        try
        {
            return await SendCommandInternalAsync(command, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or InvalidOperationException)
        {
            var hvgEx = new HVGProtocolException($"Communication error: {ex.Message}", ex);
            ConnectionLost?.Invoke(this, hvgEx);
            throw hvgEx;
        }
    }

    /// <summary>
    /// Internal command sender without event firing (used for connection testing).
    /// </summary>
    private async Task<string> SendCommandInternalAsync(string command, CancellationToken cancellationToken)
    {
        SerialPort port;

        lock (_lock)
        {
            if (_serialPort == null || !_serialPort.IsOpen)
            {
                throw new HVGProtocolException("Not connected to device");
            }
            port = _serialPort;
        }

        // Clear any pending data
        port.DiscardInBuffer();
        port.DiscardOutBuffer();

        // Send command with terminator
        var commandBytes = Encoding.ASCII.GetBytes(command + CommandTerminator);
        await port.BaseStream.WriteAsync(commandBytes, cancellationToken).ConfigureAwait(false);
        await port.BaseStream.FlushAsync(cancellationToken).ConfigureAwait(false);

        // Read response until '>' or timeout
        return await ReadUntilPromptAsync(port, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads from serial port until '>' prompt appears or timeout.
    /// </summary>
    private async Task<string> ReadUntilPromptAsync(SerialPort port, CancellationToken cancellationToken)
    {
        var response = new StringBuilder();
        var buffer = new byte[256];
        var timeoutMs = port.ReadTimeout;

        // Windows SerialPort.BaseStream.ReadAsync does NOT respect CancellationToken.
        // Strategy: start ONE persistent ReadAsync, race it against timeout via Task.WhenAny.
        // When data arrives, consume it and start a new ReadAsync for more data.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeoutMs);

        Task<int>? activeRead = null;

        try
        {
            while (true)
            {
                timeoutCts.Token.ThrowIfCancellationRequested();

                // Start a read if none active
                activeRead ??= port.BaseStream.ReadAsync(buffer, 0, buffer.Length);

                var delayTask = Task.Delay(200, timeoutCts.Token);
                var completed = await Task.WhenAny(activeRead, delayTask).ConfigureAwait(false);

                if (completed == activeRead)
                {
                    var bytesRead = await activeRead.ConfigureAwait(false);
                    activeRead = null; // consumed — will start a new one next loop

                    if (bytesRead > 0)
                    {
                        var text = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                        response.Append(text);

                        if (text.Contains(ResponsePrompt))
                        {
                            return response.ToString().Trim();
                        }
                    }
                }
                // else: delay finished first, loop back to check cancellation / start new delay
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Overall timeout reached (not user cancellation)
            throw new HVGProtocolTimeoutException(timeoutMs, response.ToString());
        }
    }

    /// <summary>
    /// Lists available COM ports on the system.
    /// </summary>
    public static string[] GetAvailablePorts() => SerialPort.GetPortNames();

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        Disconnect();
    }
}
