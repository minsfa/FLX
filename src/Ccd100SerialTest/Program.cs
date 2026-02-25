using System.IO.Ports;
using System.Text;
using System.Text.RegularExpressions;

// 수신 전용: Ccd100SerialTest listen [COMx] [baud]
// 명령 모드: Ccd100SerialTest [COMx] [baud] [cr]
const string DefaultPort = "COM7";
const int DefaultBaudRate = 9600;
const int ReadTimeoutMs = 5000;

var argsList = args.Where(a => a.Length > 0).ToList();
bool listenMode = argsList.Contains("listen", StringComparer.OrdinalIgnoreCase);
var portArgs = argsList.Where(a => !string.Equals(a, "listen", StringComparison.OrdinalIgnoreCase)).ToArray();

string portName = portArgs.Length > 0 ? portArgs[0] : DefaultPort;
int baudRate = portArgs.Length > 1 ? int.Parse(portArgs[1]) : DefaultBaudRate;
// listen 모드: portArgs[2] = baud 다음이 숫자면 초 단위 종료
int? listenSeconds = null;
if (listenMode && portArgs.Length > 2 && int.TryParse(portArgs[2], out int sec) && sec > 0)
    listenSeconds = sec;
bool useCrLf = !listenMode && (portArgs.Length <= 2 || portArgs[2] != "cr");

Console.WriteLine("========================================");
Console.WriteLine("CCD-100 Serial (COM)");
Console.WriteLine("========================================");
Console.WriteLine($"Port: {portName}");
Console.WriteLine($"Baud: {baudRate}");
if (listenMode)
{
    Console.WriteLine("Mode: LISTEN ONLY (no commands sent)");
    if (listenSeconds.HasValue)
        Console.WriteLine($"Run for {listenSeconds} seconds then exit.");
}
else
    Console.WriteLine($"EOL: {(useCrLf ? "CRLF" : "CR only")}");
Console.WriteLine("Usage: listen [COMx] [baud] [sec]  |  [COMx] [baud] [cr]");
Console.WriteLine("========================================\n");

using var port = new SerialPort(portName, baudRate)
{
    DataBits = 8,
    Parity = Parity.None,
    StopBits = StopBits.One,
    ReadTimeout = ReadTimeoutMs,
    WriteTimeout = 2000,
    Encoding = Encoding.ASCII,
    NewLine = "\r\n"
};

try
{
    port.Open();
    Console.WriteLine($"[OK] {portName} opened. Reading... (Ctrl+C to stop)\n");
}
catch (Exception ex)
{
    Console.WriteLine($"[ERROR] Failed to open {portName}: {ex.Message}");
    return 1;
}

if (listenMode)
{
    // 장비가 주기적으로 보내는 값만 수신
    var buffer = new byte[512];
    var lineBuffer = new StringBuilder();
    var lastDataAt = DateTime.UtcNow;
    // READ:0.004 ;0 형태 파싱용 (형식 다르면 로그만 보여줌)
    var readRegex = new Regex(@"READ:\s*([\d.Ee\-+]+)\s*;?", RegexOptions.Compiled);
    var deadline = listenSeconds.HasValue
        ? DateTime.UtcNow.AddSeconds(listenSeconds.Value)
        : (DateTime?)null;

    try
    {
        while (!deadline.HasValue || DateTime.UtcNow < deadline.Value)
        {
            if (port.BytesToRead > 0)
            {
                int n = port.Read(buffer, 0, buffer.Length);
                string chunk = Encoding.ASCII.GetString(buffer, 0, n);
                lineBuffer.Append(chunk);
                lastDataAt = DateTime.UtcNow;
            }
            else
            {
                // CR/LF로 줄 나누기 또는 일정 시간 동안 데이터 없으면 버퍼 한 번에 출력 (CR/LF 없는 프로토콜 대비)
                string full = lineBuffer.ToString();
                if (full.Length > 0)
                {
                    int lastCr = full.LastIndexOf('\r');
                    int lastLf = full.LastIndexOf('\n');
                    int lastEol = Math.Max(lastCr, lastLf);
                    bool flushByTimeout = (DateTime.UtcNow - lastDataAt).TotalMilliseconds > 150;

                    if (lastEol >= 0 || flushByTimeout)
                    {
                        string toProcess = lastEol >= 0 ? full[..(lastEol + 1)] : full;
                        if (lastEol >= 0 && lastEol < full.Length - 1)
                            lineBuffer = new StringBuilder(full[(lastEol + 1)..]);
                        else
                            lineBuffer.Clear();

                        string[] lines = toProcess.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        if (lines.Length == 0 && toProcess.Trim().Length > 0)
                            lines = new[] { toProcess.Trim() };

                        foreach (string line in lines)
                        {
                            string trimmed = line.Trim();
                            if (trimmed.Length == 0) continue;

                            var now = DateTime.Now.ToString("HH:mm:ss.fff");
                            Console.WriteLine($"[{now}] RX: {trimmed}");

                            var m = readRegex.Match(trimmed);
                            if (m.Success && double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float, null, out double val))
                                Console.WriteLine($"         -> Value: {val} (Torr)");
                        }
                    }
                }
                Thread.Sleep(20);
            }
        }
    }
    catch (OperationCanceledException) { }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR] {ex.Message}");
        return 1;
    }
    finally
    {
        port.Close();
        Console.WriteLine("\n[OK] Port closed.");
    }

    return 0;
}

// ----- 명령 모드 (기존) -----
string SendCommand(string command)
{
    string eol = useCrLf ? "\r\n" : "\r";
    string fullCmd = $"a{command}{eol}";
    Console.WriteLine($"TX: a{command} (EOL: {(useCrLf ? "CRLF" : "CR")})");

    port.DiscardInBuffer();
    port.DiscardOutBuffer();
    port.Write(fullCmd);
    Thread.Sleep(400);

    var response = new StringBuilder();
    var buffer = new byte[256];
    var deadline = DateTime.UtcNow.AddMilliseconds(ReadTimeoutMs);

    while (DateTime.UtcNow < deadline)
    {
        if (port.BytesToRead > 0)
        {
            int n = port.Read(buffer, 0, buffer.Length);
            var chunk = Encoding.ASCII.GetString(buffer, 0, n);
            response.Append(chunk);
            if (chunk.Contains("!a!o!") || chunk.Contains("!a!b!"))
                break;
        }
        else
        {
            Thread.Sleep(50);
        }
    }

    string result = response.ToString().Trim().Replace("\r", "").Replace("\n", " ");
    Console.WriteLine($"RX: {result}");
    return result;
}

try
{
    Console.WriteLine("[Test 1] Read Display Value (r)");
    string r1 = SendCommand("r");
    Console.WriteLine(r1.Contains("READ:") ? "  -> OK (READ:... found)" : "  -> (no READ: in response)");
    Console.WriteLine();

    Console.WriteLine("[Test 2] Read All Settings (ras)");
    string r2 = SendCommand("ras");
    Console.WriteLine(r2.Contains("SETTINGS:") ? "  -> OK (SETTINGS:... found)" : "  -> (no SETTINGS: in response)");
    Console.WriteLine();

    Console.WriteLine("[Test 3] Read Setpoint Value (spv?)");
    SendCommand("spv?");
    Console.WriteLine();

    port.Close();
    Console.WriteLine("[OK] Port closed");
}
catch (Exception ex)
{
    Console.WriteLine($"[ERROR] {ex.Message}");
    return 1;
}

Console.WriteLine("\n========================================");
Console.WriteLine("Test completed.");
Console.WriteLine("========================================");
return 0;
