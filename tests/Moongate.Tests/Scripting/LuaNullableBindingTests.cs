using DryIoc;
using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Scripting.Data.Internal;
using Moongate.Scripting.Services;
using Moongate.Tests.TestSupport;

namespace Moongate.Tests.Scripting;

public sealed class LuaNullableBindingTests
{
    [ScriptModule("nullable_test")]
    private sealed class NullableBindingScriptModule
    {
        public uint? LastValue { get; private set; }

        [ScriptFunction("capture")]
        public uint? Capture(uint? value = null)
        {
            LastValue = value;

            return value;
        }
    }

    [Test]
    public async Task ExecuteFunction_WhenLuaNumberTargetsNullableUInt_ShouldConvertWithoutThrowing()
    {
        using var temp = new TempDirectory();
        var dirs = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var scriptsDir = dirs[DirectoryType.Scripts];
        var container = new Container();
        var module = new NullableBindingScriptModule();
        Directory.CreateDirectory(scriptsDir);
        Directory.CreateDirectory(temp.Path);
        container.RegisterInstance(module);

        var service = new LuaScriptEngineService(
            dirs,
            [new(typeof(NullableBindingScriptModule))],
            container,
            new(temp.Path, scriptsDir, "0.1.0"),
            []
        );

        await service.StartAsync();

        var result = service.ExecuteFunction(
            """
            (function()
                return nullable_test.capture(123)
            end)()
            """
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(result.Data, Is.EqualTo(123d));
                Assert.That(module.LastValue, Is.EqualTo(123u));
            }
        );
    }
}
