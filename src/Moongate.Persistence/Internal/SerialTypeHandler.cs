using FreeSql.DataAnnotations;
using FreeSql.Internal;
using FreeSql.Internal.Model;
using Moongate.Core.Primitives;

namespace Moongate.Persistence.Internal;

internal sealed class SerialTypeHandler : TypeHandler<Serial>
{
    private static readonly SerialTypeHandler Instance = new();

    public static void EnsureRegistered()
    {
        if (Utils.TypeHandlers.TryGetValue(typeof(Serial), out var existing))
        {
            if (existing is not SerialTypeHandler)
            {
                throw new InvalidOperationException(
                    $"A different FreeSql type handler is already registered for {typeof(Serial).FullName}.");
            }

            return;
        }

        if (!Utils.TypeHandlers.TryAdd(typeof(Serial), Instance) &&
            Utils.TypeHandlers.TryGetValue(typeof(Serial), out existing) &&
            existing is not SerialTypeHandler)
        {
            throw new InvalidOperationException(
                $"A different FreeSql type handler is already registered for {typeof(Serial).FullName}.");
        }
    }

    public override Serial Deserialize(object value)
    {
        return new Serial(checked((uint)Convert.ToInt64(value)));
    }

    public override object Serialize(Serial value)
    {
        return (long)value.Value;
    }

    public override void FluentApi(ColumnFluent column)
    {
        column.MapType(typeof(long)).IsNullable(false);
    }
}
