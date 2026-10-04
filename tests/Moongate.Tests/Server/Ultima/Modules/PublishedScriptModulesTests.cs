using System.Reflection;
using System.Text.RegularExpressions;
using DryIoc;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Scripting.Binding;
using Moongate.Scripting.Data.Binding;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Modules;
using Moongate.Server.Ultima.Extensions;

namespace Moongate.Tests.Server.Ultima.Modules;

/// <summary>
///     What the Lua API reference of the site needs from the modules the server ships. The site is built at release
///     time, so these checks are what stops a pull request that would break it.
/// </summary>
public sealed class PublishedScriptModulesTests
{
    [Fact]
    public void EveryModuleAndFunction_HasTheTextTheReferencePrints()
    {
        var missing = new List<string>();

        foreach (var module in Published())
        {
            if (string.IsNullOrWhiteSpace(module.HelpText))
            {
                missing.Add(module.Name);
            }

            missing.AddRange(
                module.Functions
                    .Where(function => string.IsNullOrWhiteSpace(function.HelpText))
                    .Select(function => module.Name + "." + function.LuaName)
            );
        }

        Assert.Empty(missing);
    }

    [Fact]
    public void EveryName_IsAnAnchorOfItsPage()
    {
        var modules = Published();
        var invalid = modules.Where(module => !Regex.IsMatch(module.Name, "^[a-z_][a-z0-9_]*$"))
            .Select(module => module.Name)
            .Concat(
                modules.SelectMany(module => module.Functions
                    .Where(function => !Regex.IsMatch(function.LuaName, "^[a-z_][a-z0-9_]*$") ||
                                       function.LuaName is "functions" or "constants"
                    )
                    .Select(function => module.Name + "." + function.LuaName)
                )
            );

        Assert.Empty(invalid);
        Assert.Equal(modules.Count, modules.Select(module => module.Name).Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData("events", "on", "fn")]
    [InlineData("timer", "after", "fn")]
    [InlineData("timer", "every", "fn")]
    [InlineData("npc", "spawn", "callback")]
    [InlineData("prompt", "ask", "callback")]
    [InlineData("target", "pick", "callback")]
    [InlineData("target", "pick_location", "callback")]
    public void ACallbackParameter_IsTypedAsAFunction(string module, string function, string parameter)
    {
        var described = LuaFunctionDescriber.Describe(
            Published().Single(candidate => candidate.Name == module).Functions.Single(candidate => candidate.LuaName == function)
        );

        Assert.Equal("function", described.Parameters.Single(candidate => candidate.Name == parameter).LuaType);
    }

    private static List<ModuleDescription> Published()
    {
        using var container = new Container();
        container.AddUltimaScriptModules();

        return typeof(LogModule).Assembly
            .GetTypes()
            .Where(type => type.GetCustomAttribute<ScriptModuleAttribute>() is not null)
            .Concat(container.Resolve<IScriptModuleRegistry>().ModuleTypes)
            .Select(LuaModuleDescriber.Describe)
            .ToList();
    }
}
