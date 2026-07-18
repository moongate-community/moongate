using DryIoc;
using Moongate.Http.Plugin.Data.Config;
using Moongate.Http.Plugin.Endpoints.Accounts;
using Moongate.Http.Plugin.Endpoints.Admin;
using Moongate.Http.Plugin.Endpoints.Auth;
using Moongate.Http.Plugin.Endpoints.Characters;
using Moongate.Http.Plugin.Endpoints.Images;
using Moongate.Http.Plugin.Endpoints.Items;
using Moongate.Http.Plugin.Endpoints.Maps;
using Moongate.Http.Plugin.Endpoints.Mobiles;
using Moongate.Http.Plugin.Endpoints.Players;
using Moongate.Http.Plugin.Endpoints.Version;
using Moongate.Http.Plugin.Extensions;
using Moongate.Http.Plugin.Interfaces.Auth;
using Moongate.Http.Plugin.Interfaces.Images;
using Moongate.Http.Plugin.Interfaces.Maps;
using Moongate.Http.Plugin.Interfaces.Mobiles;
using Moongate.Http.Plugin.Interfaces.Ultima;
using Moongate.Http.Plugin.Services.Auth;
using Moongate.Http.Plugin.Services.Hosting;
using Moongate.Http.Plugin.Services.Images;
using Moongate.Http.Plugin.Services.Maps;
using Moongate.Http.Plugin.Services.Mobiles;
using Moongate.Http.Plugin.Services.Ultima;
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
        // One gate over Ultima's statics for every reader. Map rendering descends into Art too, and a
        // second gate would be no gate at all.
        container.Register<IUltimaReadGate, UltimaReadGate>(Reuse.Singleton);
        container.Register<IItemImageService, ItemImageService>(Reuse.Singleton);
        container.Register<IMapImageService, MapImageService>(Reuse.Singleton);
        container.Register<IItemImageExportJob, ItemImageExportJob>(Reuse.Singleton);

        // The image routes need no game service at all — only the client files and the filesystem.
        container.RegisterApiEndpoint<ItemImageEndpoints>();
        container.Register<IMapImageExportJob, MapImageExportJob>(Reuse.Singleton);
        container.RegisterApiEndpoint<MapImageEndpoints>();
        container.RegisterApiEndpoint<MapImageAdminEndpoints>();
        container.RegisterApiEndpoint<ItemImageAdminEndpoints>();

        // Mobile figures share the same statics as the other image routes, so the same gate serializes
        // them; the renderer's game-facing inputs come through the Abstractions contracts.
        container.Register<IAnimationCatalog, AnimationCatalog>(Reuse.Singleton);
        container.Register<IMobileFigureRenderer, MobileFigureRenderer>(Reuse.Singleton);
        container.Register<IBodyImageService, BodyImageService>(Reuse.Singleton);
        container.Register<IHairImageService, HairImageService>(Reuse.Singleton);
        container.Register<IMobileTemplateImageService, MobileTemplateImageService>(Reuse.Singleton);
        container.Register<IBodyImageExportJob, BodyImageExportJob>(Reuse.Singleton);
        container.RegisterApiEndpoint<BodyImageEndpoints>();
        container.RegisterApiEndpoint<HairImageEndpoints>();
        container.RegisterApiEndpoint<MobileTemplateImageEndpoints>();
        container.RegisterApiEndpoint<MobileImageAdminEndpoints>();
        container.Register<IGumpCatalog, GumpCatalog>(Reuse.Singleton);
        container.Register<IPaperdollRenderer, PaperdollRenderer>(Reuse.Singleton);
        container.Register<IPaperdollImageService, PaperdollImageService>(Reuse.Singleton);
        container.RegisterApiEndpoint<PaperdollEndpoints>();

        // The game-facing groups consume the contracts in Moongate.Server.Abstractions, which the
        // server implements — the plugin never sees Moongate.Server itself.
        container.RegisterApiEndpoint<AccountEndpoints>();
        container.RegisterApiEndpoint<VersionEndpoints>();
        container.RegisterApiEndpoint<AuthEndpoints>();
        container.RegisterApiEndpoint<AdminEndpoints>();
        container.RegisterApiEndpoint<PlayerEndpoints>();
        container.RegisterApiEndpoint<CharacterEndpoints>();
        container.RegisterApiEndpoint<CharacterAdminEndpoints>();
        container.RegisterApiEndpoint<ItemTemplateEndpoints>();

        container.RegisterStdService<HttpServerService, HttpServerService>();
    }
}
