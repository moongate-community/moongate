using Moongate.Server.Core.Types.Accounts;
using Tomlyn;
using Tomlyn.Serialization;

namespace Moongate.Server.Core.Serialization.Toml;

/// <summary>
///     Reads and writes an optional <see cref="AccountType" /> in the form of <see cref="AccountTypeTomlConverter" />;
///     an unset value is left out of the file.
/// </summary>
public sealed class NullableAccountTypeTomlConverter : TomlConverter<AccountType?>
{
    private static readonly AccountTypeTomlConverter Inner = new();

    public override AccountType? Read(TomlReader reader)
    {
        return Inner.Read(reader);
    }

    public override void Write(TomlWriter writer, AccountType? value)
    {
        if (value is null)
        {
            throw new TomlException("An unset account type has no TOML form; leave the key out.");
        }

        Inner.Write(writer, value.Value);
    }
}
