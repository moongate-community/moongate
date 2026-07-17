using DryIoc;
using Moongate.Http.Plugin.Data.Config;
using Moongate.Http.Plugin.Endpoints;
using Moongate.Http.Plugin.Extensions;
using Moongate.Http.Plugin.Interfaces;
using Moongate.Http.Plugin.Services;
using Moongate.Ultima.Catalog;
using Moongate.Ultima.Interfaces;
using SquidStd.Abstractions.Extensions.Config;
using SquidStd.Abstractions.Extensions.Services;
using SquidStd.Core.Utils;
using SquidStd.Plugin.Abstractions.Data;
using SquidStd.Plugin.Abstractions.Interfaces.Plugins;

namespace Moongate.Http.Plugin;

/// <summary>Registers the REST API: its config section, the token service and the server that runs it.</summary>
public class MoongateHttpPlugin : ISquidStdPlugin
{
    public PluginMetadata Metadata
        => new()
        {
            Id = "moongate.http.plugin",
            Version = new(VersionUtils.GetVersion(typeof(MoongateHttpPlugin).Assembly)),
            Author = "squid",
            Name = "Moongate HTTP",
            Description = "Versioned REST API"
        };

    public void Configure(IContainer container, PluginContext context)
    {
        // The signing key is minted and persisted by HttpServerService at startup, not here: Configure runs
        // before the other sections are registered, and saving the file this early would drop them from it.
        container.RegisterConfigSection<MoongateHttpConfig>("http");

        container.Register<IJwtTokenService, JwtTokenService>(Reuse.Singleton);

        // The catalog reads the UO client files through Moongate.Ultima's process-wide statics, which
        // FilesLoaderService initialises at startup. It carries no state of its own, so a singleton costs
        // nothing.
        container.Register<IItemCatalog, ItemCatalog>(Reuse.Singleton);
        container.Register<IItemImageService, ItemImageService>(Reuse.Singleton);

        // These endpoints live here rather than in Moongate.Server because they need no game service at
        // all — only the client files and the filesystem.
        container.RegisterApiEndpoint<ItemImageEndpoints>();

        container.RegisterStdService<HttpServerService, HttpServerService>();
    }
}
