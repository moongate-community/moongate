using DryIoc;
using Moongate.Persistence.Extensions;
using Moongate.Sample.Plugin.Commands;
using Moongate.Sample.Plugin.Data.Persistence;
using Moongate.Sample.Plugin.Diagnostics;
using Moongate.Sample.Plugin.Internal;
using Moongate.Sample.Plugin.Modules;
using Moongate.Sample.Plugin.Types;
using Moongate.Scripting.Extensions.Scripts;
using Moongate.Server.Core.Data.Plugins;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Plugins;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Core.Types.Commands;

namespace Moongate.Sample.Plugin;

/// <summary>
/// The sample plugin: registers a Lua module and enum, a console command, a metric provider and a persistence schema.
/// Registration only; nothing starts here.
/// </summary>
public sealed class SamplePlugin : IMoongatePlugin
{
    /// <inheritdoc />
    public MoongatePluginData Metadata { get; } = new(
        "com.github.moongate-community.moongate.plugins.greeter",
        "Greeter sample",
        new(1, 0),
        "Moongate",
        "Adds a greeter Lua module, a greet console command and a greeting counter metric."
    );

    /// <inheritdoc />
    public void Register(Container container)
    {
        container.AddPersistenceWorld<GreetingNote>();
        container.RegisterInstance(new GreetingCounter());
        container.RegisterScriptModule<GreeterModule>();
        container.RegisterScriptEnum<Tone>();
        container.RegisterCommand<GreetCommand>(
            "greet",
            "Greets someone from the console: greet <name> [tone].",
            CommandSourceType.Console,
            AccountType.Regular
        );
        container.AddMetricProvider<GreetingMetricProvider>();
    }
}
