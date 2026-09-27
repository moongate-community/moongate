namespace Moongate.Server.Ultima.Types.Characters;

/// <summary>
///     Why a character was not created. Every other bad choice is replaced with a safe value instead.
/// </summary>
public enum CharacterCreationRefusalType
{
    /// <summary>
    ///     The account already holds as many characters as <c>[characters] max_per_account</c> allows.
    /// </summary>
    TooManyCharacters,

    /// <summary>
    ///     The slot is beyond the limit or already holds a character.
    /// </summary>
    SlotUnavailable
}
