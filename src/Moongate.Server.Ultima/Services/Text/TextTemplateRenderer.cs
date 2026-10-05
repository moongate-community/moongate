using System.Text;
using Moongate.Server.Ultima.Data.Internal.Text;
using Moongate.Server.Ultima.Types.Text;

namespace Moongate.Server.Ultima.Services.Text;

/// <summary>
///     Expands tokens from the original source once, without interpreting inserted values.
/// </summary>
public static class TextTemplateRenderer
{
    public static string Render(string source, IReadOnlyDictionary<string, string> values, TextTemplateSyntaxType syntax)
    {
        var tokens = TextTemplateTokens.Find(source, syntax);
        var replacements = tokens.Select(token => token.Name is null ? "$" : values[token.Name]).ToArray();
        return Expand(source, tokens, replacements);
    }

    public static async ValueTask<string> RenderAsync(string source, Func<string, CancellationToken, ValueTask<string>> resolve,
        TextTemplateSyntaxType syntax, CancellationToken cancellationToken)
    {
        var tokens = TextTemplateTokens.Find(source, syntax);
        var replacements = new string[tokens.Count];
        for (var index = 0; index < tokens.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = tokens[index].Name;
            replacements[index] = name is null ? "$" : await resolve(name, cancellationToken);
        }

        return Expand(source, tokens, replacements);
    }

    private static string Expand(string source, IReadOnlyList<TextTemplateToken> tokens, IReadOnlyList<string> replacements)
    {
        var output = new StringBuilder(source.Length);
        var position = 0;
        for (var index = 0; index < tokens.Count; index++)
        {
            var token = tokens[index];
            output.Append(source, position, token.Index - position);
            output.Append(replacements[index]);
            position = token.Index + token.Length;
        }

        output.Append(source, position, source.Length - position);
        return output.ToString();
    }
}
