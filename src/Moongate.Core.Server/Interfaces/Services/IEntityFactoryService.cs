using Moongate.Core.Server.Interfaces.Services.Base;

namespace Moongate.Core.Server.Interfaces.Services;

public interface IEntityFactoryService : IMoongateAutostartService
{
    T CreateEntity<T>(string templateId) where T : class;
    T CreateEntity<T>(string templateId, Dictionary<string, object> overrides) where T : class;
    Task LoadTemplatesAsync(string filePath);
}
