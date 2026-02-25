using System.Text.Json.Serialization;
using Moongate.Server.Http.Data;

namespace Moongate.Server.Http.Json;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase),
 JsonSerializable(typeof(MoongateHttpLoginRequest)), JsonSerializable(typeof(MoongateHttpLoginResponse)),

]
/// <summary>
/// Represents MoongateHttpJsonContext.
/// </summary>
public partial class MoongateHttpJsonContext : JsonSerializerContext;
