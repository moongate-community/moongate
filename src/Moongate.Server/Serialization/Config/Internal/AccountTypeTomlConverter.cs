using Moongate.Server.Core.Types.Accounts;
using Tomlyn;
using Tomlyn.Serialization;

namespace Moongate.Server.Serialization.Config.Internal;

internal sealed class AccountTypeTomlConverter : TomlConverter<AccountType>
{
    public override AccountType Read(TomlReader reader)
    {
        if (reader.TokenType != TomlTokenType.String)
        {
            throw reader.CreateException("Account type must be regular, game_master, or administrator.");
        }

        var value = reader.GetString().ToLowerInvariant() switch
        {
            "regular" => AccountType.Regular,
            "game_master" => AccountType.GameMaster,
            "administrator" => AccountType.Administrator,
            _ => throw reader.CreateException("Account type must be regular, game_master, or administrator.")
        };

        reader.Read();
        return value;
    }

    public override void Write(TomlWriter writer, AccountType value)
    {
        var name = value switch
        {
            AccountType.Regular => "regular",
            AccountType.GameMaster => "game_master",
            AccountType.Administrator => "administrator",
            _ => throw new TomlException("Account type must be regular, game_master, or administrator.")
        };

        writer.WriteStringValue(name);
    }
}
