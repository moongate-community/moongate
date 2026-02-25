using Moongate.Server.Http.Data;
using Moongate.Server.Http.Interfaces.Facades;

namespace Moongate.Server.Http.Internal;

/// <summary>
/// Runtime dependencies used by mapped HTTP routes.
/// </summary>
internal sealed class MoongateHttpRouteContext
{
    public MoongateHttpRouteContext(
        IHttpSystemFacade systemFacade,
        MoongateHttpJwtOptions jwtOptions,
        IHttpAuthFacade? authFacade,
        IHttpUsersFacade? usersFacade,
        bool isUiEnabled
    )
    {
        SystemFacade = systemFacade;
        JwtOptions = jwtOptions;
        AuthFacade = authFacade;
        UsersFacade = usersFacade;
        IsUiEnabled = isUiEnabled;
    }

    public IHttpAuthFacade? AuthFacade { get; }

    public MoongateHttpJwtOptions JwtOptions { get; }

    public IHttpSystemFacade SystemFacade { get; }

    public IHttpUsersFacade? UsersFacade { get; }

    public bool IsUiEnabled { get; }
}
