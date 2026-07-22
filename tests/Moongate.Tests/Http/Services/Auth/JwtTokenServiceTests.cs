using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Moongate.Core.Types;
using Moongate.Http.Plugin.Data.Config;
using Moongate.Http.Plugin.Services.Auth;
using Moongate.Tests.Support;

namespace Moongate.Tests.Http.Services.Auth;

public class JwtTokenServiceTests
{
    private static readonly DateTimeOffset SessionStart = new(2026, 7, 22, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Issue_CarriesTheAccountSerialAndUsername()
    {
        var token = Read(Service().Issue(new(5), "tom", AccountLevelType.Player, SessionStart).Token);

        Assert.Equal("5", token.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal("tom", token.Claims.First(c => c.Type == ClaimTypes.Name).Value);
    }

    [Fact]
    public void Issue_ExpiresAfterTheConfiguredLifetime()
    {
        var now = new DateTimeOffset(2026, 7, 16, 12, 0, 0, TimeSpan.Zero);
        var result = Service(lifetimeMinutes: 30, now: now).Issue(new(5), "tom", AccountLevelType.Player, now);

        Assert.Equal(now.AddMinutes(30), result.ExpiresAt);
    }

    [Theory, InlineData(""), InlineData("too-short-for-hs256")]
    public void Issue_KeyShorterThan32Bytes_Throws(string signingKey)
    {
        // HS256 needs at least 32 bytes. Failing loudly beats minting tokens nobody can verify.
        var service = Service(signingKey);

        Assert.Throws<InvalidOperationException>(
            () => service.Issue(new(5), "tom", AccountLevelType.Player, SessionStart)
        );
    }

    [Fact]
    public void Issue_PutsTheAccountLevelInTheRoleClaim()
    {
        var token = Read(Service().Issue(new(5), "tom", AccountLevelType.Administrator, SessionStart).Token);

        Assert.Equal("Administrator", token.Claims.First(c => c.Type == ClaimTypes.Role).Value);
    }

    [Fact]
    public void Issue_UsesTheConfiguredIssuer()
    {
        var token = Read(Service().Issue(new(5), "tom", AccountLevelType.Player, SessionStart).Token);

        Assert.Equal("moongate", token.Issuer);
    }

    [Fact]
    public void Issue_CarriesTheSessionStartRatherThanTheMintingInstant()
    {
        // The renewal endpoint reads this back to cap the session, so it must be the value handed in — not
        // "now" — or a chain of renewals would never age.
        var mintedAt = SessionStart.AddHours(3);

        var token = Read(Service(now: mintedAt).Issue(new(5), "tom", AccountLevelType.Player, SessionStart).Token);

        // The literal rather than the constant: the claim name is a wire contract, so renaming it in the
        // source has to fail here instead of quietly agreeing with itself.
        Assert.Equal(
            SessionStart.ToUnixTimeSeconds().ToString(),
            token.Claims.First(c => c.Type == "auth_time").Value
        );
    }

    private static JwtSecurityToken Read(string token)
        => new JwtSecurityTokenHandler().ReadJwtToken(token);

    private static JwtTokenService Service(
        string signingKey = "a-signing-key-long-enough-for-hs256!!",
        int lifetimeMinutes = 60,
        DateTimeOffset? now = null
    )
    {
        var config = new MoongateHttpConfig
        {
            Jwt = new() { SigningKey = signingKey, LifetimeMinutes = lifetimeMinutes, Issuer = "moongate" }
        };

        return new(config, new FixedTimeProvider(now ?? DateTimeOffset.UtcNow));
    }
}
