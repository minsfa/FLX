using System.IO.Ports;

namespace HVG2020B.Driver;

/// <summary>
/// Serial port settings for HVG-2020B gauge connection.
/// </summary>
public sealed class HVGSerialSettings
{
    /// <summary>
    /// Connection mode: USB virtual COM or RS232.
    /// </summary>
    public HVGConnectionMode Mode { get; init; } = HVGConnectionMode.Usb;

    /// <summary>
    /// Baud rate. Only used for RS232 mode (default 19200).
    /// USB virtual COM ignores baud rate.
    /// </summary>
    public int BaudRate { get; init; } = 19200;

    /// <summary>
    /// Data bits (default 8).
    /// </summary>
    public int DataBits { get; init; } = 8;

    /// <summary>
    /// Parity (default None).
    /// </summary>
    public Parity Parity { get; init; } = Parity.None;

    /// <summary>
    /// Stop bits (default One).
    /// </summary>
    public StopBits StopBits { get; init; } = StopBits.One;

    /// <summary>
    /// Handshake (default None).
    /// </summary>
    public Handshake Handshake { get; init; } = Handshake.None;

    /// <summary>
    /// Read timeout in milliseconds (default 1500).
    /// </summary>
    public int ReadTimeoutMs { get; init; } = 1500;

    /// <summary>
    /// Write timeout in milliseconds (default 1000).
    /// </summary>
    public int WriteTimeoutMs { get; init; } = 1000;

    /// <summary>
    /// Creates default settings for USB virtual COM connection.
    /// </summary>
    public static HVGSerialSettings ForUsb(int readTimeoutMs = 1500) => new()
    {
        Mode = HVGConnectionMode.Usb,
        ReadTimeoutMs = readTimeoutMs
    };

    /// <summary>
    /// Creates default settings for RS232 connection.
    /// </summary>
    public static HVGSerialSettings ForRs232(int baudRate = 19200, int readTimeoutMs = 1500) => new()
    {
        Mode = HVGConnectionMode.Rs232,
        BaudRate = baudRate,
        ReadTimeoutMs = readTimeoutMs
    };
}

/// <summary>
/// HVG-2020B connection mode.
/// </summary>
public enum HVGConnectionMode
{
    /// <summary>
    /// USB Virtual COM port (baud rate ignored by device).
    /// </summary>
    Usb,

    /// <summary>
    /// RS232 serial connection.
    /// </summary>
    Rs232
}
