using Moongate.Core.Server.Interfaces.Services.Base;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Interfaces.Services;

public interface ICallbackService : IMoongateService
{
    delegate void ClickCallbackDelegate(
        Serial serial, CursorSelectionType cursorSelectionType, Point3D? cursorPosition, Serial clickedSerial = default
    );

    void AddTargetCallBack(Serial serial, ClickCallbackDelegate callback);

    Serial AddTargetCallBack(ClickCallbackDelegate callback);

    bool ExecuteCallback(Serial serial, CursorSelectionType selectionType, Point3D? point, Serial clickedSerial = default);
}
