using System.Net;
using System.Net.Sockets;

namespace DroneRacingGenesisLoader;

internal sealed class OutputMonitorServer : IDisposable
{
    private readonly TcpListener listener;
    private readonly CancellationTokenSource stop = new();
    private readonly Task worker;
    private readonly Func<string> pageProvider;
    private readonly Func<string> apiProvider;

    internal OutputMonitorServer(Func<string> pageProvider, Func<string> apiProvider)
    {
    this.pageProvider = pageProvider;
    this.apiProvider = apiProvider;
    this.pageProvider = pageProvider;
        listener = new TcpListener(IPAddress.Loopback, 8765);
        listener.Start();

        worker = Task.Run(RunAsync);
    }
    private async Task RunAsync()
    {
    while (!stop.IsCancellationRequested)
    {
        try
        {
            using TcpClient client =
                await listener.AcceptTcpClientAsync(stop.Token);

            using NetworkStream stream = client.GetStream();

            using var reader = new StreamReader(
                stream,
                System.Text.Encoding.ASCII,
                false,
                1024,
            leaveOpen: true);

            string? requestLine = await reader.ReadLineAsync();

            bool api = requestLine?.StartsWith("GET /api ", StringComparison.Ordinal) == true;

            string content = api ? apiProvider() : pageProvider();
            string contentType = api
                ? "application/json; charset=utf-8"
                : "text/html; charset=utf-8";

            byte[] body = System.Text.Encoding.UTF8.GetBytes(content);

            string headers =
                "HTTP/1.1 200 OK\r\n" +
                $"Content-Type: {contentType}\r\n" +
                $"Content-Length: {body.Length}\r\n" +
                "Connection: close\r\n\r\n";

            byte[] headerBytes =
                System.Text.Encoding.ASCII.GetBytes(headers);

            await stream.WriteAsync(headerBytes, stop.Token);
            await stream.WriteAsync(body, stop.Token);
        }
        catch (OperationCanceledException)
        {
            break;
        }
        catch (IOException)
        {
            // Browser disconnected during a refresh; keep the monitor alive.
        }
        catch (SocketException)
        {
            if (stop.IsCancellationRequested)
            break;
        }
    }
    }
    public void Dispose()
    {
        stop.Cancel();
        listener.Stop();

        try
        {
            worker.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }

        stop.Dispose();
    }
}
