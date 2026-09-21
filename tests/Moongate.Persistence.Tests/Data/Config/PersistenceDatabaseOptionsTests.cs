using Moongate.Persistence.Data.Config;
using Moongate.Persistence.Types.Persistence;
using Npgsql;

namespace Moongate.Persistence.Tests.Data.Config;

public sealed class PersistenceDatabaseOptionsTests
{
    [Theory,
     InlineData("postgres://user:synthetic_marker@localhost/"),
     InlineData("postgres://user:synthetic_marker@localhost/realm#fragment"),
     InlineData("postgres://user:synthetic_marker%XX@localhost/realm"),
     InlineData("postgres://user:synthetic_marker@localhost/realm?unknown_option=true"),
     InlineData("postgres://user:synthetic_marker@localhost/realm?port=not-a-number"),
     InlineData("https://user:synthetic_marker@localhost/realm")]
    public void ResolveRuntimeConnectionString_InvalidUri_ReportsNoCredentials(string uri)
    {
        var options = new PersistenceDatabaseOptions(PersistenceDatabaseTarget.Realm, uri);
        var error = Assert.Throws<InvalidOperationException>(options.ResolveRuntimeConnectionString);
        Assert.DoesNotContain("synthetic_marker", error.ToString());
        Assert.Contains("runtime", error.Message);
        Assert.Contains("postgres://", error.Message);
    }

    [Fact]
    public void ResolveRuntimeConnectionString_Ipv6AndDefaultPort_AreSupported()
    {
        var options = new PersistenceDatabaseOptions(
            PersistenceDatabaseTarget.Accounts,
            "postgres://user@[::1]/accounts"
        );
        var parsed = new NpgsqlConnectionStringBuilder(options.ResolveRuntimeConnectionString());
        Assert.Equal("::1", parsed.Host);
        Assert.Equal(5432, parsed.Port);
        Assert.Equal("accounts", parsed.Database);
    }

    [Theory, InlineData("postgres"), InlineData("postgresql")]
    public void ResolveRuntimeConnectionString_Uri_PreservesDecodedComponentsAndOptions(string scheme)
    {
        var options = new PersistenceDatabaseOptions(
            PersistenceDatabaseTarget.Realm,
            $"{scheme}://test%20user:synthetic%40%3A%2F%3F%23%25%2B@localhost:5433/realm%20one?sslmode=require&connect_timeout=7&application_name=Moongate%20Test&pooling=false"
        );

        var parsed = new NpgsqlConnectionStringBuilder(options.ResolveRuntimeConnectionString());

        Assert.Equal("localhost", parsed.Host);
        Assert.Equal(5433, parsed.Port);
        Assert.Equal("realm one", parsed.Database);
        Assert.Equal("test user", parsed.Username);
        Assert.Equal("synthetic@:/?#%+", parsed.Password);
        Assert.Equal(SslMode.Require, parsed.SslMode);
        Assert.Equal(7, parsed.Timeout);
        Assert.Equal("Moongate Test", parsed.ApplicationName);
        Assert.False(parsed.Pooling);
    }

    [Fact]
    public void ValidateSameDatabaseEndpoint_UriAndNativeFormat_CompareNormalizedEndpoints()
    {
        var options = new PersistenceDatabaseOptions(
            PersistenceDatabaseTarget.Realm,
            "postgres://runtime@localhost/realm",
            "Host=LOCALHOST;Database=realm;Username=schema"
        );
        var runtime = options.ResolveRuntimeConnectionString();
        var schema = options.ResolveSchemaConnectionString(runtime);
        options.ValidateSameDatabaseEndpoint(runtime, schema);
    }
}
