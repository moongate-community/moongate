using DryIoc;
using Moongate.Core.Types;
using Moongate.Server.Abstractions.Extensions;
using Moongate.Server.Abstractions.Types;
using Moongate.Server.Commands;
using SquidStd.Core.Utils;
using SquidStd.Plugin.Abstractions.Data;
using SquidStd.Plugin.Abstractions.Interfaces.Plugins;

namespace Moongate.Server.Plugins;

/// <summary>Registers Moongate's GM/admin commands, keeping the command wiring out of Program.cs.</summary>
public class MoongateCommandsPlugin : ISquidStdPlugin
{
    public PluginMetadata Metadata
        => new()
        {
            Id = "moongate.commands.plugin",
            Version = new(VersionUtils.GetVersion(typeof(MoongateCommandsPlugin).Assembly)),
            Author = "squid",
            Name = "Moongate Commands",
            Description = "GM/admin \".\" command registrations"
        };

    public void Configure(IContainer container, PluginContext context)
        => container.RegisterCommand<BroadcastCommand>(
            "broadcast|bc",
            AccountLevelType.GrandMaster,
            "Sends a server-wide system message.",
            CommandSourceType.InGame | CommandSourceType.Console | CommandSourceType.Rest
        );
}
