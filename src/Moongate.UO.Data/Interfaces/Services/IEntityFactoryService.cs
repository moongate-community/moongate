using Moongate.Core.Server.Interfaces.Services.Base;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.UO.Data.Interfaces.Services;

public interface IEntityFactoryService : IMoongateAutostartService
{
    T CreateEntity<T>(string templateId) where T : class;
    T CreateEntity<T>(string templateId, Dictionary<string, object> overrides) where T : class;
    UOItemEntity CreateItemEntity(string templateOrCategoryOrTag, Dictionary<string, object> overrides = null);

    Task LoadTemplatesAsync(string filePath);

    UOItemEntity GetNewBackpack();
}
