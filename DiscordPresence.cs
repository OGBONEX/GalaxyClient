using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace GalaxyClient;

internal sealed class DiscordPresence : IAsyncDisposable
{
    private const string ApplicationId = "1563929195153719506";
    private readonly CancellationTokenSource _stop = new();
    private string _details = "Browsing Galaxy Client";
    private string _state = "Minecraft Java Launcher";
    private Task? _worker;

    public void Start() => _worker ??= Task.Run(() => RunAsync(_stop.Token));
    public void SetActivity(string details, string state) { _details = details; _state = state; }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            for (var index = 0; index < 10 && !cancellationToken.IsCancellationRequested; index++)
            {
                try
                {
                    await using var pipe = new NamedPipeClientStream(".", $"discord-ipc-{index}", PipeDirection.InOut, PipeOptions.Asynchronous);
                    await pipe.ConnectAsync(1500, cancellationToken);
                    await WriteFrameAsync(pipe, 0, new { v = 1, client_id = ApplicationId }, cancellationToken);
                    var ready = await ReadFrameAsync(pipe, cancellationToken);
                    if (ready.Opcode != 1) break;

                    while (!cancellationToken.IsCancellationRequested && pipe.IsConnected)
                    {
                        var activity = new { cmd = "SET_ACTIVITY", args = new { pid = Environment.ProcessId, activity = new { details = _details, state = _state, timestamps = new { start = DateTimeOffset.UtcNow.ToUnixTimeSeconds() }, assets = new { large_image = "galaxy", large_text = "Galaxy Client" } } }, nonce = Guid.NewGuid().ToString("D") };
                        await WriteFrameAsync(pipe, 1, activity, cancellationToken);
                        await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
                catch (Exception ex) when (ex is IOException or TimeoutException or JsonException or InvalidOperationException) { }
            }
            try { await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken); } catch (OperationCanceledException) { return; }
        }
    }

    private static async Task WriteFrameAsync(Stream stream, int opcode, object payload, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(payload);
        var header = new byte[8];
        BitConverter.GetBytes(opcode).CopyTo(header, 0);
        BitConverter.GetBytes(json.Length).CopyTo(header, 4);
        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(json, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static async Task<(int Opcode, JsonDocument Payload)> ReadFrameAsync(Stream stream, CancellationToken cancellationToken)
    {
        var header = new byte[8];
        await ReadExactlyAsync(stream, header, cancellationToken);
        var opcode = BitConverter.ToInt32(header, 0);
        var length = BitConverter.ToInt32(header, 4);
        if (length is < 0 or > 1024 * 1024) throw new InvalidOperationException("Discord sent an invalid RPC frame.");
        var payload = new byte[length];
        await ReadExactlyAsync(stream, payload, cancellationToken);
        return (opcode, JsonDocument.Parse(payload));
    }

    private static async Task ReadExactlyAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset), cancellationToken);
            if (read == 0) throw new IOException("Discord closed the RPC connection.");
            offset += read;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        if (_worker is not null) { try { await _worker; } catch (OperationCanceledException) { } }
        _stop.Dispose();
    }
}
