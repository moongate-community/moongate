using System.Text.Json;
using Tomlyn;

namespace Moongate.Core.Utils;

/// <summary>
/// Serializes TOML documents and reads or writes UTF-8 configuration files using Tomlyn.
/// </summary>
/// <remarks>
/// When options are omitted, property names use snake_case. Explicit options replace these defaults for the current call.
/// Serialization, parsing and file-system exceptions propagate to the caller.
/// </remarks>
public static class TomlUtils
{
    private static readonly TomlSerializerOptions DefaultOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    /// <summary>
    /// Serializes a non-null value into a TOML document.
    /// </summary>
    public static string Serialize<T>(T value, TomlSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(value);

        return TomlSerializer.Serialize(value, options ?? DefaultOptions);
    }

    /// <summary>
    /// Deserializes a TOML document, including an empty document, into the requested type.
    /// </summary>
    public static T? Deserialize<T>(string toml, TomlSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(toml);

        return TomlSerializer.Deserialize<T>(toml, options ?? DefaultOptions);
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
    /// Reads a UTF-8 TOML file and deserializes it into the requested type.
    /// </summary>
    public static T? DeserializeFromFile<T>(string filePath, TomlSerializerOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        return Deserialize<T>(File.ReadAllText(filePath), options);
    }

    /// <summary>
    /// Serializes a value and asynchronously overwrites a UTF-8 file without a BOM, creating missing parent directories.
    /// </summary>
    /// <remarks>
    /// Serialization is synchronous and completes before the file system is changed.
    /// A pre-canceled token prevents file-system changes. Writes are not atomic; cancellation or an I/O failure
    /// during writing can leave a partial file.
    /// </remarks>
    public static async Task SerializeToFileAsync<T>(T value, string filePath, TomlSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = Path.GetFullPath(filePath);
        var toml = Serialize(value, options);

        cancellationToken.ThrowIfCancellationRequested();
        CreateParentDirectory(fullPath);
        await File.WriteAllTextAsync(fullPath, toml, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously reads a UTF-8 TOML file and deserializes it into the requested type.
    /// </summary>
    /// <remarks>
    /// Deserialization is synchronous. Cancellation is checked before reading and before deserialization.
    /// </remarks>
    public static async Task<T?> DeserializeFromFileAsync<T>(string filePath, TomlSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        cancellationToken.ThrowIfCancellationRequested();
        var toml = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        return Deserialize<T>(toml, options);
    }

    private static void CreateParentDirectory(string fullPath)
    {
        var directory = Path.GetDirectoryName(fullPath);
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }
    }
}
