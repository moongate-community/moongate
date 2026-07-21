using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Services.Notifications;
using Moongate.Server.Services.Server;
using Moongate.Server.Subscribers;
using Moongate.Tests.Support;
using SquidStd.Services.Core.Services;

namespace Moongate.Tests.Server.Notifications;

public sealed class AccountRegistrationSubscriberTests
{
    [Fact]
    public async Task RegistrationEvent_SendsTheVerificationOnTheConfiguredChannel()
    {
        var channel = new RecordingNotificationChannel("log");
        var bus = Wire(channel, "log");

        await bus.PublishAsync(new AccountRegistrationRequestedEvent(new(1), "tom", "tom@example.com", "abc123"));

        var (recipient, content) = Assert.Single(channel.Sent);
        Assert.Equal("tom@example.com", recipient.Address);
        Assert.Equal("log", recipient.ChannelId);
        Assert.Equal("tom/tom@example.com/abc123/https://shard.example/Britannia", content.Body.Trim());
    }

    [Fact]
    public async Task RegistrationEvent_HonoursAConfiguredEmailChannel()
    {
        var channel = new RecordingNotificationChannel("email");
        var bus = Wire(channel, "email");

        await bus.PublishAsync(new AccountRegistrationRequestedEvent(new(1), "tom", "tom@example.com", "abc123"));

        // Pointing the config at another channel is the whole switch: no code changes when SMTP lands.
        Assert.Equal("email", Assert.Single(channel.Sent).Recipient.ChannelId);
    }

    [Fact]
    public async Task UnregisteredChannel_DoesNotDeliver_AndDoesNotThrow()
    {
        var channel = new RecordingNotificationChannel("log");
        var bus = Wire(channel, "email");

        // Subscribing warns about this at startup; publishing must still be harmless.
        await bus.PublishAsync(new AccountRegistrationRequestedEvent(new(1), "tom", "tom@example.com", "abc123"));

        Assert.Empty(channel.Sent);
    }

    /// <summary>Wires the subscriber over one channel, with the verification routed at <paramref name="routeTo" />.</summary>
    private static EventBusService Wire(RecordingNotificationChannel channel, string routeTo)
    {
        var templates = new NotificationTemplateService();
        templates.Register(
            channel.Id,
            "account_verification",
            "{{ username }}/{{ email }}/{{ token }}/{{ website }}/{{ shard_name }}"
        );

        var notifications = new NotificationService(templates, [channel], new StubJobSystem(), new());

        var settings = new ServerSettingsService(new FakePersistenceService());
        settings.Update(new() { Contacts = new() { Website = "https://shard.example" } });

        var bus = new EventBusService();
        new AccountRegistrationSubscriber(
            notifications,
            [channel],
            settings,
            new() { ShardName = "Britannia", UltimaDirectory = "/tmp" },
            new() { AccountVerificationChannel = routeTo }
        ).Subscribe(bus);

        return bus;
    }
}
