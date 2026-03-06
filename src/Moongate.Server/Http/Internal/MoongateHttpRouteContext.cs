using Moongate.Core.Data.Directories;
using Moongate.Server.Http.Data;
using Moongate.Server.Interfaces.Services.Accounting;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Interfaces.Services.Metrics;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.UO.Data.Interfaces.Art;
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
        IMetricsHttpSnapshotFactory? metricsHttpSnapshotFactory,
        bool isUiEnabled,
        DirectoriesConfig directoriesConfig,
        IItemTemplateService? itemTemplateService,
        IArtService? artService,
        IGameNetworkSessionService? gameNetworkSessionService,
        ICommandSystemService? commandSystemService
    )
    {
        JwtOptions = jwtOptions;
        AccountService = accountService;
        MetricsHttpSnapshotFactory = metricsHttpSnapshotFactory;
        IsUiEnabled = isUiEnabled;
        DirectoriesConfig = directoriesConfig;
        ItemTemplateService = itemTemplateService;
        ArtService = artService;
        GameNetworkSessionService = gameNetworkSessionService;
        CommandSystemService = commandSystemService;
    }

    public IAccountService? AccountService { get; }

    public MoongateHttpJwtOptions JwtOptions { get; }

    public IMetricsHttpSnapshotFactory? MetricsHttpSnapshotFactory { get; }

    public bool IsUiEnabled { get; }

    public DirectoriesConfig DirectoriesConfig { get; }

    public IItemTemplateService? ItemTemplateService { get; }

    public IArtService? ArtService { get; }

    public IGameNetworkSessionService? GameNetworkSessionService { get; }

    public ICommandSystemService? CommandSystemService { get; }
}
