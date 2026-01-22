namespace HVG2020B.Driver;

/// <summary>
/// Base exception for HVG protocol errors.
/// </summary>
public class HVGProtocolException : Exception
{
    public HVGProtocolException(string message) : base(message) { }
    public HVGProtocolException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when the device does not respond with '>' prompt within timeout.
/// </summary>
public class HVGProtocolTimeoutException : HVGProtocolException
{
    public string? PartialResponse { get; }

    public HVGProtocolTimeoutException(int timeoutMs, string? partialResponse = null)
        : base($"Device did not respond with '>' prompt within {timeoutMs}ms. Partial response: '{partialResponse ?? "(none)"}'")
    {
        PartialResponse = partialResponse;
    }
}

/// <summary>
/// Thrown when the response cannot be parsed.
/// </summary>
public class HVGParseException : HVGProtocolException
{
    public string RawResponse { get; }

    public HVGParseException(string rawResponse, string message)
        : base($"Failed to parse response: {message}. Raw: '{rawResponse}'")
    {
        RawResponse = rawResponse;
    }
}
