using System.Text.Json.Serialization;
using Moongate.Server.Http.Data;
using Moongate.UO.Data.Templates.Items;

namespace Moongate.Server.Http.Json;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase),
 JsonSerializable(typeof(MoongateHttpLoginRequest)),
 JsonSerializable(typeof(MoongateHttpLoginResponse)),
 JsonSerializable(typeof(MoongateHttpCreateUserRequest)),
 JsonSerializable(typeof(MoongateHttpUpdateUserRequest)),
 JsonSerializable(typeof(MoongateHttpUser)),
 JsonSerializable(typeof(List<MoongateHttpUser>)),
 JsonSerializable(typeof(IReadOnlyList<MoongateHttpUser>)),
 JsonSerializable(typeof(MoongateHttpItemTemplateSummary)),
 JsonSerializable(typeof(List<MoongateHttpItemTemplateSummary>)),
 JsonSerializable(typeof(IReadOnlyList<MoongateHttpItemTemplateSummary>)),
 JsonSerializable(typeof(MoongateHttpItemTemplatePage)),
 JsonSerializable(typeof(ItemTemplateDefinition))]

/// <summary>
/// Represents MoongateHttpJsonContext.
/// </summary>
public partial class MoongateHttpJsonContext : JsonSerializerContext;
