using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Modules;

[ScriptModule("bulletin", "Provides bulletin board open helpers.")]
public sealed class BulletinBoardModule
{
    private readonly IBulletinBoardService _bulletinBoardService;

    public BulletinBoardModule(IBulletinBoardService bulletinBoardService)
    {
        _bulletinBoardService = bulletinBoardService;
    }

    [ScriptFunction("open", "Opens a bulletin board for the specified session.")]
    public bool Open(long sessionId, uint boardSerial)
    {
        if (sessionId <= 0 || boardSerial == 0)
        {
            return false;
        }

        return _bulletinBoardService.OpenBoardAsync(sessionId, (Serial)boardSerial).GetAwaiter().GetResult();
    }
}
