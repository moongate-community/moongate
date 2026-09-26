using FreeSql.DataAnnotations;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;
using Moongate.Persistence.Tests.TestSupport.Persistence.Data;

namespace Moongate.Persistence.Tests.TestSupport.Persistence;

[Table(Name = "json_mapping.profiles")]
internal sealed class JsonProfileEntity : IMoongateEntity
{
    [Column(IsPrimary = true, MapType = typeof(long))]
    public Serial Id { get; set; }

    [JsonMap, Column(DbType = "jsonb", IsNullable = true)]
    public List<QuestProgressData>? QuestProgress { get; set; } = [];

    [JsonMap, Column(DbType = "jsonb", IsNullable = true)]
    public QuestProgressData? ActiveQuest { get; set; }
}
