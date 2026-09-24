using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Grpc.Core;
using Grpc.Net.Client;
using Moongate.Admin.Contracts.V1;

var endpoint = new Uri(Environment.GetEnvironmentVariable("MOONGATE_ADMIN_ENDPOINT")
    ?? throw new InvalidOperationException("MOONGATE_ADMIN_ENDPOINT is required."));
if (endpoint.Scheme != "https") { throw new InvalidOperationException("Use a TLS administration endpoint."); }
using var handler = new SocketsHttpHandler();
var caPath = Environment.GetEnvironmentVariable("MOONGATE_ADMIN_CA");
using var ca = caPath is null ? null : X509CertificateLoader.LoadCertificateFromFile(caPath);
if (ca is not null)
{
    var policy = new X509ChainPolicy { TrustMode = X509ChainTrustMode.CustomRootTrust, RevocationMode = X509RevocationMode.NoCheck };
    policy.CustomTrustStore.Add(ca);
    handler.SslOptions.CertificateChainPolicy = policy;
}
using var channel = GrpcChannel.ForAddress(endpoint, new() { HttpHandler = handler });
var login = await new AdminLogin.AdminLoginClient(channel).LoginAsync(new()
{
    Username = Environment.GetEnvironmentVariable("MOONGATE_ADMIN_USERNAME") ?? throw new InvalidOperationException("MOONGATE_ADMIN_USERNAME is required."),
    Password = Environment.GetEnvironmentVariable("MOONGATE_ADMIN_PASSWORD") ?? throw new InvalidOperationException("MOONGATE_ADMIN_PASSWORD is required.")
}, deadline: DateTime.UtcNow.AddSeconds(10));
var headers = new Metadata { { "authorization", "Bearer " + login.AccessToken } };
var accounts = new AdminAccounts.AdminAccountsClient(channel);
var created = await accounts.CreateAccountAsync(new()
{
    Username = "admin-sample-" + Guid.NewGuid().ToString("N"),
    Password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24))
}, headers, DateTime.UtcNow.AddSeconds(10));
var page = await accounts.ListAccountsAsync(new() { PageSize = 50 }, headers, DateTime.UtcNow.AddSeconds(10));
Console.WriteLine($"Created account {created.AccountId}; listed {page.Accounts.Count} accounts in the first page.");
await new AdminSession.AdminSessionClient(channel).LogoutAsync(new(), headers, DateTime.UtcNow.AddSeconds(10));
Console.WriteLine("Logged out.");
