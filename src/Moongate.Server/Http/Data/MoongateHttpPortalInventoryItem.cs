namespace Moongate.Server.Http.Data;

/// <summary>
/// Read-only inventory item payload returned by the player portal.
/// </summary>
public sealed class MoongateHttpPortalInventoryItem
{
    public required string ItemId { get; init; }

    public required string Serial { get; init; }

    public required string Name { get; init; }

    public required int Graphic { get; init; }

    public required int Hue { get; init; }

    public required int Amount { get; init; }

    public required string Location { get; init; }

    public string? Layer { get; init; }

    public string? ContainerSerial { get; init; }

    public string? ContainerName { get; init; }

    public required string ImageUrl { get; init; }
}
