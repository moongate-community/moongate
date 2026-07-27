namespace Moongate.Server.Abstractions.Types;

/// <summary>The outcome of a web self-registration request.</summary>
public enum AccountRegisterResultType
{
    Created,
    UsernameTaken,
    UsernameEmpty,
    UsernameInvalid,
    PasswordEmpty,
    PasswordInvalid,
    EmailEmpty,
    EmailInvalid,
    EmailTaken
}
