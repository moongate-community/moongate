using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Session;
using Moongate.UO.Data.Types;
using Moongate.UO.Extensions;

namespace Moongate.UO.Data.Contexts;

public class ItemUseContext
{
    public UOMobileEntity Mobile { get; set; }
    public UOItemEntity Item { get; set; }

    private readonly GameSession _session;

    public ItemUseContext(GameSession session, UOItemEntity item, UOMobileEntity mobile)
    {
        _session = session;
        Item = item;
        Mobile = mobile;
    }

    public static ItemUseContext Create(GameSession session, UOItemEntity item, UOMobileEntity mobile)
    {
        return new ItemUseContext(session, item, mobile);
    }

    protected void WriteConsole(string message, params object[] args)
    {
        Mobile.ReceiveSpeech(Mobile, ChatMessageType.System, 3, string.Format(message, args), 3, 1);
    }

    protected void Speech(string message, params object[] args)
    {
        Mobile.Speech(
            ChatMessageType.Regular,
            3,
            string.Format(message, args),
            3,
            1
        );
    }

    protected void UseItem()
    {
        if (Item.Amount - 1 <= 0)
        {
            Mobile.GetBackpack().RemoveItem(Item);
        }
        else
        {
            Item.Amount -= 1;
        }
    }
}
