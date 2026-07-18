using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Moongate.Core.Primitives;
using Moongate.Core.Types;
using Moongate.Http.Plugin.Data;
using Moongate.Http.Plugin.Data.Config;
using Moongate.Http.Plugin.Interfaces.Auth;

namespace Moongate.Http.Plugin.Services.Auth;

/// <inheritdoc />
public sealed class JwtTokenService : IJwtTokenService
{
    private const int MinimumKeyBytes = 32;

    private readonly MoongateHttpConfig _config;
    private readonly TimeProvider _timeProvider;

    public JwtTokenService(MoongateHttpConfig config, TimeProvider timeProvider)
    {
        _config = config;
        _timeProvider = timeProvider;
    }

    public ApiTokenResult Issue(Serial accountId, string username, AccountLevelType level)
    {
        var key = SigningKey(_config.Jwt.SigningKey);
        var expiresAt = _timeProvider.GetUtcNow().AddMinutes(_config.Jwt.LifetimeMinutes);

        var token = new JwtSecurityToken(
            issuer: _config.Jwt.Issuer,
            audience: _config.Jwt.Issuer,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, accountId.Value.ToString()),
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Role, level.ToString())
            ],
            expires: expiresAt.UtcDateTime,
            signingCredentials: new(key, SecurityAlgorithms.HmacSha256)
        );

        return new(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    /// <summary>
    /// A fresh key, for a server whose config has none yet. Generated per install rather than shipped as a
    /// constant: this key signs the tokens that carry <see cref="AccountLevelType.Administrator" />, so one
    /// baked into the source would let anyone who reads it mint staff tokens against every server whose
    /// owner never changed it.
    /// </summary>
    internal static string GenerateSigningKey()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(MinimumKeyBytes));

    /// <summary>
    /// Turns the configured key into a signing key, refusing anything HS256 cannot sign with. A short key
    /// would otherwise mint tokens that nobody can verify. Shared with the server, which validates
    /// incoming tokens against the same key and must refuse a bad one the same way.
    /// </summary>
    internal static SymmetricSecurityKey SigningKey(string signingKey)
    {
        var bytes = Encoding.UTF8.GetBytes(signingKey);

        if (bytes.Length < MinimumKeyBytes)
        {
            throw new InvalidOperationException(
                $"http.Jwt.SigningKey must be at least {MinimumKeyBytes} bytes for HS256; it is {bytes.Length}. " +
                "Set a longer key in moongate.yaml."
            );
        }

        return new(bytes);
    }
}
