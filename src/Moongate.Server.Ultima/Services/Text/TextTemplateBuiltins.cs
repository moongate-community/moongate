using System.Globalization;
using Moongate.Server.Ultima.Data.Books;

namespace Moongate.Server.Ultima.Services.Text;

public static class TextTemplateBuiltins
{
    public static IReadOnlyDictionary<string, string> Values(TextTemplateContext context)
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["version"] = context.Version,
            ["codename"] = context.Codename,
            ["server_name"] = context.ServerName,
            ["realm_name"] = context.RealmName,
            ["player_name"] = context.PlayerName,
            ["users_online"] = context.UsersOnline.ToString(CultureInfo.InvariantCulture)
        };
    }
}
