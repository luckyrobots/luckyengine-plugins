using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;

namespace LuckyEngine.PluginSdk;

/// <summary>Runs a plugin using bounded UTF-8 JSON messages over its inherited standard streams.</summary>
public static class PluginApplication
{
    public static Task RunAsync(ILuckyPlugin plugin, CancellationToken cancellation = default) =>
        RunAsync(plugin, Console.In, Console.Out, cancellation);

    public static async Task RunAsync(ILuckyPlugin plugin, TextReader input, TextWriter output,
        CancellationToken cancellation = default)
    {
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        using var writing = new SemaphoreSlim(1);
        var pending = new ConcurrentDictionary<long, TaskCompletionSource<JsonElement>>();
        var events = Channel.CreateBounded<UiEvent>(new BoundedChannelOptions(64)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true
        });
        long nextRequest = 0;

        async Task Send(object message, CancellationToken cancel)
        {
            string line = JsonSerializer.Serialize(message, PluginProtocol.Json);
            if (line.Length > PluginProtocol.MaximumMessageCharacters)
                throw new InvalidDataException("Plugin message exceeds the protocol limit.");
            await writing.WaitAsync(cancel);
            try
            {
                await output.WriteLineAsync(line.AsMemory(), cancel);
                await output.FlushAsync(cancel);
            }
            finally
            {
                writing.Release();
            }
        }

        async Task<JsonElement> Request(string method, JsonObject arguments, CancellationToken cancel)
        {
            if (pending.Count >= 64)
                throw new InvalidOperationException("Too many pending engine requests.");
            long id = Interlocked.Increment(ref nextRequest);
            var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending[id] = completion;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel, lifetime.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            try
            {
                await Send(new { protocol = PluginProtocol.Version, type = "request", requestId = id, method, arguments }, timeout.Token);
                return await completion.Task.WaitAsync(timeout.Token);
            }
            finally
            {
                pending.TryRemove(id, out _);
            }
        }

        Task? reader = null;
        try
        {
            string? first = await ReadMessageAsync(input, lifetime.Token);
            if (first is null)
                return;
            using var boot = JsonDocument.Parse(first);
            var root = boot.RootElement;
            if (root.GetProperty("protocol").GetInt32() != PluginProtocol.Version || root.GetProperty("type").GetString() != "start")
                throw new InvalidDataException("A compatible engine start message is required.");
            var start = root.GetProperty("start").Deserialize<PluginStart>(PluginProtocol.Json)
                ?? throw new InvalidDataException("Missing plugin start configuration.");
            var context = new PluginContext(start, Send, Request);
            reader = Task.Run(async () =>
            {
                try
                {
                    while (!lifetime.IsCancellationRequested)
                    {
                        string? line = await ReadMessageAsync(input, lifetime.Token);
                        if (line is null)
                            break;
                        using var document = JsonDocument.Parse(line);
                        var message = document.RootElement;
                        if (message.GetProperty("protocol").GetInt32() != PluginProtocol.Version)
                            throw new InvalidDataException("Unsupported engine protocol version.");
                        switch (message.GetProperty("type").GetString())
                        {
                            case "response":
                                if (pending.TryGetValue(message.GetProperty("requestId").GetInt64(), out var completion))
                                {
                                    if (message.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String)
                                        completion.TrySetException(new PluginRequestException(error.GetString()!));
                                    else
                                        completion.TrySetResult(message.GetProperty("result").Clone());
                                }
                                break;
                            case "event":
                                var uiEvent = message.GetProperty("input").Deserialize<UiEvent>(PluginProtocol.Json)
                                    ?? throw new InvalidDataException("Missing UI event.");
                                if (!events.Writer.TryWrite(uiEvent))
                                    throw new InvalidDataException("Plugin event queue overflow; refusing to replay stale input.");
                                break;
                            case "stop":
                                return;
                            default:
                                throw new InvalidDataException("Unsupported engine message.");
                        }
                    }
                }
                finally
                {
                    events.Writer.TryComplete();
                    await lifetime.CancelAsync();
                }
            }, CancellationToken.None);

            await plugin.StartAsync(context, lifetime.Token);
            await foreach (UiEvent item in events.Reader.ReadAllAsync(lifetime.Token))
            {
                try
                {
                    await plugin.HandleEventAsync(item, lifetime.Token);
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    await context.ReportAsync("error", error.Message, lifetime.Token);
                }
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
        }
        finally
        {
            await lifetime.CancelAsync();
            foreach (var request in pending.Values)
                request.TrySetCanceled();
            await plugin.DisposeAsync();
            if (reader is not null)
                await reader;
        }
    }

    private static async Task<string?> ReadMessageAsync(TextReader input, CancellationToken cancellation)
    {
        var line = new StringBuilder();
        var character = new char[1];
        while (await input.ReadAsync(character.AsMemory(), cancellation) != 0)
        {
            if (character[0] == '\n')
                return line.ToString().TrimEnd('\r');
            if (line.Length == PluginProtocol.MaximumMessageCharacters)
                throw new InvalidDataException("Engine message exceeds the protocol limit.");
            line.Append(character[0]);
        }
        if (line.Length != 0)
            throw new EndOfStreamException("Truncated engine message.");
        return null;
    }
}
