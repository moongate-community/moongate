using Moongate.Core.Server.Interfaces.Services.Base;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Interfaces.Services.Systems;

public interface INotificationSystem : IMoongateService
{
    void SendSystemMessage(string message);
    void SendChatMessage(UOMobileEntity mobile,  ChatMessageType messageType, short hue, string text );

}
