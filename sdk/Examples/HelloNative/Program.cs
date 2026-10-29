using LuckyEngine.PluginSdk;
using System.Text.Json.Nodes;

await PluginApplication.RunAsync(new HelloPlugin());

sealed class HelloPlugin : ILuckyPlugin
{
    private PluginContext context = null!;
    private int clicks;
    public async Task StartAsync(PluginContext context, CancellationToken cancellation)
    {
        this.context = context;
        var host = await context.RequestAsync("host.info", cancellation: cancellation);
        await context.PublishViewAsync("example.hello.panel", Widget.Column("root",
            Widget.Label("title", "Hello from an independent plugin"),
            Widget.Label("api", "Host API " + host.GetProperty("api")),
            Widget.Button("count", "Click me"), Widget.Label("value", "0 clicks")), cancellation);
    }
    public Task HandleEventAsync(UiEvent input, CancellationToken cancellation)
    {
        if (input.WidgetId != "count") return Task.CompletedTask;
        return context.PatchAsync("example.hello.panel", new Dictionary<string, JsonObject>
        { ["value"] = new() { ["text"] = $"{++clicks} clicks" } }, cancellation);
    }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
