using Moongate.UO.Data.Factory;
using Moongate.UO.Interfaces.Services;

namespace Moongate.Server.Services;

public class NameService : INameService
{

    private readonly Dictionary<string, List<string>> _names = new();

    public string GenerateName(string type)
    {
        return string.Empty;
    }

    public void AddNames(string type, params string[] names)
    {
        if (!_names.TryGetValue(type, out _))
        {
            _names[type] = [];
        }

        _names[type].AddRange(names);
    }

    public string GenerateName(MobileTemplate template)
    {
        return string.Empty;
    }


    public void Dispose()
    {
    }
}
