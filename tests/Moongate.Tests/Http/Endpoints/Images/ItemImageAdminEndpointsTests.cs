using System.Net;
using System.Net.Http.Json;
using DryIoc;
using Moongate.Http.Plugin.Data;
using Moongate.Http.Plugin.Endpoints.Images;
using Moongate.Http.Plugin.Interfaces.Images;
using Moongate.Http.Plugin.Interfaces.Ultima;
using Moongate.Http.Plugin.Services.Images;
using Moongate.Http.Plugin.Services.Ultima;
using Moongate.Http.Plugin.Types;
using Moongate.Tests.Support;
using Moongate.Ultima.Catalog;
using Moongate.Ultima.Interfaces;

namespace Moongate.Tests.Http.Endpoints.Images;

[Collection("UltimaClientData")]
public class ItemImageAdminEndpointsTests
{
    [Fact]
    public async Task Get_AsStaff_ReportsTheState()
    {
        using var fixture = ItemImageFixture.Create();
        await using var server = await StartAsync(fixture);
        await server.AuthenticateAsync();

        var status = await server.Client.GetFromJsonAsync<ItemImageExportStatus>("/api/v1/admin/images/items");

        Assert.NotNull(status);
        Assert.Equal(nameof(ItemImageExportStateType.Idle), status.State);
    }

    [Fact]
    public async Task Post_AsStaff_IsAccepted()
    {
        using var fixture = ItemImageFixture.Create();
        await using var server = await StartAsync(fixture);
        await server.AuthenticateAsync();

        var response = await server.Client.PostAsync("/api/v1/admin/images/items", null);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithoutAToken_IsUnauthorized()
    {
        using var fixture = ItemImageFixture.Create();
        await using var server = await StartAsync(fixture);

        var response = await server.Client.PostAsync("/api/v1/admin/images/items", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<TestApiServer> StartAsync(ItemImageFixture fixture)
        => await TestApiServer.StartAsync(
            configure: container =>
            {
                container.RegisterInstance(fixture.Directories);
                container.Register<IItemCatalog, ItemCatalog>(Reuse.Singleton);
                container.Register<IUltimaReadGate, UltimaReadGate>(Reuse.Singleton);
                container.Register<IItemImageService, ItemImageService>(Reuse.Singleton);
                container.Register<IItemImageExportJob, ItemImageExportJob>(Reuse.Singleton);
                container.RegisterApiEndpointInstance(
                    new ItemImageAdminEndpoints(
                        container.Resolve<IItemImageExportJob>(),
                        container.Resolve<IItemImageService>()
                    )
                );
            }
        );
}
