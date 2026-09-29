using Moongate.Core.Utils;
using Tomlyn.Model;

namespace Moongate.Tests.TestSupport.Config;

/// <summary>
///     Reads one plugin section of a <c>moongate.toml</c> text, as <c>AddConfig</c> does.
/// </summary>
public static class TomlSections
{
    /// <summary>
    ///     The <c>[section]</c> table bound to <typeparamref name="T" />, or its defaults when the section is missing.
    /// </summary>
    public static T Read<T>(string toml, string section)
        where T : class, new()
    {
        var document = TomlUtils.Deserialize<TomlTable>(toml)!;

        return document.TryGetValue(section, out var value) && value is TomlTable table
            ? TomlUtils.Deserialize<T>(TomlUtils.Serialize(table))!
            : new T();
    }
}
