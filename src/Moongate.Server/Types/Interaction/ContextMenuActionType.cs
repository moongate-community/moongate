namespace Moongate.Server.Types.Interaction;

/// <summary>
/// Supported context menu actions handled by server runtime.
/// </summary>
public enum ContextMenuActionType : byte
{
    None = 0,
    OpenPaperdoll = 1,
    VendorBuy = 2,
    VendorSell = 3,
    QuestDialog = 4,
    Script = 5
}
