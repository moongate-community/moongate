using Moongate.Plugin.Abstractions.Interfaces;

namespace PluginTemplate;

public sealed class Plugin : IMoongatePlugin
{
    public string Id => "__PLUGIN_ID__";

    public string Name => "PluginTemplate";

    public string Version => "1.0.0";

    public IReadOnlyList<string> Authors => ["__PLUGIN_AUTHORS__"];

    public string? Description => "__PLUGIN_DESCRIPTION__";

    public void Configure(IMoongatePluginContext context)
    {
    }

    public Task InitializeAsync(
        IMoongatePluginRuntimeContext context,
        CancellationToken cancellationToken
    )
    {
        return Task.CompletedTask;
    }
}
