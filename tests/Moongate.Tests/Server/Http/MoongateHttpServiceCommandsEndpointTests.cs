using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.Http;
using Moongate.Server.Http.Data;
using Moongate.Server.Types.Commands;
using Moongate.Tests.Server.Http.Support;
using Moongate.Tests.TestSupport;

namespace Moongate.Tests.Server.Http;

public class MoongateHttpServiceCommandsEndpointTests
{
    [Test]
    public async Task CommandsExecuteEndpoint_WhenConfigured_ShouldReturnCollectedOutput()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var port = GetRandomPort();
        var commandService = new TestCommandSystemService
        {
            ExecuteCommandWithOutputAsyncImpl = (command, source, _, _) => Task.FromResult<IReadOnlyList<string>>(
                [$"executed: {command}", $"source: {source}"]
            )
        };

        var service = new MoongateHttpService(
            new()
            {
                DirectoriesConfig = directories,
                Port = port,
                IsOpenApiEnabled = false
            },
            commandSystemService: commandService
        );

        await service.StartAsync();

        try
        {
            using var http = new HttpClient();
            var response = await http.PostAsJsonAsync(
                               $"http://127.0.0.1:{port}/api/commands/execute",
                               new MoongateHttpExecuteCommandRequest { Command = "help" }
                           );

            var payload = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;

            Assert.Multiple(
                () =>
                {
                    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                    Assert.That(root.GetProperty("success").GetBoolean(), Is.True);
                    Assert.That(root.GetProperty("command").GetString(), Is.EqualTo("help"));
                    Assert.That(root.GetProperty("outputLines").GetArrayLength(), Is.EqualTo(2));
                    Assert.That(root.GetProperty("outputLines")[0].GetString(), Is.EqualTo("executed: help"));
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
