using DryIoc;
using Moongate.Core.Server.Instances;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Interfaces.Services;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Moongate.UO.Extensions;
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

    public UOItemEntity CreateItem(string templateId)
    {
        var item = _entityFactoryService.CreateItemEntity(templateId);
        if (item == null)
        {
            throw new InvalidOperationException($"Item template '{templateId}' not found.");
        }

        _logger.Debug("Created item {ItemId} from template {TemplateId}", item.Id, templateId);
        return item;
    }

    public void AddItemToBackpack(string templateId, UOMobileEntity mobile)
    {
        var backpack = mobile.GetBackpack();
        if (backpack == null)
        {
            throw new InvalidOperationException("Backpack not found.");
        }

        var item = CreateItem(templateId);
        _logger.Debug("Adding item {ItemId} to backpack {BackpackId} of mobile {MobileId}", item.Id, backpack.Id, mobile.Id);
        backpack.AddItem(item, Point2D.Zero);
    }


}
