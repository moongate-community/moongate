using Moongate.Server.Ultima.Data.Books;
using Moongate.Server.Ultima.Data.Motd;
using Moongate.Server.Ultima.Interfaces.Motd;
using Moongate.Server.Ultima.Services.Text;
using Moongate.Server.Ultima.Types.Text;

namespace Moongate.Server.Ultima.Services.Motd;

/// <summary>
///     Expands registered variables in a single pass over the original template.
/// </summary>
public sealed class MotdRenderer
{
    private readonly IMotdVariableRegistry _variables;

    public MotdRenderer(IMotdVariableRegistry variables)
    {
        _variables = variables;
    }

    public static void RegisterBuiltins(IMotdVariableRegistry variables)
    {
        foreach (var name in TextTemplateBuiltins.Values(new TextTemplateContext()).Keys)
        {
            variables.Register(
                name,
                (context, _) => ValueTask.FromResult(
                    TextTemplateBuiltins.Values(
                        new TextTemplateContext
                        {
                            ServerName = context.ServerName,
                            RealmName = context.RealmName,
                            Version = context.Version,
                            Codename = context.Codename,
                            PlayerName = context.PlayerName,
                            UsersOnline = context.UsersOnline
                        }
                    )[name]
                )
            );
        }
    }

    public ValueTask<string> RenderAsync(MotdLine line, MotdContext context, CancellationToken cancellationToken)
    {
        return TextTemplateRenderer.RenderAsync(
            line.Template,
            (name, token) => _variables.ResolveAsync(name, context, token),
            TextTemplateSyntaxType.Motd,
            cancellationToken
        );
    }
}
