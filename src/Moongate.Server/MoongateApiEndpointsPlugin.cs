using DryIoc;
using Moongate.Http.Plugin.Extensions;
using Moongate.Server.Endpoints;
using SquidStd.Core.Utils;
using SquidStd.Plugin.Abstractions.Data;
using SquidStd.Plugin.Abstractions.Interfaces.Plugins;

namespace Moongate.Server;

/// <summary>
/// Registers Moongate's REST endpoint groups, which depend on Server services. They live here rather
/// than in Moongate.Http.Plugin because Program.cs adds that plugin, so it cannot reference Server back;
/// the plugin owns the API's plumbing and the game owns the endpoints that need game services.
/// </summary>
public class MoongateApiEndpointsPlugin : ISquidStdPlugin
{
    public PluginMetadata Metadata
        => new()
        {
            Id = "moongate.apiendpoints.plugin",
            Version = new(VersionUtils.GetVersion(typeof(MoongateApiEndpointsPlugin).Assembly)),
            Author = "squid",
            Name = "Moongate API Endpoints",
            Description = "Server-side REST endpoint registrations"
        };

    public void Configure(IContainer container, PluginContext context)
    {
        container.RegisterApiEndpoint<VersionEndpoints>();
        container.RegisterApiEndpoint<AuthEndpoints>();
        container.RegisterApiEndpoint<AdminEndpoints>();
        container.RegisterApiEndpoint<PlayerEndpoints>();
    }
}
