using DryIoc;
using Moongate.Server.Abstractions.Extensions;
using Moongate.Server.Handlers;
using SquidStd.Core.Utils;
using SquidStd.Plugin.Abstractions.Data;
using SquidStd.Plugin.Abstractions.Interfaces.Plugins;

namespace Moongate.Server;

/// <summary>Registers Moongate's inbound packet handlers, keeping the opcode wiring out of Program.cs.</summary>
public class MoongatePacketHandlersPlugin : ISquidStdPlugin
{
    public PluginMetadata Metadata
        => new()
        {
            Id = "moongate.packethandlers.plugin",
            Version = new(VersionUtils.GetVersion(typeof(MoongatePacketHandlersPlugin).Assembly)),
            Author = "squid",
            Name = "Moongate Packet Handlers",
            Description = "Inbound packet handler registrations"
        };

    public void Configure(IContainer container, PluginContext context)
    {
        container.RegisterPacketHandler<LoginSeedHandler>();
        container.RegisterPacketHandler<AccountLoginHandler>();
        container.RegisterPacketHandler<SelectServerHandler>();
        container.RegisterPacketHandler<GameServerLoginHandler>();
        container.RegisterPacketHandler<CharacterCreationHandler>();
        container.RegisterPacketHandler<CharacterSelectHandler>();
        container.RegisterPacketHandler<DeleteCharacterHandler>();
        container.RegisterPacketHandler<PingHandler>();
        container.RegisterPacketHandler<ClientVersionHandler>();
        container.RegisterPacketHandler<GeneralInformationHandler>();
        container.RegisterPacketHandler<SkillLockChangeHandler>();
        container.RegisterPacketHandler<SingleClickHandler>();
        container.RegisterPacketHandler<MegaClilocHandler>();
        container.RegisterPacketHandler<DoubleClickHandler>();
        container.RegisterPacketHandler<MoveRequestHandler>();
    }
}
