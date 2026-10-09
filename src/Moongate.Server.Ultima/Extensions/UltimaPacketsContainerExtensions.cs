using DryIoc;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Ultima.Handlers.Books;
using Moongate.Server.Ultima.Handlers.BulletinBoards;
using Moongate.Server.Ultima.Handlers.Characters;
using Moongate.Server.Ultima.Handlers.Combat;
using Moongate.Server.Ultima.Handlers.General;
using Moongate.Server.Ultima.Handlers.Gumps;
using Moongate.Server.Ultima.Handlers.HuePicking;
using Moongate.Server.Ultima.Handlers.Items;
using Moongate.Server.Ultima.Handlers.Login;
using Moongate.Server.Ultima.Handlers.Movement;
using Moongate.Server.Ultima.Handlers.Prompts;
using Moongate.Server.Ultima.Handlers.Skills;
using Moongate.Server.Ultima.Handlers.Targeting;
using Moongate.Server.Ultima.Handlers.Tooltips;
using Moongate.Server.Ultima.Handlers.Vendors;
using Moongate.Server.Ultima.Packets.Books;
using Moongate.Server.Ultima.Packets.BulletinBoards;
using Moongate.Server.Ultima.Packets.Characters;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Packets.Gumps;
using Moongate.Server.Ultima.Packets.Vendors;

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
        container.RegisterIncomingPacket<HuePickerResponsePacket>();
        container.RegisterPacketHandler<HuePickerResponsePacket, HuePickerResponsePacketHandler>();
        container.RegisterIncomingPacket<TextPromptResponsePacket>();
        container.RegisterPacketHandler<TextPromptResponsePacket, TextPromptResponsePacketHandler>();
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
        container.RegisterIncomingPacket<WarModeRequestPacket>();
        container.RegisterPacketHandler<WarModeRequestPacket, WarModeRequestPacketHandler>();
        container.RegisterIncomingPacket<UpdateRangePacket>();
        container.RegisterPacketHandler<UpdateRangePacket, UpdateRangePacketHandler>();
        container.RegisterIncomingPacket<BulletinBoardRequestPacket>();
        container.RegisterPacketHandler<BulletinBoardRequestPacket, BulletinBoardRequestPacketHandler>();
        container.RegisterIncomingPacket<ExtendedCommandPacket>();
        container.RegisterPacketHandler<ExtendedCommandPacket, ExtendedCommandPacketHandler>();
        container.RegisterIncomingPacket<QueryPropertiesPacket>();
        container.RegisterPacketHandler<QueryPropertiesPacket, QueryPropertiesPacketHandler>();
        container.RegisterIncomingPacket<AttackRequestPacket>();
        container.RegisterPacketHandler<AttackRequestPacket, AttackRequestPacketHandler>();
        container.RegisterIncomingPacket<VendorBuyReplyPacket>();
        container.RegisterPacketHandler<VendorBuyReplyPacket, VendorBuyReplyPacketHandler>();
        container.RegisterIncomingPacket<VendorSellReplyPacket>();
        container.RegisterPacketHandler<VendorSellReplyPacket, VendorSellReplyPacketHandler>();
        container.RegisterIncomingPacket<SkillLockPacket>();
        container.RegisterPacketHandler<SkillLockPacket, SkillLockPacketHandler>();
        container.RegisterIncomingPacket<TextCommandPacket>();
        container.RegisterPacketHandler<TextCommandPacket, TextCommandPacketHandler>();
        RegisterIgnoredPacket<ProfileRequestPacket>(container);
        RegisterIgnoredPacket<ProtocolExtensionPacket>(container);
        RegisterIgnoredPacket<ResynchronizeRequestPacket>(container);
        RegisterIgnoredPacket<OpenChatWindowPacket>(container);
        RegisterIgnoredPacket<HelpRequestPacket>(container);
        RegisterIgnoredPacket<ClientTypePacket>(container);
        RegisterIgnoredPacket<PublicHouseContentPacket>(container);
        // What a player wrote in a book: its pages, its title and its author.
        container.RegisterIncomingPacket<BookPagesRequestPacket>();
        container.RegisterPacketHandler<BookPagesRequestPacket, BookEditPacketHandler>();
        container.RegisterIncomingPacket<BookHeaderChangePacket>();
        container.RegisterPacketHandler<BookHeaderChangePacket, BookEditPacketHandler>();
        container.RegisterIncomingPacket<OldBookHeaderChangePacket>();
        container.RegisterPacketHandler<OldBookHeaderChangePacket, BookEditPacketHandler>();
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
