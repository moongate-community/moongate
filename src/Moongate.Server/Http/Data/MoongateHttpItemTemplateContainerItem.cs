namespace Moongate.Server.Http.Data;

/// <summary>
/// Resolved container child template shown in item-template detail responses.
/// </summary>
public sealed class MoongateHttpItemTemplateContainerItem
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string ItemId { get; init; }
}
