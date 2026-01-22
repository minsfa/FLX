using System.IO.Ports;

namespace HVG2020B.Core;

/// <summary>
/// Common serial connection settings for gauge devices.
/// </summary>
public record GaugeConnectionSettings
{
    /// <summary>
    /// Connection mode.
    /// </summary>
    public ConnectionMode Mode { get; init; } = ConnectionMode.Usb;

    /// <summary>
    /// Baud rate (used for RS232 mode).
    /// </summary>
    public int BaudRate { get; init; } = 19200;

    /// <summary>
    /// Data bits.
    /// </summary>
    public int DataBits { get; init; } = 8;

    /// <summary>
    /// Parity.
    /// </summary>
    public Parity Parity { get; init; } = Parity.None;

    /// <summary>
    /// Stop bits.
    /// </summary>
    public StopBits StopBits { get; init; } = StopBits.One;

    /// <summary>
    /// Read timeout in milliseconds.
    /// </summary>
    public int ReadTimeoutMs { get; init; } = 1500;

    /// <summary>
    /// Write timeout in milliseconds.
    /// </summary>
    public int WriteTimeoutMs { get; init; } = 1000;

    /// <summary>
    /// Default settings for USB virtual COM port.
    /// </summary>
    public static GaugeConnectionSettings DefaultUsb => new() { Mode = ConnectionMode.Usb };

    /// <summary>
    /// Default settings for RS232.
    /// </summary>
    public static GaugeConnectionSettings DefaultRs232 => new() { Mode = ConnectionMode.Rs232 };
}

/// <summary>
/// Connection mode for gauge devices.
/// </summary>
public enum ConnectionMode
{
    /// <summary>
    /// USB Virtual COM port (baud rate typically ignored).
    /// </summary>
    Usb,

    /// <summary>
    /// RS232 serial connection.
    /// </summary>
    Rs232
}
