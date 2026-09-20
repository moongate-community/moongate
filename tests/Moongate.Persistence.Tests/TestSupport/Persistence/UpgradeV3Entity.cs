using FreeSql.DataAnnotations;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;

namespace Moongate.Persistence.Tests.TestSupport.Persistence;

[Table(Name = "plugin_upgrade.characters")]
internal sealed class UpgradeV3Entity : IMoongateEntity
{
    [Column(Name = "id", IsPrimary = true)]
    public Serial Id { get; set; }

    [Column(Name = "display_name", StringLength = 160)]
    public string DisplayName { get; set; } = "";

    [Column(Name = "level", IsNullable = false)]
    public long Level { get; set; } = 1;
}
