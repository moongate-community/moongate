using Moongate.UO.Data.Factory;
using Moongate.UO.Interfaces.Services;

namespace Moongate.Server.Services;

public class NameService : INameService
{
    private readonly Dictionary<string, List<string>> _names = new();

    public void AddNames(string type, params string[] names)
    {
        if (!_names.TryGetValue(type, out _))
        {
            _names[type] = [];
        }

        _names[type].AddRange(names);
    }

    public void Dispose() { }

    public string GenerateName(string type)
        => string.Empty;

    public string GenerateName(MobileTemplate template)
        => string.Empty;
}
