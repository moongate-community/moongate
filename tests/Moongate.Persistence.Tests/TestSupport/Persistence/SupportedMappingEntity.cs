using FreeSql.DataAnnotations;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;

namespace Moongate.Persistence.Tests.TestSupport.Persistence;

[Table(Name = "plugin_mapping.supported_entities")]
internal sealed class SupportedMappingEntity : IMoongateEntity
{
    [Column(Name = "id", IsPrimary = true)]
    public Serial Id { get; set; }

    [Column(Name = "scores")]
    public int[] Scores { get; set; } = [];

    [Column(IsIgnore = true)]
    public MappingPosition Position { get; set; } = new();

    [Column(Name = "character_id")]
    public Serial CharacterId { get; set; }

    [Navigate(nameof(CharacterId))]
    public CharacterEntity? Character { get; set; }
}
