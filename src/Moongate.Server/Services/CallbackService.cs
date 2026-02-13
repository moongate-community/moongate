using Moongate.Core.Server.Interfaces.Services;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Interfaces.Services;
using Moongate.UO.Data.Types;
using Serilog;

namespace Moongate.Server.Services;

public class CallbackService : ICallbackService
{
    private readonly ILogger _logger = Log.ForContext<CallbackService>();

    private readonly Dictionary<Serial, ICallbackService.ClickCallbackDelegate> _callbacks = new();

    private readonly IEventLoopService _eventLoopService;

    public CallbackService(IEventLoopService eventLoopService)
        => _eventLoopService = eventLoopService;

    public void AddTargetCallBack(Serial serial, Action<Serial> callback) { }

    public void AddTargetCallBack(Serial serial, ICallbackService.ClickCallbackDelegate callback)
    {
        _logger.Debug("Adding callback for serial {Serial}", serial);
        _callbacks[serial] = callback;
    }

    public Serial AddTargetCallBack(ICallbackService.ClickCallbackDelegate callback)
    {
        var rndSerial = Serial.RandomSerial();

        AddTargetCallBack(rndSerial, callback);

        return rndSerial;
    }

    public void Dispose() { }

    public bool ExecuteCallback(
        Serial serial,
        CursorSelectionType selectionType,
        Point3D? point,
        Serial clickedSerial = default
    )
    {
        if (_callbacks.TryGetValue(serial, out var callback))
        {
            _eventLoopService.EnqueueAction(
                "CallbackService.ExecuteCallback",
                () => { callback(serial, selectionType, point, clickedSerial); }
            );

            _callbacks.Remove(serial);

            return true;
        }

        _logger.Warning("No callback found for serial {Serial} with selection type {SelectionType}", serial, selectionType);

        return false;
    }
}
