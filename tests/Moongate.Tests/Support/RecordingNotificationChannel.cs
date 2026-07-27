using Moongate.Server.Abstractions.Data.Notifications;
using Moongate.Server.Abstractions.Interfaces.Notifications;

namespace Moongate.Tests.Support;

/// <summary>
/// A channel that records what it was asked to deliver, and can be told to fail a given number of times
/// first so retry behaviour can be observed.
/// </summary>
public sealed class RecordingNotificationChannel : INotificationChannel
{
    private int _failuresLeft;

    public RecordingNotificationChannel(string id = "test", int failuresBeforeSuccess = 0)
    {
        Id = id;
        _failuresLeft = failuresBeforeSuccess;
    }

    public string Id { get; }

    /// <summary>Every delivery attempt that reached the channel, failed ones included.</summary>
    public int Attempts { get; private set; }

    /// <summary>What was successfully delivered.</summary>
    public List<(NotificationRecipient Recipient, NotificationContent Content)> Sent { get; } = [];

    public ValueTask SendAsync(
        NotificationRecipient recipient,
        NotificationContent content,
        CancellationToken cancellationToken = default
    )
    {
        Attempts++;

        if (_failuresLeft > 0)
        {
            _failuresLeft--;

            throw new InvalidOperationException("channel unavailable");
        }

        Sent.Add((recipient, content));

        return ValueTask.CompletedTask;
    }
}
