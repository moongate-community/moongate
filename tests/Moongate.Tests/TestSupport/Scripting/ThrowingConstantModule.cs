using Moongate.Scripting.Attributes.Scripts;

namespace Moongate.Tests.TestSupport.Scripting;

[ScriptModule("throwing_constant")]
public sealed class ThrowingConstantModule
{
    [ScriptConstant] public static int Broken => throw new InvalidDataException("the value is not available yet");
}
