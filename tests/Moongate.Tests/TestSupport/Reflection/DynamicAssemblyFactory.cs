using System.Reflection;
using System.Reflection.Emit;

namespace Moongate.Tests.TestSupport.Reflection;

public static class DynamicAssemblyFactory
{
    public static Assembly Create(Version version, string? informationalVersion = null, string? codename = null)
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

        if (codename is not null)
        {
            var constructor = typeof(AssemblyMetadataAttribute).GetConstructor([typeof(string), typeof(string)])!;
            assembly.SetCustomAttribute(new CustomAttributeBuilder(constructor, ["Codename", codename]));
        }

        return assembly;
    }
}
