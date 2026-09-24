using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;

namespace Moongate.Tools.Internal;

internal static class VerifyNuGetPackages
{
    private const string RepositoryUrl = "https://github.com/moongate-community/moongate";
    private const string IconHash = "44501F487EF8670A73FF709A8F78CE33154B8ED42F13245A579ECCd056BC4A0F";
    private static readonly Dictionary<string, string[]> InternalDependencies = new(StringComparer.Ordinal)
    {
        ["Moongate.Admin.Contracts"] = [],
        ["Moongate.Core"] = [],
        ["Moongate.Network"] = [],
        ["Moongate.Network.Packets"] = ["Moongate.Core"],
        ["Moongate.Persistence"] = ["Moongate.Core", "Moongate.Persistence.Migrations"],
        ["Moongate.Persistence.Migrations"] = [],
        ["Moongate.Scripting"] = ["Moongate.Core", "Moongate.Server.Core"],
        ["Moongate.Server.Core"] = ["Moongate.Core", "Moongate.Network", "Moongate.Network.Packets"],
        ["Moongate.Ultima"] = []
    };

    private static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("Usage: VerifyNuGetPackages <repository-root> <package-directory>");
            return 2;
        }

        try
        {
            var repository = Path.GetFullPath(args[0]);
            var packages = Path.GetFullPath(args[1]);
            var version = XDocument.Load(Path.Combine(repository, "Directory.Build.props"))
                .Descendants("Version").Single().Value;
            var expectedFiles = InternalDependencies.Keys.SelectMany(id => new[]
            {
                $"{id}.{version}.nupkg", $"{id}.{version}.snupkg"
            }).ToHashSet(StringComparer.Ordinal);
            var actualFiles = Directory.EnumerateFiles(packages)
                .Where(path => path.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase)
                               || path.EndsWith(".snupkg", StringComparison.OrdinalIgnoreCase))
                .Select(path => Path.GetFileName(path)!).ToHashSet(StringComparer.Ordinal);
            Require(actualFiles.SetEquals(expectedFiles), $"Package set differs from the {InternalDependencies.Count} expected libraries and symbols."
                + $" Missing: {string.Join(", ", expectedFiles.Except(actualFiles))}."
                + $" Unexpected: {string.Join(", ", actualFiles.Except(expectedFiles))}.");

            foreach (var id in InternalDependencies.Keys)
            {
                try
                {
                    VerifyPackage(repository, packages, id, version);
                }
                catch (Exception exception)
                {
                    throw new InvalidDataException($"{id}: {exception.Message}", exception);
                }
                Console.WriteLine($"PASS {id}: metadata, contents, dependencies and portable symbols");
            }
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Package verification failed: {exception.Message}");
            return 1;
        }
    }

    private static void VerifyPackage(string repository, string packages, string id, string version)
    {
        using var archive = ZipFile.OpenRead(Path.Combine(packages, $"{id}.{version}.nupkg"));
        var metadata = ReadMetadata(archive);
        var ns = metadata.Name.Namespace;
        var project = XDocument.Load(Path.Combine(repository, "src", id, $"{id}.csproj"));
        Require(Value(metadata, "id") == id && Value(metadata, "version") == version, "Incorrect package identity.");
        Require(Value(metadata, "description") == project.Descendants("Description").Single().Value,
            "Description does not match the project.");
        Require(Value(metadata, "authors") == "Squid Development", "Incorrect authors.");
        var tags = Value(metadata, "tags").Split([';', ' '], StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        Require(tags.Count > 0 && tags.SetEquals(project.Descendants("PackageTags").Single().Value.Split(';')),
            "Incorrect package tags.");
        Require(Value(metadata, "license") == "AGPL-3.0-or-later"
                && (string?)metadata.Element(ns + "license")?.Attribute("type") == "expression", "Incorrect license.");
        Require(Value(metadata, "projectUrl") == RepositoryUrl, "Incorrect project URL.");
        var source = metadata.Element(ns + "repository");
        Require((string?)source?.Attribute("url") == RepositoryUrl
                && (string?)source?.Attribute("type") == "git", "Incorrect repository metadata.");
        var commit = (string?)source?.Attribute("commit");
        Require(commit is { Length: 40 } && commit.All(Uri.IsHexDigit), "Missing repository commit.");

        Require(Value(metadata, "readme") == "README.md", "Missing README metadata.");
        Require(ReadEntry(archive, "README.md").SequenceEqual(File.ReadAllBytes(Path.Combine(repository, "src", id, "README.md"))),
            "README does not match the library documentation.");
        Require(Value(metadata, "icon") == "moongate_logo.png", "Missing icon metadata.");
        Require(Convert.ToHexString(SHA256.HashData(ReadEntry(archive, "moongate_logo.png")))
            .Equals(IconHash, StringComparison.OrdinalIgnoreCase), "Icon does not match the original Moongate logo.");

        var assemblyPath = $"lib/net10.0/{id}.dll";
        var assembly = ReadEntry(archive, assemblyPath);
        Require(assembly.Length > 0 && ReadEntry(archive, $"lib/net10.0/{id}.xml").Length > 0,
            "Missing assembly or XML documentation.");
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "README.md", "moongate_logo.png", assemblyPath, $"lib/net10.0/{id}.xml", $"lib/net10.0/{id}.pdb"
        };
        if (id == "Moongate.Admin.Contracts")
        {
            foreach (var name in new[] { "common", "auth", "server", "accounts" })
            {
                var path = $"proto/moongate/admin/v1/{name}.proto";
                allowed.Add(path);
                Require(ReadEntry(archive, path).SequenceEqual(File.ReadAllBytes(Path.Combine(repository, "src", id, path))),
                    $"Missing or changed portable contract: {path}");
            }
        }
        VerifyPayload(archive, id, allowed);
        VerifyDependencies(metadata, project, id, version);

        using var symbols = ZipFile.OpenRead(Path.Combine(packages, $"{id}.{version}.snupkg"));
        var symbolMetadata = ReadMetadata(symbols);
        Require(Value(symbolMetadata, "id") == id && Value(symbolMetadata, "version") == version,
            "Incorrect symbol package identity.");
        var pdbPath = $"lib/net10.0/{id}.pdb";
        VerifyPayload(symbols, id, new HashSet<string>(StringComparer.Ordinal) { pdbPath });
        VerifyPortablePdb(ReadEntry(symbols, pdbPath), assembly, commit!);
    }

    private static void VerifyDependencies(XElement metadata, XDocument project, string id, string version)
    {
        var ns = metadata.Name.Namespace;
        var groups = metadata.Element(ns + "dependencies")?.Elements(ns + "group").ToArray() ?? [];
        Require(groups.Length == 1 && (string?)groups[0].Attribute("targetFramework") == "net10.0",
            "Expected one net10.0 dependency group.");
        var dependencies = groups[0].Elements(ns + "dependency").ToDictionary(
            dependency => (string?)dependency.Attribute("id") ?? throw new InvalidDataException("Dependency without ID."),
            dependency => (string?)dependency.Attribute("version") ?? throw new InvalidDataException("Dependency without version."),
            StringComparer.Ordinal);
        var expected = InternalDependencies[id].ToDictionary(name => name, _ => version, StringComparer.Ordinal);
        foreach (var reference in project.Descendants("PackageReference"))
        {
            var privateAssets = (string?)reference.Attribute("PrivateAssets") ?? (string?)reference.Element("PrivateAssets");
            if (privateAssets?.Split(';').Any(asset => asset.Trim().Equals("all", StringComparison.OrdinalIgnoreCase)) == true)
            {
                continue;
            }
            var name = (string?)reference.Attribute("Include");
            if (name is not null)
            {
                expected.Add(name, (string?)reference.Attribute("Version") ?? reference.Element("Version")!.Value);
            }
        }
        Require(expected.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(dependencies.Keys), "Incorrect direct dependencies.");
        foreach (var (name, expectedVersion) in expected)
        {
            var actual = dependencies[name];
            Require(actual == expectedVersion || actual == $"[{expectedVersion},)" || actual == $"[{expectedVersion}]",
                $"Incorrect dependency version for {name}: {actual}.");
        }
    }

    private static void VerifyPortablePdb(byte[] pdb, byte[] assembly, string commit)
    {
        using var pdbStream = new MemoryStream(pdb, writable: false);
        using var provider = MetadataReaderProvider.FromPortablePdbStream(pdbStream);
        var reader = provider.GetMetadataReader();
        Require(reader.Documents.Count > 0, "Portable PDB has no source documents.");
        using var assemblyStream = new MemoryStream(assembly, writable: false);
        using var pe = new PEReader(assemblyStream);
        var codeView = pe.ReadDebugDirectory().Single(entry => entry.Type == DebugDirectoryEntryType.CodeView);
        var pdbId = new BlobContentId(reader.DebugMetadataHeader!.Id);
        Require(pe.ReadCodeViewDebugDirectoryData(codeView).Guid == pdbId.Guid && codeView.Stamp == pdbId.Stamp,
            "Portable PDB does not belong to the packaged assembly.");

        var sourceLinkId = new Guid("CC110556-A091-4D38-9FEC-25AB9A351A6A");
        var sourceLink = reader.CustomDebugInformation.Select(reader.GetCustomDebugInformation)
            .Single(info => reader.GetGuid(info.Kind) == sourceLinkId);
        using var document = JsonDocument.Parse(reader.GetBlobBytes(sourceLink.Value));
        var mappings = document.RootElement.GetProperty("documents").EnumerateObject().ToArray();
        var prefix = $"https://raw.githubusercontent.com/moongate-community/moongate/{commit}/";
        Require(mappings.Length > 0 && mappings.All(mapping =>
            mapping.Value.GetString()?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true),
            "SourceLink must target the repository commit recorded in the nuspec.");
    }

    private static void VerifyPayload(ZipArchive archive, string id, HashSet<string> allowed)
    {
        Require(archive.Entries.Select(entry => entry.FullName).Distinct(StringComparer.Ordinal).Count() == archive.Entries.Count,
            "Duplicate archive entries.");
        foreach (var entry in archive.Entries)
        {
            Require(allowed.Contains(entry.FullName) || entry.FullName == $"{id}.nuspec"
                    || entry.FullName is "[Content_Types].xml" or "_rels/.rels"
                    || entry.FullName.StartsWith("package/services/metadata/core-properties/", StringComparison.Ordinal),
                $"Unexpected payload: {entry.FullName}.");
        }
    }

    private static XElement ReadMetadata(ZipArchive archive)
    {
        var entry = archive.Entries.Single(item => item.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
        using var stream = entry.Open();
        var root = XDocument.Load(stream).Root ?? throw new InvalidDataException("Empty nuspec.");
        return root.Element(root.Name.Namespace + "metadata") ?? throw new InvalidDataException("Missing nuspec metadata.");
    }

    private static string Value(XElement metadata, string name)
    {
        return metadata.Element(metadata.Name.Namespace + name)?.Value
            ?? throw new InvalidDataException($"Missing metadata: {name}.");
    }

    private static byte[] ReadEntry(ZipArchive archive, string name)
    {
        var entry = archive.GetEntry(name) ?? throw new InvalidDataException($"Missing entry: {name}.");
        using var source = entry.Open();
        using var buffer = new MemoryStream();
        source.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidDataException(message);
        }
    }
}
