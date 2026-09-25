using FreeSql.DataAnnotations;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;

namespace Moongate.Persistence.Tests.TestSupport.Persistence.Stress;

[Table(Name = "stress.sessions"), Index("ix_stress_session_number", nameof(SessionNumber), true)]
internal sealed class SyntheticSessionEntity : IMoongateEntity
{
    [Column(IsPrimary = true, MapType = typeof(long))]
    public Serial Id { get; set; }

    public int SessionNumber { get; set; }
    public long Revision { get; set; }

    [Column(StringLength = 1024)] public string Payload { get; set; } = "";
}
