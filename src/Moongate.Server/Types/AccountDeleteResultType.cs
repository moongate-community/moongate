namespace Moongate.Server.Types;

/// <summary>Outcome of an account deletion attempt.</summary>
public enum AccountDeleteResultType : byte
{
    /// <summary>The account and every character it owned were deleted.</summary>
    Deleted,

    /// <summary>No account answers to that username.</summary>
    NotFound,

    /// <summary>
    /// One of the account's characters is being played right now. Deleting it would pull the world out
    /// from under that session, so the whole account is refused rather than half-deleted.
    /// </summary>
    CharacterBeingPlayed
}
