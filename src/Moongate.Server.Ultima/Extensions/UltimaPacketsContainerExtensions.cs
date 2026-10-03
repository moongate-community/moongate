using DryIoc;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Ultima.Handlers.Characters;
using Moongate.Server.Ultima.Handlers.General;
using Moongate.Server.Ultima.Handlers.Gumps;
using Moongate.Server.Ultima.Handlers.Items;
using Moongate.Server.Ultima.Handlers.Login;
using Moongate.Server.Ultima.Handlers.Movement;
using Moongate.Server.Ultima.Handlers.Targeting;
using Moongate.Server.Ultima.Handlers.Tooltips;
using Moongate.Server.Ultima.Packets.Characters;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Packets.Gumps;

namespace Moongate.Server.Ultima.Extensions;

/// <summary>
///     Registers the packets the game role reads and their handlers, and the ones it recognises and ignores.
/// </summary>
public static class UltimaPacketsContainerExtensions
{
    /// <summary>
    ///     Registers the packets the game role reads and their handlers, and the ones it recognises and ignores.
    /// </summary>
    public static Container AddUltimaGamePackets(this Container container)
    {
        container.RegisterPacketHandler<LoginSeedPacket, LoginSeedPacketHandler>();
        container.RegisterAsyncPacketHandler<GameLoginPacket, GameLoginPacketHandler>();
        container.RegisterIncomingPacket<CreateCharacterPacket>();
        container.RegisterIncomingPacket<CreateCharacterEnhancedPacket>();
        container.RegisterIncomingPacket<DeleteCharacterPacket>();
        container.RegisterIncomingPacket<PlayCharacterPacket>();
        container.RegisterAsyncPacketHandler<PlayCharacterPacket, PlayCharacterPacketHandler>();
        container.RegisterIncomingPacket<MoveRequestPacket>();
        container.RegisterPacketHandler<MoveRequestPacket, MoveRequestPacketHandler>();
        container.RegisterIncomingPacket<UseRequestPacket>();
        container.RegisterPacketHandler<UseRequestPacket, UseRequestPacketHandler>();
        container.RegisterIncomingPacket<AsciiSpeechRequestPacket>();
        container.RegisterAsyncPacketHandler<AsciiSpeechRequestPacket, SpeechRequestPacketHandler>();
        container.RegisterIncomingPacket<UnicodeSpeechRequestPacket>();
        container.RegisterAsyncPacketHandler<UnicodeSpeechRequestPacket, SpeechRequestPacketHandler>();
        container.RegisterIncomingPacket<TargetResponsePacket>();
        container.RegisterPacketHandler<TargetResponsePacket, TargetResponsePacketHandler>();
        container.RegisterIncomingPacket<GumpResponsePacket>();
        container.RegisterPacketHandler<GumpResponsePacket, GumpResponsePacketHandler>();
        container.RegisterIncomingPacket<LiftRequestPacket>();
        container.RegisterPacketHandler<LiftRequestPacket, LiftRequestPacketHandler>();
        container.RegisterIncomingPacket<DropRequestPacket>();
        container.RegisterPacketHandler<DropRequestPacket, DropRequestPacketHandler>();
        container.RegisterIncomingPacket<EquipRequestPacket>();
        container.RegisterPacketHandler<EquipRequestPacket, EquipRequestPacketHandler>();

        // Sent by the client around and after entering the world; recognised so it is not disconnected.
        // The packet itself is registered by the plugin for both roles: the login server receives it too.
        container.RegisterPacketHandler<ClientHardwareInfoPacket, IgnoredPacketHandler<ClientHardwareInfoPacket>>();
        container.RegisterIncomingPacket<LookRequestPacket>();
        container.RegisterPacketHandler<LookRequestPacket, LookRequestPacketHandler>();
        container.RegisterIncomingPacket<MobileQueryPacket>();
        container.RegisterPacketHandler<MobileQueryPacket, MobileQueryPacketHandler>();
        RegisterIgnoredPacket<WarModeRequestPacket>(container);
        container.RegisterIncomingPacket<UpdateRangePacket>();
        container.RegisterPacketHandler<UpdateRangePacket, UpdateRangePacketHandler>();
        container.RegisterIncomingPacket<ExtendedCommandPacket>();
        container.RegisterPacketHandler<ExtendedCommandPacket, ExtendedCommandPacketHandler>();
        container.RegisterIncomingPacket<QueryPropertiesPacket>();
        container.RegisterPacketHandler<QueryPropertiesPacket, QueryPropertiesPacketHandler>();
        RegisterIgnoredPacket<AttackRequestPacket>(container);
        RegisterIgnoredPacket<TextCommandPacket>(container);
        RegisterIgnoredPacket<ProfileRequestPacket>(container);
        RegisterIgnoredPacket<ResynchronizeRequestPacket>(container);
        RegisterIgnoredPacket<OpenChatWindowPacket>(container);
        RegisterIgnoredPacket<ClientTypePacket>(container);
        RegisterIgnoredPacket<PublicHouseContentPacket>(container);
        container.RegisterAsyncPacketHandler<DeleteCharacterPacket, DeleteCharacterPacketHandler>();
        container.RegisterAsyncPacketHandler<CreateCharacterPacket, CreateCharacterPacketHandler>();
        container.RegisterAsyncPacketHandler<CreateCharacterEnhancedPacket, CreateCharacterEnhancedPacketHandler>();

        return container;
    }

    private static void RegisterIgnoredPacket<TPacket>(Container container)
        where TPacket : class, IIncomingPacket<TPacket>
    {
        container.RegisterIncomingPacket<TPacket>();
        container.RegisterPacketHandler<TPacket, IgnoredPacketHandler<TPacket>>();
    }
}
