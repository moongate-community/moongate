using Moongate.Scripting.Attributes.Scripts;

namespace Moongate.Tests.Scripting.Attributes;

public sealed class ScriptAttributesTests
{
    [Fact]
    public void ScriptConstant_DefaultsToNoOverride()
    {
        Assert.Null(new ScriptConstantAttribute().Name);
    }

    [Fact]
    public void ScriptFunction_DefaultsToNoOverride()
    {
        var attribute = new ScriptFunctionAttribute();

        Assert.Null(attribute.Name);
        Assert.Null(attribute.HelpText);
    }

    [Fact]
    public void ScriptFunction_RejectsAnOverrideThatIsNotALuaIdentifier()
    {
        Assert.Throws<ArgumentException>(() => new ScriptFunctionAttribute("bad name"));
    }

    [Fact]
    public void ScriptModule_KeepsNameAndHelpText()
    {
        var attribute = new ScriptModuleAttribute("log", "Logging.");

        Assert.Equal("log", attribute.Name);
        Assert.Equal("Logging.", attribute.HelpText);
    }

    [Theory, InlineData(""), InlineData(" "), InlineData("Log"), InlineData("my-module"), InlineData("1st")]
    public void ScriptModule_RejectsANameThatIsNotALuaIdentifierInLowerCase(string name)
    {
        Assert.Throws<ArgumentException>(() => new ScriptModuleAttribute(name));
    }
}
