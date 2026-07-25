using Moongate.Server.Abstractions.Types;

namespace Moongate.Server.Abstractions.Data;

/// <summary>
/// A registration outcome plus, on success, the raw verification token that the caller must deliver to
/// the registrant. The account persistence stores only a hash of that token.
/// </summary>
public sealed class AccountRegisterResult
{
    public AccountRegisterResultType Result { get; init; }

    public string? Token { get; init; }
}
