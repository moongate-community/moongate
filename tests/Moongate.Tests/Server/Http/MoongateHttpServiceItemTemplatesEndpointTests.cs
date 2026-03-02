using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.Http;
using Moongate.Tests.Server.Http.Support;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Services.Templates;
using Moongate.UO.Data.Templates.Items;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Moongate.Tests.Server.Http;

public class MoongateHttpServiceItemTemplatesEndpointTests
{
    [Test]
    public async Task ItemTemplatesEndpoint_WhenConfigured_ShouldReturnPaginatedTemplates()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var port = GetRandomPort();
        var itemTemplateService = new ItemTemplateService();
        itemTemplateService.UpsertRange(
        [
            new ItemTemplateDefinition { Id = "a_item", Name = "A", Category = "cat", ItemId = "0x1000" },
            new ItemTemplateDefinition { Id = "b_item", Name = "B", Category = "cat", ItemId = "0x1001" },
            new ItemTemplateDefinition { Id = "c_item", Name = "C", Category = "cat", ItemId = "0x1002" }
        ]
        );

        var service = new MoongateHttpService(
            new()
            {
                DirectoriesConfig = directories,
                Port = port,
                IsOpenApiEnabled = false
            },
            itemTemplateService: itemTemplateService
        );

        await service.StartAsync();

        try
        {
            using var http = new HttpClient();
            var response = await http.GetAsync($"http://127.0.0.1:{port}/api/item-templates?page=2&pageSize=2");
            var payload = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;

            Assert.Multiple(
                () =>
                {
                    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                    Assert.That(root.GetProperty("page").GetInt32(), Is.EqualTo(2));
                    Assert.That(root.GetProperty("pageSize").GetInt32(), Is.EqualTo(2));
                    Assert.That(root.GetProperty("totalCount").GetInt32(), Is.EqualTo(3));
                    Assert.That(root.GetProperty("items").GetArrayLength(), Is.EqualTo(1));
                }
            );
        }
        finally
        {
            await service.StopAsync();
        }
    }

    [Test]
    public async Task ItemTemplateByIdEndpoint_WhenConfigured_ShouldReturnTemplateOrNotFound()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var port = GetRandomPort();
        var itemTemplateService = new ItemTemplateService();
        itemTemplateService.Upsert(
            new ItemTemplateDefinition { Id = "test_item", Name = "Test", Category = "test", ItemId = "0x1F9E" }
        );

        var service = new MoongateHttpService(
            new()
            {
                DirectoriesConfig = directories,
                Port = port,
                IsOpenApiEnabled = false
            },
            itemTemplateService: itemTemplateService
        );

        await service.StartAsync();

        try
        {
            using var http = new HttpClient();
            var okResponse = await http.GetAsync($"http://127.0.0.1:{port}/api/item-templates/test_item");
            var notFoundResponse = await http.GetAsync($"http://127.0.0.1:{port}/api/item-templates/missing");

            Assert.Multiple(
                () =>
                {
                    Assert.That(okResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                    Assert.That(notFoundResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
                }
            );
        }
        finally
        {
            await service.StopAsync();
        }
    }

    [Test]
    public async Task ItemTemplateImageEndpoint_WhenConfigured_ShouldGenerateAndCacheImage()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var port = GetRandomPort();

        var artService = new TestArtService
        {
            GetArtImpl = (itemId, _) =>
                         {
                             if (itemId != 0x1F9E)
                             {
                                 return null;
                             }

                             var image = new Image<Rgba32>(1, 1);
                             image[0, 0] = new Rgba32(255, 255, 255, 255);

                             return image;
                         }
        };

        var service = new MoongateHttpService(
            new()
            {
                DirectoriesConfig = directories,
                Port = port,
                IsOpenApiEnabled = false
            },
            artService: artService
        );

        await service.StartAsync();

        try
        {
            using var http = new HttpClient();
            var invalidFormatResponse =
                await http.GetAsync($"http://127.0.0.1:{port}/api/item-templates/by-item-id/1F9E/image");
            var response = await http.GetAsync($"http://127.0.0.1:{port}/api/item-templates/by-item-id/0x1F9E/image");
            var expectedPath = Path.Combine(directories[DirectoryType.Images], "items", "0x1F9E.png");

            Assert.Multiple(
                () =>
                {
                    Assert.That(invalidFormatResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                    Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("image/png"));
                    Assert.That(File.Exists(expectedPath), Is.True);
                }
            );
        }
        finally
        {
            await service.StopAsync();
        }
    }

    private static int GetRandomPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;

        return endpoint.Port;
    }
}
