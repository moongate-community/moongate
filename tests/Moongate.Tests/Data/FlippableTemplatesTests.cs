using Moongate.Server.Loaders;
using Moongate.Server.Services.Items;
using SquidStd.Core.Directories;

namespace Moongate.Tests.Data;

[Collection("ItemTemplateSeeding")]
public class FlippableTemplatesTests
{
    [Fact]
    public async Task EveryFlippableList_IsSelfConsistentAndPortAddedSome()
    {
        var root = Path.Combine(Path.GetTempPath(), "mg-flippable-" + Guid.NewGuid().ToString("N"));
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        var templates = new ItemTemplateService();

        try
        {
            await new ItemTemplatesLoader(templates, directories).LoadAsync();

            var withFlip = templates.All
                .Where(t => t.FlippableItemIds is { Count: > 0 })
                .ToList();

            // The port added flip data well beyond the 3 hand-authored files.
            Assert.True(withFlip.Count > 20, $"Expected many flippable templates, got {withFlip.Count}");

            foreach (var t in withFlip)
            {
                Assert.True(t.FlippableItemIds!.Count >= 2, $"{t.Id} has a single-variant flip list");
                Assert.Contains(t.ItemId, t.FlippableItemIds);
            }

            // Hand-authored armoire pair preserved.
            var armoire = templates.All.First(t => t.FlippableItemIds is { } f && f.Contains(2639) && f.Contains(2643));
            Assert.Equal(2, armoire.FlippableItemIds!.Count);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
