using System.Buffers.Binary;

namespace Moongate.Ultima.Io;

/// <summary>
/// Reads the UO client version from a <c>client.exe</c> by locating the Win32 <c>VS_VERSION_INFO</c>
/// resource and decoding its fixed version fields. Returns a plain <see cref="Version" /> so the reader
/// stays free of any Moongate dependency.
/// </summary>
public static class ClientVersionReader
{
    // "VS_VERSION_INFO" in UTF-16 (15 chars x 2 bytes).
    private static ReadOnlySpan<byte> VersionInfoSignature
        =>
        [
            0x56, 0x00, 0x53, 0x00, 0x5F, 0x00, 0x56, 0x00,
            0x45, 0x00, 0x52, 0x00, 0x53, 0x00, 0x49, 0x00,
            0x4F, 0x00, 0x4E, 0x00, 0x5F, 0x00, 0x49, 0x00,
            0x4E, 0x00, 0x46, 0x00, 0x4F, 0x00
        ];

    // Signature (30 bytes) + 12 bytes to the fixed version fields.
    private const int VersionFieldOffset = 42;

    /// <summary>
    /// Reads the client version from the <c>client.exe</c> located in the UO client directory
    /// configured on <see cref="Files" />.
    /// </summary>
    /// <returns>
    /// The parsed version, or <c>null</c> when <c>client.exe</c> (or its version resource) is not found.
    /// </returns>
    public static Version? Read()
    {
        var path = Files.GetFilePath("client.exe");

        return path is null ? null : ReadFromFile(path);
    }

    /// <summary>Reads the client version from a <c>client.exe</c> file.</summary>
    /// <param name="path">Path to the executable.</param>
    /// <returns>The parsed version, or <c>null</c> when the version resource is not found.</returns>
    public static Version? ReadFromFile(string path)
    {
        var bytes = File.ReadAllBytes(path);

        return TryRead(bytes, out var version) ? version : null;
    }

    /// <summary>Parses the client version from the raw bytes of a <c>client.exe</c>.</summary>
    /// <param name="exeContent">The full contents of the executable.</param>
    /// <param name="version">The parsed version on success; otherwise <c>0.0</c>.</param>
    /// <returns><c>true</c> if the version resource was found and decoded.</returns>
    public static bool TryRead(ReadOnlySpan<byte> exeContent, out Version version)
    {
        version = new(0, 0);

        var index = exeContent.IndexOf(VersionInfoSignature);

        if (index < 0 || index + VersionFieldOffset + 8 > exeContent.Length)
        {
            return false;
        }

        var fields = exeContent[(index + VersionFieldOffset)..];

        var minor = BinaryPrimitives.ReadUInt16LittleEndian(fields);
        var major = BinaryPrimitives.ReadUInt16LittleEndian(fields[2..]);
        var revision = BinaryPrimitives.ReadUInt16LittleEndian(fields[4..]);
        var build = BinaryPrimitives.ReadUInt16LittleEndian(fields[6..]);

        version = new(major, minor, build, revision);

        return true;
    }
}
