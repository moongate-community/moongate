using System.Net;
using System.Net.Http.Json;
using DryIoc;
using Moongate.Core.Types;
using Moongate.Http.Plugin.Data;
using Moongate.Http.Plugin.Data.Api.Items;
using Moongate.Http.Plugin.Endpoints.Items;
using Moongate.Server.Abstractions.Interfaces.Items;
using Moongate.Server.Services.Items;
using Moongate.Tests.Support;
using Moongate.UO.Data.Items;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Http.Endpoints.Items;

public class ItemTemplateEndpointsTests
{
    private const string Route = "/api/v1/admin/items/templates";

    private static async Task<TestApiServer> StartAsync(AccountLevelType level = AccountLevelType.Administrator)
        => await TestApiServer.StartAsync(
            level,
            configure: container =>
            {
                var templates = new ItemTemplateService();
                templates.Register(
                    Template("katana", "a katana", "weapons", 0x13FF, ["weapon", "sword"], ItemRarityType.Common)
                );
                templates.Register(Template("bread", "a bread loaf", "food", 0x103B, ["food"]));
                templates.Register(
                    Template("elder_katana", "an elder katana", "weapons", 0x13FF, ["weapon", "sword"], ItemRarityType.Legendary)
                );
                container.RegisterInstance<IItemTemplateService>(templates);
                container.RegisterApiEndpointInstance(new ItemTemplateEndpoints(templates));
            }
        );

    [Fact]
    public async Task List_WithoutAToken_IsUnauthorized()
    {
        await using var server = await StartAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await server.Client.GetAsync(Route)).StatusCode);
    }

    [Fact]
    public async Task List_AsPlayer_IsForbidden()
    {
        await using var server = await StartAsync(AccountLevelType.Player);
        await server.AuthenticateAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await server.Client.GetAsync(Route)).StatusCode);
    }

    [Fact]
    public async Task List_AsStaff_ReturnsPagedSummariesOrderedById()
    {
        await using var server = await StartAsync();
        await server.AuthenticateAsync();

        var page = await server.Client.GetFromJsonAsync<PagedResponse<ItemTemplateSummaryResponse>>(Route);

        Assert.Equal(3, page!.Total);
        Assert.Equal(["bread", "elder_katana", "katana"], page.Items.Select(item => item.Id));
        var katana = page.Items.Single(item => item.Id == "katana");
        Assert.Equal("/api/v1/images/items/0x13ff.png", katana.ImageUrl);
        Assert.Equal("Common", katana.Rarity);
    }

    [Fact]
    public async Task List_SearchMatchesNameCategoryAndTags_CaseInsensitively()
    {
        await using var server = await StartAsync();
        await server.AuthenticateAsync();

        var byTag = await server.Client.GetFromJsonAsync<PagedResponse<ItemTemplateSummaryResponse>>($"{Route}?search=SWORD");
        var byCategory = await server.Client.GetFromJsonAsync<PagedResponse<ItemTemplateSummaryResponse>>($"{Route}?search=food");

        Assert.Equal(2, byTag!.Total);
        Assert.Equal(["bread"], byCategory!.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task List_NoMatches_IsAnEmptyPageNotAnError()
    {
        await using var server = await StartAsync();
        await server.AuthenticateAsync();

        var page = await server.Client.GetFromJsonAsync<PagedResponse<ItemTemplateSummaryResponse>>($"{Route}?search=nothing");

        Assert.Equal(0, page!.Total);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task List_BadPaging_IsABadRequest()
    {
        await using var server = await StartAsync();
        await server.AuthenticateAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await server.Client.GetAsync($"{Route}?page=0")).StatusCode);
    }

    [Fact]
    public async Task Get_ReturnsTheFullTemplate_SpecsIncluded()
    {
        await using var server = await StartAsync();
        await server.AuthenticateAsync();

        var template = await server.Client.GetFromJsonAsync<ItemTemplateResponse>($"{Route}/KATANA");

        Assert.Equal("katana", template!.Id);
        Assert.Equal("a fine blade", template.Description);
        Assert.True(template.Stackable is null or false);
        Assert.NotNull(template.Params);
        Assert.Equal("100", template.Params!["durability"].Value);
        Assert.Equal("/api/v1/images/items/0x13ff.png", template.ImageUrl);
    }

    [Fact]
    public async Task Get_UnknownId_IsANotFound()
    {
        await using var server = await StartAsync();
        await server.AuthenticateAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await server.Client.GetAsync($"{Route}/nope")).StatusCode);
    }

    [Fact]
    public async Task Get_WithoutAToken_IsUnauthorized()
    {
        await using var server = await StartAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await server.Client.GetAsync($"{Route}/katana")).StatusCode);
    }

    private static ItemTemplate Template(
        string id,
        string name,
        string category,
        int itemId,
        string[] tags,
        ItemRarityType rarity = ItemRarityType.Common
    )
        => new()
        {
            Id = id,
            Name = name,
            Category = category,
            Description = id == "katana" ? "a fine blade" : string.Empty,
            ItemId = itemId,
            GoldValue = 10,
            Weight = 2.5,
            Rarity = rarity,
            Tags = [.. tags],
            Params = id == "katana"
                ? new() { ["durability"] = new() { Type = "int", Value = "100" } }
                : null
        };
}
