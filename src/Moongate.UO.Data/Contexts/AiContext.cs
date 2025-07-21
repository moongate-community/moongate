using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Contexts;

public class AiContext : IDisposable
{
    protected UOMobileEntity MobileEntity;

    public void InitializeContext(UOMobileEntity mobile)
    {
        MobileEntity = mobile;
    }

    protected void Say(string message)
    {
        MobileEntity.Speech(ChatMessageType.Regular, 1, message, 0, 3);
    }

    public void Dispose()
    {
    }
}
