using FreeSql.DataAnnotations;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;

namespace Moongate.Sample.Plugin.Data.Persistence;

/// <summary>
///     A plugin-owned row demonstrating stable attribute mappings and application-assigned identities.
/// </summary>
[Table(Name = "sample_greeter.notes")]
public sealed class GreetingNote : IMoongateEntity
{
    [Column(Name = "id", IsPrimary = true, MapType = typeof(long))]
    public Serial Id { get; set; }

    [Column(Name = "text", StringLength = 200)]
    public string Text { get; set; } = "";
}
