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

    private SerialPort? _serialPort;
    private readonly object _lock = new();
    private bool _disposed;
    private HVGSerialSettings _settings = HVGSerialSettings.ForUsb();

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

    /// <inheritdoc />
    public Task ConnectAsync(string portName, CancellationToken cancellationToken = default)
    {
        return ConnectAsync(portName, _settings, cancellationToken);
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
            if (_serialPort?.IsOpen == true)
            {
                try
                {
                    _serialPort.Close();
                }
                catch
                {
                    // Ignore close errors
                }
            }
            _serialPort?.Dispose();
            _serialPort = null;
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
        SerialPort port;

        lock (_lock)
        {
            if (_serialPort == null || !_serialPort.IsOpen)
            {
                throw new HVGProtocolException("Not connected to device");
            }
            port = _serialPort;
        }

        try
        {
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
    /// Reads from serial port until '>' prompt appears or timeout.
    /// </summary>
    private async Task<string> ReadUntilPromptAsync(SerialPort port, CancellationToken cancellationToken)
    {
        var response = new StringBuilder();
        var buffer = new byte[256];
        var timeoutMs = port.ReadTimeout;
        var startTime = DateTime.UtcNow;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
            if (elapsed >= timeoutMs)
            {
                throw new HVGProtocolTimeoutException(timeoutMs, response.ToString());
            }

            var remainingTimeout = (int)(timeoutMs - elapsed);

            try
            {
                // Use a short read timeout to allow checking cancellation
                using var readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                readCts.CancelAfter(Math.Min(remainingTimeout, 100));

                var bytesRead = await port.BaseStream.ReadAsync(buffer.AsMemory(0, buffer.Length), readCts.Token).ConfigureAwait(false);

                if (bytesRead > 0)
                {
                    var text = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                    response.Append(text);

                    // Check if we received the prompt
                    if (text.Contains(ResponsePrompt))
                    {
                        return response.ToString().Trim();
                    }
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Short timeout expired, continue loop to check overall timeout
                continue;
            }
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
