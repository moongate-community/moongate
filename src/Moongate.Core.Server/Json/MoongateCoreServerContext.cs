using System.Text.Json.Serialization;
using Moongate.Core.Server.Data.Configs.Server;

namespace Moongate.Core.Server.Json;


[JsonSourceGenerationOptions(
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(MoongateServerConfig))]
[JsonSerializable(typeof(NetworkConfig))]
public partial class MoongateCoreServerContext : JsonSerializerContext
{

}
