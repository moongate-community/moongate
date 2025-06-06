
using Moongate.Core.Json;
using Moongate.Core.Server.Data.Configs.Server;
using Moongate.Core.Server.Json;

JsonUtils.RegisterJsonContext(MoongateCoreServerContext.Default);

var test = new MoongateServerConfig();

var json = JsonUtils.Serialize(test);

Console.WriteLine(json);

Console.WriteLine("Starting Moongate Ultima Online Emulator...");
