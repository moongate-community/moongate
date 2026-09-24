using Moongate.Tests.TestSupport.Persistence;
using Moongate.Tests.TestSupport.Server.Ultima;
using Moongate.Tests.TestSupport.Scripting;
using Serilog;

namespace Moongate.Tests.Integration.Admin;

[Collection(PostgresTestCollection.Name)]
public sealed class AdminAuditTests
{
    [Fact]
    public async Task CreateAccount_ProviderError_DoesNotLogCredentialMaterial()
    {
        var previous = Log.Logger;
        var sink = new CapturingLogSink();
        using var logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        Log.Logger = logger;
        try
        {
            await using var fixture = await AccountServiceFixture.CreateAsync();
            var secret = Guid.NewGuid().ToString("N");
            await fixture.Database.ExecuteAsync($"""
                CREATE FUNCTION auth.reject_insert() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION '{secret}'; END $$;
                CREATE TRIGGER reject_insert BEFORE INSERT ON auth.accounts FOR EACH ROW EXECUTE FUNCTION auth.reject_insert();
                """);
            var result = await fixture.Service.CreateAccountAsync("admin", fixture.Password);
            Assert.False(result.Success);
            var errors = sink.Events.Where(e => e.Level == Serilog.Events.LogEventLevel.Error).ToArray();
            Assert.NotEmpty(errors);
            Assert.DoesNotContain(errors, e => (e.RenderMessage() + e.Exception).Contains(secret, StringComparison.Ordinal));
        }
        finally { Log.Logger = previous; }
    }
}
