using DryIoc;
using Microsoft.AspNetCore.Routing;
using Moongate.Http.Plugin.Extensions;
using Moongate.Http.Plugin.Interfaces;

namespace Moongate.Tests.Http;

public class ApiEndpointRegistrationExtensionsTests
{
    private sealed class FirstEndpoints : IApiEndpointRegistration
    {
        public void Register(IEndpointRouteBuilder routes)
        {
        }
    }

    private sealed class SecondEndpoints : IApiEndpointRegistration
    {
        public void Register(IEndpointRouteBuilder routes)
        {
        }
    }

    [Fact]
    public void RegisterApiEndpoint_CollectsEveryEndpointUnderTheSameContract()
    {
        var container = new Container();

        container.RegisterApiEndpoint<FirstEndpoints>();
        container.RegisterApiEndpoint<SecondEndpoints>();

        var registrations = container.Resolve<IEnumerable<IApiEndpointRegistration>>().ToList();

        Assert.Equal(2, registrations.Count);
        Assert.Contains(registrations, r => r is FirstEndpoints);
        Assert.Contains(registrations, r => r is SecondEndpoints);
    }

    [Fact]
    public void RegisterApiEndpoint_RegistersAsSingleton()
    {
        var container = new Container();
        container.RegisterApiEndpoint<FirstEndpoints>();

        var first = container.Resolve<IEnumerable<IApiEndpointRegistration>>().Single();
        var second = container.Resolve<IEnumerable<IApiEndpointRegistration>>().Single();

        Assert.Same(first, second);
    }
}
