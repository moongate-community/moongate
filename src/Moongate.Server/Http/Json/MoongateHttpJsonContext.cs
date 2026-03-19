using System.Text.Json.Serialization;
using Moongate.Server.Http.Data;
using Moongate.UO.Data.Templates.Items;

namespace Moongate.Server.Http.Json;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase),
 JsonSerializable(typeof(MoongateHttpLoginRequest)),
 JsonSerializable(typeof(MoongateHttpLoginResponse)),
 JsonSerializable(typeof(MoongateHttpBranding)),
 JsonSerializable(typeof(MoongateHttpUpdatePortalProfileRequest)),
 JsonSerializable(typeof(MoongateHttpUpdatePortalPasswordRequest)),
 JsonSerializable(typeof(MoongateHttpPortalAccount)),
 JsonSerializable(typeof(MoongateHttpPortalCharacter)),
 JsonSerializable(typeof(MoongateHttpPortalInventory)),
 JsonSerializable(typeof(MoongateHttpPortalInventoryItem)),
 JsonSerializable(typeof(MoongateHttpHelpTicket)),
 JsonSerializable(typeof(MoongateHttpHelpTicketPage)),
 JsonSerializable(typeof(MoongateHttpUpdateHelpTicketStatusRequest)),
 JsonSerializable(typeof(MoongateHttpCreateUserRequest)),
 JsonSerializable(typeof(MoongateHttpUpdateUserRequest)),
 JsonSerializable(typeof(MoongateHttpUser)),
 JsonSerializable(typeof(List<MoongateHttpUser>)),
 JsonSerializable(typeof(IReadOnlyList<MoongateHttpUser>)),
 JsonSerializable(typeof(MoongateHttpItemTemplateSummary)),
 JsonSerializable(typeof(MoongateHttpItemTemplateDetail)),
 JsonSerializable(typeof(MoongateHttpItemTemplateContainerItem)),
 JsonSerializable(typeof(ItemTemplateParamDefinition)),
 JsonSerializable(typeof(Dictionary<string, ItemTemplateParamDefinition>)),
 JsonSerializable(typeof(IReadOnlyDictionary<string, ItemTemplateParamDefinition>)),
 JsonSerializable(typeof(List<MoongateHttpItemTemplateSummary>)),
 JsonSerializable(typeof(IReadOnlyList<MoongateHttpItemTemplateSummary>)),
 JsonSerializable(typeof(List<MoongateHttpItemTemplateContainerItem>)),
 JsonSerializable(typeof(IReadOnlyList<MoongateHttpItemTemplateContainerItem>)),
 JsonSerializable(typeof(MoongateHttpActiveSession)),
 JsonSerializable(typeof(List<MoongateHttpActiveSession>)),
 JsonSerializable(typeof(IReadOnlyList<MoongateHttpActiveSession>)),
 JsonSerializable(typeof(List<MoongateHttpPortalCharacter>)),
 JsonSerializable(typeof(IReadOnlyList<MoongateHttpPortalCharacter>)),
 JsonSerializable(typeof(List<MoongateHttpPortalInventoryItem>)),
 JsonSerializable(typeof(IReadOnlyList<MoongateHttpPortalInventoryItem>)),
 JsonSerializable(typeof(List<MoongateHttpHelpTicket>)),
 JsonSerializable(typeof(IReadOnlyList<MoongateHttpHelpTicket>)),
 JsonSerializable(typeof(MoongateHttpExecuteCommandRequest)),
 JsonSerializable(typeof(MoongateHttpExecuteCommandResponse)),
 JsonSerializable(typeof(MoongateHttpServerVersion)),
 JsonSerializable(typeof(MoongateHttpItemTemplatePage))]

/// <summary>
/// Represents MoongateHttpJsonContext.
/// </summary>
public partial class MoongateHttpJsonContext : JsonSerializerContext;
