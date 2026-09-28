using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Moongate.Server.Ultima.Data.Motd;
using Moongate.Server.Ultima.Interfaces.Motd;

namespace Moongate.Server.Ultima.Services.Motd;

/// <summary>Expands registered variables in a single pass over the original template.</summary>
public sealed class MotdRenderer
{
    private readonly IMotdVariableRegistry _variables;

    public MotdRenderer(IMotdVariableRegistry variables)
    {
        _variables = variables;
    }

    public static void RegisterBuiltins(IMotdVariableRegistry variables)
    {
        variables.Register("version", (context, _) => ValueTask.FromResult(context.Version));
        variables.Register("codename", (context, _) => ValueTask.FromResult(context.Codename));
        variables.Register("server_name", (context, _) => ValueTask.FromResult(context.ServerName));
        variables.Register("realm_name", (context, _) => ValueTask.FromResult(context.RealmName));
        variables.Register("player_name", (context, _) => ValueTask.FromResult(context.PlayerName));
        variables.Register("users_online", (context, _) => ValueTask.FromResult(context.UsersOnline.ToString(CultureInfo.InvariantCulture)));
    }

    public async ValueTask<string> RenderAsync(MotdLine line, MotdContext context, CancellationToken cancellationToken)
    {
        var output = new StringBuilder(line.Template.Length);
        var position = 0;

        foreach (Match token in MotdTemplateTokens.Find(line.Template))
        {
            cancellationToken.ThrowIfCancellationRequested();
            output.Append(line.Template, position, token.Index - position);
            output.Append(await _variables.ResolveAsync(token.Groups[1].Value, context, cancellationToken));
            position = token.Index + token.Length;
        }

        output.Append(line.Template, position, line.Template.Length - position);

        return output.ToString();
    }
}
