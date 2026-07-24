using DryIoc;
using Moongate.Http.Plugin;
using Moongate.Http.Plugin.Data.Config;
using Moongate.Http.Plugin.Endpoints.Accounts;
using Moongate.Http.Plugin.Endpoints.Admin;
using Moongate.Http.Plugin.Endpoints.Auth;
using Moongate.Http.Plugin.Endpoints.Characters;
using Moongate.Http.Plugin.Endpoints.Console;
using Moongate.Http.Plugin.Endpoints.Images;
using Moongate.Http.Plugin.Endpoints.Items;
using Moongate.Http.Plugin.Endpoints.Maps;
using Moongate.Http.Plugin.Endpoints.Mobiles;
using Moongate.Http.Plugin.Endpoints.Players;
using Moongate.Http.Plugin.Endpoints.Plugins;
using Moongate.Http.Plugin.Endpoints.Registration;
using Moongate.Http.Plugin.Endpoints.ServerInfo;
using Moongate.Http.Plugin.Endpoints.Stats;
using Moongate.Http.Plugin.Endpoints.Version;
using Moongate.Http.Plugin.Interfaces.Auth;
using Moongate.Http.Plugin.Interfaces.Endpoints;
using Moongate.Http.Plugin.Services.Auth;
using Moongate.Http.Plugin.Services.Hosting;
using Moongate.Ultima.Catalog;
using Moongate.Ultima.Interfaces;
using SquidStd.Core.Directories;

namespace Moongate.Tests.Http;

public class MoongateHttpPluginTests
{
    [Fact]
    public void Configure_RegistersEveryEndpointGroupTheApiServes()
    {
        // Registrations rather than instances: the game-facing groups need services this bare
        // container has no reason to hold; what the plugin is responsible for is the registering.
        var registered = Configured()
            .GetServiceRegistrations()
            .Where(registration => registration.ServiceType == typeof(IApiEndpointRegistration))
            .Select(registration => registration.ImplementationType)
            .OrderBy(type => type!.Name)
            .ToArray();

        Assert.Equal(
            [
                typeof(AccountEndpoints),
                typeof(AdminEndpoints),
                typeof(AuthEndpoints),
                typeof(BodyImageEndpoints),
                typeof(CharacterAdminEndpoints),
                typeof(CharacterEndpoints),
                typeof(ConsoleEndpoints),
                typeof(HairImageEndpoints),
                typeof(ItemImageAdminEndpoints),
                typeof(ItemImageEndpoints),
                typeof(ItemTemplateEndpoints),
                typeof(MapImageAdminEndpoints),
                typeof(MapImageEndpoints),
                typeof(MobileImageAdminEndpoints),
                typeof(MobileTemplateImageEndpoints),
                typeof(PaperdollEndpoints),
                typeof(PlayerEndpoints),
                typeof(PluginAdminEndpoints),
                typeof(RegistrationEndpoints),
                typeof(ServerInfoEndpoints),
                typeof(ServerSettingsAdminEndpoints),
                typeof(StatsEndpoints),
                typeof(VersionEndpoints)
            ],
            registered
        );
    }

    [Fact]
    public void Configure_RegistersTheItemCatalog()
        => Assert.IsType<ItemCatalog>(Configured().Resolve<IItemCatalog>());

    [Fact]
    public void Configure_RegistersTheServerThatRunsTheApi()
        => Assert.NotNull(Configured().Resolve<HttpServerService>());

    [Fact]
    public void Configure_RegistersTheTokenService()
        => Assert.IsType<JwtTokenService>(Configured().Resolve<IJwtTokenService>());

    [Fact]
    public void Metadata_IdentifiesThePlugin()
        => Assert.Equal("moongate.http.plugin", new MoongateHttpPlugin().Metadata.Id);

    /// <summary>A container carrying what the real bootstrap supplies around the plugin.</summary>
    private static IContainer Configured()
    {
        var container = new Container();
        container.RegisterInstance(new MoongateHttpConfig());
        container.RegisterInstance(TimeProvider.System);
        container.RegisterInstance(
            new DirectoriesConfig(Path.Combine(Path.GetTempPath(), "mg-plugin-test-" + Guid.NewGuid().ToString("N")), [])
        );

        new MoongateHttpPlugin().Configure(container, new());

        return container;
    }
}
