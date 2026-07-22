using System.Reflection;
using Moongate.Server.Abstractions.Interfaces.Plugins;
using Moongate.Server.Services.Plugins;

namespace Moongate.Tests.Server.Plugins;

public class PluginDiscoveryTests
{
    private static readonly Assembly Tests = typeof(PluginDiscoveryTests).Assembly;
    private static readonly Assembly Abstractions = typeof(IPluginCatalog).Assembly;

    [Fact]
    public void IsExternal_IsFalseForTheHostItself()
    {
        Assert.False(PluginDiscovery.IsExternal(Tests, Tests));
    }

    [Fact]
    public void IsExternal_IsFalseForAnAssemblyTheHostReferences()
    {
        // Moongate.Tests references Moongate.Server.Abstractions transitively and directly enough to appear
        // in its reference list: anything in the compile-time graph was shipped with the server.
        Assert.False(PluginDiscovery.IsExternal(Tests, Abstractions));
    }

    [Fact]
    public void IsExternal_IsTrueForAnAssemblyOutsideTheHostGraph()
    {
        // Reversing the roles gives a genuine outsider: Moongate.Server.Abstractions cannot reference the
        // test project, so from its point of view the tests are an externally loaded assembly. This is the
        // same shape as a DLL dropped into plugins/.
        Assert.True(PluginDiscovery.IsExternal(Abstractions, Tests));
    }

    [Fact]
    public void ExternalPluginTypes_FindsPluginTypesOnlyOutsideTheHostGraph()
    {
        var found = PluginDiscovery.ExternalPluginTypes(Abstractions, [Tests]).ToList();

        Assert.Contains(typeof(DiscoverablePlugin), found);
    }

    [Fact]
    public void ExternalPluginTypes_SkipsAssembliesInsideTheHostGraph()
    {
        // The host's own plugins are recorded explicitly at activation; sweeping them up here would
        // resurrect the very plugin a configuration flag switched off.
        Assert.Empty(PluginDiscovery.ExternalPluginTypes(Tests, [Tests]));
    }

    [Fact]
    public void ExternalPluginTypes_SkipsAbstractTypesAndInterfaces()
    {
        var found = PluginDiscovery.ExternalPluginTypes(Abstractions, [Tests]).ToList();

        Assert.DoesNotContain(typeof(AbstractPlugin), found);
    }
}
