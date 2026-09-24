using System.Diagnostics;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Tests.TestSupport.Admin;
using Moongate.Tests.TestSupport.Persistence;

namespace Moongate.Tests.Integration.Admin;

[Collection(PostgresTestCollection.Name)]
public sealed class AdminProtocolTests
{
    [AdminProtocolFact]
    public async Task PythonClient_GeneratedFromRawContracts_CompletesCrossRoleWorkflow()
    {
        await using var fixture = await AdminHostFixture.CreateAsync();
        await fixture.Backend.Accounts.Service.CreateAccountAsync(
            new()
            {
                Username = "admin", Password = fixture.Backend.Accounts.Password, AccountType = AccountType.Administrator,
                CanAccessApi = true
            }
        );
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("MOONGATE_ADMIN_PYTHON")!)
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add(Environment.GetEnvironmentVariable("MOONGATE_ADMIN_PYTHON_CLIENT")!);
        start.Environment["PYTHONPATH"] = Environment.GetEnvironmentVariable("MOONGATE_ADMIN_PROTO_PATH");
        start.Environment["MOONGATE_ADMIN_ENDPOINT"] = fixture.LoginEndpoint;
        start.Environment["MOONGATE_ADMIN_GAME_ENDPOINT"] = fixture.GameEndpoint;
        start.Environment["MOONGATE_ADMIN_CA"] = fixture.Certificates.RootPemPath;
        start.Environment["MOONGATE_ADMIN_USERNAME"] = "admin";
        start.Environment["MOONGATE_ADMIN_PASSWORD"] = fixture.Backend.Accounts.Password;
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(0, process.ExitCode);
        Assert.Contains("Administration workflow passed", await stdout);
        Assert.Equal("", await stderr);
    }
}
