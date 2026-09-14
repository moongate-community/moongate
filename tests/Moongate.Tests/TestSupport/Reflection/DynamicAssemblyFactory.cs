using System.Reflection;
using System.Reflection.Emit;

namespace Moongate.Tests.TestSupport.Reflection;

public static class DynamicAssemblyFactory
{
    public static Assembly Create(Version version, string? informationalVersion = null)
    {
        var name = new AssemblyName($"Moongate.Tests.Dynamic.{Guid.NewGuid():N}")
        {
            Version = version
        };
        var assembly = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);

        if (informationalVersion is not null)
        {
            var constructor = typeof(AssemblyInformationalVersionAttribute).GetConstructor([typeof(string)])!;
            assembly.SetCustomAttribute(new CustomAttributeBuilder(constructor, [informationalVersion]));
        }

        return assembly;
    }
}
