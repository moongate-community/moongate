using Moongate.Server.Data.Session;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Data.Items;

/// <summary>
/// Carries runtime data for an item script hook dispatch.
/// </summary>
public sealed class ItemScriptContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ItemScriptContext"/> class.
    /// </summary>
    /// <param name="session">Source game session when available.</param>
    /// <param name="item">Item target for the script hook.</param>
    /// <param name="hook">Hook name to dispatch (for example <c>on_use</c>).</param>
    /// <param name="metadata">Optional metadata map for hook-specific payload.</param>
    public ItemScriptContext(
        GameSession? session,
        UOItemEntity item,
        string hook,
        IReadOnlyDictionary<string, object?>? metadata = null
    )
    {
        Session = session;
        Item = item;
        Hook = hook;
        Metadata = metadata ?? new Dictionary<string, object?>();
    }

    /// <summary>
    /// Gets source game session, when the action originated from a connected player.
    /// </summary>
    public GameSession? Session { get; }

    /// <summary>
    /// Gets source mobile character from <see cref="Session"/>, when available.
    /// </summary>
    public UOMobileEntity? Mobile => Session?.Character;

    /// <summary>
    /// Gets item entity bound to this script dispatch.
    /// </summary>
    public UOItemEntity Item { get; }

    /// <summary>
    /// Gets hook name to dispatch.
    /// </summary>
    public string Hook { get; }

    /// <summary>
    /// Gets optional metadata payload for hook-specific values.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Metadata { get; }
}
