using Moongate.Core.Server.Interfaces.Services;

namespace Moongate.Server.Packets;

using System;

/// <summary>
/// Static class for registering all Ultima Online packets
/// </summary>
public static class PacketRegistration
{
    /// <summary>
    /// Registers all UO packets with the network service
    /// </summary>
    /// <param name="networkService">The network service to register packets with</param>
    public static void RegisterPackets(INetworkService networkService)
    {
        RegisterMovementPackets(networkService);
        RegisterCombatPackets(networkService);
        RegisterCommunicationPackets(networkService);
        RegisterItemInteractionPackets(networkService);
        RegisterCharacterPackets(networkService);
        RegisterContainerPackets(networkService);
        RegisterGumpPackets(networkService);
        RegisterSkillPackets(networkService);
        RegisterTradePackets(networkService);
        RegisterMapPackets(networkService);
        RegisterBookPackets(networkService);
        RegisterEffectPackets(networkService);
        RegisterSoundPackets(networkService);
        RegisterSystemPackets(networkService);
        RegisterLoginPackets(networkService);
        RegisterGodClientPackets(networkService);
        RegisterChatPackets(networkService);
        RegisterMiscPackets(networkService);
    }

    #region Movement Packets

    /// <summary>
    /// Register movement related packets
    /// </summary>
    private static void RegisterMovementPackets(INetworkService networkService)
    {
        /// Move Request
        networkService.RegisterPacket(0x02, 7, "Move Request");

        /// Character Move ACK/Resync Request
        networkService.RegisterPacket(0x22, 3, "Character Move ACK/Resync Request");

        /// Pathfinding in Client
        networkService.RegisterPacket(0x38, 7, "Pathfinding in Client");

        /// Control Animation
        networkService.RegisterPacket(0x1E, 8, "Control Animation");

        /// Follow
        networkService.RegisterPacket(0x15, 9, "Follow");
    }

    #endregion

    #region Combat Packets

    /// <summary>
    /// Register combat related packets
    /// </summary>
    private static void RegisterCombatPackets(INetworkService networkService)
    {
        /// Request Attack
        networkService.RegisterPacket(0x05, 5, "Request Attack");

        /// Request War Mode
        networkService.RegisterPacket(0x72, 5, "Request War Mode");

        /// Generic AOS Commands
        networkService.RegisterPacket(0xD7, -1, "Generic AOS Commands");
    }

    #endregion

    #region Communication Packets

    /// <summary>
    /// Register communication related packets
    /// </summary>
    private static void RegisterCommunicationPackets(INetworkService networkService)
    {
        /// Talk Request
        networkService.RegisterPacket(0x03, -1, "Talk Request");

        /// Unicode/Ascii speech request
        networkService.RegisterPacket(0xAD, -1, "Unicode/Ascii speech request");

        /// Unicode TextEntry
        networkService.RegisterPacket(0xC2, -1, "Unicode TextEntry");

        /// Chat Text
        networkService.RegisterPacket(0xB3, -1, "Chat Text");

        /// Open Chat Window
        networkService.RegisterPacket(0xB5, 64, "Open Chat Window");

        /// Bulletin Board Messages
        networkService.RegisterPacket(0x71, -1, "Bulletin Board Messages");

        /// Board Header
        networkService.RegisterPacket(0x50, -1, "Board Header");

        /// Board Message
        networkService.RegisterPacket(0x51, -1, "Board Message");

        /// Board Post Message
        networkService.RegisterPacket(0x52, -1, "Board Post Message");
    }

    #endregion

    #region Item Interaction Packets

    /// <summary>
    /// Register item interaction packets
    /// </summary>
    private static void RegisterItemInteractionPackets(INetworkService networkService)
    {
        /// Double Click
        networkService.RegisterPacket(0x06, 5, "Double Click");

        /// Single Click
        networkService.RegisterPacket(0x09, 5, "Single Click");

        /// Pick Up Item
        networkService.RegisterPacket(0x07, 7, "Pick Up Item");

        /// Drop Item
        networkService.RegisterPacket(0x08, 14, "Drop Item");

        /// Drop->Wear Item
        networkService.RegisterPacket(0x13, 10, "Drop->Wear Item");

        /// Dye Window
        networkService.RegisterPacket(0x95, 9, "Dye Window");
    }

