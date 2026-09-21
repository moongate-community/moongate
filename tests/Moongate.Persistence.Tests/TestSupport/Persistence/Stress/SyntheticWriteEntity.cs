using FreeSql.DataAnnotations;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;

namespace Moongate.Persistence.Tests.TestSupport.Persistence.Stress;

[Table(Name = "stress.writes"), Index("ix_stress_write_session", nameof(SessionNumber), false)]
internal sealed class SyntheticWriteEntity : IMoongateEntity
{
    [Column(IsPrimary = true, MapType = typeof(long))]
    public Serial Id { get; set; }
    public int SessionNumber { get; set; }
    public long Revision { get; set; }
    public bool RolledBack { get; set; }
}
