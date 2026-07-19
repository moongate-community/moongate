using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Moongate.Core.Types;
using Moongate.Http.Plugin.Data.Config;
using Moongate.Http.Plugin.Services.Auth;
using Moongate.Tests.Support;

namespace Moongate.Tests.Http.Services.Auth;

public class JwtTokenServiceTests
{
    [Fact]
    public void Issue_CarriesTheAccountSerialAndUsername()
    {
        var token = Read(Service().Issue(new(5), "tom", AccountLevelType.Player).Token);

        Assert.Equal("5", token.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal("tom", token.Claims.First(c => c.Type == ClaimTypes.Name).Value);
    }

    [Fact]
    public void Issue_ExpiresAfterTheConfiguredLifetime()
    {
        var now = new DateTimeOffset(2026, 7, 16, 12, 0, 0, TimeSpan.Zero);
        var result = Service(lifetimeMinutes: 30, now: now).Issue(new(5), "tom", AccountLevelType.Player);

        Assert.Equal(now.AddMinutes(30), result.ExpiresAt);
    }

    [Theory, InlineData(""), InlineData("too-short-for-hs256")]
    public void Issue_KeyShorterThan32Bytes_Throws(string signingKey)
    {
        // HS256 needs at least 32 bytes. Failing loudly beats minting tokens nobody can verify.
        var service = Service(signingKey);

        Assert.Throws<InvalidOperationException>(() => service.Issue(new(5), "tom", AccountLevelType.Player));
    }

    [Fact]
    public void Issue_PutsTheAccountLevelInTheRoleClaim()
    {
        var token = Read(Service().Issue(new(5), "tom", AccountLevelType.Administrator).Token);

        Assert.Equal("Administrator", token.Claims.First(c => c.Type == ClaimTypes.Role).Value);
    }

    [Fact]
    public void Issue_UsesTheConfiguredIssuer()
    {
        var token = Read(Service().Issue(new(5), "tom", AccountLevelType.Player).Token);

        Assert.Equal("moongate", token.Issuer);
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
