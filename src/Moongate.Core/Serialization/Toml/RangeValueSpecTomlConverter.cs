using System.Numerics;
using Moongate.Core.Primitives;
using Tomlyn.Serialization;

namespace Moongate.Core.Serialization.Toml;

/// <summary>Reads and writes a <see cref="RangeValueSpec{T}" /> as the text or bare number it documents.</summary>
public sealed class RangeValueSpecTomlConverter<T> : TomlConverter<RangeValueSpec<T>> where T : struct, INumber<T>
{
    /// <inheritdoc />
    public override RangeValueSpec<T> Read(TomlReader reader)
    {
        switch (reader.TokenType)
        {
            case TomlTokenType.String:
            {
                var text = reader.GetString();

                if (!RangeValueSpec<T>.TryParse(text, out var spec))
                {
                    throw reader.CreateException($"'{text}' is not a valid {typeof(T).Name} value or range.");
                }

                return spec;
            }
            case TomlTokenType.Integer:
                return RangeValueSpec<T>.FromValue(T.CreateChecked(reader.GetInt64()));
            case TomlTokenType.Float:
                return RangeValueSpec<T>.FromValue(T.CreateChecked(reader.GetDouble()));
            default:
                throw reader.CreateException($"Expected a number or a range for {typeof(T).Name}.");
        }
    }

    /// <inheritdoc />
    public override void Write(TomlWriter writer, RangeValueSpec<T> value)
    {
        if (value.IsRandom)
        {
            writer.WriteStringValue(value.ToString());

            return;
        }

        var resolved = double.CreateChecked(value.Resolve());

        if (resolved == Math.Truncate(resolved))
        {
            writer.WriteIntegerValue((long)resolved);
        }
        else
        {
            writer.WriteFloatValue(resolved);
        }
    }
}
