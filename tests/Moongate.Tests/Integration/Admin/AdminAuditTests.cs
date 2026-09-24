using Grpc.Core;
using Moongate.Admin.Contracts.V1;
using Moongate.Server.Ultima.Data.Account;
using Moongate.Tests.TestSupport.Admin;
using Moongate.Tests.TestSupport.Persistence;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Server.Ultima;
using Serilog;
using Serilog.Events;
using DomainAccountType = Moongate.Server.Core.Types.Accounts.AccountType;

namespace Moongate.Tests.Integration.Admin;

[Collection(PostgresTestCollection.Name)]
public sealed class AdminAuditTests
{
    [Fact]
    public async Task LoginLogoutAndRejectedCall_AuditSafeIdentityAndOutcome()
    {
        var previous = Log.Logger;
        var sink = new CapturingLogSink();
        using var logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        Log.Logger = logger;
        try
        {
            string accountId;
            string password;
            string token;
            await using (var fixture = await AdminGrpcFixture.CreateAsync())
            {
                password = fixture.Backend.Accounts.Password;
                var created = await fixture.Backend.Accounts.Service.CreateAccountAsync(new AccountCreateOptions
                {
                    Username = "audit-admin", Password = password,
                    AccountType = DomainAccountType.Administrator, CanAccessApi = true
                });
                accountId = created.Account!.Id.Value.ToString();
                var login = await new AdminLogin.AdminLoginClient(fixture.Channel).LoginAsync(new()
                {
                    Username = "audit-admin", Password = password
                });
                token = login.AccessToken;
                await new AdminSession.AdminSessionClient(fixture.Channel).LogoutAsync(new(),
                    new Metadata { { "authorization", "Bearer " + token } });
                fixture.Gate.StopAccepting();
                await Assert.ThrowsAsync<RpcException>(() => new AdminServer.AdminServerClient(fixture.Channel)
                    .GetServerInfoAsync(new()).ResponseAsync);
            }
            var audits = sink.Events.Where(e => e.Properties.ContainsKey("Operation")).ToArray();
            foreach (var operation in new[] { "/Login", "/Logout" })
            {
                var entry = Assert.Single(audits, e => e.Properties["Operation"].ToString().Contains(operation, StringComparison.Ordinal));
                Assert.Equal(accountId, ((ScalarValue)entry.Properties["ActorId"]).Value?.ToString());
            }
            Assert.Contains(audits, e => ((ScalarValue)e.Properties["Status"]).Value?.ToString() == "Unavailable");
            Assert.DoesNotContain(sink.Events, e => (e.RenderMessage() + e.Exception).Contains(password, StringComparison.Ordinal) ||
                (e.RenderMessage() + e.Exception).Contains(token, StringComparison.Ordinal));
        }
        finally { Log.Logger = previous; }
    }

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
