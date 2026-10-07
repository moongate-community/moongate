using System.Collections.Frozen;

namespace Moongate.Server.Ultima.Data.Internal.ContextMenus;

/// <summary>
///     The fixed numbers the Enhanced Client sends for an entry of a context menu chosen from one of its own icons,
///     such as those of a vendor's status bar, instead of the entry's place in the menu: each stands for a text of
///     the client, and so for the entry of the menu that shows it.
/// </summary>
/// <remarks>
///     ServUO's <c>ContextMenu.GetIndexEC</c>. Every number is 0x64 or more, above any place in a menu.
/// </remarks>
internal static class EnhancedClientMenuIndexes
{
    /// <summary>
    ///     The lowest of the fixed numbers: a choice below it is a place in the menu.
    /// </summary>
    public const int First = 0x64;

    private static readonly FrozenDictionary<int, int> Clilocs = new Dictionary<int, int>
    {
        [0x0078] = 3006105, // Open Bank Box
        [0x0082] = 3006107, // Command: Guard
        [0x0083] = 3006108, // Command: Follow
        [0x0086] = 3006111, // Command: Kill
        [0x0087] = 3006114, // Command: Stay
        [0x0089] = 3006112, // Command: Stop
        [0x012D] = 3006130, // Tame
        [0x0134] = 3006157, // Cancel Protection
        [0x0140] = 1113797, // Enable PvP Warning
        [0x0193] = 3006152, // Bulk Order Info
        [0x0194] = 3006154, // View Quest Log
        [0x0195] = 3006155, // Cancel Quest
        [0x0196] = 3006156, // Quest Conversation
        [0x01A0] = 1114299, // Open Item Insurance Menu
        [0x01A2] = 3006201, // Toggle Item Insurance
        [0x01A3] = 1152294, // Bribe
        [0x025A] = 3006205, // Release Co-Ownership
        [0x025C] = 3006207, // Leave House
        [0x0321] = 3006169, // Toggle Quest Item
        [0x032A] = 3000197, // Add Party Member
        [0x032B] = 3000198, // Remove Party Member
        [0x0334] = 3006168, // Siege Bless Item
        [0x0393] = 1049594, // Loyalty Rating
        [0x0396] = 1115022, // Open Titles Menu
        [0x03F2] = 1152531, // Void Pool
        [0x03F5] = 1154112, // Allow Trades
        [0x03F6] = 1154113  // Refuse Trades
    }.ToFrozenDictionary();

    /// <summary>
    ///     Gets the text of the client a fixed number stands for; false for a number that is none of them.
    /// </summary>
    public static bool TryGetCliloc(int index, out int cliloc)
    {
        return Clilocs.TryGetValue(index, out cliloc);
    }
}
