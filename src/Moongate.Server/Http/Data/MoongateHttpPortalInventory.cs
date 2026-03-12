namespace Moongate.Server.Http.Data;

/// <summary>
/// Inventory payload returned by the player portal for one character.
/// </summary>
public sealed class MoongateHttpPortalInventory
{
    public required string CharacterId { get; init; }

    public required string CharacterName { get; init; }

    public required IReadOnlyList<MoongateHttpPortalInventoryItem> Items { get; init; }

    public required IReadOnlyList<MoongateHttpPortalInventoryItem> BankItems { get; init; }
}
