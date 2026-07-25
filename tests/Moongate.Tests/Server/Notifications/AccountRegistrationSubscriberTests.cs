using System.Reflection;

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
    public async Task RegistrationEvent_SendsCanonicalVerificationUrlAndPreservesCustomTemplateFields()
    {
        var channel = new RecordingNotificationChannel("log");
        var bus = Wire(channel, "log");

        await bus.PublishAsync(new AccountRegistrationRequestedEvent(new(1), "tom", "tom@example.com", "abc+123"));

        var (recipient, content) = Assert.Single(channel.Sent);
        Assert.Equal("tom@example.com", recipient.Address);
        Assert.Equal("log", recipient.ChannelId);
        Assert.Contains(
            "https://shard.example/moongate/verify?token=abc%2B123",
            content.Body,
            StringComparison.Ordinal
        );
        Assert.Contains("tom/abc+123/https://shard.example/moongate/", content.Body, StringComparison.Ordinal);
        Assert.Contains("/Britannia", content.Body, StringComparison.Ordinal);
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
    public async Task RegistrationEvent_ReplacesWebsiteQueryAndFragment()
    {
        var channel = new RecordingNotificationChannel("log");
        var bus = Wire(channel, "log", "https://shard.example/moongate%20portal/?legacy=true#old");

        await bus.PublishAsync(new AccountRegistrationRequestedEvent(new(1), "tom", "tom@example.com", "abc+123"));

        var content = Assert.Single(channel.Sent).Content;
        Assert.Contains(
            "/https://shard.example/moongate%20portal/verify?token=abc%2B123/Britannia",
            content.Body,
            StringComparison.Ordinal
        );
    }

    [Theory]
    [InlineData("http:/portal")]
    [InlineData("https:/portal")]
    [InlineData("http:portal")]
    [InlineData("http:///portal")]
    public void BuildVerificationUrl_HostlessHttpWebsite_ThrowsArgumentException(string website)
    {
        var method = typeof(AccountRegistrationSubscriber).GetMethod(
            "BuildVerificationUrl",
            BindingFlags.Static | BindingFlags.NonPublic
        );

        if (method is null)
        {
            throw new InvalidOperationException("BuildVerificationUrl was not found.");
        }

        var invocationException = Assert.Throws<TargetInvocationException>(
            () => method.Invoke(null, [website, "abc123"])
        );
        var exception = Assert.IsType<ArgumentException>(invocationException.InnerException);

        Assert.Equal("website", exception.ParamName);
        Assert.Contains("absolute HTTP(S) URI", exception.Message, StringComparison.Ordinal);
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
    private static EventBusService Wire(
        RecordingNotificationChannel channel,
        string routeTo,
        string website = "https://shard.example/moongate/"
    )
    {
        var templates = new NotificationTemplateService();
        templates.Register(
            channel.Id,
            "account_verification",
            "{{ username }}/{{ token }}/{{ website }}/{{ verification_url }}/{{ shard_name }}"
        );

        var notifications = new NotificationService(templates, [channel], new StubJobSystem(), new());

        var settings = new ServerSettingsService(new FakePersistenceService());
        settings.Update(new() { Contacts = new() { Website = website } });

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
