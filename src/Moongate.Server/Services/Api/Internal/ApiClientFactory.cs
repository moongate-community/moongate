using Moongate.Api.Client;
using Moongate.Api.Registry;
using Moongate.Core.Directories;
using Moongate.Server.Core.Data.Realms.Api;
using Moongate.Server.Data.Config.Sections;

namespace Moongate.Server.Services.Api.Internal;

internal static class ApiClientFactory
{
    public static ApiClient Create(ApiConfig config, DirectoriesConfig directories, TimeProvider clock)
    {
        using var tls = ApiTlsMaterial.Load(config, directories, clock, true);
        var registry = new ApiRegistry();
        registry.RegisterContract<RegisterRealmRequest, RegisterRealmResponse>();
        registry.RegisterContract<RenewRealmRequest, RenewRealmResponse>();
        registry.RegisterContract<UnregisterRealmRequest, UnregisterRealmResponse>();
        return new(registry, new(), tls.Options, clock);
    }
}
