using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Scripting.Items;
using Moongate.Server.Abstractions.Data.Internal;
using Moongate.Server.Abstractions.Types.Items;
using MoonSharp.Interpreter;

namespace Moongate.Tests.Scripting;

public class LuaItemScriptRuntimeTests
{
    private sealed class Fixture : IDisposable
    {
        private readonly string _root;

        public Script Script { get; }

        public LuaItemScriptRuntime Runtime { get; }

        public Fixture()
        {
            _root = Path.Combine(Path.GetTempPath(), "mg-item-scripts-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_root, "scripts", "items"));

            Script = new();
            Runtime = new(Script, new(_root, ["scripts"]));
        }

        public void Dispose()
            => Directory.Delete(_root, true);

        /// <summary>Writes a script at a path relative to the scripts directory, without the extension.</summary>
        public void WriteScript(string relativePath, string body)
        {
            var path = Path.Combine(_root, "scripts", relativePath.Replace('/', Path.DirectorySeparatorChar) + ".lua");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, body);
        }
    }

    [Fact]
    public void HasHook_ReportsWhatTheScriptDefines()
    {
        using var fixture = new Fixture();
        fixture.WriteScript(
            "magic_torch",
            """
            local torch = { id = "magic_torch" }

            function torch.on_equip(ctx) end

            return torch
            """
        );

        Assert.True(fixture.Runtime.HasHook("magic_torch", ItemScriptHookType.Equipped));
        Assert.False(fixture.Runtime.HasHook("magic_torch", ItemScriptHookType.Unequipped));
    }

    [Fact]
    public void Invoke_CallsTheMatchingHookAndSeesTheItem()
    {
        using var fixture = new Fixture();
        fixture.WriteScript(
            "magic_torch",
            """
            local torch = { id = "magic_torch" }

            function torch.on_double_click(ctx)
                _G.seen_serial = ctx.item.serial
                _G.seen_name = ctx.item.name
                _G.seen_actor = ctx.actor.serial
            end

            return torch
            """
        );

        var item = new ItemEntity { Id = (Serial)11, TemplateId = "torch", ScriptId = "magic_torch", Name = "Torch" };
        var actor = new MobileEntity { Id = (Serial)7 };

        Assert.True(fixture.Runtime.Invoke(ItemScriptHookType.DoubleClick, ItemScriptContext.For(item, actor)));
        Assert.Equal(11d, fixture.Script.Globals.Get("seen_serial").Number);
        Assert.Equal("Torch", fixture.Script.Globals.Get("seen_name").String);
        Assert.Equal(7d, fixture.Script.Globals.Get("seen_actor").Number);
    }

    [Fact]
    public void Invoke_DottedScriptId_ResolvesAsAPathUnderScripts()
    {
        // Every shipped template uses this shape: items.light_source, items.food, items.beverage.
        using var fixture = new Fixture();
        fixture.WriteScript(
            "items/light_source",
            """
            local light = { id = "items.light_source" }

            function light.on_double_click(ctx)
                _G.lit = true
            end

            return light
            """
        );

        var item = new ItemEntity { Id = (Serial)11, ScriptId = "items.light_source" };

        Assert.True(fixture.Runtime.Invoke(ItemScriptHookType.DoubleClick, ItemScriptContext.For(item, null)));
        Assert.True(fixture.Script.Globals.Get("lit").Boolean);
    }

    [Fact]
    public void Invoke_DroppedHook_CarriesTheContainer()
    {
        using var fixture = new Fixture();
        fixture.WriteScript(
            "magic_torch",
            """
            local torch = { id = "magic_torch" }

            function torch.on_drop(ctx)
                _G.seen_container = ctx.container_id
            end

            return torch
            """
        );

        var item = new ItemEntity { Id = (Serial)11, ScriptId = "magic_torch" };
        var context = new ItemScriptContext(item, null, (Serial)99, null);

        Assert.True(fixture.Runtime.Invoke(ItemScriptHookType.Dropped, context));
        Assert.Equal(99d, fixture.Script.Globals.Get("seen_container").Number);
    }

    [Fact]
    public void Invoke_HookNotDefined_ReturnsFalse()
    {
        using var fixture = new Fixture();
        fixture.WriteScript("magic_torch", "return { id = \"magic_torch\" }");

        var item = new ItemEntity { Id = (Serial)11, ScriptId = "magic_torch" };

        Assert.False(fixture.Runtime.Invoke(ItemScriptHookType.DoubleClick, ItemScriptContext.For(item, null)));
    }

    [Theory, InlineData("../brains/guard"), InlineData(".."), InlineData("items..food"), InlineData(".items"),
     InlineData("items."), InlineData("Items.Food")]

    // a separator is never allowed
    // nor a bare parent
    // nor an empty segment
    // nor a leading dot
    // nor a trailing one
     // nor uppercase
    public void Invoke_InvalidScriptId_IsRefused(string scriptId)
    {
        // Nothing may resolve outside the scripts directory.
        using var fixture = new Fixture();

        var item = new ItemEntity { Id = (Serial)11, ScriptId = scriptId };

        Assert.False(fixture.Runtime.Invoke(ItemScriptHookType.DoubleClick, ItemScriptContext.For(item, null)));
    }

    [Fact]
    public void Invoke_MissingFile_ReturnsFalse()
    {
        using var fixture = new Fixture();

        var item = new ItemEntity { Id = (Serial)11, ScriptId = "does_not_exist" };

        Assert.False(fixture.Runtime.Invoke(ItemScriptHookType.DoubleClick, ItemScriptContext.For(item, null)));
    }

    [Fact]
    public void Invoke_NoScriptId_ReturnsFalseWithoutTouchingTheDisk()
    {
        using var fixture = new Fixture();

        var item = new ItemEntity { Id = (Serial)11, ScriptId = "" };

        Assert.False(fixture.Runtime.Invoke(ItemScriptHookType.DoubleClick, ItemScriptContext.For(item, null)));
    }

    [Fact]
    public void Invoke_ScriptIdNone_IsTreatedAsNoScript()
    {
        // "none" is what the shipped item templates carry, and it is not a script.
        using var fixture = new Fixture();

        var item = new ItemEntity { Id = (Serial)11, ScriptId = "none" };

        Assert.False(fixture.Runtime.Invoke(ItemScriptHookType.DoubleClick, ItemScriptContext.For(item, null)));
    }

    [Fact]
    public void Invoke_ScriptThatThrows_IsSwallowed()
    {
        using var fixture = new Fixture();
        fixture.WriteScript(
            "broken",
            """
            local broken = { id = "broken" }

            function broken.on_double_click(ctx)
                error("boom")
            end

            return broken
            """
        );

        var item = new ItemEntity { Id = (Serial)11, ScriptId = "broken" };

        // A content author's mistake must not take down the game loop.
        Assert.False(fixture.Runtime.Invoke(ItemScriptHookType.DoubleClick, ItemScriptContext.For(item, null)));
    }

    [Fact]
    public void Invoke_ShippedExample_IsSeededAndLoads()
    {
        using var fixture = new Fixture();

        // Nothing was written by the test: the runtime seeds the shipped scripts on first use.
        Assert.True(fixture.Runtime.HasHook("items.magic_torch", ItemScriptHookType.DoubleClick));
    }
}
