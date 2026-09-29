using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ODCC.Infrastructure.Tests.Modules.Integration;

/// <summary>
/// یک سرور HTTP بسیار سبک روی localhost برای آزمون واقعیِ تحویل وب‌هوک.
///
/// <b>چرا:</b> ماژول یکپارچه‌سازی از طریق HTTP واقعی با اندپوینت‌های بیرونی
/// صحبت می‌کند. آدرس‌های خیالی (مثل example.test) همیشه شکست می‌خورند و
/// امکان بررسی «تحویل موفق»، «امضای ارسالی» و «کد وضعیت» را نمی‌دهند. این
/// سرور روی یک پورت آزاد از <see cref="TcpListener"/> استفاده می‌کند (بدون
/// نیاز به دسترسی مدیر یا رزرو URL) و درخواست‌های دریافتی را ضبط می‌کند.
///
/// این کلاس فقط در آزمون‌هاست و هرگز در کد تولید استفاده نمی‌شود.
/// </summary>
internal sealed class MiniWebhookServer : IDisposable
{
    private readonly TcpListener _listener;
    private readonly int _statusCode;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _loopTask;

    /// <summary>پورتی که سرور روی آن گوش می‌دهد.</summary>
    public int Port { get; }

    /// <summary>درخواست‌های دریافتی به‌ترتیب رسیدن.</summary>
    public ConcurrentQueue<CapturedRequest> Requests { get; } = new();

    /// <summary>یک درخواست ضبط‌شده: متد، مسیر، هدرها و بدنه.</summary>
    public sealed record CapturedRequest(
        string Method,
        string Path,
        IReadOnlyDictionary<string, string> Headers,
        string Body);

    /// <param name="statusCode">کد وضعیتی که در پاسخ برگردانده می‌شود.</param>
    public MiniWebhookServer(int statusCode = 200)
    {
        _statusCode = statusCode;

        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;

        _loopTask = Task.Run(AcceptLoopAsync);
    }

    /// <summary>آدرس پایه‌ای که اندپوینت‌های آزمون باید به آن اشاره کنند.</summary>
    public string BaseUrl => $"http://127.0.0.1:{Port}";

    /// <summary>صبر کردن تا حداقل یک درخواست دریافت شود (با مهلت زمانی).</summary>
    public async Task<CapturedRequest> WaitForRequestAsync(TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));

        while (DateTime.UtcNow < deadline)
        {
            if (Requests.TryDequeue(out var request))
            {
                return request;
            }

            await Task.Delay(20);
        }

        throw new TimeoutException("هیچ درخواست وب‌هوکی در زمان مقرر دریافت نشد.");
    }

    private async Task AcceptLoopAsync()
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(_cts.Token);

                // مدیریت هر اتصال به‌صورت مستقل تا یک اتصال کند، بقیه را متوقف نکند.
                _ = Task.Run(() => HandleConnectionAsync(client));
            }
        }
        catch (OperationCanceledException)
        {
            // خاموشی طبیعی سرور آزمون.
        }
        catch
        {
            // خطای غیرمنتظره‌ی listener نباید آزمون را بشکند؛ سرور best-effort است.
        }
    }

    private async Task HandleConnectionAsync(TcpClient client)
    {
        try
        {
            using (client)
            using (var stream = client.GetStream())
            {
                client.ReceiveTimeout = 5000;
                client.SendTimeout = 5000;

                using var bufferStream = new MemoryStream();
                var buffer = new byte[8192];

                // خواندن تا رسیدن به پایان سرآیند (\r\n\r\n).
                var headerEnd = -1;

                while (headerEnd < 0)
                {
                    var read = await stream.ReadAsync(buffer, _cts.Token);

                    if (read == 0)
                    {
                        return;
                    }

                    bufferStream.Write(buffer, 0, read);

                    var data = bufferStream.ToArray();
                    headerEnd = IndexOfHeaderEnd(data);
                }

                var allBytes = bufferStream.ToArray();
                var headerText = Encoding.ASCII.GetString(allBytes, 0, headerEnd);
                var lines = headerText.Split("\r\n");
                var requestLine = lines[0].Split(' ');

                var method = requestLine.Length > 0 ? requestLine[0] : "POST";
                var path = requestLine.Length > 1 ? requestLine[1] : "/";

                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var line in lines.Skip(1))
                {
                    if (string.IsNullOrEmpty(line))
                    {
                        continue;
                    }

                    var separatorIndex = line.IndexOf(':');

                    if (separatorIndex > 0)
                    {
                        headers[line[..separatorIndex].Trim()] = line[(separatorIndex + 1)..].Trim();
                    }
                }

                // خواندن بقیه‌ی بدنه بر اساس Content-Length.
                var contentLength = headers.TryGetValue("Content-Length", out var lengthText)
                    && int.TryParse(lengthText, out var parsedLength)
                        ? parsedLength
                        : 0;

                var bodyStart = headerEnd + 4;

                while (allBytes.Length - bodyStart < contentLength)
                {
                    var read = await stream.ReadAsync(buffer, _cts.Token);

                    if (read == 0)
                    {
                        break;
                    }

                    bufferStream.Write(buffer, 0, read);
                    allBytes = bufferStream.ToArray();
                }

                var bodyLength = Math.Max(0, Math.Min(contentLength, allBytes.Length - bodyStart));
                var body = bodyLength > 0
                    ? Encoding.UTF8.GetString(allBytes, bodyStart, bodyLength)
                    : string.Empty;

                Requests.Enqueue(new CapturedRequest(method, path, headers, body));

                // پاسخ با کد وضعیت پیکربندی‌شده. Content-Length: 0 و بستن اتصال
                // باعث می‌شود HttpClient بداند پاسخ کامل است.
                var response = $"HTTP/1.1 {_statusCode} OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";
                var responseBytes = Encoding.ASCII.GetBytes(response);

                await stream.WriteAsync(responseBytes, _cts.Token);
                await stream.FlushAsync(_cts.Token);
            }
        }
        catch
        {
            // یک اتصال ناموفق نباید سرور آزمون را از کار بیندازد.
        }
    }

    private static int IndexOfHeaderEnd(byte[] data)
    {
        for (var i = 0; i <= data.Length - 4; i++)
        {
            if (data[i] == 13 && data[i + 1] == 10 && data[i + 2] == 13 && data[i + 3] == 10)
            {
                return i;
            }
        }

        return -1;
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener.Stop();

        try
        {
            _loopTask.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // بهترین‌حالت: منتظر نماندن به‌خاطر خطای لغو.
        }

        _cts.Dispose();
    }
}
