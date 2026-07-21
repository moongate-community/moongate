using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Moongate.Server.Abstractions.Data.Config;
using Moongate.Server.Abstractions.Types;
using Moongate.Smtp.Plugin.Data.Config;
using Moongate.Smtp.Plugin.Services;
using Moongate.Tests.Support;

namespace Moongate.Tests.Smtp;

public sealed class SmtpNotificationChannelTests
{
    [Fact]
    public void Id_IsEmail()
        => Assert.Equal("email", Channel(new RecordingSmtpTransport()).Id);

    [Fact]
    public async Task SendAsync_BuildsTheMessageFromConfigAndContent()
    {
        var transport = new RecordingSmtpTransport();

        await Channel(transport).SendAsync(new("email", "tom@example.com"), new("Verify", "hello"));

        var message = Assert.Single(transport.Sent);
        Assert.Equal("Verify", message.Subject);
        Assert.Equal("shard@example.com", Assert.IsType<MailboxAddress>(Assert.Single(message.From)).Address);
        Assert.Equal("tom@example.com", Assert.IsType<MailboxAddress>(Assert.Single(message.To)).Address);
        Assert.Equal("hello", message.TextBody!.Trim());
    }

    [Fact]
    public async Task SendAsync_WithoutFromName_UsesTheShardName()
    {
        var transport = new RecordingSmtpTransport();

        await Channel(transport, fromName: string.Empty).SendAsync(new("email", "tom@example.com"), new("s", "b"));

        var from = Assert.IsType<MailboxAddress>(Assert.Single(Assert.Single(transport.Sent).From));
        Assert.Equal("Britannia", from.Name);
    }

    [Fact]
    public async Task SendAsync_HtmlContent_SendsAnHtmlPart()
    {
        var transport = new RecordingSmtpTransport();

        await Channel(transport).SendAsync(
            new("email", "tom@example.com"),
            new("Verify", "<p>hi</p>", NotificationContentType.Html)
        );

        var message = Assert.Single(transport.Sent);
        Assert.Equal("<p>hi</p>", message.HtmlBody!.Trim());
        Assert.Null(message.TextBody);
    }

    [Fact]
    public async Task SendAsync_NullSubject_SendsAnEmptySubject()
    {
        var transport = new RecordingSmtpTransport();

        await Channel(transport).SendAsync(new("email", "tom@example.com"), new(null, "body"));

        Assert.Equal(string.Empty, Assert.Single(transport.Sent).Subject);
    }

    [Fact]
    public async Task SendAsync_TransientFailure_Rethrows()
    {
        // A 4xx is the server saying "not now": the pipeline's retry is exactly the right response.
        var transient = new SmtpCommandException(
            SmtpErrorCode.MessageNotAccepted,
            SmtpStatusCode.MailboxBusy,
            "try later"
        );

        await Assert.ThrowsAsync<SmtpCommandException>(
            async () => await Channel(new RecordingSmtpTransport(transient))
                              .SendAsync(new("email", "tom@example.com"), new("s", "b"))
        );
    }

    [Fact]
    public async Task SendAsync_PermanentFailure_DoesNotRethrow()
    {
        // A 5xx will not improve on the third attempt; rethrowing would only delay the error in the log.
        var permanent = new SmtpCommandException(
            SmtpErrorCode.RecipientNotAccepted,
            SmtpStatusCode.MailboxUnavailable,
            "no such user"
        );

        await Channel(new RecordingSmtpTransport(permanent))
              .SendAsync(new("email", "tom@example.com"), new("s", "b"));
    }

    [Fact]
    public async Task SendAsync_AuthenticationFailure_DoesNotRethrow()
    {
        // Retrying bad credentials can trip a provider's rate limits.
        await Channel(new RecordingSmtpTransport(new AuthenticationException("bad credentials")))
              .SendAsync(new("email", "tom@example.com"), new("s", "b"));
    }

    private static SmtpNotificationChannel Channel(RecordingSmtpTransport transport, string fromName = "Shard Mail")
        => new(
            transport,
            new MoongateSmtpConfig
            {
                Host = "localhost",
                FromAddress = "shard@example.com",
                FromName = fromName
            },
            new() { ShardName = "Britannia", UltimaDirectory = "/tmp" }
        );
}
