using System.Collections.Concurrent;
using System.Text.Json;
using Tomlyn;
using Tomlyn.Serialization;

namespace Moongate.Core.Utils;

/// <summary>
/// Serializes TOML documents and reads or writes UTF-8 configuration files using Tomlyn.
/// </summary>
/// <remarks>
/// When options are omitted, property names use snake_case, plus whatever converters were added with
/// <see cref="AddTomlConverter" />. Explicit options replace these defaults for the current call and are
/// never affected by converter registration. Serialization, parsing and file-system exceptions propagate
/// to the caller.
/// </remarks>
public static class TomlUtils
{
    private static readonly ConcurrentBag<TomlConverter> Converters = new();
    private static readonly Lock RebuildLock = new();

    private static volatile TomlSerializerOptions _defaultOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    /// <summary>
    /// Adds a TOML converter to every call that does not pass its own options. Thread-safe; a second
    /// converter of the same type is ignored.
    /// </summary>
    public static void AddTomlConverter(TomlConverter converter)
    {
        ArgumentNullException.ThrowIfNull(converter);

        var converterType = converter.GetType();

        if (Converters.Any(existing => existing.GetType() == converterType))
        {
            return;
        }

        Converters.Add(converter);
        RebuildDefaultOptions();
    }

    /// <summary>
    /// Deserializes a TOML document, including an empty document, into the requested type.
    /// </summary>
    public static T? Deserialize<T>(string toml, TomlSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(toml);

        return TomlSerializer.Deserialize<T>(toml, options ?? _defaultOptions);
    }

    /// <summary>
    /// Reads a UTF-8 TOML file and deserializes it into the requested type.
    /// </summary>
    public static T? DeserializeFromFile<T>(string filePath, TomlSerializerOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        return Deserialize<T>(File.ReadAllText(filePath), options);
    }

    /// <summary>
    /// Asynchronously reads a UTF-8 TOML file and deserializes it into the requested type.
    /// </summary>
    /// <remarks>
    /// Deserialization is synchronous. Cancellation is checked before reading and before deserialization.
    /// </remarks>
    public static async Task<T?> DeserializeFromFileAsync<T>(
        string filePath,
        TomlSerializerOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        cancellationToken.ThrowIfCancellationRequested();
        var toml = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        return Deserialize<T>(toml, options);
    }

    /// <summary>Gets the converters currently added with <see cref="AddTomlConverter" />.</summary>
    public static IReadOnlyList<TomlConverter> GetTomlConverters()
    {
        var snapshot = new TomlConverter[Converters.Count];
        Converters.CopyTo(snapshot, 0);

        return Array.AsReadOnly(snapshot);
    }

    /// <summary>Removes every added converter of type <typeparamref name="T" />. Thread-safe.</summary>
    /// <returns>True if a converter was removed.</returns>
    public static bool RemoveTomlConverter<T>() where T : TomlConverter
    {
        var removed = false;
        var kept = new ConcurrentBag<TomlConverter>();

        foreach (var converter in Converters)
        {
            if (converter is T)
            {
                removed = true;
            }
            else
            {
                kept.Add(converter);
            }
        }

        if (!removed)
        {
            return false;
        }

        Converters.Clear();

        foreach (var converter in kept)
        {
            Converters.Add(converter);
        }

        RebuildDefaultOptions();

        return true;
    }

    /// <summary>
    /// Serializes a non-null value into a TOML document.
    /// </summary>
    public static string Serialize<T>(T value, TomlSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(value);

        return TomlSerializer.Serialize(value, options ?? _defaultOptions);
    }

    /// <summary>
    /// Serializes a value and overwrites a UTF-8 file without a BOM, creating missing parent directories.
    /// </summary>
    /// <remarks>
    /// Serialization completes before the file system is changed. The file write is not atomic.
    /// </remarks>
    public static void SerializeToFile<T>(T value, string filePath, TomlSerializerOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var fullPath = Path.GetFullPath(filePath);
        var toml = Serialize(value, options);

        CreateParentDirectory(fullPath);
        File.WriteAllText(fullPath, toml);
    }

    /// <summary>
    /// Serializes a value and asynchronously overwrites a UTF-8 file without a BOM, creating missing parent directories.
    /// </summary>
    /// <remarks>
    /// Serialization is synchronous and completes before the file system is changed.
    /// A pre-canceled token prevents file-system changes. Writes are not atomic; cancellation or an I/O failure
    /// during writing can leave a partial file.
    /// </remarks>
    public static async Task SerializeToFileAsync<T>(
        T value,
        string filePath,
        TomlSerializerOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = Path.GetFullPath(filePath);
        var toml = Serialize(value, options);

        cancellationToken.ThrowIfCancellationRequested();
        CreateParentDirectory(fullPath);
        await File.WriteAllTextAsync(fullPath, toml, cancellationToken).ConfigureAwait(false);
    }

    private static void CreateParentDirectory(string fullPath)
    {
        var directory = Path.GetDirectoryName(fullPath);

        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static void RebuildDefaultOptions()
    {
        lock (RebuildLock)
        {
            _defaultOptions = new()
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                Converters = Converters.ToArray()
            };
        }
    }
}
