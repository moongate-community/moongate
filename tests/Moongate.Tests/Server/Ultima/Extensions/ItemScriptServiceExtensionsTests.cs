using Moongate.Scripting.Data.Scripts;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Tests.TestSupport.Ultima.Items;

namespace Moongate.Tests.Server.Ultima.Extensions;

public sealed class ItemScriptServiceExtensionsTests
{
    private readonly ItemEntity _item = new() { Id = new(0x40000001), TemplateId = "item", ItemId = 0x0F52, Amount = 1 };

    [Fact]
    public void Allows_OnlyFalseRefuses()
    {
        var scripts = new RecordingItemScriptService();
        scripts.Scripted.Add("item");

        Assert.True(scripts.Allows(_item, "can_equip", 2L));

        scripts.Result = ScriptResult.Completed([true]);
        Assert.True(scripts.Allows(_item, "can_equip", 2L));

        scripts.Result = ScriptResult.Completed([null]);
        Assert.True(scripts.Allows(_item, "can_equip", 2L));

        scripts.Result = ScriptResult.Suspended;
        Assert.True(scripts.Allows(_item, "can_equip", 2L));

        scripts.Result = ScriptResult.Completed([false]);
        Assert.False(scripts.Allows(_item, "can_equip", 2L));
        Assert.Equal("0x40000001 can_equip 2", scripts.Calls[^1]);
    }

    [Fact]
    public void Allows_AnItemWithoutAScriptOrNoScriptService_IsAllowed()
    {
        var scripts = new RecordingItemScriptService { Result = ScriptResult.Completed([false]) };

        Assert.True(scripts.Allows(_item, "can_equip", 2L));
        Assert.True(((IItemScriptService?)null).Allows(_item, "can_equip", 2L));
        Assert.Empty(scripts.Calls);
    }
}
