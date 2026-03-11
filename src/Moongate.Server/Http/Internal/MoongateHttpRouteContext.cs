using Moongate.Core.Data.Directories;
using Moongate.Server.Http.Data;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Services.Accounting;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Interfaces.Services.Metrics;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.UO.Data.Interfaces.Art;
using Moongate.UO.Data.Interfaces.Maps;
using Moongate.UO.Data.Interfaces.Templates;

namespace Moongate.Server.Http.Internal;

/// <summary>
/// Runtime dependencies used by mapped HTTP routes.
/// </summary>
internal sealed class MoongateHttpRouteContext
{
    public MoongateHttpRouteContext(
        MoongateHttpJwtOptions jwtOptions,
        IAccountService? accountService,
        ICharacterService? characterService,
        IMetricsHttpSnapshotFactory? metricsHttpSnapshotFactory,
        bool isUiEnabled,
        DirectoriesConfig directoriesConfig,
        IItemTemplateService? itemTemplateService,
        IArtService? artService,
        IGameNetworkSessionService? gameNetworkSessionService,
        ICommandSystemService? commandSystemService,
        IMapImageService? mapImageService
    )
    {
        JwtOptions = jwtOptions;
        AccountService = accountService;
        CharacterService = characterService;
        MetricsHttpSnapshotFactory = metricsHttpSnapshotFactory;
        IsUiEnabled = isUiEnabled;
        DirectoriesConfig = directoriesConfig;
        ItemTemplateService = itemTemplateService;
        ArtService = artService;
        GameNetworkSessionService = gameNetworkSessionService;
        CommandSystemService = commandSystemService;
        MapImageService = mapImageService;
    }

    public IAccountService? AccountService { get; }

    public ICharacterService? CharacterService { get; }

    public MoongateHttpJwtOptions JwtOptions { get; }

    public IMetricsHttpSnapshotFactory? MetricsHttpSnapshotFactory { get; }

    public bool IsUiEnabled { get; }

    public DirectoriesConfig DirectoriesConfig { get; }

    public IItemTemplateService? ItemTemplateService { get; }

    public IArtService? ArtService { get; }

    public IGameNetworkSessionService? GameNetworkSessionService { get; }

    public ICommandSystemService? CommandSystemService { get; }

    public IMapImageService? MapImageService { get; }
}
