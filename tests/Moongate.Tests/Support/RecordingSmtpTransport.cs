using MimeKit;
using Moongate.Smtp.Plugin.Interfaces;

namespace Moongate.Tests.Support;

/// <summary>
/// An <see cref="ISmtpTransport" /> that records what it was asked to send, or throws whatever a test
/// hands it so failure classification can be observed.
/// </summary>
public sealed class RecordingSmtpTransport : ISmtpTransport
{
    private readonly Exception? _failure;

    public RecordingSmtpTransport(Exception? failure = null)
    {
        _failure = failure;
    }

    /// <summary>Messages that were sent successfully.</summary>
    public List<MimeMessage> Sent { get; } = [];

    public ValueTask SendAsync(MimeMessage message, CancellationToken cancellationToken = default)
    {
        if (_failure is not null)
        {
            throw _failure;
        }

        Sent.Add(message);

        return ValueTask.CompletedTask;
    }
}
