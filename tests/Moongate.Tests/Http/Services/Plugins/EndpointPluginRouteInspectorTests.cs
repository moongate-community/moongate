using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Primitives;
using Moongate.Http.Plugin.Data.Plugins;
using Moongate.Http.Plugin.Services.Plugins;

namespace Moongate.Tests.Http.Services.Plugins;

public class EndpointPluginRouteInspectorTests
{
    private static readonly MethodInfo Handler =
        typeof(EndpointPluginRouteInspectorTests).GetMethod(
            nameof(SampleHandler),
            BindingFlags.NonPublic | BindingFlags.Static
        )!;

    [Fact]
    public void RoutesByAssembly_GroupsUnderTheAssemblyDeclaringTheHandler()
    {
        var inspector = new EndpointPluginRouteInspector();

        var result = inspector.RoutesByAssembly(new StubEndpointDataSource(Route("/api/v1/news", "GET")));

        var assembly = typeof(EndpointPluginRouteInspectorTests).Assembly.GetName().Name!;
        var routes = Assert.Contains(assembly, result);

        Assert.Equal(new PluginRouteInfo("GET", "/api/v1/news", null), Assert.Single(routes));
    }

    [Fact]
    public void RoutesByAssembly_ReportsTheAuthorizationPolicy()
    {
        var inspector = new EndpointPluginRouteInspector();

        var result = inspector.RoutesByAssembly(
            new StubEndpointDataSource(Route("/api/v1/admin/news", "POST", policy: "admin"))
        );

        Assert.Equal("admin", Assert.Single(result.Values.Single()).Policy);
    }

    [Fact]
    public void RoutesByAssembly_ReportsAllowAnonymousAsNoPolicy()
    {
        // AllowAnonymous wins at runtime over any policy also present, so it must win here too.
        var inspector = new EndpointPluginRouteInspector();

        var result = inspector.RoutesByAssembly(
            new StubEndpointDataSource(Route("/api/v1/news", "GET", policy: "admin", allowAnonymous: true))
        );

        Assert.Null(Assert.Single(result.Values.Single()).Policy);
    }

    [Fact]
    public void RoutesByAssembly_EmitsOneEntryPerVerb()
    {
        var inspector = new EndpointPluginRouteInspector();

        var result = inspector.RoutesByAssembly(new StubEndpointDataSource(Route("/api/v1/news", "GET", "HEAD")));

        Assert.Equal(["GET", "HEAD"], result.Values.Single().Select(route => route.Method));
    }

    [Fact]
    public void RoutesByAssembly_UsesAStarWhenNoVerbIsDeclared()
    {
        var inspector = new EndpointPluginRouteInspector();

        var result = inspector.RoutesByAssembly(new StubEndpointDataSource(Route("/api/v1/news")));

        Assert.Equal("*", Assert.Single(result.Values.Single()).Method);
    }

    [Fact]
    public void RoutesByAssembly_GroupsEndpointsWithoutAHandlerUnderAnEmptyKey()
    {
        // Swagger and Scalar map routes carrying no MethodInfo. They must not be dropped: the caller turns
        // the empty key into the synthetic "host" entry.
        var inspector = new EndpointPluginRouteInspector();

        var endpoint = new RouteEndpoint(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse("/scalar/v1"),
            0,
            new(new HttpMethodMetadata(["GET"])),
            "/scalar/v1"
        );

        var result = inspector.RoutesByAssembly(new StubEndpointDataSource(endpoint));

        Assert.Equal("/scalar/v1", Assert.Single(result[string.Empty]).Path);
    }

    private static void SampleHandler()
    {
    }

    private static RouteEndpoint Route(
        string pattern,
        string? method = null,
        string? secondMethod = null,
        string? policy = null,
        bool allowAnonymous = false
    )
    {
        var metadata = new List<object> { Handler };

        if (method is not null)
        {
            // Typed as List<string> rather than inferred: an inferred array would come out string?[] from
            // the nullable parameters and would not bind to HttpMethodMetadata.
            List<string> verbs = secondMethod is null ? [method] : [method, secondMethod];

            metadata.Add(new HttpMethodMetadata(verbs));
        }

        if (policy is not null)
        {
            metadata.Add(new AuthorizeAttribute(policy));
        }

        if (allowAnonymous)
        {
            metadata.Add(new AllowAnonymousAttribute());
        }

        return new(_ => Task.CompletedTask, RoutePatternFactory.Parse(pattern), 0, new(metadata), pattern);
    }

    private sealed class StubEndpointDataSource : EndpointDataSource
    {
        private readonly IReadOnlyList<Endpoint> _endpoints;

        public StubEndpointDataSource(params Endpoint[] endpoints)
        {
            _endpoints = endpoints;
        }

        public override IReadOnlyList<Endpoint> Endpoints => _endpoints;

        public override IChangeToken GetChangeToken() => new CancellationChangeToken(CancellationToken.None);
    }
}
