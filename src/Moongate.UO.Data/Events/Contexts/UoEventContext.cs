using DryIoc;
using Moongate.Core.Server.Instances;
using Moongate.UO.Data.Interfaces.Services;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Serilog;

namespace Moongate.UO.Data.Events.Contexts;

public class UoEventContext
{
    private readonly IEntityFactoryService _entityFactoryService = MoongateContext.Container.Resolve<IEntityFactoryService>();
    private readonly ILogger _logger = Log.ForContext<UoEventContext>();


    public static UoEventContext CreateInstance()
    {
        return new UoEventContext();
    }

    /// <summary>
    ///  Adds an item to a mobile entity based on the specified template ID and layer.
    /// </summary>
    /// <param name="templateId"></param>
    /// <param name="layer"></param>
    /// <param name="mobile"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public void AddItem(string templateId, ItemLayerType layer, UOMobileEntity mobile)
    {
        var item = _entityFactoryService.CreateItemEntity(templateId);
        if (item == null)
        {
            throw new InvalidOperationException($"Item template '{templateId}' not found.");
        }

        _logger.Debug("Adding item {ItemId} to mobile {MobileId} on layer {Layer}", item.Id, mobile.Id, layer);
        mobile.AddItem(layer, item);
    }
}
