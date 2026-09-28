using Moongate.Core.Primitives;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Takes characters out of the world when their session closes and saves them.
/// </summary>
public interface ICharacterLeaveWorldService
{
    /// <summary>
    ///     Completes when every leave save of the account's characters that is still running has finished, so a new
    ///     login reads what the last one saved.
    /// </summary>
    Task WaitForAccountAsync(Serial account);
}
