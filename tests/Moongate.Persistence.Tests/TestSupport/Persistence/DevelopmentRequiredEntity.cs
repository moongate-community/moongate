using FreeSql.DataAnnotations;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;

namespace Moongate.Persistence.Tests.TestSupport.Persistence;

[Table(Name = "auth.development_required")]
internal sealed class DevelopmentRequiredEntity : IMoongateEntity
{
    [Column(IsPrimary = true, MapType = typeof(long))]
    public Serial Id { get; set; }

    [Column(IsNullable = false)]
    public int Level { get; set; }
}
