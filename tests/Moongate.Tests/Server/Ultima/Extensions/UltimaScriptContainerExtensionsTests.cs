using System.Reflection;
using DryIoc;
using Moongate.Core.Types.Geometry;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Scripting.Interfaces;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Types.Speech;

namespace Moongate.Tests.Server.Ultima.Extensions;

public sealed class UltimaScriptContainerExtensionsTests
{
    [Fact]
    public void AddUltimaScriptModules_RunsOnABareContainer_AndFillsTheRegistry()
    {
        using var container = new Container();

        container.AddUltimaScriptModules();

        var registry = container.Resolve<IScriptModuleRegistry>();
        Assert.Contains(typeof(NpcModule), registry.ModuleTypes);
        Assert.Contains(typeof(DiceModule), registry.ModuleTypes);
        Assert.Contains(typeof(DirectionType), registry.EnumTypes);
        Assert.Contains(typeof(SpeechKeywordType), registry.EnumTypes);
        Assert.Contains(typeof(SpeechType), registry.EnumTypes);
    }

    [Fact]
    public void EveryScriptModuleOfTheAssembly_IsRegistered()
    {
        using var container = new Container();
        container.AddUltimaScriptModules();

        var attributed = typeof(NpcModule).Assembly
            .GetTypes()
            .Where(type => type.GetCustomAttribute<ScriptModuleAttribute>() is not null)
            .OrderBy(type => type.Name, StringComparer.Ordinal);

        Assert.Equal(
            attributed,
            container.Resolve<IScriptModuleRegistry>().ModuleTypes.OrderBy(type => type.Name, StringComparer.Ordinal)
        );
    }
}
