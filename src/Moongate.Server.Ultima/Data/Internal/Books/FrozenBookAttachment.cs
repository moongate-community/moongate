using System.Collections.Immutable;
using System.Text.Json;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Types.Templates;

namespace Moongate.Server.Ultima.Data.Internal.Books;

internal sealed record FrozenBookAttachment
{
    public required string TemplateId { get; init; }
    public int ItemId { get; init; }
    public ushort Hue { get; init; }
    public int Amount { get; init; }
    public ItemRarityType Rarity { get; init; }
    public string? Name { get; init; }
    public bool? Movable { get; init; }
    public AccountType? Visibility { get; init; }
    public DateTime? DecayAt { get; init; }
    public ImmutableDictionary<string, JsonElement> Props { get; init; } = ImmutableDictionary<string, JsonElement>.Empty;
}
