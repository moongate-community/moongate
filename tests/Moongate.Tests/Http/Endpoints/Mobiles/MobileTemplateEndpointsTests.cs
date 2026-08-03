using System.Net;
using System.Net.Http.Json;
using DryIoc;
using Moongate.Core.Types;
using Moongate.Http.Plugin.Data;
using Moongate.Http.Plugin.Data.Api.Mobiles;
using Moongate.Http.Plugin.Endpoints.Mobiles;
using Moongate.Http.Plugin.Extensions;
using Moongate.Server.Abstractions.Interfaces.Mobiles;
using Moongate.Server.Services.Mobiles;
using Moongate.Tests.Support;
using Moongate.UO.Data.Mobiles.Templates;

namespace Moongate.Tests.Http.Endpoints.Mobiles;

/// <summary>
/// The staff view of the spawn template registry: paged, searchable across the fields someone would
/// actually type, and closed to anyone who is not staff.
/// </summary>
public class MobileTemplateEndpointsTests
{
    private const string Route = "/api/v1/admin/mobiles/templates";

    [Fact]
    public async Task List_ReturnsEveryTemplateWithItsFigure()
    {
        await using var server = await StartAsync(Orc("orc"), Orc("brigand"));

        await server.AuthenticateAsync();

        var page = await server.Client.GetFromJsonAsync<PagedResponse<MobileTemplateSummaryResponse>>(Route);

        Assert.Equal(2, page!.Total);
        Assert.All(page.Items, template => Assert.StartsWith("/api/v1/images/mobiles/templates/", template.ImageUrl));
    }

    [Fact]
    public async Task List_IsOrderedById()
    {
        await using var server = await StartAsync(Orc("orc"), Orc("brigand"));

        await server.AuthenticateAsync();

        var page = await server.Client.GetFromJsonAsync<PagedResponse<MobileTemplateSummaryResponse>>(Route);

        Assert.Equal(["brigand", "orc"], page!.Items.Select(template => template.Id));
    }

    // Searching a tag is the case a caller cannot get any other way: tags are not a column, so no
    // client-side filter over the visible rows could find one.
    [Fact]
    public async Task List_SearchMatchesATag()
    {
        var tagged = Orc("brigand");

        tagged.Tags = ["humanoid", "bandit"];

        await using var server = await StartAsync(Orc("orc"), tagged);

        await server.AuthenticateAsync();

        var page = await server.Client.GetFromJsonAsync<PagedResponse<MobileTemplateSummaryResponse>>(
                       $"{Route}?search=bandit"
                   );

        Assert.Equal("brigand", Assert.Single(page!.Items).Id);
    }

    [Fact]
    public async Task List_SearchMatchesTheTitle()
    {
        var titled = Orc("brigand");

        titled.Title = "the highwayman";

        await using var server = await StartAsync(Orc("orc"), titled);

        await server.AuthenticateAsync();

        var page = await server.Client.GetFromJsonAsync<PagedResponse<MobileTemplateSummaryResponse>>(
                       $"{Route}?search=highwayman"
                   );

        Assert.Equal("brigand", Assert.Single(page!.Items).Id);
    }

    [Fact]
    public async Task List_NoMatches_IsAnEmptyPageNotANotFound()
    {
        await using var server = await StartAsync(Orc("orc"));

        await server.AuthenticateAsync();

        var response = await server.Client.GetAsync($"{Route}?search=nobody");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedResponse<MobileTemplateSummaryResponse>>();

        Assert.Equal(0, page!.Total);
        Assert.Empty(page.Items);
    }

    [Theory, InlineData("?page=0"), InlineData("?pageSize=5000")]
    public async Task List_OutOfRangePaging_IsBadRequest(string query)
    {
        await using var server = await StartAsync(Orc("orc"));

        await server.AuthenticateAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await server.Client.GetAsync($"{Route}{query}")).StatusCode);
    }

    [Fact]
    public async Task Get_ReturnsOneTemplateWithItsVariants()
    {
        await using var server = await StartAsync(Orc("orc"));

        await server.AuthenticateAsync();

        var template = await server.Client.GetFromJsonAsync<MobileTemplateResponse>($"{Route}/orc");

        Assert.Equal("an orc", template!.Name);
        Assert.Equal("orc scout", Assert.Single(template.Variants).Name);
    }

    [Fact]
    public async Task Get_IdsAreCaseInsensitive()
    {
        await using var server = await StartAsync(Orc("orc"));

        await server.AuthenticateAsync();

        Assert.Equal(HttpStatusCode.OK, (await server.Client.GetAsync($"{Route}/ORC")).StatusCode);
    }

    [Fact]
    public async Task Get_AnUnknownId_IsNotFound()
    {
        await using var server = await StartAsync(Orc("orc"));

        await server.AuthenticateAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await server.Client.GetAsync($"{Route}/nope")).StatusCode);
    }

    // A valid token that is simply not staff, which is a 403 rather than a 401.
    [Fact]
    public async Task List_AsAPlayer_IsForbidden()
    {
        await using var server = await StartAsync(AccountLevelType.Player, Orc("orc"));

        await server.AuthenticateAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await server.Client.GetAsync(Route)).StatusCode);
    }

    [Fact]
    public async Task List_WithoutAToken_IsUnauthorized()
    {
        await using var server = await StartAsync(Orc("orc"));

        Assert.Equal(HttpStatusCode.Unauthorized, (await server.Client.GetAsync(Route)).StatusCode);
    }

    private static MobileTemplate Orc(string id)
    {
        var template = new MobileTemplate
        {
            Id = id,
            Name = "an orc",
            Category = "monster",
            Tags = ["monster"],
            Appearance = new() { Body = 17 },
        };

        template.Variants.Add(new() { Name = "orc scout", Weight = 1 });

        return template;
    }

    private static Task<TestApiServer> StartAsync(params MobileTemplate[] templates)
        => StartAsync(AccountLevelType.Administrator, templates);

    private static async Task<TestApiServer> StartAsync(AccountLevelType level, params MobileTemplate[] templates)
        => await TestApiServer.StartAsync(
               level,
               configure: container =>
                          {
                              var registry = new MobileTemplateService();

                              foreach (var template in templates)
                              {
                                  registry.Register(template);
                              }

                              container.RegisterInstance<IMobileTemplateService>(registry);
                              container.RegisterApiEndpointInstance(
                                  new MobileTemplateEndpoints(container.Resolve<IMobileTemplateService>())
                              );
                          }
           );
}
