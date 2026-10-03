using FreeSql.DataAnnotations;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Entities.Internal;

namespace Moongate.Server.Ultima.Entities.World;

/// <summary>
///     What the shard as a whole keeps across restarts: one row with the props scripts set with world.set_prop.
/// </summary>
/// <remarks>
///     The summary is the comment of the table: one line, as the migration writes it.
/// </remarks>
[Table(Name = "world.state")]
public class WorldStateEntity : IMoongateEntity
{
    /// <summary>
    ///     The id the one row always has.
    /// </summary>
    public static readonly Serial RowId = new(1);

    /// <summary>
    ///     The id of the one row.
    /// </summary>
    [Column(Name = "id", IsPrimary = true, MapType = typeof(long))]
    public Serial Id { get; set; } = RowId;

    /// <summary>
    ///     The values scripts keep for the whole shard: strings, numbers and bools by key.
    /// </summary>
    [JsonMap, Column(DbType = "jsonb", IsNullable = true)]
    public Dictionary<string, object?>? Props { get; set; }

    /// <summary>
    ///     Sets the prop <paramref name="key" />.
    /// </summary>
    /// <exception cref="ArgumentException">
    ///     The key is empty, or the value is not a string, a number, a bool or an enum.
    /// </exception>
    public void SetProp(string key, object? value)
    {
        Props = PropsDictionary.Set(Props, key, value);
    }

    /// <summary>
    ///     Removes the prop <paramref name="key" />; false when there was none.
    /// </summary>
    public bool RemoveProp(string key)
    {
        Props = PropsDictionary.Remove(Props, key, out var removed);

        return removed;
    }

    /// <summary>
    ///     Gets a detached copy to save: the live one keeps changing on the game loop while the copy is written.
    /// </summary>
    public WorldStateEntity Snapshot()
    {
        return new() { Id = Id, Props = Props is null ? null : new Dictionary<string, object?>(Props) };
    }
}
