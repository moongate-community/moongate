using Moongate.Server.Services.Notifications;
using Moongate.Server.Services.Notifications.Channels;
using Moongate.Tests.Support;

namespace Moongate.Tests.Server.Notifications;

public sealed class NotificationServiceTests
{
    [Fact]
    public void Notify_RendersTheTemplateAndSendsItOnTheNamedChannel()
    {
        var templates = new NotificationTemplateService();
        templates.Register("test", "greet", "Hello {{ username }}");
        var channel = new RecordingNotificationChannel();
        var jobs = new StubJobSystem();
        var service = new NotificationService(templates, [channel], jobs, new());

        service.Notify("greet", new("test", "tom@example.com"), new { Username = "tom" });

        Assert.Equal(1, jobs.Scheduled);
        var (recipient, content) = Assert.Single(channel.Sent);
        Assert.Equal("tom@example.com", recipient.Address);
        Assert.Equal("Hello tom", content.Body.Trim());
    }

    [Fact]
    public void Notify_RoutesToTheChannelMatchingTheRecipient()
    {
        var templates = new NotificationTemplateService();
        templates.Register("second", "greet", "hi");
        var first = new RecordingNotificationChannel("first");
        var second = new RecordingNotificationChannel("second");
        var service = new NotificationService(templates, [first, second], new StubJobSystem(), new());

        service.Notify("greet", new("second", "somewhere"), new { });

        Assert.Empty(first.Sent);
        Assert.Single(second.Sent);
    }

    [Fact]
    public void Notify_UnknownChannel_DropsWithoutThrowingOrScheduling()
    {
        var templates = new NotificationTemplateService();
        var channel = new RecordingNotificationChannel();
        var jobs = new StubJobSystem();
        var service = new NotificationService(templates, [channel], jobs, new());

        // A notification is a side effect: an unregistered channel must not take down the caller.
        service.Notify("greet", new("nope", "somewhere"), new { });

        Assert.Equal(0, jobs.Scheduled);
        Assert.Empty(channel.Sent);
    }

    [Fact]
    public void Notify_MissingTemplate_DropsWithoutSending()
    {
        var templates = new NotificationTemplateService();
        var channel = new RecordingNotificationChannel();
        var service = new NotificationService(templates, [channel], new StubJobSystem(), new());

        service.Notify("absent", new("test", "somewhere"), new { });

        Assert.Equal(0, channel.Attempts);
    }

    [Fact]
    public void Notify_RetriesUntilTheChannelSucceeds()
    {
        var templates = new NotificationTemplateService();
        templates.Register("test", "greet", "hi");
        var channel = new RecordingNotificationChannel(failuresBeforeSuccess: 2);
        var service = new NotificationService(
            templates,
            [channel],
            new StubJobSystem(),
            new() { MaxAttempts = 3, RetryDelaySeconds = 0 }
        );

        service.Notify("greet", new("test", "somewhere"), new { });

        Assert.Equal(3, channel.Attempts);
        Assert.Single(channel.Sent);
    }

    [Fact]
    public void Notify_GivesUpAfterMaxAttempts_WithoutThrowing()
    {
        var templates = new NotificationTemplateService();
        templates.Register("test", "greet", "hi");
        var channel = new RecordingNotificationChannel(failuresBeforeSuccess: 99);
        var service = new NotificationService(
            templates,
            [channel],
            new StubJobSystem(),
            new() { MaxAttempts = 2, RetryDelaySeconds = 0 }
        );

        // The job runs inline here, so an escaping exception would fail this test — which is the point:
        // on a real worker thread it would be an unobserved crash.
        service.Notify("greet", new("test", "somewhere"), new { });

        Assert.Equal(2, channel.Attempts);
        Assert.Empty(channel.Sent);
    }

    [Fact]
    public void LogChannel_IdentifiesItselfAsLog()
        => Assert.Equal("log", new LogNotificationChannel().Id);
}
