using System.Text;
using System.Text.Json;

namespace Moongate.Tests.Server.FileLoaders;

public sealed class MobileTemplateRepositoryIntegrityTests
{
    [Test]
    public void RepositoryMobileTemplates_ShouldNotContainDuplicateIdsAcrossFiles()
    {
        var repositoryRoot = GetRepositoryRoot();
        var mobilesRoot = Path.Combine(repositoryRoot, "moongate_data", "templates", "mobiles");
        var pathsById = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var filePath in Directory.EnumerateFiles(mobilesRoot, "*.json", SearchOption.AllDirectories))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(filePath));

            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (!element.TryGetProperty("type", out var typeProperty) ||
                    !string.Equals(typeProperty.GetString(), "mobile", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!element.TryGetProperty("id", out var idProperty))
                {
                    continue;
                }

                var id = idProperty.GetString();
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                if (!pathsById.TryGetValue(id, out var paths))
                {
                    paths = [];
                    pathsById[id] = paths;
                }

                paths.Add(Path.GetRelativePath(repositoryRoot, filePath));
            }
        }

        var duplicates = pathsById
            .Where(static pair => pair.Value.Count > 1)
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .ToList();

        if (duplicates.Count == 0)
        {
            return;
        }

        var message = new StringBuilder();
        foreach (var duplicate in duplicates)
        {
            message.AppendLine(duplicate.Key);
            foreach (var path in duplicate.Value)
            {
                message.Append("  ").AppendLine(path);
            }
        }

        Assert.Fail(message.ToString());
    }

    private static string GetRepositoryRoot()
    {
        return Path.GetFullPath(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..")
        );
    }
}
