using Moongate.Scripting.Binding;
using Moongate.Scripting.Data.Binding;
using Moongate.Tests.TestSupport.Scripting;

namespace Moongate.Tests.Scripting.Binding;

public sealed class LuaFunctionDescriberTests
{
    [Fact]
    public void Describe_GivesEachParameterItsLuaTypeOptionalFlagAndDefault()
    {
        var function = Describe(typeof(SignatureModule), "walk_to");

        Assert.Equal("walk_to", function.LuaName);
        Assert.Equal("Walks.", function.HelpText);
        Assert.Equal(
            [
                new ParameterDescription("serial", "integer", false, null),
                new ParameterDescription("z", "integer", true, null),
                new ParameterDescription("range", "integer", true, "2"),
                new ParameterDescription("running", "boolean", true, "false"),
                new ParameterDescription("mode", "string", true, "\"walk\"")
            ],
            function.Parameters
        );
    }

    [Fact]
    public void Describe_ANullableReturn_EndsWithAQuestionMark()
    {
        Assert.Equal("string?", Describe(typeof(SignatureModule), "walk_to").Returns);
        Assert.Equal("integer", Describe(typeof(ProbeModule), "add").Returns);
    }

    [Fact]
    public void Describe_AVoidFunction_ReturnsNothing()
    {
        Assert.Null(Describe(typeof(ProbeModule), "record").Returns);
    }

    [Fact]
    public void Describe_AParamsParameter_IsTheVarargs_NeverOptional()
    {
        var parameters = Describe(typeof(ProbeModule), "record").Parameters;

        Assert.Equal(new ParameterDescription("what", "string", false, null), parameters[0]);
        Assert.Equal(new ParameterDescription("...", "any", false, null), parameters[1]);
    }

    [Fact]
    public void Describe_AnEnumParameter_AlsoTakesItsNameAsAString_AndItsDefaultIsTheMember()
    {
        Assert.Equal(
            new ParameterDescription("colour", "ProbeColour|string", true, "ProbeColour.Red"),
            Assert.Single(Describe(typeof(EnumSignaturesModule), "tint").Parameters)
        );
        Assert.Equal(
            new ParameterDescription("colour", "ProbeColour|string", true, null),
            Assert.Single(Describe(typeof(EnumSignaturesModule), "paint").Parameters)
        );
        Assert.Equal(
            new ParameterDescription("...", "ProbeColour|string", false, null),
            Assert.Single(Describe(typeof(EnumSignaturesModule), "mix").Parameters)
        );
        Assert.Equal("ProbeColour", Describe(typeof(ProbeModule), "next_colour").Returns);
    }

    [Fact]
    public void Describe_ADeclaredParameterType_Wins()
    {
        Assert.Equal("EventName", Assert.Single(Describe(typeof(TypedParameterModule), "accept").Parameters).LuaType);
    }

    [Theory]
    [InlineData(typeof(int), "integer")]
    [InlineData(typeof(long?), "integer")]
    [InlineData(typeof(double), "number")]
    [InlineData(typeof(bool), "boolean")]
    [InlineData(typeof(string), "string")]
    [InlineData(typeof(Lua.LuaTable), "table")]
    [InlineData(typeof(ProbeColour?), "ProbeColour")]
    [InlineData(typeof(object), "any")]
    public void LuaTypeName_NamesTheLuaTypeOfAClrType(Type type, string expected)
    {
        Assert.Equal(expected, LuaFunctionDescriber.LuaTypeName(type));
    }

    [Fact]
    public void LuaLiteral_WritesAValueAsALuaToken()
    {
        Assert.Equal("nil", LuaFunctionDescriber.LuaLiteral(null, typeof(string)));
        Assert.Equal("\"a\\\"b\"", LuaFunctionDescriber.LuaLiteral("a\"b", typeof(string)));
        Assert.Equal("true", LuaFunctionDescriber.LuaLiteral(true, typeof(bool)));
        Assert.Equal("1", LuaFunctionDescriber.LuaLiteral(ProbeColour.Green, typeof(ProbeColour)));
        Assert.Equal("0.5", LuaFunctionDescriber.LuaLiteral(0.5, typeof(double)));
        Assert.Equal("math.huge", LuaFunctionDescriber.LuaLiteral(double.PositiveInfinity, typeof(double)));
    }

    private static FunctionDescription Describe(Type moduleType, string luaName)
    {
        var function = LuaModuleDescriber.Describe(moduleType).Functions.Single(function => function.LuaName == luaName);

        return LuaFunctionDescriber.Describe(function);
    }
}
