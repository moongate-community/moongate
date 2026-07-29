namespace Moongate.UO.Data.Items;

/// <summary>
/// Maps an item graphic to the cliloc that names it. Sending this number instead of text is how UO
/// names anything nobody renamed: the client resolves it from its own files, in the player's
/// language, and pluralises it itself when it renders a stack.
/// </summary>
public static class ItemClilocs
{
    /// <summary>Ids below <see cref="ExtendedFrom" /> hang off this base.</summary>
    private const int ClassicBase = 1020000;

    /// <summary>Ids at or above <see cref="ExtendedFrom" /> hang off this one.</summary>
    private const int ExtendedBase = 1078872;

    /// <summary>Where the classic art range ends and the extended one begins.</summary>
    private const int ExtendedFrom = 0x4000;

    /// <summary>The cliloc naming <paramref name="itemId" />. Matches ModernUO's Item.LabelNumber.</summary>
    public static int ForItemId(int itemId)
        => itemId < ExtendedFrom ? ClassicBase + itemId : ExtendedBase + itemId;
}
