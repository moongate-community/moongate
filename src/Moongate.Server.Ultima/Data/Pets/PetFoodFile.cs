namespace Moongate.Server.Ultima.Data.Pets;

/// <summary>
///     The root of <c>data/pet_food.toml</c>: one <c>[[food]]</c> per kind of food.
/// </summary>
public class PetFoodFile
{
    public List<PetFood> Food { get; set; } = [];
}
