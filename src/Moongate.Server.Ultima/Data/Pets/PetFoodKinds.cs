namespace Moongate.Server.Ultima.Data.Pets;

/// <summary>
///     The kinds of food a pet can eat.
/// </summary>
public static class PetFoodKinds
{
    /// <summary>
    ///     Every kind, as written in <c>data/taming.toml</c> and <c>data/pet_food.toml</c>.
    /// </summary>
    public static readonly string[] All = ["meat", "fruit", "grain", "fish", "eggs"];
}
