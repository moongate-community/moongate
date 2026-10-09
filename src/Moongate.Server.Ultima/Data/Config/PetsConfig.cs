namespace Moongate.Server.Ultima.Data.Config;

/// <summary>
///     TOML settings of the pets: how many followers a player may have.
/// </summary>
public sealed class PetsConfig
{
    public const int MaximumFollowersLimit = 50;

    /// <summary>
    ///     Gets or sets the followers a player may have: the slots of the creatures that are its own and in the world, and of
    ///     the one it rides.
    /// </summary>
    public int MaxFollowers { get; set; } = 5;

    /// <summary>
    ///     Validates the settings before server services begin startup.
    /// </summary>
    public void Validate()
    {
        if (MaxFollowers is < 1 or > MaximumFollowersLimit)
        {
            throw new InvalidOperationException(
                $"ultima.pets.max_followers must be from 1 to {MaximumFollowersLimit}, found {MaxFollowers}."
            );
        }
    }
}
