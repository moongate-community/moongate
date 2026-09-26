using Lua;
using Lua.Standard;
using Moongate.Scripting.Binding;
using Moongate.Scripting.Internal;
using Moongate.Scripting.Utils;
using Moongate.Server.Core.Data.Config;
using Moongate.Server.Ultima.Data.Messages;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Loaders;

namespace Moongate.Tests.Server.Ultima.Modules;

public sealed class LocalizationModuleTests
{
    [Fact]
    public void Get_FillsInValuesFromLua()
    {
        var result = Run("return localization.get(0), localization.get(1, 'Bob', 'un drago'), localization.get(2, 31)");

        Assert.Equal("Sali a bordo della barca.", result[0].Read<string>());
        Assert.Equal("Bob è stato ucciso da un drago!", result[1].Read<string>());
        Assert.Equal("{tag} 0x1f", result[2].Read<string>());
    }

    [Fact]
    public void Get_UnknownId_RaisesALuaError()
    {
        var result = Run("local ok, err = pcall(localization.get, 99) return ok, err");

        Assert.False(result[0].Read<bool>());
        Assert.Contains("No message has id 99", result[1].Read<string>());
    }

    [Fact]
    public void TextAndLanguage_ReturnTheRawTextNilAndTheCode()
    {
        var result = Run("return localization.text(2), localization.text(99), localization.language()");

        Assert.Equal("{{tag}} 0x{0:x}", result[0].Read<string>());
        Assert.Equal(LuaValue.Nil, result[1]);
        Assert.Equal("ita", result[2].Read<string>());
    }

    [Fact]
    public void Definitions_MarkTextAsMaybeNil()
    {
        using var state = LuaState.Create();
        var bound = new LuaModuleBinder(NoThreadGuard.Instance).Bind(state, CreateModule());

        var text = LuaDefinitionsGenerator.Render([bound], []);

        Assert.Contains("---@param id integer\n---@return string?\nfunction localization.text(id) end", text, StringComparison.Ordinal);
        Assert.Contains("---@param ... any\n---@return string\nfunction localization.get(id, ...) end", text, StringComparison.Ordinal);
    }

    private static LuaValue[] Run(string chunk)
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        new LuaModuleBinder(NoThreadGuard.Instance).Bind(state, CreateModule());

        return SyncValueTask.Run(state.DoStringAsync(chunk, "t"));
    }

    private static LocalizationModule CreateModule()
    {
        var dataLoaderService = new StubDataLoaderService().With(
            new MessageContent { Id = 0, Text = "Sali a bordo della barca." },
            new MessageContent { Id = 1, Text = "{0} è stato ucciso da {1}!" },
            new MessageContent { Id = 2, Text = "{{tag}} 0x{0:x}" }
        );

        return new(new LocalizationService(new LocalizationConfig { Language = "ita" }, dataLoaderService));
    }
}
