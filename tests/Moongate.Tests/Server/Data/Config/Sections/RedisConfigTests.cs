using Moongate.Core.Utils;
using Moongate.Server.Data.Config;
using Moongate.Server.Data.Config.Sections;
using Moongate.Tests.TestSupport.Environment;

namespace Moongate.Tests.Server.Data.Config.Sections;

[Collection(EnvironmentTestsCollection.Name)]
public sealed class RedisConfigTests
{
    [Fact]
    public void DefaultConfig_RoundTripsSnakeCaseWithoutResolvingSecret()
    {
        var path = Path.Combine(Path.GetTempPath(), $"moongate-redis-{Guid.NewGuid():N}.toml");

        try
        {
            TomlUtils.SerializeToFile(new MoongateServerConfig(), path);
            var text = File.ReadAllText(path);

            Assert.Contains("[redis]", text);
            Assert.Contains("connection_string = \"$MOONGATE_REDIS_CONNECTION_STRING\"", text);
            Assert.Contains("handoff_secret = \"$MOONGATE_HANDOFF_SECRET\"", text);
            Assert.Equal(
                "$MOONGATE_REDIS_CONNECTION_STRING",
                TomlUtils.DeserializeFromFile<MoongateServerConfig>(path)!.Redis.ConnectionString
            );
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ResolveRuntime_RejectsBlankEndpointWithoutPrintingSecret()
    {
        var config = new RedisConfig { ConnectionString = " ", HandoffSecret = "private-value-that-must-not-be-logged" };

        var exception = Assert.Throws<InvalidOperationException>(config.ResolveConnectionString);

        Assert.Contains("redis.connection_string", exception.Message);
        Assert.DoesNotContain(config.HandoffSecret, exception.ToString());
    }

    [Fact]
    public void ResolveRuntime_ExpandsEnvironmentWithoutLeakingValues()
    {
        var connectionName = "MOONGATE_REDIS_" + Guid.NewGuid().ToString("N");
        var secretName = "MOONGATE_HANDOFF_" + Guid.NewGuid().ToString("N");
        using var connection = new EnvironmentVariableScope(connectionName, "localhost:56379,password=synthetic-secret");
        using var secret = new EnvironmentVariableScope(secretName, new('a', 32));
        var config = new RedisConfig
        {
            ConnectionString = "$" + connectionName,
            HandoffSecret = "$" + secretName
        };

        Assert.Equal("localhost:56379,password=synthetic-secret", config.ResolveConnectionString());
        Assert.Equal(new('a', 32), config.ResolveHandoffSecret());
    }

    [Fact]
    public void ResolveHandoffSecret_RejectsShortValueWithoutPrintingIt()
    {
        var config = new RedisConfig { HandoffSecret = "short-secret" };

        var exception = Assert.Throws<InvalidOperationException>(config.ResolveHandoffSecret);

        Assert.Contains("redis.handoff_secret", exception.Message);
        Assert.DoesNotContain("short-secret", exception.ToString());
    }
}
