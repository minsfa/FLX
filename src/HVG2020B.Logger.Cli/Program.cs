using System.Globalization;
using HVG2020B.Driver;
using HVG2020B.Logger.Cli;

// Configure console for clean output
Console.OutputEncoding = System.Text.Encoding.UTF8;

// Parse arguments
LoggerOptions options;
try
{
    options = LoggerOptions.Parse(args);
    options.Validate();
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    Console.Error.WriteLine();
    LoggerOptions.PrintUsage();
    return 1;
}

// Setup cancellation
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Console.WriteLine();
    Console.WriteLine("Stopping... (press Ctrl+C again to force)");
    cts.Cancel();
};

// Run logger
return await RunLoggerAsync(options, cts.Token);

static async Task<int> RunLoggerAsync(LoggerOptions options, CancellationToken cancellationToken)
{
    var outputPath = options.GetEffectiveOutputPath();

    Console.WriteLine($"HVG-2020B Logger v1.0.0");
    Console.WriteLine($"Port: {options.Port}");
    Console.WriteLine($"Mode: {options.Mode}");
    if (options.Mode == HVGConnectionMode.Rs232)
    {
        Console.WriteLine($"Baud: {options.BaudRate}");
    }
    Console.WriteLine($"Interval: {options.IntervalMs}ms");
    Console.WriteLine($"Timeout: {options.TimeoutMs}ms");
    Console.WriteLine($"Reconnect: {(options.Reconnect ? "enabled" : "disabled")}");
    Console.WriteLine($"Output: {Path.GetFullPath(outputPath)}");
    Console.WriteLine();

    using var client = new HVG2020BClient();
    using var csvLogger = CsvLogger.Create(outputPath);

    Console.WriteLine($"CSV file created: {csvLogger.FilePath}");
    Console.WriteLine($"Study ID: {csvLogger.StudyId}");
    Console.WriteLine("Press Ctrl+C to stop.");
    Console.WriteLine();
    Console.WriteLine($"{"Timestamp",-30} {"Pressure (Torr)",-20} {"Unit",-10} {"Status"}");
    Console.WriteLine(new string('-', 80));

    var settings = options.ToSerialSettings();
    var reconnectDelayMs = 1000;
    var maxReconnectDelayMs = 30000;
    long sampleCount = 0;
    long errorCount = 0;

    while (!cancellationToken.IsCancellationRequested)
    {
        // Connect if needed
        if (!client.IsConnected)
        {
            try
            {
                Console.WriteLine($"Connecting to {options.Port}...");
                await client.ConnectAsync(options.Port, settings, cancellationToken);
                Console.WriteLine("Connected.");
                reconnectDelayMs = 1000; // Reset delay on successful connect
            }
            catch (HVGProtocolException ex)
            {
                Console.Error.WriteLine($"Connection failed: {ex.Message}");

                if (!options.Reconnect)
                {
                    return 2;
                }

                Console.WriteLine($"Retrying in {reconnectDelayMs / 1000.0:F1}s...");
                await DelayWithCancellation(reconnectDelayMs, cancellationToken);

                // Exponential backoff
                reconnectDelayMs = Math.Min(reconnectDelayMs * 2, maxReconnectDelayMs);
                continue;
            }
        }

        // Read pressure
        try
        {
            var reading = await client.ReadOnceAsync(cancellationToken);
            var timestamp = reading.Timestamp;

            // Log to CSV
            csvLogger.Log(timestamp, reading);
            sampleCount++;

            // Flush periodically (every 10 samples)
            if (sampleCount % 10 == 0)
            {
                csvLogger.Flush();
            }

            // Print to console
            var timestampStr = timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
            var pressureStr = reading.PressureTorr.ToString("E3", CultureInfo.InvariantCulture);
            var unitStr = reading.RawUnit ?? "Torr";
            var statusStr = reading.WasConverted ? "(converted)" : "";

            Console.WriteLine($"{timestampStr,-30} {pressureStr,-20} {unitStr,-10} {statusStr}");

            // Wait for next interval
            await DelayWithCancellation(options.IntervalMs, cancellationToken);
        }
        catch (HVGProtocolTimeoutException ex)
        {
            errorCount++;
            Console.Error.WriteLine($"Timeout: {ex.Message}");

            if (!options.Reconnect)
            {
                return 3;
            }

            // Try to reconnect
            client.Disconnect();
            Console.WriteLine($"Reconnecting in {reconnectDelayMs / 1000.0:F1}s...");
            await DelayWithCancellation(reconnectDelayMs, cancellationToken);
            reconnectDelayMs = Math.Min(reconnectDelayMs * 2, maxReconnectDelayMs);
        }
        catch (HVGParseException ex)
        {
            errorCount++;
            Console.Error.WriteLine($"Parse error: {ex.Message}");

            // Continue polling, don't disconnect for parse errors
            await DelayWithCancellation(options.IntervalMs, cancellationToken);
        }
        catch (HVGProtocolException ex)
        {
            errorCount++;
            Console.Error.WriteLine($"Communication error: {ex.Message}");

            if (!options.Reconnect)
            {
                return 4;
            }

            client.Disconnect();
            Console.WriteLine($"Reconnecting in {reconnectDelayMs / 1000.0:F1}s...");
            await DelayWithCancellation(reconnectDelayMs, cancellationToken);
            reconnectDelayMs = Math.Min(reconnectDelayMs * 2, maxReconnectDelayMs);
        }
    }

    // Graceful shutdown
    Console.WriteLine();
    Console.WriteLine(new string('-', 80));
    Console.WriteLine($"Logging stopped. Samples: {sampleCount}, Errors: {errorCount}");
    Console.WriteLine($"Output file: {Path.GetFullPath(csvLogger.FilePath)}");

    csvLogger.Flush();
    client.Disconnect();

    return 0;
}

static async Task DelayWithCancellation(int milliseconds, CancellationToken cancellationToken)
{
    try
    {
        await Task.Delay(milliseconds, cancellationToken);
    }
    catch (OperationCanceledException)
    {
        // Expected when cancelled
    }
}
