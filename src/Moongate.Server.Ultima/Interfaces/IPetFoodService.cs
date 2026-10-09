namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     What the pets eat: the kinds of food of <c>data/pet_food.toml</c> against the kinds each creature eats.
/// </summary>
public interface IPetFoodService
{
    /// <summary>
    ///     Gets whether a creature of that mobile template eats an item of that item template. A creature with no taming
    ///     entry eats nothing; one with no <c>food</c> there eats meat.
    /// </summary>
    bool Accepts(string? creatureTemplate, string? itemTemplate);
}