    #endregion

    #region Character Packets

    /// <summary>
    /// Register character related packets
    /// </summary>
    private static void RegisterCharacterPackets(INetworkService networkService)
    {
        /// Create Character
        networkService.RegisterPacket(0x00, 104, "Create Character");

        networkService.RegisterPacket(0xF8, 106, "Create Character");
        /// Character Creation (KR + SA 3D clients only)
        networkService.RegisterPacket(0x8D, -1, "Character Creation (KR + SA 3D clients only)");

        /// Character Creation (7.0.16.0)
        networkService.RegisterPacket(0xF8, -1, "Character Creation (7.0.16.0)");

        /// Delete Character
        networkService.RegisterPacket(0x83, 39, "Delete Character");

        /// Login Character
        networkService.RegisterPacket(0x5D, 73, "Login Character");

        /// Rename Character
        networkService.RegisterPacket(0x75, 35, "Rename Character");

        /// Get Player Status
        networkService.RegisterPacket(0x34, 10, "Get Player Status");

        /// Request/Char Profile
        networkService.RegisterPacket(0xB8, -1, "Request/Char Profile");

        /// Resurrection Menu
        networkService.RegisterPacket(0x2C, 2, "Resurrection Menu");

        /// All Names (3D Client Only)
        networkService.RegisterPacket(0x98, -1, "All Names (3D Client Only)");
    }

    #endregion

    #region Container Packets

    /// <summary>
    /// Register container and inventory packets
    /// </summary>
    private static void RegisterContainerPackets(INetworkService networkService)
    {
        /// Buy Item(s)
        networkService.RegisterPacket(0x3B, -1, "Buy Item(s)");

        /// Sell List Reply
        networkService.RegisterPacket(0x9F, -1, "Sell List Reply");
    }

    #endregion

    #region Gump Packets

    /// <summary>
    /// Register gump and dialog packets
    /// </summary>
    private static void RegisterGumpPackets(INetworkService networkService)
    {
        /// Gump Menu Selection
        networkService.RegisterPacket(0xB1, -1, "Gump Menu Selection");

        /// Gump Text Entry Dialog Reply
        networkService.RegisterPacket(0xAC, -1, "Gump Text Entry Dialog Reply");

        /// Response To Dialog Box
        networkService.RegisterPacket(0x7D, 13, "Response To Dialog Box");
    }

    #endregion

    #region Skill Packets

    /// <summary>
    /// Register skill related packets
    /// </summary>
    private static void RegisterSkillPackets(INetworkService networkService)
    {
        /// Request Skill etc use
        networkService.RegisterPacket(0x12, -1, "Request Skill etc use");

        /// Send Skills
        networkService.RegisterPacket(0x3A, -1, "Send Skills");
    }

    #endregion

    #region Trade Packets

    /// <summary>
    /// Register trading packets
    /// </summary>
    private static void RegisterTradePackets(INetworkService networkService)
    {
        /// Secure Trading
        networkService.RegisterPacket(0x6F, -1, "Secure Trading");
    }

    #endregion

    #region Map Packets

    /// <summary>
    /// Register map and location packets
    /// </summary>
    private static void RegisterMapPackets(INetworkService networkService)
    {
        /// Map Packet (cartography/treasure)
        networkService.RegisterPacket(0x56, 11, "Map Packet (cartography/treasure)");

        /// Invalid Map (Request?)
        networkService.RegisterPacket(0xC5, 1, "Invalid Map (Request?)");

        /// Client View Range
        networkService.RegisterPacket(0xC8, 2, "Client View Range");
    }

    #endregion

    #region Book Packets

