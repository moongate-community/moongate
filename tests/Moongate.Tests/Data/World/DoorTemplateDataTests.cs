using Moongate.Server.Loaders;
using Moongate.Server.Services.Items;
using Moongate.UO.Data.World;
using SquidStd.Core.Directories;

namespace Moongate.Tests.Data.World;

/// <summary>
/// Holds <see cref="DoorTemplates" /> against the templates that actually ship. The map is only true
/// while those 13 exist and still carry a door script: renaming one, or dropping its
/// <c>ScriptId</c>, turns 944 doors back into scenery without breaking a build or failing a
/// placement test — this is what notices.
/// </summary>
[Collection("ItemTemplateSeeding")]
public class DoorTemplateDataTests
{
    [Fact]
    public async Task EveryMappedTemplate_ShipsAndCarriesTheDoorScript()
    {
        var root = Path.Combine(Path.GetTempPath(), "mg-doors-" + Guid.NewGuid().ToString("N"));
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        var templates = new ItemTemplateService();

        try
        {
            await new ItemTemplatesLoader(templates, directories).LoadAsync();

            foreach (var templateId in DoorTemplates.All)
            {
                var template = templates.GetById(templateId);

                Assert.True(template is not null, $"Door template '{templateId}' does not ship.");
                Assert.Equal("items.door", template!.ScriptId);

                // The template's own graphic is the class's base, which is the whole basis of the
                // script's arithmetic: a zero here would make every facing derive from nothing.
                Assert.True(template.ItemId > 0, $"Door template '{templateId}' has no base graphic.");
            }
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
