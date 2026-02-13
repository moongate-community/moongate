using Moongate.Core.Server.Interfaces.Services;

namespace Moongate.Server.Packets;

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
        RegisterServerOnlyPackets(networkService);
        RegisterKRSAPackets(networkService);
        RegisterAOSPackets(networkService);
        RegisterHSPackets(networkService);
        RegisterAdditionalClientPackets(networkService);
    }

#region Additional Client Packets

    /// <summary>
    /// Register additional client-only packets
    /// </summary>
    private static void RegisterAdditionalClientPackets(INetworkService networkService)
    {
        /// Edit Area (God Client)
        networkService.RegisterPacket(0x0B, 266, "Edit Area (God Client)");
    }

#endregion

#region AOS Packets

    /// <summary>
    /// Register Age of Shadows specific packets
    /// </summary>
    private static void RegisterAOSPackets(INetworkService networkService)
    {
        /// Generic AOS Commands (already registered in combat)
        // networkService.RegisterPacket(0xD7, -1, "Generic AOS Commands");

        /// Buff/Debuff System (already registered in server only)
        // networkService.RegisterPacket(0xDF, -1, "Buff/Debuff System");
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

#region Character Packets

    /// <summary>
    /// Register character related packets
    /// </summary>
    private static void RegisterCharacterPackets(INetworkService networkService)
    {
        /// Create Character
        networkService.RegisterPacket(0x00, 104, "Create Character");

        /// Character Creation (KR + SA 3D clients only)
        networkService.RegisterPacket(0x8D, -1, "Character Creation (KR + SA 3D clients only)");

        /// Character Creation (7.0.16.0)
        networkService.RegisterPacket(0xF8, 106, "Character Creation (7.0.16.0)");

        /// Delete Character
        networkService.RegisterPacket(0x83, 39, "Delete Character");

        /// Login Character
        networkService.RegisterPacket(0x5D, 73, "Login Character");

        /// Rename Character
        networkService.RegisterPacket(0x75, 35, "Rename Character");

        /// Get Player Status
        networkService.RegisterPacket(0x34, 10, "Get Player Status");

        /// Status Bar Info
        networkService.RegisterPacket(0x11, -1, "Status Bar Info");

        /// Request/Char Profile
        networkService.RegisterPacket(0xB8, -1, "Request/Char Profile");

        /// Resurrection Menu
        networkService.RegisterPacket(0x2C, 2, "Resurrection Menu");

        /// All Names (3D Client Only)
        networkService.RegisterPacket(0x98, -1, "All Names (3D Client Only)");

        /// Draw Game Player
        networkService.RegisterPacket(0x20, 19, "Draw Game Player");

        /// Char Locale and Body
        networkService.RegisterPacket(0x1B, 37, "Char Locale and Body");

        /// Update Player
        networkService.RegisterPacket(0x77, 17, "Update Player");

        /// Draw Object
        networkService.RegisterPacket(0x78, -1, "Draw Object");

        /// Resend Characters After Delete
        networkService.RegisterPacket(0x86, -1, "Resend Characters After Delete");

        /// Open Paperdoll
        networkService.RegisterPacket(0x88, 66, "Open Paperdoll");

        /// Corpse Clothing
        networkService.RegisterPacket(0x89, -1, "Corpse Clothing");

        /// Display Death Action
        networkService.RegisterPacket(0xAF, 13, "Display Death Action");

        /// Characters / Starting Locations
        networkService.RegisterPacket(0xA9, -1, "Characters / Starting Locations");

        /// Mob Attributes
        networkService.RegisterPacket(0x2D, 17, "Mob Attributes");

        /// Character Animation
        networkService.RegisterPacket(0x6E, 14, "Character Animation");
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
        networkService.RegisterPacket(0x9A, -1, "Console Entry Prompt");
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

        /// Attack Ok
        networkService.RegisterPacket(0x30, 5, "Attack Ok");

        /// Attack Ended
        networkService.RegisterPacket(0x31, 5, "Attack Ended");

        /// Fight Occurring
        networkService.RegisterPacket(0x2F, 10, "Fight Occurring");

        /// Allow/Refuse Attack
        networkService.RegisterPacket(0xAA, 5, "Allow/Refuse Attack");
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

        /// Send Speech
        networkService.RegisterPacket(0x1C, -1, "Send Speech");

        /// Unicode/Ascii speech request
        networkService.RegisterPacket(0xAD, -1, "Unicode/Ascii speech request");

        /// Unicode Speech message
        networkService.RegisterPacket(0xAE, -1, "Unicode Speech message");

        /// Unicode TextEntry
        networkService.RegisterPacket(0xC2, -1, "Unicode TextEntry");

        /// Chat Text
        networkService.RegisterPacket(0xB3, -1, "Chat Text");

        /// Open Chat Window
        networkService.RegisterPacket(0xB5, 64, "Open Chat Window");

        /// Chat Message
        networkService.RegisterPacket(0xB2, -1, "Chat Message");

        /// Bulletin Board Messages
        networkService.RegisterPacket(0x71, -1, "Bulletin Board Messages");

        /// Board Header
        networkService.RegisterPacket(0x50, -1, "Board Header");

        /// Board Message
        networkService.RegisterPacket(0x51, -1, "Board Message");

        /// Board Post Message
        networkService.RegisterPacket(0x52, -1, "Board Post Message");

        /// Cliloc Message
        networkService.RegisterPacket(0xC1, -1, "Cliloc Message");

        /// Cliloc Message Affix
        networkService.RegisterPacket(0xCC, -1, "Cliloc Message Affix");
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

        /// Draw Container
        networkService.RegisterPacket(0x24, 7, "Draw Container");

        /// Add Item To Container
        networkService.RegisterPacket(0x25, 20, "Add Item To Container");

        /// Add multiple Items In Container
        networkService.RegisterPacket(0x3C, -1, "Add multiple Items In Container");

        /// Open Buy Window
        networkService.RegisterPacket(0x74, -1, "Open Buy Window");

        /// Sell List
        networkService.RegisterPacket(0x9E, -1, "Sell List");
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

        /// Graphical Effect
        networkService.RegisterPacket(0x70, 28, "Graphical Effect");

        /// Graphical Effect (Enhanced)
        networkService.RegisterPacket(0xC0, 36, "Graphical Effect (Enhanced)");

        /// 3D Particle Effect
        networkService.RegisterPacket(0xC7, 49, "3D Particle Effect");

        /// Blood
        networkService.RegisterPacket(0x2A, 5, "Blood");

        /// Semivisible
        networkService.RegisterPacket(0xC4, 6, "Semivisible");
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

        /// God Mode (God Client)
        networkService.RegisterPacket(0x2B, 2, "God Mode (God Client)");

        /// Resource Tile Data (God Client)
        networkService.RegisterPacket(0x36, -1, "Resource Tile Data (God Client)");

        /// Versions (God Client)
        networkService.RegisterPacket(0x3E, -1, "Versions (God Client)");

        /// Update Statics (God Client)
        networkService.RegisterPacket(0x3F, -1, "Update Statics (God Client)");
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

        /// Send Gump Menu Dialog
        networkService.RegisterPacket(0xB0, -1, "Send Gump Menu Dialog");

        /// Gump Text Entry Dialog
        networkService.RegisterPacket(0xAB, -1, "Gump Text Entry Dialog");

        /// Open Dialog Box
        networkService.RegisterPacket(0x7C, -1, "Open Dialog Box");

        /// Compressed Gump
        networkService.RegisterPacket(0xDD, -1, "Compressed Gump");
    }

#endregion

#region HS Packets

    /// <summary>
    /// Register High Seas specific packets
    /// </summary>
    private static void RegisterHSPackets(INetworkService networkService)
    {
        // High Seas packets are generally extensions of existing packets
        // Most new functionality uses existing packet IDs with new subcodes
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
        networkService.RegisterPacket(0x08, 15, "Drop Item");

        /// Drop->Wear Item
        networkService.RegisterPacket(0x13, 10, "Drop->Wear Item");

        /// Dye Window
        networkService.RegisterPacket(0x95, 9, "Dye Window");

        /// Object Info
        networkService.RegisterPacket(0x1A, -1, "Object Info");

        /// Delete Object
        networkService.RegisterPacket(0x1D, 5, "Delete Object");

        /// Dragging Of Item
        networkService.RegisterPacket(0x23, 26, "Dragging Of Item");

        /// Reject Move Item Request
        networkService.RegisterPacket(0x27, 2, "Reject Move Item Request");

        /// Drop Item Failed/Clear Square
        networkService.RegisterPacket(0x28, 5, "Drop Item Failed/Clear Square");

        /// Drop Item Approved
        networkService.RegisterPacket(0x29, 1, "Drop Item Approved");

        /// Worn Item
        networkService.RegisterPacket(0x2E, 15, "Worn Item");
    }

#endregion

#region KR/SA Packets

    /// <summary>
    /// Register KR and SA specific packets
    /// </summary>
    private static void RegisterKRSAPackets(INetworkService networkService)
    {
        /// New Character Animation (KR)
        networkService.RegisterPacket(0xE2, -1, "New Character Animation (KR)");

        /// KR Encryption Response
        networkService.RegisterPacket(0xE3, -1, "KR Encryption Response");

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

        /// Connect To Game Server
        networkService.RegisterPacket(0x8C, 11, "Connect To Game Server");

        /// Game Server List
        networkService.RegisterPacket(0xA8, -1, "Game Server List");

        /// Reject Character Logon
        networkService.RegisterPacket(0x53, 2, "Reject Character Logon");
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

        /// Map Message
        networkService.RegisterPacket(0x90, 19, "Map Message");

        /// Invalid Map Enable
        networkService.RegisterPacket(0xC6, -1, "Invalid Map Enable");

        /// New Map Message
        networkService.RegisterPacket(0xF5, -1, "New Map Message");
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
    }

#endregion

#region Movement Packets

    /// <summary>
    /// Register movement related packets
    /// </summary>
    private static void RegisterMovementPackets(INetworkService networkService)
    {
        /// Move Request
        networkService.RegisterPacket(0x02, 7, "Move Request");

        /// Character Move Rejection
        networkService.RegisterPacket(0x21, 8, "Character Move Rejection");

        /// Character Move ACK/Resync Request
        networkService.RegisterPacket(0x22, 3, "Character Move ACK/Resync Request");

        /// Pathfinding in Client
        networkService.RegisterPacket(0x38, 7, "Pathfinding in Client");

        /// Control Animation
        networkService.RegisterPacket(0x1E, 8, "Control Animation");

        /// Follow
        networkService.RegisterPacket(0x15, 9, "Follow");

        /// Move Player
        networkService.RegisterPacket(0x97, 2, "Move Player");
    }

#endregion

#region Server Only Packets

    /// <summary>
    /// Register server-only packets
    /// </summary>
    private static void RegisterServerOnlyPackets(INetworkService networkService)
    {
        /// Damage
        networkService.RegisterPacket(0x0B, -1, "Damage");

        /// New Health bar status update (SA)
        networkService.RegisterPacket(0x16, -1, "New Health bar status update (SA)");

        /// Health bar status update (KR)
        networkService.RegisterPacket(0x17, -1, "Health bar status update (KR)");

        /// Unknown
        networkService.RegisterPacket(0x32, 2, "Unknown");

        /// New Subserver
        networkService.RegisterPacket(0x76, 16, "New Subserver");

        /// Request Assistance
        networkService.RegisterPacket(0x9C, -1, "Request Assistance");

        /// Update Current Health
        networkService.RegisterPacket(0xA1, 9, "Update Current Health");

        /// Update Current Mana
        networkService.RegisterPacket(0xA2, 9, "Update Current Mana");

        /// Update Current Stamina
        networkService.RegisterPacket(0xA3, 9, "Update Current Stamina");

        /// Open Web Browser
        networkService.RegisterPacket(0xA5, -1, "Open Web Browser");

        /// Tip/Notice Window
        networkService.RegisterPacket(0xA6, -1, "Tip/Notice Window");

        /// Help/Tip Data
        networkService.RegisterPacket(0xB7, -1, "Help/Tip Data");

        /// Extended 0x20
        networkService.RegisterPacket(0xD2, 25, "Extended 0x20");

        /// Extended 0x78
        networkService.RegisterPacket(0xD3, -1, "Extended 0x78");

        /// Send Custom House
        networkService.RegisterPacket(0xD8, -1, "Send Custom House");

        /// Character Transfer Log
        networkService.RegisterPacket(0xDB, -1, "Character Transfer Log");

        /// SE Introduced Revision
        networkService.RegisterPacket(0xDC, -1, "SE Introduced Revision");

        /// Update Mobile Status
        networkService.RegisterPacket(0xDE, -1, "Update Mobile Status");

        /// Buff/Debuff System
        networkService.RegisterPacket(0xDF, -1, "Buff/Debuff System");

        /// Krrios client special
        networkService.RegisterPacket(0xF0, -1, "Krrios client special");

        /// Object Information (SA)
        networkService.RegisterPacket(0xF3, -1, "Object Information (SA)");
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

#region Sound Packets

    /// <summary>
    /// Register sound related packets
    /// </summary>
    private static void RegisterSoundPackets(INetworkService networkService)
    {
        /// Play Sound Effect
        networkService.RegisterPacket(0x54, 12, "Play Sound Effect");

        /// Play Midi Music
        networkService.RegisterPacket(0x6D, 3, "Play Midi Music");
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
        networkService.RegisterPacket(0xA4, 149, "Client Spy");

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

        /// Pause Client
        networkService.RegisterPacket(0x33, 2, "Pause Client");

        /// Kick Player
        networkService.RegisterPacket(0x26, 5, "Kick Player");

        /// Time
        networkService.RegisterPacket(0x5B, 4, "Time");

        /// Set Weather
        networkService.RegisterPacket(0x65, 4, "Set Weather");

        /// Personal Light Level
        networkService.RegisterPacket(0x4E, 6, "Personal Light Level");

        /// Overall Light Level
        networkService.RegisterPacket(0x4F, 2, "Overall Light Level");

        /// Login Complete
        networkService.RegisterPacket(0x55, 1, "Login Complete");

        /// Seasonal Information
        networkService.RegisterPacket(0xBC, 3, "Seasonal Information");

        /// Enable locked client features
        networkService.RegisterPacket(0xB9, 3, "Enable locked client features");

        /// Quest Arrow
        networkService.RegisterPacket(0xBA, 6, "Quest Arrow");

        /// Global Que Count
        networkService.RegisterPacket(0xCB, 7, "Global Que Count");
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
}
