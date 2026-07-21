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
    public async Task RegistrationEvent_SendsTheVerificationOnTheLogChannel()
    {
        var templates = new NotificationTemplateService();
        templates.Register(
            "log",
            "account_verification",
            "{{ username }}/{{ email }}/{{ token }}/{{ website }}/{{ shard_name }}"
        );

        var channel = new RecordingNotificationChannel("log");
        var notifications = new NotificationService(templates, [channel], new StubJobSystem(), new());

        var settings = new ServerSettingsService(new FakePersistenceService());
        settings.Update(new() { Contacts = new() { Website = "https://shard.example" } });

        var bus = new EventBusService();
        new AccountRegistrationSubscriber(
            notifications,
            settings,
            new() { ShardName = "Britannia", UltimaDirectory = "/tmp" }
        ).Subscribe(bus);

        await bus.PublishAsync(new AccountRegistrationRequestedEvent(new(1), "tom", "tom@example.com", "abc123"));

        var (recipient, content) = Assert.Single(channel.Sent);
        Assert.Equal("tom@example.com", recipient.Address);
        Assert.Equal("log", recipient.ChannelId);
        Assert.Equal("tom/tom@example.com/abc123/https://shard.example/Britannia", content.Body.Trim());
    }
}
