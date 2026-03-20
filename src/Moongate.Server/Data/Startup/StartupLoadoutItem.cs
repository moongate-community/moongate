using System.Text.Json;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Data.Startup;

/// <summary>
/// Represents one startup item entry resolved after template composition.
/// </summary>
public sealed class StartupLoadoutItem
{
    /// <summary>
    /// Gets or sets the item template identifier.
    /// </summary>
    public required string TemplateId { get; init; }

    /// <summary>
    /// Gets or sets the stack amount for the item.
    /// </summary>
    public int Amount { get; init; } = 1;

    /// <summary>
    /// Gets or sets optional item arguments payload.
    /// </summary>
    public JsonElement? Args { get; init; }

    /// <summary>
    /// Gets or sets the target equipment layer when the item should be equipped.
    /// </summary>
    public ItemLayerType? Layer { get; init; }
}
