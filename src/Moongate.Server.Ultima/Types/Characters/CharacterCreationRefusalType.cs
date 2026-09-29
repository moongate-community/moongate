namespace Moongate.Server.Ultima.Types.Characters;

/// <summary>
///     Why a character was not created. Every other bad choice is replaced with a safe value instead.
/// </summary>
public enum CharacterCreationRefusalType
{
    /// <summary>
    ///     The account already holds as many characters as <c>ultima.characters.max_per_account</c> allows.
    /// </summary>
    TooManyCharacters,

    /// <summary>
    ///     Another create for the same account took the chosen slot at the same moment.
    /// </summary>
    SlotUnavailable
}
