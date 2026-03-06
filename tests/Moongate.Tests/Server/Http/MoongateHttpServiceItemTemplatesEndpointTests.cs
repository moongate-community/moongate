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
    public async Task ItemTemplateByIdEndpoint_WhenConfigured_ShouldReturnTemplateOrNotFound()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var port = GetRandomPort();
        var itemTemplateService = new ItemTemplateService();
        itemTemplateService.Upsert(
            new()
            {
                Id = "test_item",
                Name = "Test",
                Category = "test",
                ItemId = "0x1F9E",
                Params = new()
                {
                    ["owner"] = new() { Type = ItemTemplateParamType.Serial, Value = "0x40000001" }
                }
            }
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
            using var okDoc = JsonDocument.Parse(await okResponse.Content.ReadAsStringAsync());

            Assert.Multiple(
                () =>
                {
                    Assert.That(okResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                    Assert.That(notFoundResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
                    Assert.That(okDoc.RootElement.GetProperty("params").TryGetProperty("owner", out var owner), Is.True);
                    Assert.That(owner.GetProperty("type").GetInt32(), Is.EqualTo((int)ItemTemplateParamType.Serial));
                    Assert.That(owner.GetProperty("value").GetString(), Is.EqualTo("0x40000001"));
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
                             image[0, 0] = new(255, 255, 255, 255);

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

    [Test]
    public async Task ItemTemplatesEndpoint_WhenConfigured_ShouldReturnPaginatedTemplates()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var port = GetRandomPort();
        var itemTemplateService = new ItemTemplateService();
        itemTemplateService.UpsertRange(
            [
                new()
                {
                    Id = "a_item",
                    Name = "A",
                    Category = "cat",
                    ItemId = "0x1000",
                    Params = new()
                    {
                        ["tooltip"] = new() { Type = ItemTemplateParamType.String, Value = "alpha" }
                    }
                },
                new() { Id = "b_item", Name = "B", Category = "cat", ItemId = "0x1001" },
                new() { Id = "c_item", Name = "C", Category = "cat", ItemId = "0x1002" }
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
                    Assert.That(root.GetProperty("items")[0].TryGetProperty("params", out _), Is.True);
                    Assert.That(
                        root.GetProperty("items")[0].GetProperty("params").ValueKind,
                        Is.EqualTo(JsonValueKind.Object)
                    );
                }
            );
        }
        finally
        {
            await service.StopAsync();
        }
    }

    [Test]
    public async Task ItemTemplatesEndpoint_WhenFilteringByNameAndTag_ShouldReturnMatchingTemplatesOnly()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var port = GetRandomPort();
        var itemTemplateService = new ItemTemplateService();
        itemTemplateService.UpsertRange(
            [
                new()
                {
                    Id = "longsword",
                    Name = "Longsword",
                    Category = "weapons",
                    ItemId = "0x0F60",
                    Tags = ["weapon", "melee"]
                },
                new()
                {
                    Id = "katana",
                    Name = "Katana",
                    Category = "weapons",
                    ItemId = "0x13FF",
                    Tags = ["weapon", "samurai"]
                },
                new()
                {
                    Id = "red_potion",
                    Name = "Greater Heal Potion",
                    Category = "consumables",
                    ItemId = "0x0F0C",
                    Tags = ["potion", "healing"]
                }
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
            var byNameResponse =
                await http.GetAsync($"http://127.0.0.1:{port}/api/item-templates?page=1&pageSize=10&name=kata");
            var byTagResponse =
                await http.GetAsync($"http://127.0.0.1:{port}/api/item-templates?page=1&pageSize=10&tag=healing");
            var byNameAndTagResponse = await http.GetAsync(
                                           $"http://127.0.0.1:{port}/api/item-templates?page=1&pageSize=10&name=long&tag=melee"
                                       );

            using var byNameDoc = JsonDocument.Parse(await byNameResponse.Content.ReadAsStringAsync());
            using var byTagDoc = JsonDocument.Parse(await byTagResponse.Content.ReadAsStringAsync());
            using var byNameAndTagDoc = JsonDocument.Parse(await byNameAndTagResponse.Content.ReadAsStringAsync());

            Assert.Multiple(
                () =>
                {
                    Assert.That(byNameResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                    Assert.That(byTagResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                    Assert.That(byNameAndTagResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

                    Assert.That(byNameDoc.RootElement.GetProperty("totalCount").GetInt32(), Is.EqualTo(1));
                    Assert.That(
                        byNameDoc.RootElement.GetProperty("items")[0].GetProperty("id").GetString(),
                        Is.EqualTo("katana")
                    );

                    Assert.That(byTagDoc.RootElement.GetProperty("totalCount").GetInt32(), Is.EqualTo(1));
                    Assert.That(
                        byTagDoc.RootElement.GetProperty("items")[0].GetProperty("id").GetString(),
                        Is.EqualTo("red_potion")
                    );

                    Assert.That(byNameAndTagDoc.RootElement.GetProperty("totalCount").GetInt32(), Is.EqualTo(1));
                    Assert.That(
                        byNameAndTagDoc.RootElement.GetProperty("items")[0].GetProperty("id").GetString(),
                        Is.EqualTo("longsword")
                    );
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
