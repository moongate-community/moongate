using System.Net;
using System.Net.Http.Json;
using DryIoc;
using Moongate.Core.Types;
using Moongate.Http.Plugin.Data;
using Moongate.Http.Plugin.Data.Api.Mobiles;
using Moongate.Http.Plugin.Endpoints.Mobiles;
using Moongate.Http.Plugin.Services.Mobiles;
using Moongate.Server.Services.Items;
using Moongate.Server.Services.Mobiles;
using Moongate.Tests.Support;
using Moongate.Ultima.Types;
using Moongate.UO.Data.Mobiles.Templates;
using SquidStd.Core.Directories;

namespace Moongate.Tests.Http.Endpoints.Mobiles;

public sealed class MobileImageEndpointsTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("mg-mobimg-").FullName;

    public void Dispose()
        => Directory.Delete(_root, true);

    private async Task<TestApiServer> StartAsync(AccountLevelType level = AccountLevelType.Administrator)
    {
        var catalog = new FakeAnimationCatalog();
        catalog.Bodies.Add((400, MobType.Human));
        catalog.Bodies.Add((5000, MobType.Equipment));
        catalog.Frames[(400, 0)] = (W: 20, H: 40, Cx: 10, Cy: 0);
        catalog.ItemAnimations[0x203B] = 0x2FBF;
        catalog.Frames[(0x2FBF, 0)] = (W: 20, H: 20, Cx: 10, Cy: 20);

        var templates = new MobileTemplateService();
        templates.Register(new MobileTemplate { Id = "villager", Appearance = new() { Body = 400 } });

        return await TestApiServer.StartAsync(
            level,
            configure: container =>
            {
                var gate = new StubUltimaReadGate();
                var directories = new DirectoriesConfig(_root, Array.Empty<string>());
                var bodies = new BodyImageService(catalog, directories, gate);
                var renderer = new MobileFigureRenderer(catalog, new ItemTemplateService());

                container.RegisterApiEndpointInstance(new BodyImageEndpoints(bodies));
                container.RegisterApiEndpointInstance(
                    new HairImageEndpoints(new HairImageService(renderer, directories, gate))
                );
                container.RegisterApiEndpointInstance(
                    new MobileTemplateImageEndpoints(
                        new MobileTemplateImageService(renderer, templates, directories, gate)
                    )
                );
                container.RegisterApiEndpointInstance(
                    new MobileImageAdminEndpoints(catalog, new BodyImageExportJob(bodies, catalog), bodies)
                );

                var gumps = new FakeGumpCatalog();
                gumps.Gumps[(0x000C, 0)] = (W: 40, H: 100); // male body gump

                container.RegisterApiEndpointInstance(
                    new PaperdollEndpoints(
                        new PaperdollImageService(
                            new PaperdollRenderer(gumps, catalog, new ItemTemplateService()),
                            templates,
                            directories,
                            gate
                        )
                    )
                );
            }
        );
    }

    [Fact]
    public async Task BodyImage_KnownBody_ServesPngAnonymously()
    {
        await using var server = await StartAsync();

        var response = await server.Client.GetAsync("/api/v1/images/bodies/400.png");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task BodyImage_UnknownBody_Is404_AndGarbageIs400()
    {
        await using var server = await StartAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await server.Client.GetAsync("/api/v1/images/bodies/999.png")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await server.Client.GetAsync("/api/v1/images/bodies/abc.png")).StatusCode);
    }

    [Fact]
    public async Task HairImage_KnownStyle_ServesPng()
    {
        await using var server = await StartAsync();

        Assert.Equal(
            HttpStatusCode.OK,
            (await server.Client.GetAsync("/api/v1/images/hair/8251.png")).StatusCode
        );
    }

    [Fact]
    public async Task TemplateFigure_SeededTemplate_ServesPng_UnknownIs404()
    {
        await using var server = await StartAsync();

        Assert.Equal(
            HttpStatusCode.OK,
            (await server.Client.GetAsync("/api/v1/images/mobiles/templates/villager.png")).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await server.Client.GetAsync("/api/v1/images/mobiles/templates/nope.png")).StatusCode
        );
    }

    [Fact]
    public async Task AdminBodies_ExcludesEquipment_AndNeedsStaff()
    {
        await using var server = await StartAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await server.Client.GetAsync("/api/v1/admin/bodies")).StatusCode);

        await server.AuthenticateAsync();
        var page = await server.Client.GetFromJsonAsync<PagedResponse<BodySummary>>("/api/v1/admin/bodies");

        Assert.Equal(1, page!.Total);
        Assert.Equal(400, page.Items[0].Body);
        Assert.Equal("/api/v1/images/bodies/400.png", page.Items[0].ImageUrl);
    }

    [Fact]
    public async Task AdminHairStyles_FacialFlagSwitchesTheCatalog()
    {
        await using var server = await StartAsync();
        await server.AuthenticateAsync();

        var hair = await server.Client.GetFromJsonAsync<PagedResponse<HairStyleSummary>>("/api/v1/admin/hair-styles");
        var facial = await server.Client.GetFromJsonAsync<PagedResponse<HairStyleSummary>>("/api/v1/admin/hair-styles?facial=true");

        Assert.Equal(10, hair!.Total);
        Assert.Equal(7, facial!.Total);
        Assert.All(facial.Items, style => Assert.True(style.Facial));
    }

    [Fact]
    public async Task AdminBodies_AsPlayer_IsForbidden()
    {
        await using var server = await StartAsync(AccountLevelType.Player);
        await server.AuthenticateAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await server.Client.GetAsync("/api/v1/admin/bodies")).StatusCode);
    }

    [Fact]
    public async Task BodyExport_StartsOnce_ThenReports()
    {
        await using var server = await StartAsync();
        await server.AuthenticateAsync();

        var accepted = await server.Client.PostAsync("/api/v1/admin/images/bodies", null);

        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await server.Client.GetAsync("/api/v1/admin/images/bodies")).StatusCode);
    }

    [Fact]
    public async Task Paperdoll_SeededTemplate_ServesPng()
    {
        await using var server = await StartAsync();

        var response = await server.Client.GetAsync("/api/v1/images/mobiles/templates/villager/paperdoll.png");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Paperdoll_BackgroundFalse_IsAnonymousAndOk()
    {
        await using var server = await StartAsync();

        var response = await server.Client.GetAsync("/api/v1/images/mobiles/templates/villager/paperdoll.png?background=false");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Paperdoll_UnknownTemplate_Is404()
    {
        await using var server = await StartAsync();

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await server.Client.GetAsync("/api/v1/images/mobiles/templates/nope/paperdoll.png")).StatusCode
        );
    }
}
