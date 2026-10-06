using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Speech;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class ItemHearingServiceTests
{
    private readonly SectorService _sectors = TestSectors.Create();
    private readonly ItemService _items;
    private readonly RecordingItemScriptService _scripts = new();
    private readonly ItemHearingService _hearing;

    private readonly MobileEntity _aria = new()
    {
        Id = new Serial(2), Map = MapType.Trammel, Location = new Point3D(1600, 1600, 0)
    };

    public ItemHearingServiceTests()
    {
        _items = TestItems.Create(_sectors);
        _scripts.Scripted.Add("keyword_teleporter");
        _hearing = new(_sectors, _scripts);
    }

    [Fact]
    public void Heard_AScriptedItemInRange_RunsItsOnSpeechWithTheSpeakerAndTheText()
    {
        Ground(0x40000010, 1600 + 15, 1600);

        _hearing.Heard(_aria, "om om om", [0x3B]);

        Assert.StartsWith("0x40000010 on_speech 2 om om om", Assert.Single(_scripts.Calls));
    }

    [Fact]
    public void Heard_ItemsTooFarOrWithoutAScript_AreLeftAlone()
    {
        Ground(0x40000010, 1600 + 15 + 1, 1600);
        Ground(0x40000011, 1600, 1600, "gold");

        _hearing.Heard(_aria, "om om om");

        Assert.Empty(_scripts.Calls);
    }

    [Theory]
    [InlineData(SpeechType.Whisper, 1)]
    [InlineData(SpeechType.Emote, 15)]
    [InlineData(SpeechType.Yell, 18)]
    public void Heard_AWhisperAnEmoteOrAYell_ReachesTheItemsWithinItsOwnRange_WithItsType(SpeechType type, int range)
    {
        Ground(0x40000010, 1600 + range, 1600);
        Ground(0x40000011, 1601 + range, 1600);

        _hearing.Heard(_aria, "om", null, type);

        var call = Assert.Single(_scripts.Calls);
        Assert.StartsWith("0x40000010 on_speech 2 om", call);
        Assert.EndsWith($" {(long)type}", call);
    }

    private void Ground(uint serial, int x, int y, string template = "keyword_teleporter")
    {
        var item = new ItemEntity { Id = new Serial(serial), TemplateId = template, ItemId = 0x1BC3, Amount = 1 };
        _items.Add([item]);
        _items.PlaceOnGround(item, MapType.Trammel, new Point3D(x, y, 0));
    }
}
