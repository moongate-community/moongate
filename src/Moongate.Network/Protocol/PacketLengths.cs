namespace Moongate.Network.Protocol;

/// <summary>
/// Packet sizes for every documented UO packet id, mirrored 1:1 from the POL packet
/// guide (https://docs.polserver.com/packets/index.php). <see cref="Variable" /> means
/// the total length travels as a big-endian ushort at bytes 1-2; <see cref="Unknown" />
/// means the id is not part of the protocol (framing error). Where POL lists two sizes
/// per client era, the 7.x value is used (0x08=15, 0x25=21, 0xB9=5, 0xBA=10).
/// </summary>
public static class PacketLengths
{
    public const short Variable = -1;
    public const short Unknown = 0;

    private static readonly short[] _lengths = BuildTable();

    /// <summary>The number of documented packet ids in the table.</summary>
    public static int Count { get; } = _lengths.Count(static length => length != Unknown);

    public static short Get(byte packetId)
        => _lengths[packetId];

    private static short[] BuildTable()
    {
        var table = new short[256];

        table[0x00] = 104;      // CreateCharacter
        table[0x01] = 5;        // DisconnectNotification
        table[0x02] = 7;        // MoveRequest
        table[0x03] = Variable; // TalkRequest
        table[0x04] = 2;        // RequestGodMode
        table[0x05] = 5;        // RequestAttack
        table[0x06] = 5;        // DoubleClick
        table[0x07] = 7;        // PickUpItem
        table[0x08] = 15;       // DropItem
        table[0x09] = 5;        // SingleClick
        table[0x0A] = 11;       // Edit
        table[0x0B] = 7;        // Damage
        table[0x0C] = Variable; // EditTileData
        table[0x11] = Variable; // StatusBarInfo
        table[0x12] = Variable; // RequestSkillUse
        table[0x13] = 10;       // DropWearItem
        table[0x14] = 6;        // SendElevation
        table[0x15] = 9;        // Follow
        table[0x16] = Variable; // NewHealthBarStatusUpdate
        table[0x17] = 12;       // HealthBarStatusUpdate
        table[0x1A] = Variable; // ObjectInfo
        table[0x1B] = 37;       // CharLocaleAndBody
        table[0x1C] = Variable; // SendSpeech
        table[0x1D] = 5;        // DeleteObject
        table[0x1E] = 4;        // ControlAnimation
        table[0x1F] = 8;        // Explosion
        table[0x20] = 19;       // DrawGamePlayer
        table[0x21] = 8;        // CharMoveRejection
        table[0x22] = 3;        // CharacterMoveAck
        table[0x23] = 26;       // DraggingOfItem
        table[0x24] = 7;        // DrawContainer
        table[0x25] = 21;       // AddItemToContainer
        table[0x26] = 5;        // KickPlayer
        table[0x27] = 2;        // RejectMoveItemRequest
        table[0x28] = 5;        // DropItemFailed
        table[0x29] = 1;        // DropItemApproved
        table[0x2A] = 5;        // Blood
        table[0x2B] = 2;        // GodMode
        table[0x2C] = 2;        // ResurrectionMenu
        table[0x2D] = 17;       // MobAttributes
        table[0x2E] = 15;       // WornItem
        table[0x2F] = 10;       // FightOccurring
        table[0x30] = 5;        // AttackOk
        table[0x31] = 1;        // AttackEnded
        table[0x32] = 2;        // Unknown32
        table[0x33] = 2;        // PauseClient
        table[0x34] = 10;       // GetPlayerStatus
        table[0x35] = 653;      // AddResource
        table[0x36] = Variable; // ResourceTileData
        table[0x37] = 8;        // MoveItem
        table[0x38] = 7;        // PathfindingInClient
        table[0x39] = 9;        // RemoveGroup
        table[0x3A] = Variable; // SendSkills
        table[0x3B] = Variable; // BuyItems
        table[0x3C] = Variable; // AddMultipleItemsInContainer
        table[0x3E] = 37;       // Versions
        table[0x3F] = Variable; // UpdateStatics
        table[0x45] = 5;        // VersionOk
        table[0x46] = Variable; // NewArtwork
        table[0x47] = 11;       // NewTerrain
        table[0x48] = 73;       // NewAnimation
        table[0x49] = 93;       // NewHues
        table[0x4A] = 5;        // DeleteArt
        table[0x4B] = 9;        // CheckClientVersion
        table[0x4C] = Variable; // ScriptNames
        table[0x4D] = Variable; // EditScriptFile
        table[0x4E] = 6;        // PersonalLightLevel
        table[0x4F] = 2;        // OverallLightLevel
        table[0x50] = Variable; // BoardHeader
        table[0x51] = Variable; // BoardMessage
        table[0x52] = Variable; // BoardPostMessage
        table[0x53] = 2;        // RejectCharacterLogon
        table[0x54] = 12;       // PlaySoundEffect
        table[0x55] = 1;        // LoginComplete
        table[0x56] = 11;       // MapPacket
        table[0x57] = 110;      // UpdateRegions
        table[0x58] = 106;      // AddRegion
        table[0x59] = Variable; // NewContextFx
        table[0x5A] = Variable; // UpdateContextFx
        table[0x5B] = 4;        // Time
        table[0x5C] = 2;        // RestartVersion
        table[0x5D] = 73;       // LoginCharacter
        table[0x5E] = Variable; // ServerListing
        table[0x5F] = 49;       // ServerListAddEntry
        table[0x60] = 5;        // ServerListRemoveEntry
        table[0x61] = 9;        // RemoveStaticObject
        table[0x62] = 15;       // MoveStaticObject
        table[0x63] = 13;       // LoadArea
        table[0x64] = 1;        // LoadAreaRequest
        table[0x65] = 4;        // SetWeather
        table[0x66] = Variable; // BooksPages
        table[0x69] = 5;        // ChangeTextEmoteColors
        table[0x6C] = 19;       // TargetCursorCommands
        table[0x6D] = 3;        // PlayMidiMusic
        table[0x6E] = 14;       // CharacterAnimation
        table[0x6F] = Variable; // SecureTrading
        table[0x70] = 28;       // GraphicalEffect
        table[0x71] = Variable; // BulletinBoardMessages
        table[0x72] = 5;        // RequestWarMode
        table[0x73] = 2;        // PingMessage
        table[0x74] = Variable; // OpenBuyWindow
        table[0x75] = 35;       // RenameCharacter
        table[0x76] = 16;       // NewSubserver
        table[0x77] = 17;       // UpdatePlayer
        table[0x78] = Variable; // DrawObject
        table[0x7C] = Variable; // OpenDialogBox
        table[0x7D] = 13;       // ResponseToDialogBox
        table[0x80] = 62;       // LoginRequest
        table[0x82] = 2;        // LoginDenied
        table[0x83] = 39;       // DeleteCharacter
        table[0x86] = 304;      // ResendCharactersAfterDelete
        table[0x88] = 66;       // OpenPaperdoll
        table[0x89] = Variable; // CorpseClothing
        table[0x8C] = 11;       // ConnectToGameServer
        table[0x8D] = 146;      // CharacterCreationKR
        table[0x90] = 19;       // MapMessage
        table[0x91] = 65;       // GameServerLogin
        table[0x93] = 99;       // BookHeaderOld
        table[0x95] = 9;        // DyeWindow
        table[0x97] = 2;        // MovePlayer
        table[0x98] = Variable; // AllNames
        table[0x99] = 26;       // GiveBoatHousePlacementView
        table[0x9A] = Variable; // ConsoleEntryPrompt
        table[0x9B] = 258;      // RequestHelp
        table[0x9C] = 53;       // RequestAssistance
        table[0x9E] = Variable; // SellList
        table[0x9F] = Variable; // SellListReply
        table[0xA0] = 3;        // SelectServer
        table[0xA1] = 9;        // UpdateCurrentHealth
        table[0xA2] = 9;        // UpdateCurrentMana
        table[0xA3] = 9;        // UpdateCurrentStamina
        table[0xA4] = Variable; // ClientSpy
        table[0xA5] = Variable; // OpenWebBrowser
        table[0xA6] = Variable; // TipNoticeWindow
        table[0xA7] = 4;        // RequestTipNoticeWindow
        table[0xA8] = Variable; // GameServerList
        table[0xA9] = Variable; // CharactersStartingLocations
        table[0xAA] = 5;        // AllowRefuseAttack
        table[0xAB] = Variable; // GumpTextEntryDialog
        table[0xAC] = Variable; // GumpTextEntryDialogReply
        table[0xAD] = Variable; // UnicodeAsciiSpeechRequest
        table[0xAE] = Variable; // UnicodeSpeechMessage
        table[0xAF] = 13;       // DisplayDeathAction
        table[0xB0] = Variable; // SendGumpMenuDialog
        table[0xB1] = Variable; // GumpMenuSelection
        table[0xB2] = Variable; // ChatMessage
        table[0xB3] = Variable; // ChatText
        table[0xB5] = 64;       // OpenChatWindow
        table[0xB6] = 9;        // SendHelpTipRequest
        table[0xB7] = Variable; // HelpTipData
        table[0xB8] = Variable; // RequestCharProfile
        table[0xB9] = 5;        // EnableLockedClientFeatures
        table[0xBA] = 10;       // QuestArrow
        table[0xBB] = 9;        // UltimaMessenger
        table[0xBC] = 3;        // SeasonalInformation
        table[0xBD] = Variable; // ClientVersion
        table[0xBE] = Variable; // AssistVersion
        table[0xBF] = Variable; // GeneralInformationPacket
        table[0xC0] = 36;       // GraphicalEffectHued
        table[0xC1] = Variable; // ClilocMessage
        table[0xC2] = Variable; // UnicodeTextEntry
        table[0xC4] = 6;        // Semivisible
        table[0xC5] = 1;        // InvalidMapRequest
        table[0xC6] = 1;        // InvalidMapEnable
        table[0xC7] = 49;       // ParticleEffect3d
        table[0xC8] = 2;        // ClientViewRange
        table[0xC9] = 6;        // GetAreaServerPing
        table[0xCA] = 6;        // GetUserServerPing
        table[0xCB] = 7;        // GlobalQueCount
        table[0xCC] = Variable; // ClilocMessageAffix
        table[0xD0] = Variable; // ConfigurationFile
        table[0xD1] = 2;        // LogoutStatus
        table[0xD2] = 25;       // Extended0x20
        table[0xD3] = Variable; // Extended0x78
        table[0xD4] = Variable; // BookHeaderNew
        table[0xD6] = Variable; // MegaCliloc
        table[0xD7] = Variable; // GenericAosCommands
        table[0xD8] = Variable; // SendCustomHouse
        table[0xD9] = Variable; // SpyOnClient
        table[0xDB] = Variable; // CharacterTransferLog
        table[0xDC] = 9;        // SeIntroducedRevision
        table[0xDD] = Variable; // CompressedGump
        table[0xDE] = Variable; // UpdateMobileStatus
        table[0xDF] = Variable; // BuffDebuffSystem
        table[0xE0] = Variable; // BugReportKR
        table[0xE1] = 9;        // ClientTypeKRSA
        table[0xE2] = 10;       // NewCharacterAnimationKR
        table[0xE3] = 77;       // KREncryptionResponse
        table[0xEC] = Variable; // EquipMacroKR
        table[0xED] = Variable; // UnequipItemMacroKR
        table[0xEF] = 21;       // LoginSeed
        table[0xF0] = Variable; // KrriosClientSpecial
        table[0xF1] = Variable; // FreeshardList
        table[0xF3] = 24;       // ObjectInformationSA
        table[0xF5] = 21;       // NewMapMessage
        table[0xF8] = 106;      // CharacterCreationNew
        table[0xFA] = 1;        // OpenUoStore
        table[0xFB] = 2;        // UpdateViewPublicHouseContents

        return table;
    }
}
