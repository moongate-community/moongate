using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Ultima.Mounts;

/// <summary>
///     Records the mounts and dismounts it is asked for; a rider is mounted when it is in <see cref="Mounted" />.
/// </summary>
public sealed class RecordingMountService : IMountService
{
    public HashSet<Serial> Mounted { get; } = [];

    public bool Accepts { get; set; } = true;

    public List<(MobileEntity Rider, MobileEntity Pet, bool Force)> Mounts { get; } = [];

    public List<MobileEntity> Dismounts { get; } = [];

    public List<string> Calls { get; } = [];

    /// <summary>
    ///     Gets or sets what runs first when a dismount is asked for, so a test can look at the state at that moment.
    /// </summary>
    public Action? OnDismount { get; set; }

    public bool IsMounted(MobileEntity rider)
    {
        return Mounted.Contains(rider.Id);
    }

    public bool TryMount(MobileEntity rider, MobileEntity pet, bool force = false)
    {
        Mounts.Add((rider, pet, force));
        Calls.Add($"Mount {rider.Id.Value} {pet.Id.Value}");

        return Accepts;
    }

    public List<(MobileEntity Rider, ItemEntity Statuette)> Ethereals { get; } = [];

    public bool TryMountEthereal(MobileEntity rider, ItemEntity statuette)
    {
        Ethereals.Add((rider, statuette));
        Calls.Add($"Ethereal {rider.Id.Value} {statuette.Id.Value}");

        return Accepts;
    }

    public bool Dismount(MobileEntity rider)
    {
        OnDismount?.Invoke();
        Dismounts.Add(rider);
        Calls.Add($"Dismount {rider.Id.Value}");

        return Mounted.Remove(rider.Id);
    }
}
