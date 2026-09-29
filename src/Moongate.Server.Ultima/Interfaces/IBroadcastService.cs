namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Sends system chat messages to connected characters in this instance's world.
/// </summary>
public interface IBroadcastService
{
    /// <summary>
    ///     Queues a message on the game loop and returns the number of recipients whose send queue accepted it.
    /// </summary>
    /// <remarks>
    ///     Cancellation before loop admission prevents delivery. Once admitted, the caller waits for delivery to
    ///     finish. Acceptance by a send queue does not guarantee receipt by the client.
    ///     Text must fit both the Unicode speech frame and the compressed transport; invalid text is rejected
    ///     before any message is queued.
    /// </remarks>
    Task<int> BroadcastAsync(string text, CancellationToken cancellationToken = default);
}
