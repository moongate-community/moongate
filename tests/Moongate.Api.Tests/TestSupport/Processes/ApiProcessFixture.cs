using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Moongate.Api.Tests.TestSupport.Security;

namespace Moongate.Api.Tests.TestSupport.Processes;

internal sealed class ApiProcessFixture : IAsyncDisposable
{
    private readonly string _root;
    private readonly string _admin;
    private readonly string _denied;
    private readonly string _loginFingerprint;
    private readonly string _gameFingerprint;
    private ApiChildProcess? _login;
    private ApiChildProcess? _game;
    private int _loginPort;
    private int _gamePort;

    private ApiProcessFixture(string root, string admin, string denied, string loginFingerprint, string gameFingerprint)
    {
        _root = root;
        _admin = admin;
        _denied = denied;
        _loginFingerprint = loginFingerprint;
        _gameFingerprint = gameFingerprint;
    }

    public Task<string> CallForbiddenAsync()
        => CallAsync(_gamePort, _gameFingerprint, 41, true);

    public async Task<int> CallGameIncrementAsync(int value)
        => int.Parse(await CallAsync(_gamePort, _gameFingerprint, value));

    public async Task<int> CallIncrementAsync(int value)
        => int.Parse(await CallAsync(_loginPort, _loginFingerprint, value));

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_login is not null)
            {
                await _login.DisposeAsync();
            }
        }
        finally
        {
            if (_game is not null)
            {
                await _game.DisposeAsync();
            }
        }
    }

    public async Task<int> GameInvocationCountAsync()
    {
        await _game!.SendAsync("COUNT");
        var result = await _game.ReadLineAsync();

        return int.Parse(result["COUNT ".Length..]);
    }

    public Task<string> LoseReplyAndReconnectAsync()
        => CallAsync(_gamePort, _gameFingerprint, -1, reconnect: true);

    public static async Task<ApiProcessFixture> StartAsync()
    {
        using var ca = new TestCertificateAuthority();
        using var login = ca.Issue();
        using var game = ca.Issue();
        using var admin = ca.Issue();
        using var denied = ca.Issue();
        var root = Convert.ToBase64String(ca.Root.RawData);
        var fixture = new ApiProcessFixture(root, Export(admin), Export(denied), Fingerprint(login), Fingerprint(game));
        var peers = new Dictionary<string, string> { [Fingerprint(admin)] = "admin", [Fingerprint(denied)] = "denied" };
        var permissions = new Dictionary<string, ushort[]> { ["admin"] = [100], ["denied"] = [] };

        try
        {
            fixture._login = await ApiChildProcess.StartAsync(
                                 "login",
                                 new { Certificate = Export(login), Root = root, Peers = peers, Permissions = permissions }
                             );
            fixture._loginPort = ParsePort(await fixture._login.ReadLineAsync());
            fixture._game = await ApiChildProcess.StartAsync(
                                "game",
                                new { Certificate = Export(game), Root = root, Peers = peers, Permissions = permissions }
                            );
            fixture._gamePort = ParsePort(await fixture._game.ReadLineAsync());

            return fixture;
        }
        catch
        {
            await fixture.DisposeAsync();

            throw;
        }
    }

    public async Task StopLoginAsync()
    {
        if (_login is null)
        {
            return;
        }

        await _login.SendAsync("STOP");
        await _login.ExpectExitAsync();
        await _login.DisposeAsync();
        _login = null;
    }

    private async Task<string> CallAsync(
        int port,
        string fingerprint,
        int value,
        bool denied = false,
        bool reconnect = false
    )
    {
        await using var client = await ApiChildProcess.StartAsync(
                                     "client",
                                     new
                                     {
                                         Certificate = denied ? _denied : _admin,
                                         Root = _root,
                                         Peers = new Dictionary<string, string> { [fingerprint] = "target" },
                                         Permissions = new Dictionary<string, ushort[]> { ["target"] = [100] },
                                         Port = port,
                                         Value = value,
                                         ReconnectAfterLoss = reconnect
                                     }
                                 );
        var result = await client.ReadLineAsync();
        await client.ExpectExitAsync();

        return result;
    }

    private static string Export(X509Certificate2 certificate)
        => Convert.ToBase64String(certificate.Export(X509ContentType.Pkcs12));

    private static string Fingerprint(X509Certificate2 certificate)
        => certificate.GetCertHashString(HashAlgorithmName.SHA256);

    private static int ParsePort(string ready)
    {
        if (!ready.StartsWith("READY ", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Test host did not become ready.");
        }

        return int.Parse(ready["READY ".Length..]);
    }
}
