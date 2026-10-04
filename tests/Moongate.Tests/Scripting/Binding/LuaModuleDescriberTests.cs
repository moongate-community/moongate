using Moongate.Scripting.Binding;
using Moongate.Tests.TestSupport.Scripting;

namespace Moongate.Tests.Scripting.Binding;

public sealed class LuaModuleDescriberTests
{
    [Fact]
    public void Describe_NeedsNoInstance()
    {
        var module = LuaModuleDescriber.Describe(typeof(DependentModule));

        Assert.Equal("dependent", module.Name);
        Assert.Equal("Needs a service to exist.", module.HelpText);
        Assert.Equal(typeof(DependentModule), module.ModuleType);
        var function = Assert.Single(module.Functions);
        Assert.Equal("ask_twice", function.LuaName);
        Assert.Equal("Asks.", function.HelpText);
        Assert.Empty(module.Constants);
    }

    [Fact]
    public void Describe_NamesFunctionsInSnakeCase_OrAsTheAttributeSays_AndSkipsUnmarkedMethods()
    {
        var names = LuaModuleDescriber.Describe(typeof(ProbeModule)).Functions.Select(function => function.LuaName).ToList();

        Assert.Contains("add", names);
        Assert.Contains("next_colour", names);
        Assert.Contains("scale", names);
        Assert.DoesNotContain("multiply", names);
        Assert.DoesNotContain("not_exposed", names);
    }

    [Fact]
    public void Describe_ReadsConstantsWithTheirTypesAndValues()
    {
        var constants = LuaModuleDescriber.Describe(typeof(LimitsModule))
            .Constants
            .ToDictionary(constant => constant.LuaName, constant => (constant.Type, constant.Value));

        Assert.Equal((typeof(int), 250), constants["MAX_PLAYERS"]);
        Assert.Equal((typeof(ProbeColour), ProbeColour.Green), constants["DEFAULT_COLOUR"]);
        Assert.Equal((typeof(string), "1.2.3"), constants["version"]);
    }

    [Fact]
    public void Describe_ATypeWithoutScriptModule_Throws()
    {
        var error = Assert.Throws<InvalidOperationException>(() => LuaModuleDescriber.Describe(typeof(UnmarkedModule)));

        Assert.Contains("carries no [ScriptModule]", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_ALuaNameUsedTwice_Throws()
    {
        var error = Assert.Throws<InvalidOperationException>(() => LuaModuleDescriber.Describe(typeof(DuplicateNameModule)));

        Assert.Contains("'same' is already used in module 'twice'", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(typeof(EnumSignaturesModule))]
    [InlineData(typeof(LimitsModule))]
    [InlineData(typeof(ProbeModule))]
    public void EnumsOf_ListsEachEnumOfParametersReturnsAndConstantsOnce(Type moduleType)
    {
        var enums = LuaModuleDescriber.EnumsOf(LuaModuleDescriber.Describe(moduleType));

        Assert.Equal([typeof(ProbeColour)], enums);
    }

    [Fact]
    public void EnumsOf_AModuleWithoutEnums_IsEmpty()
    {
        Assert.Empty(LuaModuleDescriber.EnumsOf(LuaModuleDescriber.Describe(typeof(DependentModule))));
    }
}
