using Moongate.Server.Ultima.Data.Combat;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Ultima.Combat;

/// <summary>
///     Records the shots spent and the ammunition left on the ground, and answers <see cref="Has" /> to a shot.
/// </summary>
public sealed class RecordingAmmoService : IAmmoService
{
    public bool Has { get; set; } = true;

    public List<MobileEntity> Spent { get; } = [];

    public List<MobileEntity> Recovered { get; } = [];

    public bool Spend(MobileEntity shooter, WeaponInfo weapon)
    {
        Spent.Add(shooter);

        return Has;
    }

    public void Recover(MobileEntity target, WeaponInfo weapon)
    {
        Recovered.Add(target);
    }
}
