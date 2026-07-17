namespace Moongate.Server.Abstractions.Types;

/// <summary>Outcome of an account creation attempt.</summary>
public enum AccountCreateResultType : byte
{
    /// <summary>The account was created and persisted.</summary>
    Created,

    /// <summary>An account already answers to that username. Usernames are the login handle, so they are unique.</summary>
    UsernameTaken,

    /// <summary>The username was empty or blank.</summary>
    UsernameEmpty,

    /// <summary>The password was empty.</summary>
    PasswordEmpty
}
