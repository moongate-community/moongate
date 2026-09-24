using FreeSql.DataAnnotations;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;

namespace Moongate.Persistence.Tests.TestSupport.Persistence;

[Table(Name = "auth.convention_entities")]
internal sealed class ConventionNamedEntity : IMoongateEntity
{
    [Column(IsPrimary = true, MapType = typeof(long))]
    public Serial Id { get; set; }

    public string Username { get; set; } = "";

    public string HashPassword { get; set; } = "";

    public DateTime CreatedAt { get; set; }

    [Column(Name = "display_label", StringLength = 100)]
    public string DisplayName { get; set; } = "";
}
