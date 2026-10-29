using System.Text.Json;
using System.Text.Json.Nodes;

namespace LuckyEngine.PluginSdk;

public static class PluginProtocol
{
    public const int Version = 1;
    public const int MaximumMessageCharacters = 8 * 1024 * 1024;
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        MaxDepth = 48
    };
}

public sealed record PluginStart(string PluginId, string SettingsDirectory, string PackageDirectory);
public sealed record UiEvent(string PanelId, string WidgetId, string Event, JsonElement Value);

/// <summary>A native widget description. No HTML, engine assemblies or private headers are required.</summary>
public sealed record Widget(string Type, string Id)
{
    public string? Text { get; init; }
    public string? Hint { get; init; }
    public double? Value { get; init; }
    public double? Min { get; init; }
    public double? Max { get; init; }
    public double? Step { get; init; }
    public float? Width { get; init; }
    public float? Height { get; init; }
    public bool? Enabled { get; init; }
    public bool? Visible { get; init; }
    public bool? CommitOnly { get; init; }
    public string? ImageId { get; init; }
    public string[]? Options { get; init; }
    public Widget[]? Children { get; init; }

    public static Widget Column(string id, params Widget[] children) => new("column", id) { Children = children };
    public static Widget Row(string id, params Widget[] children) => new("row", id) { Children = children };
    public static Widget Label(string id, string text) => new("text", id) { Text = text };
    public static Widget Button(string id, string text) => new("button", id) { Text = text };
    public static Widget Slider(string id, double value, double min, double max, double step, bool commitOnly = true) =>
        new("slider", id) { Value = value, Min = min, Max = max, Step = step, CommitOnly = commitOnly };
}

public interface ILuckyPlugin : IAsyncDisposable
{
    Task StartAsync(PluginContext context, CancellationToken cancellation);
    Task HandleEventAsync(UiEvent input, CancellationToken cancellation);
}

public sealed class PluginRequestException(string message) : Exception(message);

public sealed record CameraFrame(string ImageId, int Width, int Height, string JpegBase64,
    long Sequence, long TimestampUnixMicroseconds, string ClockDomain, string DeviceId);

public sealed class PluginContext
{
    private readonly Func<object, CancellationToken, Task> send;
    private readonly Func<string, JsonObject, CancellationToken, Task<JsonElement>> request;

    internal PluginContext(PluginStart start, Func<object, CancellationToken, Task> send,
        Func<string, JsonObject, CancellationToken, Task<JsonElement>> request)
    {
        Start = start;
        this.send = send;
        this.request = request;
    }

    public PluginStart Start { get; }

    public Task PublishViewAsync(string panelId, Widget root, CancellationToken cancellation = default) =>
        send(new { protocol = PluginProtocol.Version, type = "view", panelId, root }, cancellation);

    public Task PatchAsync(string panelId, IReadOnlyDictionary<string, JsonObject> widgets,
        CancellationToken cancellation = default) =>
        send(new { protocol = PluginProtocol.Version, type = "patch", panelId, widgets }, cancellation);

    public Task PublishFrameAsync(CameraFrame frame, CancellationToken cancellation = default) =>
        send(new { protocol = PluginProtocol.Version, type = "frame", frame }, cancellation);

    public Task<JsonElement> RequestAsync(string method, JsonObject? arguments = null,
        CancellationToken cancellation = default) => request(method, arguments ?? new JsonObject(), cancellation);

    public Task ReportAsync(string level, string message, CancellationToken cancellation = default) =>
        send(new { protocol = PluginProtocol.Version, type = "log", level, message }, cancellation);
}
