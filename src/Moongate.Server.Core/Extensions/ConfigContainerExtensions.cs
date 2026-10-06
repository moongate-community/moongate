using DryIoc;
using Moongate.Core.Utils;
using Moongate.Server.Core.Data.Config;
using Moongate.Server.Core.Interfaces.Config;
using Serilog;
using Tomlyn.Model;

namespace Moongate.Server.Core.Extensions;

/// <summary>
///     Lets a plugin own a section of <c>config/moongate.toml</c>.
/// </summary>
public static class ConfigContainerExtensions
{
    extension(Container container)
    {
        /// <summary>
        ///     Reads the <c>[section]</c> table of the configuration file into <typeparamref name="T" />, validates it
        ///     when it is an <see cref="IConfigSection" />, registers it as a singleton and returns it. A missing section
        ///     gets the defaults of <typeparamref name="T" />, which are appended to the end of the file so operators
        ///     see them; the rest of the file is left as it is.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        ///     The host or another plugin already owns <paramref name="section" />, or the section is not valid.
        /// </exception>
        public T AddConfig<T>(string section)
            where T : class, new()
        {
            ArgumentNullException.ThrowIfNull(container);
            ArgumentException.ThrowIfNullOrWhiteSpace(section);

            var document = container.Resolve<ServerConfigDocument>();

            if (!document.TryReserve(section))
            {
                throw new InvalidOperationException(
                    $"The configuration section [{section}] is already owned by the server or by another plugin."
                );
            }

            T config;

            if (document.Table.TryGetValue(section, out var value))
            {
                // Appending [section] next to a key of another kind would define it twice and break the next start.
                if (value is not TomlTable table)
                {
                    throw new InvalidOperationException(
                        $"'{document.FilePath}': [{section}] must be a table, found {value?.GetType().Name ?? "nothing"}."
                    );
                }

                config = TomlUtils.Deserialize<T>(TomlUtils.Serialize(table)) ?? new T();
            }
            else
            {
                config = new();
                Append(document, section, config);
            }

            if (config is IConfigSection validated)
            {
                validated.Validate();
            }

            container.RegisterInstance(config);

            return config;
        }
    }

    private static void Append<T>(ServerConfigDocument document, string section, T config)
        where T : class
    {
        var toml = TomlUtils.Serialize(new Dictionary<string, T> { [section] = config });

        try
        {
            var existing = File.Exists(document.FilePath) ? File.ReadAllText(document.FilePath) : "";
            var newline = existing.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var separator = existing.Length == 0 ? "" :
                existing.EndsWith('\n') ? newline : newline + newline;
            File.AppendAllText(document.FilePath, separator + toml.ReplaceLineEndings(newline));
            // the TOML was just produced by serializing a table, so it parses to a table.
            document.Table[section] = TomlUtils.Deserialize<TomlTable>(toml)![section];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.ForContext(typeof(ConfigContainerExtensions))
                .Warning(
                    exception,
                    "Could not add the [{Section}] section to {ConfigFile}; using its defaults",
                    section,
                    document.FilePath
                );
        }
    }
}
