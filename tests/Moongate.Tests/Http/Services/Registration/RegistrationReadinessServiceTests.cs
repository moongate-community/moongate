using Moongate.Http.Plugin.Services.Registration;
using Moongate.Server.Abstractions.Interfaces.Notifications;
using Moongate.Tests.Support;

namespace Moongate.Tests.Http.Services.Registration;

public sealed class RegistrationReadinessServiceTests
{
    [Theory, InlineData("http:/portal"), InlineData("https:/portal"), InlineData("http:portal"),
     InlineData("http:///portal")]
    public void Evaluate_HostlessHttpWebsite_IsNotValidOrReady(string website)
    {
        var service = new RegistrationReadinessService(
            new() { AccountVerificationChannel = "email" },
            [new RecordingNotificationChannel("email")]
        );

        var readiness = service.Evaluate(website);

        Assert.False(readiness.WebsiteValid);
        Assert.False(readiness.Ready);
    }

    [Theory, InlineData(null, "email", "email", false, true, true),
     InlineData("https://shard.example", "log", "email", true, false, true),
     InlineData("https://shard.example", "email", null, true, true, false),
     InlineData("https://shard.example", "EMAIL", "EMAIL", true, true, true)]
    public void Evaluate_ReportsEveryReadinessProperty(
        string? website,
        string selected,
        string? registeredChannel,
        bool websiteValid,
        bool emailChannelSelected,
        bool emailChannelAvailable
    )
    {
        INotificationChannel[] channels = registeredChannel is null
                                              ? []
                                              : [new RecordingNotificationChannel(registeredChannel)];
        var service = new RegistrationReadinessService(
            new() { AccountVerificationChannel = selected },
            channels
        );

        var readiness = service.Evaluate(website);

        Assert.Equal(websiteValid, readiness.WebsiteValid);
        Assert.Equal(emailChannelSelected, readiness.EmailChannelSelected);
        Assert.Equal(emailChannelAvailable, readiness.EmailChannelAvailable);
        Assert.Equal(websiteValid && emailChannelSelected && emailChannelAvailable, readiness.Ready);
    }

    [Theory, InlineData("https://shard.example", "email", true, true),
     InlineData("http://shard.example/moongate", "EMAIL", true, true), InlineData(null, "email", true, false),
     InlineData("ftp://shard.example", "email", true, false), InlineData("https://shard.example", "log", true, false),
     InlineData("https://shard.example", "email", false, false)]
    public void Evaluate_RequiresWebsiteSelectedEmailAndRegisteredChannel(
        string? website,
        string selected,
        bool channelRegistered,
        bool expected
    )
    {
        INotificationChannel[] channels = channelRegistered ? [new RecordingNotificationChannel("email")] : [];
        var service = new RegistrationReadinessService(
            new() { AccountVerificationChannel = selected },
            channels
        );

        Assert.Equal(expected, service.Evaluate(website).Ready);
    }
}
