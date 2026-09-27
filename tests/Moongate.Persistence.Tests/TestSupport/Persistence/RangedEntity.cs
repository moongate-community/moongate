using FreeSql.DataAnnotations;
using Moongate.Core.Attributes;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;

namespace Moongate.Persistence.Tests.TestSupport.Persistence;

/// <summary>
///     An entity whose serials live in a range that does not start at 1, as world items do.
/// </summary>
[Table(Name = "plugin_ranged.things")]
[SerialRange(1000, 2000)]
internal sealed class RangedEntity : IMoongateEntity
{
    [Column(Name = "id", IsPrimary = true)]
    public Serial Id { get; set; }

    [Column(Name = "name", StringLength = 160)]
    public string Name { get; set; } = "";
}