    /// <summary>
    /// Register book related packets
    /// </summary>
    private static void RegisterBookPackets(INetworkService networkService)
    {
        /// Books (Pages)
        networkService.RegisterPacket(0x66, -1, "Books (Pages)");

        /// Book Header (Old)
        networkService.RegisterPacket(0x93, 99, "Book Header (Old)");

        /// Book Header (New)
        networkService.RegisterPacket(0xD4, -1, "Book Header (New)");
    }

    #endregion

    #region Effect Packets

    /// <summary>
    /// Register visual effect packets
    /// </summary>
    private static void RegisterEffectPackets(INetworkService networkService)
    {
        /// Target Cursor Commands
        networkService.RegisterPacket(0x6C, 19, "Target Cursor Commands");

        /// Change Text/Emote Colors
        networkService.RegisterPacket(0x69, 5, "Change Text/Emote Colors");
    }

    #endregion

    #region Sound Packets

    /// <summary>
    /// Register sound related packets
    /// </summary>
    private static void RegisterSoundPackets(INetworkService networkService)
    {
        // No client-only sound packets found in the documentation
    }

    #endregion

    #region System Packets

    /// <summary>
    /// Register system and network packets
    /// </summary>
    private static void RegisterSystemPackets(INetworkService networkService)
    {
        /// Disconnect Notification
        networkService.RegisterPacket(0x01, 5, "Disconnect Notification");

        /// Ping Message
        networkService.RegisterPacket(0x73, 2, "Ping Message");

        /// General Information Packet
        networkService.RegisterPacket(0xBF, -1, "General Information Packet");

        /// Client Version
        networkService.RegisterPacket(0xBD, -1, "Client Version");

        /// Assist Version
        networkService.RegisterPacket(0xBE, -1, "Assist Version");

        /// Client Spy
        networkService.RegisterPacket(0xA4, -1, "Client Spy");

        /// Spy On Client
        networkService.RegisterPacket(0xD9, -1, "Spy On Client");

        /// Configuration File
        networkService.RegisterPacket(0xD0, -1, "Configuration File");

        /// Logout Status
        networkService.RegisterPacket(0xD1, 2, "Logout Status");

        /// Mega Cliloc
        networkService.RegisterPacket(0xD6, -1, "Mega Cliloc");

        /// Get Area Server Ping (God Client)
        networkService.RegisterPacket(0xC9, 6, "Get Area Server Ping (God Client)");

        /// Get User Server Ping (God Client)
        networkService.RegisterPacket(0xCA, 6, "Get User Server Ping (God Client)");

        /// Client Type (KR/SA)
        networkService.RegisterPacket(0xE1, -1, "Client Type (KR/SA)");

        /// KR/2D Client Login/Seed
        networkService.RegisterPacket(0xEF, 21, "KR/2D Client Login/Seed");
    }

    #endregion

    #region Login Packets

    /// <summary>
    /// Register login and server selection packets
    /// </summary>
    private static void RegisterLoginPackets(INetworkService networkService)
    {
        /// Login Request
        networkService.RegisterPacket(0x80, 62, "Login Request");

        /// Game Server Login
        networkService.RegisterPacket(0x91, 65, "Game Server Login");

        /// Select Server
        networkService.RegisterPacket(0xA0, 3, "Select Server");

        /// Server Listing
        networkService.RegisterPacket(0x5E, -1, "Server Listing");

        /// Server List Add Entry
        networkService.RegisterPacket(0x5F, -1, "Server List Add Entry");

        /// Server List Remove Entry
        networkService.RegisterPacket(0x60, -1, "Server List Remove Entry");

        /// Freeshard List
        networkService.RegisterPacket(0xF1, -1, "Freeshard List");

        /// Login Denied
        networkService.RegisterPacket(0x82, 2, "Login Denied");


        networkService.RegisterPacket(0x8C, 11, "Connect To Game Server");

    }

    #endregion

    #region God Client Packets

