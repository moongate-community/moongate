using Moongate.Server.Data.Config.Sections;
using Tomlyn.Serialization;

namespace Moongate.Server.Serialization.Config.Internal;

internal sealed class ApiOperationPermissionsTomlConverter : TomlConverter<ApiOperationPermissionsConfig>
{
    public override ApiOperationPermissionsConfig Read(TomlReader reader)
    {
        if (reader.TokenType != TomlTokenType.StartArray)
        {
            throw reader.CreateException("allowed_operations must be an array of operation IDs or [\"*\"].");
        }

        reader.Read();
        var operations = new List<ushort>();
        var allowsAll = false;

        while (reader.TokenType != TomlTokenType.EndArray)
        {
            if (allowsAll)
            {
                throw reader.CreateException("The allowed_operations wildcard must appear alone: [\"*\"].");
            }

            if (reader.TokenType == TomlTokenType.String && reader.GetString() == "*" && operations.Count == 0)
            {
                allowsAll = true;
            }
            else if (reader.TokenType == TomlTokenType.Integer && reader.GetInt64() is > 0 and <= ushort.MaxValue)
            {
                operations.Add((ushort)reader.GetInt64());
            }
            else
            {
                throw reader.CreateException("allowed_operations accepts integer IDs from 1 to 65535 or [\"*\"] alone.");
            }

            reader.Read();
        }

        reader.Read();

        return new(operations, allowsAll);
    }

    public override void Write(TomlWriter writer, ApiOperationPermissionsConfig value)
    {
        value.Validate();
        writer.WriteStartArray();

        if (value.AllowsAll)
        {
            writer.WriteStringValue("*");
        }
        else
        {
            foreach (var operation in value.OperationIds)
            {
                writer.WriteIntegerValue(operation);
            }
        }

        writer.WriteEndArray();
    }
}
