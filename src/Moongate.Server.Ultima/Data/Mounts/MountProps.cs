namespace Moongate.Server.Ultima.Data.Mounts;

/// <summary>
///     The keys of the props and tags that make a creature a mount: the owner a tamed creature has, the data a mount
///     item keeps of the creature it came from, and the mobile template tag that names the item a creature turns into.
/// </summary>
public static class MountProps
{
    /// <summary>
    ///     Prop of a creature: the serial, as a long, of the mobile that owns it.
    /// </summary>
    public const string Owner = "owner";

    /// <summary>
    ///     Prop of a mount item: the id of the mobile template the creature was made from.
    /// </summary>
    public const string PetTemplate = "pet_template";

    /// <summary>
    ///     Prop of a mount item: the owner the creature had, as a long serial.
    /// </summary>
    public const string PetOwner = "pet_owner";

    /// <summary>
    ///     Tag of a mobile template: the id of the item template, on the mount layer, that the creature turns into when it
    ///     is ridden. An empty value, or no tag, makes the creature no mount.
    /// </summary>
    public const string MountItemTag = "mount_item";
}
