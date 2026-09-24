using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace Moongate.Server.Admin.Internal;

internal static class AdminToken
{
    public static bool TryGetDigest(IHeaderDictionary headers, out string digest)
    {
        digest = "";
        var values = headers.Authorization;
        if (values.Count != 1) { return false; }
        var value = values[0];
        if (value is null || value.Length != 71 || !value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        var token = value[7..];
        if (!token.All(Uri.IsHexDigit)) { return false; }
        digest = Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(token)));
        return true;
    }
}
