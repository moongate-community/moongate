namespace Moongate.Server.Ultima.Data.Pets;

/// <summary>
///     One <c>[[food]]</c> of <c>data/pet_food.toml</c>: the item templates that are food of one kind for the pets.
/// </summary>
public class PetFood
{
    /// <summary>
    ///     The kind: meat, fruit, grain, fish or eggs.
    /// </summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    ///     The ids of the item templates that are food of this kind.
    /// </summary>
    public List<string> Items { get; set; } = [];
}
