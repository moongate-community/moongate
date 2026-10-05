using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Moongate.Server.Ultima;

namespace Moongate.Tests.TestSupport.Ultima.Loaders;

/// <summary>
///     Reads the shipped data files as the server does: the TOML converters the Ultima plugin adds at startup are
///     added once for the whole test run. They are process-wide, so without this a test read a shipped file
///     rightly or wrongly by which test ran before it.
/// </summary>
internal static class UltimaTomlConverters
{
    [ModuleInitializer]
    [SuppressMessage("Usage", "CA2255:The 'ModuleInitializer' attribute should not be used in libraries", Justification = "A test assembly.")]
    internal static void Register()
    {
        MoongateUltimaPlugin.RegisterTomlConverters();
    }
}
