using System.Net;
using Moongate.Api.Registry;
using Moongate.Api.Server;
using Moongate.Core.Directories;
using Moongate.Server.Data.Config.Sections;

namespace Moongate.Server.Services.Api.Internal;

internal static class ApiServerFactory
{
    public static ApiServer Create(ApiConfig config, DirectoriesConfig directories, ApiRegistry registry, TimeProvider clock)
    {
        using var tls = ApiTlsMaterial.Load(config, directories, clock);
        return new(new(IPAddress.Parse(config.ListenAddress), config.Port), registry, new(), tls.Options, clock);
    }
}
