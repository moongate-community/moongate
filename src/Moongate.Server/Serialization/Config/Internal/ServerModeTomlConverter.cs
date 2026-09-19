using Moongate.Server.Core.Types.Hosting;
using Tomlyn;
using Tomlyn.Serialization;

namespace Moongate.Server.Serialization.Config.Internal;

internal sealed class ServerModeTomlConverter : TomlConverter<ServerMode>
{
    public ServerModeTomlConverter()
    {
    }

    public override ServerMode Read(TomlReader reader)
    {
        if (reader.TokenType != TomlTokenType.String)
        {
            throw reader.CreateException("Server mode must be a string: login, game, or standalone.");
        }

        var mode = reader.GetString().ToLowerInvariant() switch
        {
            "login" => ServerMode.Login,
            "game" => ServerMode.Game,
            "standalone" => ServerMode.Standalone,
            _ => throw reader.CreateException("Server mode must be login, game, or standalone.")
        };

        reader.Read();
        return mode;
    }

    public override void Write(TomlWriter writer, ServerMode value)
    {
        var name = value switch
        {
            ServerMode.Login => "login",
            ServerMode.Game => "game",
            ServerMode.Standalone => "standalone",
            _ => throw new TomlException("Server mode must be login, game, or standalone.")
        };

        writer.WriteStringValue(name);
    }
}
