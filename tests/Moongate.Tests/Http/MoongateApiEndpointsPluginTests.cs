using DryIoc;
using Moongate.Http.Plugin.Interfaces;
using Moongate.Server;
using Moongate.Server.Endpoints;

namespace Moongate.Tests.Http;

public class MoongateApiEndpointsPluginTests
{
    [Fact]
    public void Configure_RegistersEveryEndpointGroupTheApiServes()
    {
        // Registrations rather than instances: the groups need game services this bare container has no
        // reason to hold, and what the plugin is responsible for is the registering.
        var container = new Container();

        new MoongateApiEndpointsPlugin().Configure(container, new());

        var registered = container.GetServiceRegistrations()
                                  .Where(registration => registration.ServiceType == typeof(IApiEndpointRegistration))
                                  .Select(registration => registration.ImplementationType)
                                  .OrderBy(type => type!.Name)
                                  .ToArray();

        Assert.Equal(
            [
                typeof(AccountEndpoints),
                typeof(AdminEndpoints),
                typeof(AuthEndpoints),
                typeof(CharacterAdminEndpoints),
                typeof(CharacterEndpoints),
                typeof(PlayerEndpoints),
                typeof(VersionEndpoints)
            ],
            registered
        );
    }

    [Fact]
    public void Metadata_IdentifiesThePlugin()
        => Assert.Equal("moongate.apiendpoints.plugin", new MoongateApiEndpointsPlugin().Metadata.Id);
}
