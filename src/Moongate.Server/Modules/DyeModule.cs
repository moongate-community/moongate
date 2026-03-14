using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.UO.Data.Ids;
using MoonSharp.Interpreter;

namespace Moongate.Server.Modules;

[ScriptModule("dye", "Provides classic dye tub target and hue picker helpers.")]
public sealed class DyeModule
{
    private readonly IDyeColorService _dyeColorService;

    public DyeModule(IDyeColorService dyeColorService)
    {
        _dyeColorService = dyeColorService;
    }

    [ScriptFunction("begin", "Begins the classic dye tub target flow for a session.")]
    public bool Begin(long sessionId, uint dyeTubSerial, Closure? callback = null)
    {
        if (sessionId <= 0 || dyeTubSerial == 0)
        {
            return false;
        }

        return _dyeColorService.BeginAsync(
                                sessionId,
                                (Serial)dyeTubSerial,
                                callback is null ? null : CreateLuaTargetCallback(callback)
                            )
                            .GetAwaiter()
                            .GetResult();
    }

    [ScriptFunction("send_dyeable", "Opens the dye window directly for a known dyeable item.")]
    public bool SendDyeable(long sessionId, uint itemSerial, int model = 0x0FAB)
    {
        if (sessionId <= 0 || itemSerial == 0 || model is < 0 or > ushort.MaxValue)
        {
            return false;
        }

        return _dyeColorService.SendDyeableAsync(sessionId, (Serial)itemSerial, (ushort)model).GetAwaiter().GetResult();
    }

    private static Func<Moongate.UO.Data.Persistence.Entities.UOItemEntity, bool> CreateLuaTargetCallback(Closure callback)
        => item =>
           {
               try
               {
                    var result = callback.OwnerScript.Call(callback, DynValue.NewNumber((uint)item.Id));

                    return result.Type != DataType.Boolean || result.Boolean;
                }
                catch
                {
                    return false;
                }
            };
}
