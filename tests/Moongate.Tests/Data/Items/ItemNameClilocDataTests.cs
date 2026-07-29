using Moongate.Server.Loaders;
using Moongate.Server.Services.Items;
using Moongate.Tests.Support;
using Moongate.Ultima.Io;
using Moongate.Ultima.Localization;
using Moongate.UO.Data.Items;
using SquidStd.Core.Directories;

namespace Moongate.Tests.Data.Items;

/// <summary>
/// The shipped templates against a real client. These skip where there is no client directory, so
/// they are a developer's check rather than a CI gate.
/// </summary>
[Collection("UltimaClientData")]
public class ItemNameClilocDataTests
{
    [ClientFilesFact]
    public async Task EveryTemplateWithNoName_HasAClilocToFallBackOn()
    {
        var (templates, cliloc) = await Load();

        var nameless = templates.All
                                .Where(template => string.IsNullOrWhiteSpace(template.Name))
                                .Where(template => !cliloc.ContainsKey(ItemClilocs.ForItemId(template.ItemId)))
                                .Select(template => template.Id)
                                .ToList();

        Assert.True(nameless.Count == 0, $"Nameless templates with no cliloc: {string.Join(", ", nameless)}");
    }

    // Keeps redundant names from creeping back as content grows.
    [ClientFilesFact]
    public async Task NoTemplate_RepeatsWhatTheClientAlreadySays()
    {
        var (templates, cliloc) = await Load();

        var redundant = templates.All
                                 .Where(template => !string.IsNullOrWhiteSpace(template.Name))
                                 .Where(
                                     template =>
                                         cliloc.TryGetValue(ItemClilocs.ForItemId(template.ItemId), out var text) &&
                                         Normalize(template.Name) == Normalize(text)
                                 )
                                 .Select(template => template.Id)
                                 .ToList();

        Assert.True(
            redundant.Count == 0,
            $"Templates naming what the client already names: {string.Join(", ", redundant)}"
        );
    }

    private static string Normalize(string value)
        => new(
            value.Replace('_', ' ')
                 .ToLowerInvariant()
                 .Where(character => char.IsLetterOrDigit(character) || character == ' ')
                 .ToArray()
        );

    private static async Task<(ItemTemplateService Templates, Dictionary<int, string> Cliloc)> Load()
    {
        Files.SetDirectory(ClientFiles.Directory);

        var cliloc = new Dictionary<int, string>();

        foreach (var entry in new StringList("enu", false).Entries)
        {
            cliloc[entry.Number] = entry.Text;
        }

        // Seeds the embedded assets into a temp root, so this reads the templates as shipped rather
        // than whatever a developer's runtime directory happens to hold.
        var root = Path.Combine(Path.GetTempPath(), "mg-names-" + Guid.NewGuid().ToString("N"));
        var templates = new ItemTemplateService();

        await new ItemTemplatesLoader(templates, new DirectoriesConfig(root, [])).LoadAsync();

        Directory.Delete(root, true);

        return (templates, cliloc);
    }
}
