using FreeSql.DataAnnotations;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;
namespace Moongate.Tests.Fixtures.Plugins.PersistencePlugin;

[Table(Name = "fixture_data.items")]
public sealed class PluginEntity : IMoongateEntity
{
    [Column(Name = "id", IsPrimary = true, MapType = typeof(long))]
    public Serial Id { get; set; }
    [Column(Name = "value")]
    public string Value { get; set; } = "";
}
