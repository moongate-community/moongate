using Moongate.Http.Plugin.Services.Registration;
using Moongate.Server.Abstractions.Data.Config;
using Moongate.Server.Abstractions.Interfaces.Notifications;
using Moongate.Tests.Support;
using Xunit;

namespace Moongate.Tests.Http.Services.Registration;

public sealed class RegistrationReadinessServiceTests
{
    [Theory]
    [InlineData("https://shard.example", "email", true, true)]
    [InlineData("http://shard.example/moongate", "EMAIL", true, true)]
    [InlineData(null, "email", true, false)]
    [InlineData("ftp://shard.example", "email", true, false)]
    [InlineData("https://shard.example", "log", true, false)]
    [InlineData("https://shard.example", "email", false, false)]
    public void Evaluate_RequiresWebsiteSelectedEmailAndRegisteredChannel(
        string? website,
        string selected,
        bool channelRegistered,
        bool expected
    )
    {
        INotificationChannel[] channels = channelRegistered ? [new RecordingNotificationChannel("email")] : [];
        var service = new RegistrationReadinessService(
            new NotificationConfig { AccountVerificationChannel = selected },
            channels
        );

        Assert.Equal(expected, service.Evaluate(website).Ready);
    }
}
