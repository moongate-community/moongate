using DryIoc;
using Moongate.Scripting.Extensions.Scripts;
using Moongate.Scripting.Interfaces;
using Moongate.Tests.TestSupport.Scripting;

namespace Moongate.Tests.Scripting.Extensions;

public sealed class ContainerScriptingExtensionsTests
{
    [Fact]
    public void RegisterScriptEnum_IsIdempotent()
    {
        using var container = new Container();

        container.RegisterScriptEnum<RegistryColour>().RegisterScriptEnum<RegistryColour>();

        Assert.Equal([typeof(RegistryColour)], container.Resolve<IScriptModuleRegistry>().EnumTypes);
    }

    [Fact]
    public void RegisterScriptModule_AppendsInOrderAndRegistersASingleton()
    {
        using var container = new Container();

        var result = container.AddScriptModule<FirstRegistryModule>().AddScriptModule<SecondRegistryModule>();

        Assert.Same(container, result);
        Assert.Equal(
            [typeof(FirstRegistryModule), typeof(SecondRegistryModule)],
            container.Resolve<IScriptModuleRegistry>().ModuleTypes
        );
        Assert.Same(container.Resolve<FirstRegistryModule>(), container.Resolve<FirstRegistryModule>());
    }

    [Fact]
    public void RegisterScriptModule_TwiceForTheSameType_Throws()
    {
        using var container = new Container();
        container.AddScriptModule<FirstRegistryModule>();

        Assert.Throws<InvalidOperationException>(() => container.AddScriptModule<FirstRegistryModule>());
    }

    [Fact]
    public void RegisterScriptModule_WithoutTheAttribute_Throws()
    {
        using var container = new Container();

        Assert.Throws<ArgumentException>(() => container.AddScriptModule<UnmarkedModule>());
    }
}
