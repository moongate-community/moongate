using DryIoc;
using Moongate.Http.Plugin.Interfaces.Endpoints;
using Moongate.News.Plugin;
using Moongate.News.Plugin.Endpoints;
using Moongate.News.Plugin.Interfaces;
using Xunit;

namespace Moongate.Tests.News;

public class MoongateNewsPluginTests
{
    [Fact]
    public void Configure_registers_the_service_and_both_endpoint_groups()
    {
        var container = new Container();

        new MoongateNewsPlugin().Configure(container, new());

        Assert.True(container.IsRegistered<INewsService>());
        var endpoints = container.GetServiceRegistrations()
            .Where(registration => registration.ServiceType == typeof(IApiEndpointRegistration))
            .Select(registration => registration.ImplementationType)
            .ToArray();
        Assert.Contains(typeof(NewsAdminEndpoints), endpoints);
        Assert.Contains(typeof(NewsEndpoints), endpoints);
    }

    [Fact]
    public void Metadata_identifies_the_plugin()
        => Assert.Equal("moongate.news.plugin", new MoongateNewsPlugin().Metadata.Id);
}
