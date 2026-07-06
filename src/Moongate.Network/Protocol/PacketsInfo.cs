using Moongate.Network.Data;
using Moongate.Network.Types;

namespace Moongate.Network.Protocol;

/// <summary>
/// Static catalog of every documented UO packet: name, direction and size per id,
/// mirrored 1:1 from the POL packet guide (https://docs.polserver.com/packets/index.php).
/// Sizes stay consistent with <see cref="PacketLengths"/> for every id (locked by a test).
/// </summary>
public static class PacketsInfo
{
    private static readonly PacketInfo?[] _table = BuildTable();

    public static PacketInfo? GetPacket(byte packetId)
    {
        return _table[packetId];
    }

    private static PacketInfo?[] BuildTable()
    {
        var table = new PacketInfo?[256];

        void Add(byte id, string name, PacketDirectionType direction, short size)
        {
            table[id] = new PacketInfo
            {
                Id = id,
                Name = name,
                Direction = direction,
                Size = size
            };
        }

        Add(0x00, "CreateCharacter", PacketDirectionType.Input, 104);
        Add(0x01, "DisconnectNotification", PacketDirectionType.Input, 5);
        Add(0x02, "MoveRequest", PacketDirectionType.Input, 7);
        Add(0x03, "TalkRequest", PacketDirectionType.Input, PacketLengths.Variable);
        Add(0x04, "RequestGodMode", PacketDirectionType.Input, 2);
        Add(0x05, "RequestAttack", PacketDirectionType.Input, 5);
        Add(0x06, "DoubleClick", PacketDirectionType.Input, 5);
        Add(0x07, "PickUpItem", PacketDirectionType.Input, 7);
        Add(0x08, "DropItem", PacketDirectionType.Input, 15);
        Add(0x09, "SingleClick", PacketDirectionType.Input, 5);
        Add(0x0A, "Edit", PacketDirectionType.Input, 11);
        Add(0x0B, "Damage", PacketDirectionType.Output, 7);
        Add(0x0C, "EditTileData", PacketDirectionType.Input | PacketDirectionType.Output, PacketLengths.Variable);
        Add(0x11, "StatusBarInfo", PacketDirectionType.Output, PacketLengths.Variable);
        Add(0x12, "RequestSkillUse", PacketDirectionType.Input, PacketLengths.Variable);
        Add(0x13, "DropWearItem", PacketDirectionType.Input, 10);
        Add(0x14, "SendElevation", PacketDirectionType.Input, 6);
        Add(0x15, "Follow", PacketDirectionType.Input | PacketDirectionType.Output, 9);
        Add(0x16, "NewHealthBarStatusUpdate", PacketDirectionType.Output, PacketLengths.Variable);
        Add(0x17, "HealthBarStatusUpdate", PacketDirectionType.Output, 12);
        Add(0x1A, "ObjectInfo", PacketDirectionType.Output, PacketLengths.Variable);
        Add(0x1B, "CharLocaleAndBody", PacketDirectionType.Output, 37);
        Add(0x1C, "SendSpeech", PacketDirectionType.Output, PacketLengths.Variable);
        Add(0x1D, "DeleteObject", PacketDirectionType.Output, 5);
        Add(0x1E, "ControlAnimation", PacketDirectionType.Input, 4);
        Add(0x1F, "Explosion", PacketDirectionType.Output, 8);
        Add(0x20, "DrawGamePlayer", PacketDirectionType.Output, 19);
        Add(0x21, "CharMoveRejection", PacketDirectionType.Output, 8);
        Add(0x22, "CharacterMoveAck", PacketDirectionType.Input | PacketDirectionType.Output, 3);
        Add(0x23, "DraggingOfItem", PacketDirectionType.Output, 26);
        Add(0x24, "DrawContainer", PacketDirectionType.Output, 7);
        Add(0x25, "AddItemToContainer", PacketDirectionType.Output, 21);
        Add(0x26, "KickPlayer", PacketDirectionType.Output, 5);
        Add(0x27, "RejectMoveItemRequest", PacketDirectionType.Output, 2);
        Add(0x28, "DropItemFailed", PacketDirectionType.Output, 5);
        Add(0x29, "DropItemApproved", PacketDirectionType.Output, 1);
        Add(0x2A, "Blood", PacketDirectionType.Output, 5);
        Add(0x2B, "GodMode", PacketDirectionType.Output, 2);
        Add(0x2C, "ResurrectionMenu", PacketDirectionType.Input | PacketDirectionType.Output, 2);
        Add(0x2D, "MobAttributes", PacketDirectionType.Output, 17);
        Add(0x2E, "WornItem", PacketDirectionType.Output, 15);
        Add(0x2F, "FightOccurring", PacketDirectionType.Output, 10);
        Add(0x30, "AttackOk", PacketDirectionType.Output, 5);
        Add(0x31, "AttackEnded", PacketDirectionType.Output, 1);
        Add(0x32, "Unknown32", PacketDirectionType.Output, 2);
        Add(0x33, "PauseClient", PacketDirectionType.Output, 2);
        Add(0x34, "GetPlayerStatus", PacketDirectionType.Input, 10);
        Add(0x35, "AddResource", PacketDirectionType.Input, 653);
        Add(0x36, "ResourceTileData", PacketDirectionType.Output, PacketLengths.Variable);
        Add(0x37, "MoveItem", PacketDirectionType.Input, 8);
        Add(0x38, "PathfindingInClient", PacketDirectionType.Input, 7);
        Add(0x39, "RemoveGroup", PacketDirectionType.Input | PacketDirectionType.Output, 9);
        Add(0x3A, "SendSkills", PacketDirectionType.Input | PacketDirectionType.Output, PacketLengths.Variable);
        Add(0x3B, "BuyItems", PacketDirectionType.Input, PacketLengths.Variable);
        Add(0x3C, "AddMultipleItemsInContainer", PacketDirectionType.Output, PacketLengths.Variable);
        Add(0x3E, "Versions", PacketDirectionType.Output, 37);
        Add(0x3F, "UpdateStatics", PacketDirectionType.Output, PacketLengths.Variable);
        Add(0x45, "VersionOk", PacketDirectionType.Input, 5);
        Add(0x46, "NewArtwork", PacketDirectionType.Input, PacketLengths.Variable);
        Add(0x47, "NewTerrain", PacketDirectionType.Input, 11);
        Add(0x48, "NewAnimation", PacketDirectionType.Input, 73);
        Add(0x49, "NewHues", PacketDirectionType.Input, 93);
        Add(0x4A, "DeleteArt", PacketDirectionType.Input, 5);
        Add(0x4B, "CheckClientVersion", PacketDirectionType.Input, 9);
        Add(0x4C, "ScriptNames", PacketDirectionType.Input, PacketLengths.Variable);
        Add(0x4D, "EditScriptFile", PacketDirectionType.Input, PacketLengths.Variable);
        Add(0x4E, "PersonalLightLevel", PacketDirectionType.Output, 6);
        Add(0x4F, "OverallLightLevel", PacketDirectionType.Output, 2);
        Add(0x50, "BoardHeader", PacketDirectionType.Input, PacketLengths.Variable);
        Add(0x51, "BoardMessage", PacketDirectionType.Input, PacketLengths.Variable);
        Add(0x52, "BoardPostMessage", PacketDirectionType.Input, PacketLengths.Variable);
        Add(0x53, "RejectCharacterLogon", PacketDirectionType.Output, 2);
        Add(0x54, "PlaySoundEffect", PacketDirectionType.Output, 12);
        Add(0x55, "LoginComplete", PacketDirectionType.Output, 1);
        Add(0x56, "MapPacket", PacketDirectionType.Input | PacketDirectionType.Output, 11);
        Add(0x57, "UpdateRegions", PacketDirectionType.Input, 110);
        Add(0x58, "AddRegion", PacketDirectionType.Input, 106);
        Add(0x59, "NewContextFx", PacketDirectionType.Input, PacketLengths.Variable);
        Add(0x5A, "UpdateContextFx", PacketDirectionType.Input, PacketLengths.Variable);
        Add(0x5B, "Time", PacketDirectionType.Output, 4);
        Add(0x5C, "RestartVersion", PacketDirectionType.Input, 2);
        Add(0x5D, "LoginCharacter", PacketDirectionType.Input, 73);
        Add(0x5E, "ServerListing", PacketDirectionType.Input, PacketLengths.Variable);
        Add(0x5F, "ServerListAddEntry", PacketDirectionType.Input, 49);
        Add(0x60, "ServerListRemoveEntry", PacketDirectionType.Input, 5);
        Add(0x61, "RemoveStaticObject", PacketDirectionType.Input, 9);
        Add(0x62, "MoveStaticObject", PacketDirectionType.Input, 15);
        Add(0x63, "LoadArea", PacketDirectionType.Input, 13);
        Add(0x64, "LoadAreaRequest", PacketDirectionType.Input, 1);
        Add(0x65, "SetWeather", PacketDirectionType.Output, 4);
        Add(0x66, "BooksPages", PacketDirectionType.Input | PacketDirectionType.Output, PacketLengths.Variable);
        Add(0x69, "ChangeTextEmoteColors", PacketDirectionType.Input, 5);
        Add(0x6C, "TargetCursorCommands", PacketDirectionType.Input | PacketDirectionType.Output, 19);
        Add(0x6D, "PlayMidiMusic", PacketDirectionType.Output, 3);
        Add(0x6E, "CharacterAnimation", PacketDirectionType.Output, 14);
        Add(0x6F, "SecureTrading", PacketDirectionType.Input | PacketDirectionType.Output, PacketLengths.Variable);
        Add(0x70, "GraphicalEffect", PacketDirectionType.Output, 28);
        Add(0x71, "BulletinBoardMessages", PacketDirectionType.Input | PacketDirectionType.Output, PacketLengths.Variable);
        Add(0x72, "RequestWarMode", PacketDirectionType.Input | PacketDirectionType.Output, 5);
        Add(0x73, "PingMessage", PacketDirectionType.Input | PacketDirectionType.Output, 2);
        Add(0x74, "OpenBuyWindow", PacketDirectionType.Output, PacketLengths.Variable);
        Add(0x75, "RenameCharacter", PacketDirectionType.Input, 35);
        Add(0x76, "NewSubserver", PacketDirectionType.Output, 16);
        Add(0x77, "UpdatePlayer", PacketDirectionType.Output, 17);
        Add(0x78, "DrawObject", PacketDirectionType.Output, PacketLengths.Variable);
        Add(0x7C, "OpenDialogBox", PacketDirectionType.Output, PacketLengths.Variable);
        Add(0x7D, "ResponseToDialogBox", PacketDirectionType.Input, 13);
        Add(0x80, "LoginRequest", PacketDirectionType.Input, 62);
        Add(0x82, "LoginDenied", PacketDirectionType.Output, 2);
        Add(0x83, "DeleteCharacter", PacketDirectionType.Input, 39);
        Add(0x86, "ResendCharactersAfterDelete", PacketDirectionType.Output, 304);
        Add(0x88, "OpenPaperdoll", PacketDirectionType.Output, 66);
        Add(0x89, "CorpseClothing", PacketDirectionType.Output, PacketLengths.Variable);
        Add(0x8C, "ConnectToGameServer", PacketDirectionType.Output, 11);
        Add(0x8D, "CharacterCreationKR", PacketDirectionType.Input, 146);
        Add(0x90, "MapMessage", PacketDirectionType.Output, 19);
        Add(0x91, "GameServerLogin", PacketDirectionType.Input, 65);
        Add(0x93, "BookHeaderOld", PacketDirectionType.Input | PacketDirectionType.Output, 99);
        Add(0x95, "DyeWindow", PacketDirectionType.Input | PacketDirectionType.Output, 9);
        Add(0x97, "MovePlayer", PacketDirectionType.Output, 2);
        Add(0x98, "AllNames", PacketDirectionType.Input | PacketDirectionType.Output, PacketLengths.Variable);
        Add(0x99, "GiveBoatHousePlacementView", PacketDirectionType.Input | PacketDirectionType.Output, 26);
        Add(0x9A, "ConsoleEntryPrompt", PacketDirectionType.Input | PacketDirectionType.Output, PacketLengths.Variable);
        Add(0x9B, "RequestHelp", PacketDirectionType.Input, 258);
        Add(0x9C, "RequestAssistance", PacketDirectionType.Output, 53);
        Add(0x9E, "SellList", PacketDirectionType.Output, PacketLengths.Variable);
        Add(0x9F, "SellListReply", PacketDirectionType.Input, PacketLengths.Variable);
        Add(0xA0, "SelectServer", PacketDirectionType.Input, 3);
        Add(0xA1, "UpdateCurrentHealth", PacketDirectionType.Output, 9);
        Add(0xA2, "UpdateCurrentMana", PacketDirectionType.Output, 9);
        Add(0xA3, "UpdateCurrentStamina", PacketDirectionType.Output, 9);
        Add(0xA4, "ClientSpy", PacketDirectionType.Input, PacketLengths.Variable);
        Add(0xA5, "OpenWebBrowser", PacketDirectionType.Output, PacketLengths.Variable);
        Add(0xA6, "TipNoticeWindow", PacketDirectionType.Output, PacketLengths.Variable);
        Add(0xA7, "RequestTipNoticeWindow", PacketDirectionType.Input, 4);
        Add(0xA8, "GameServerList", PacketDirectionType.Output, PacketLengths.Variable);
        Add(0xA9, "CharactersStartingLocations", PacketDirectionType.Output, PacketLengths.Variable);
        Add(0xAA, "AllowRefuseAttack", PacketDirectionType.Output, 5);
        Add(0xAB, "GumpTextEntryDialog", PacketDirectionType.Output, PacketLengths.Variable);
        Add(0xAC, "GumpTextEntryDialogReply", PacketDirectionType.Input, PacketLengths.Variable);
        Add(0xAD, "UnicodeAsciiSpeechRequest", PacketDirectionType.Input, PacketLengths.Variable);
        Add(0xAE, "UnicodeSpeechMessage", PacketDirectionType.Output, PacketLengths.Variable);
        Add(0xAF, "DisplayDeathAction", PacketDirectionType.Output, 13);
        Add(0xB0, "SendGumpMenuDialog", PacketDirectionType.Output, PacketLengths.Variable);
        Add(0xB1, "GumpMenuSelection", PacketDirectionType.Input, PacketLengths.Variable);
        Add(0xB2, "ChatMessage", PacketDirectionType.Output, PacketLengths.Variable);
        Add(0xB3, "ChatText", PacketDirectionType.Input, PacketLengths.Variable);
        Add(0xB5, "OpenChatWindow", PacketDirectionType.Input, 64);
        Add(0xB6, "SendHelpTipRequest", PacketDirectionType.Input, 9);
        Add(0xB7, "HelpTipData", PacketDirectionType.Output, PacketLengths.Variable);
        Add(0xB8, "RequestCharProfile", PacketDirectionType.Input | PacketDirectionType.Output, PacketLengths.Variable);
        Add(0xB9, "EnableLockedClientFeatures", PacketDirectionType.Output, 5);
        Add(0xBA, "QuestArrow", PacketDirectionType.Output, 10);
        Add(0xBB, "UltimaMessenger", PacketDirectionType.Input | PacketDirectionType.Output, 9);
        Add(0xBC, "SeasonalInformation", PacketDirectionType.Output, 3);
        Add(0xBD, "ClientVersion", PacketDirectionType.Input | PacketDirectionType.Output, PacketLengths.Variable);
        Add(0xBE, "AssistVersion", PacketDirectionType.Input | PacketDirectionType.Output, PacketLengths.Variable);
        Add(0xBF, "GeneralInformationPacket", PacketDirectionType.Input | PacketDirectionType.Output, PacketLengths.Variable);
        Add(0xC0, "GraphicalEffectHued", PacketDirectionType.Output, 36);
        Add(0xC1, "ClilocMessage", PacketDirectionType.Output, PacketLengths.Variable);
        Add(0xC2, "UnicodeTextEntry", PacketDirectionType.Input | PacketDirectionType.Output, PacketLengths.Variable);
        Add(0xC4, "Semivisible", PacketDirectionType.Output, 6);
        Add(0xC5, "InvalidMapRequest", PacketDirectionType.Input, 1);
        Add(0xC6, "InvalidMapEnable", PacketDirectionType.Output, 1);
        Add(0xC7, "ParticleEffect3d", PacketDirectionType.Output, 49);
        Add(0xC8, "ClientViewRange", PacketDirectionType.Input | PacketDirectionType.Output, 2);
        Add(0xC9, "GetAreaServerPing", PacketDirectionType.Input | PacketDirectionType.Output, 6);
        Add(0xCA, "GetUserServerPing", PacketDirectionType.Input | PacketDirectionType.Output, 6);
        Add(0xCB, "GlobalQueCount", PacketDirectionType.Output, 7);
        Add(0xCC, "ClilocMessageAffix", PacketDirectionType.Output, PacketLengths.Variable);
        Add(0xD0, "ConfigurationFile", PacketDirectionType.Input | PacketDirectionType.Output, PacketLengths.Variable);
        Add(0xD1, "LogoutStatus", PacketDirectionType.Input | PacketDirectionType.Output, 2);
        Add(0xD2, "Extended0x20", PacketDirectionType.Output, 25);
        Add(0xD3, "Extended0x78", PacketDirectionType.Output, PacketLengths.Variable);
        Add(0xD4, "BookHeaderNew", PacketDirectionType.Input | PacketDirectionType.Output, PacketLengths.Variable);
        Add(0xD6, "MegaCliloc", PacketDirectionType.Input | PacketDirectionType.Output, PacketLengths.Variable);
        Add(0xD7, "GenericAosCommands", PacketDirectionType.Input | PacketDirectionType.Output, PacketLengths.Variable);
        Add(0xD8, "SendCustomHouse", PacketDirectionType.Output, PacketLengths.Variable);
        Add(0xD9, "SpyOnClient", PacketDirectionType.Input, PacketLengths.Variable);
        Add(0xDB, "CharacterTransferLog", PacketDirectionType.Output, PacketLengths.Variable);
        Add(0xDC, "SeIntroducedRevision", PacketDirectionType.Output, 9);
        Add(0xDD, "CompressedGump", PacketDirectionType.Output, PacketLengths.Variable);
        Add(0xDE, "UpdateMobileStatus", PacketDirectionType.Output, PacketLengths.Variable);
        Add(0xDF, "BuffDebuffSystem", PacketDirectionType.Output, PacketLengths.Variable);
        Add(0xE0, "BugReportKR", PacketDirectionType.Input, PacketLengths.Variable);
        Add(0xE1, "ClientTypeKRSA", PacketDirectionType.Input, 9);
        Add(0xE2, "NewCharacterAnimationKR", PacketDirectionType.Output, 10);
        Add(0xE3, "KREncryptionResponse", PacketDirectionType.Output, 77);
        Add(0xEC, "EquipMacroKR", PacketDirectionType.Input, PacketLengths.Variable);
        Add(0xED, "UnequipItemMacroKR", PacketDirectionType.Input, PacketLengths.Variable);
        Add(0xEF, "LoginSeed", PacketDirectionType.Input, 21);
        Add(0xF0, "KrriosClientSpecial", PacketDirectionType.Output, PacketLengths.Variable);
        Add(0xF1, "FreeshardList", PacketDirectionType.Input | PacketDirectionType.Output, PacketLengths.Variable);
        Add(0xF3, "ObjectInformationSA", PacketDirectionType.Output, 24);
        Add(0xF5, "NewMapMessage", PacketDirectionType.Output, 21);
        Add(0xF8, "CharacterCreationNew", PacketDirectionType.Input, 106);
        Add(0xFA, "OpenUoStore", PacketDirectionType.Input, 1);
        Add(0xFB, "UpdateViewPublicHouseContents", PacketDirectionType.Input, 2);

        return table;
    }
}
