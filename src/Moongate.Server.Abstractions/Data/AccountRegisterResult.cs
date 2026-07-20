using Moongate.Server.Abstractions.Types;

namespace Moongate.Server.Abstractions.Data;

/// <summary>
/// A registration outcome plus, on success, the verification token — the future email feature needs the
/// token to build a verify link; today it is only logged and carried on the domain event.
/// </summary>
public sealed class AccountRegisterResult
{
    public AccountRegisterResultType Result { get; init; }

    public string? Token { get; init; }
}
