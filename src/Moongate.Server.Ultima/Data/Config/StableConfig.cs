namespace Moongate.Server.Ultima.Data.Config;

/// <summary>
///     TOML settings of the stablemasters: how many pets a player may leave and what each costs.
/// </summary>
public sealed class StableConfig
{
    public const int MaximumPetsLimit = 50;
    public const int MaximumFeeLimit = 100000;

    /// <summary>
    ///     Gets or sets how many pets a player may leave in the stable.
    /// </summary>
    public int MaxPets { get; set; } = 10;

    /// <summary>
    ///     Gets or sets the gold a pet costs when it is stabled, taken from the backpack and then the bank; 0 makes it free.
    /// </summary>
    public int Fee { get; set; } = 30;

    /// <summary>
    ///     Validates the settings before server services begin startup.
    /// </summary>
    public void Validate()
    {
        if (MaxPets is < 1 or > MaximumPetsLimit)
        {
            throw new InvalidOperationException(
                $"ultima.stable.max_pets must be from 1 to {MaximumPetsLimit}, found {MaxPets}."
            );
        }

        if (Fee is < 0 or > MaximumFeeLimit)
        {
            throw new InvalidOperationException(
                $"ultima.stable.fee must be from 0 to {MaximumFeeLimit}, found {Fee}."
            );
        }
    }
}
