using FreeSql.DataAnnotations;
using FreeSql.Internal;
using FreeSql.Internal.Model;
using Moongate.Core.Primitives;

namespace Moongate.Persistence.Internal;

/// <summary>
///     Stores a <see cref="Hue" /> as an integer column. Hues go up to 0xFFFF, above the PostgreSQL smallint range, so
///     the column is a plain integer.
/// </summary>
internal sealed class HueTypeHandler : TypeHandler<Hue>
{
    private static readonly HueTypeHandler Instance = new();

    public override Hue Deserialize(object value)
    {
        return new(checked((ushort)Convert.ToInt32(value)));
    }

    public static void EnsureRegistered()
    {
        if (Utils.TypeHandlers.TryGetValue(typeof(Hue), out var existing))
        {
            if (existing is not HueTypeHandler)
            {
                throw new InvalidOperationException(
                    $"A different FreeSql type handler is already registered for {typeof(Hue).FullName}."
                );
            }

            return;
        }

        if (!Utils.TypeHandlers.TryAdd(typeof(Hue), Instance) &&
            Utils.TypeHandlers.TryGetValue(typeof(Hue), out existing) &&
            existing is not HueTypeHandler)
        {
            throw new InvalidOperationException(
                $"A different FreeSql type handler is already registered for {typeof(Hue).FullName}."
            );
        }
    }

    public override void FluentApi(ColumnFluent column)
    {
        column.MapType(typeof(int)).IsNullable(false);
    }

    public override object Serialize(Hue value)
    {
        return (int)value.Value;
    }
}
