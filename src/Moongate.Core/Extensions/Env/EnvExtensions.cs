using System.Text.RegularExpressions;

namespace Moongate.Core.Extensions.Env;

/// <summary>
///     Provides extension methods for expanding environment variables in strings
/// </summary>
public static partial class EnvExtensions
{
    /// <summary>
    ///     Expands $VARIABLE and ${VARIABLE} references once, without expanding the substituted values.
    /// </summary>
    /// <param name="input">
    ///     The input string containing environment variable references
    /// </param>
    /// <param name="requireDefined">
    ///     Whether an undefined variable raises an exception.
    /// </param>
    /// <returns>
    ///     The string with environment variables expanded to their values
    /// </returns>
    public static string ExpandEnvironmentVariables(this string input, bool requireDefined = false)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        return EnvironmentVariablePattern()
            .Replace(
                input,
                match =>
                {
                    var name = match.Groups["name"].Value;

                    return Environment.GetEnvironmentVariable(name) ??
                           (requireDefined
                               ? throw new InvalidOperationException($"Environment variable '{name}' is not defined.")
                               : match.Value);
                }
            );
    }

    [GeneratedRegex(@"\$\{(?<name>[A-Za-z_][A-Za-z0-9_]*)\}|\$(?<name>[A-Za-z_][A-Za-z0-9_]*)")]
    private static partial Regex EnvironmentVariablePattern();
}
