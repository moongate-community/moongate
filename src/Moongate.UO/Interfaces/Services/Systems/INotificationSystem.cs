using Moongate.Core.Server.Interfaces.Services.Base;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Interfaces.Services.Systems;

public interface INotificationSystem : IMoongateService
{
    void SendSystemMessageToAll(string message);
    void SendSystemMessageToMobile(UOMobileEntity mobile, string message);
    Task SendChatMessageAsync(UOMobileEntity mobile,  ChatMessageType messageType, short hue, string text, int graphic, int font);

}
