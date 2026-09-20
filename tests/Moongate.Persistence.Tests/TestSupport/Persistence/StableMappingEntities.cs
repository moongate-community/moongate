using FreeSql.DataAnnotations;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;

namespace Moongate.Persistence.Tests.TestSupport.Persistence;

[Table(Name = "plugin_characters.entities")]
internal sealed class CharacterSharedEntity : IMoongateEntity
{
    [Column(Name = "id", IsPrimary = true)]
    public Serial Id { get; set; }

    [Column(Name = "name", StringLength = 160)]
    public string Name { get; set; } = "";
}

[Table(Name = "plugin_inventory.entities")]
internal sealed class InventorySharedEntity : IMoongateEntity
{
    [Column(Name = "id", IsPrimary = true)]
    public Serial Id { get; set; }

    [Column(Name = "balance")]
    public int Balance { get; set; }
}

[Table(Name = "plugin_shared.accounts_entities")]
internal sealed class AccountsSharedEntity : IMoongateEntity
{
    [Column(Name = "id", IsPrimary = true)]
    public Serial Id { get; set; }
}

[Table(Name = "plugin_shared.realm_entities")]
internal sealed class RealmSharedEntity : IMoongateEntity
{
    [Column(Name = "id", IsPrimary = true)]
    public Serial Id { get; set; }
}

[Table(Name = "plugin_colliding.entities")]
internal sealed class FirstCollidingEntity : IMoongateEntity
{
    [Column(Name = "id", IsPrimary = true)]
    public Serial Id { get; set; }
}

[Table(Name = "plugin_colliding.entities")]
internal sealed class SecondCollidingEntity : IMoongateEntity
{
    [Column(Name = "id", IsPrimary = true)]
    public Serial Id { get; set; }
}