    /// <summary>
    /// Register God Client specific packets
    /// </summary>
    private static void RegisterGodClientPackets(INetworkService networkService)
    {
        /// Request God Mode (God Client)
        networkService.RegisterPacket(0x04, 2, "Request God Mode (God Client)");

        /// Edit (God Client)
        networkService.RegisterPacket(0x0A, 11, "Edit (God Client)");

        /// Edit Tile Data (God Client)
        networkService.RegisterPacket(0x0C, -1, "Edit Tile Data (God Client)");

        /// Send Elevation (God Client)
        networkService.RegisterPacket(0x14, 6, "Send Elevation (God Client)");

        /// Add Resource (God Client)
        networkService.RegisterPacket(0x35, 653, "Add Resource (God Client)");

        /// Move Item (God Client)
        networkService.RegisterPacket(0x37, 8, "Move Item (God Client)");
    }

    #endregion

    #region Chat Packets

    /// <summary>
    /// Register chat system packets
    /// </summary>
    private static void RegisterChatPackets(INetworkService networkService)
    {
        /// Ultima Messenger
        networkService.RegisterPacket(0xBB, 9, "Ultima Messenger");

        /// Console Entry Prompt
        networkService.RegisterPacket(0x9A, 16, "Console Entry Prompt");
    }

    #endregion

    #region Misc Packets

    /// <summary>
    /// Register miscellaneous packets
    /// </summary>
    private static void RegisterMiscPackets(INetworkService networkService)
    {
        /// Remove (Group)
        networkService.RegisterPacket(0x39, 9, "Remove (Group)");

        /// Version OK
        networkService.RegisterPacket(0x45, -1, "Version OK");

        /// New Artwork
        networkService.RegisterPacket(0x46, -1, "New Artwork");

        /// New Terrain
        networkService.RegisterPacket(0x47, -1, "New Terrain");

        /// New Animation
        networkService.RegisterPacket(0x48, -1, "New Animation");

        /// New Hues
        networkService.RegisterPacket(0x49, -1, "New Hues");

        /// Delete Art
        networkService.RegisterPacket(0x4A, -1, "Delete Art");

        /// Check Client Version
        networkService.RegisterPacket(0x4B, -1, "Check Client Version");

        /// Script Names
        networkService.RegisterPacket(0x4C, -1, "Script Names");

        /// Edit Script File
        networkService.RegisterPacket(0x4D, -1, "Edit Script File");

        /// Update Regions
        networkService.RegisterPacket(0x57, -1, "Update Regions");

        /// Add Region
        networkService.RegisterPacket(0x58, -1, "Add Region");

        /// New Context FX
        networkService.RegisterPacket(0x59, -1, "New Context FX");

        /// Update Context FX
        networkService.RegisterPacket(0x5A, -1, "Update Context FX");

        /// Restart Version
        networkService.RegisterPacket(0x5C, -1, "Restart Version");

        /// Remove Static Object
        networkService.RegisterPacket(0x61, -1, "Remove Static Object");

        /// Move Static Object
        networkService.RegisterPacket(0x62, -1, "Move Static Object");

        /// Load Area
        networkService.RegisterPacket(0x63, -1, "Load Area");

        /// Load Area Request
        networkService.RegisterPacket(0x64, -1, "Load Area Request");

        /// Give Boat/House Placement View
        networkService.RegisterPacket(0x99, 26, "Give Boat/House Placement View");

        /// Request Help
        networkService.RegisterPacket(0x9B, 258, "Request Help");

        /// Request Tip/Notice Window
        networkService.RegisterPacket(0xA7, 4, "Request Tip/Notice Window");

        /// Send Help/Tip Request
        networkService.RegisterPacket(0xB6, 9, "Send Help/Tip Request");

        /// Bug Report (KR)
        networkService.RegisterPacket(0xE0, -1, "Bug Report (KR)");

        /// Equip Macro (KR)
        networkService.RegisterPacket(0xEC, -1, "Equip Macro (KR)");

        /// Unequip Item Macro (KR)
        networkService.RegisterPacket(0xED, -1, "Unequip Item Macro (KR)");

        /// Open UO Store
        networkService.RegisterPacket(0xFA, -1, "Open UO Store");

        /// Update View Public House Contents
        networkService.RegisterPacket(0xFB, -1, "Update View Public House Contents");
    }

    #endregion
}
