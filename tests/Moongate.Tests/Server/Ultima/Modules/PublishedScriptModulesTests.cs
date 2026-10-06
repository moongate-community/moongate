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
    public void BookModule_PublishesTheThreeFunctionsWithExamples()
    {
        var module = Assert.Single(Published(), candidate => candidate.Name == "book");
        Assert.Equal(
            ["give", "open", "write"],
            module.Functions.Select(function => function.LuaName).Order(StringComparer.Ordinal)
        );
        var path = Path.Combine(RepositoryRoot(), "website/lua/examples/book.md");
        Assert.True(File.Exists(path), "The book module needs its published examples.");
        var examples = File.ReadAllText(path);
        foreach (var name in new[] { "give", "write", "open" })
        {
            Assert.Contains("## " + name, examples);
        }
    }

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
            Published()
                .Single(candidate => candidate.Name == module)
                .Functions.Single(candidate => candidate.LuaName == function)
        );

        Assert.Equal("function", described.Parameters.Single(candidate => candidate.Name == parameter).LuaType);
    }

    [Fact]
    public void TheModulesOfTheScriptingAssembly_AreTheFourTheEngineAndTheServerPublish()
    {
        // The reference reflects this assembly, because engine, timer and events are bound by the engine itself and
        // log by the server. A fifth module here is on the site at once: make sure the server publishes it too.
        var modules = typeof(LogModule).Assembly
            .GetTypes()
            .Where(type => type.GetCustomAttribute<ScriptModuleAttribute>() is not null)
            .OrderBy(type => type.Name, StringComparer.Ordinal);

        Assert.Equal([typeof(EngineModule), typeof(EventsModule), typeof(LogModule), typeof(TimerModule)], modules);
    }

    [Fact]
    public void EveryExampleOfTheReference_NamesAPublishedFunction()
    {
        var modules = Published()
            .ToDictionary(
                module => module.Name,
                module => module.Functions.Select(function => function.LuaName).ToHashSet(StringComparer.Ordinal),
                StringComparer.Ordinal
            );
        var unknown = new List<string>();

        foreach (var file in Directory.EnumerateFiles(Path.Combine(RepositoryRoot(), "website", "lua", "examples"), "*.md"))
        {
            var module = Path.GetFileNameWithoutExtension(file);
            var fenced = false;

            foreach (var line in File.ReadLines(file))
            {
                if (line.StartsWith("```", StringComparison.Ordinal))
                {
                    fenced = !fenced;
                }

                // As website/scripts/build-lua.mjs reads them: a "## <function>" line outside a code block.
                if (fenced || !line.StartsWith("## ", StringComparison.Ordinal))
                {
                    continue;
                }

                var function = line[3..].TrimEnd();

                if (!modules.TryGetValue(module, out var functions) || !functions.Contains(function))
                {
                    unknown.Add(module + "." + function);
                }
            }
        }

        Assert.Empty(unknown);
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

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Moongate.slnx")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("The repository root was not found above " + AppContext.BaseDirectory);
        }

        return directory.FullName;
    }
}
