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
    ///     Gets or sets the minutes between two drains of the loyalty of the pets in the world. 1 to 1440.
    /// </summary>
    public int LoyaltyDrainMinutes { get; set; } = 60;

    /// <summary>
    ///     Gets or sets the loyalty a pet loses at each drain. 1 to 100.
    /// </summary>
    public int LoyaltyDrain { get; set; } = 10;

    /// <summary>
    ///     Gets or sets the loyalty a pet gains for each item of food it eats. 1 to 100.
    /// </summary>
    public int FoodGain { get; set; } = 10;

    /// <summary>
    ///     Gets or sets the loyalty a pet gains when it obeys. 0 to 100.
    /// </summary>
    public int ObeyGain { get; set; } = 1;

    /// <summary>
    ///     Gets or sets the loyalty a pet loses when it disobeys. 0 to 100.
    /// </summary>
    public int DisobeyLoss { get; set; } = 3;

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

        Check(LoyaltyDrainMinutes, 1, 1440, "loyalty_drain_minutes");
        Check(LoyaltyDrain, 1, 100, "loyalty_drain");
        Check(FoodGain, 1, 100, "food_gain");
        Check(ObeyGain, 0, 100, "obey_gain");
        Check(DisobeyLoss, 0, 100, "disobey_loss");
    }

    private static void Check(int value, int min, int max, string name)
    {
        if (value < min || value > max)
        {
            throw new InvalidOperationException($"ultima.pets.{name} must be from {min} to {max}, found {value}.");
        }
    }
}
