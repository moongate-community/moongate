using System.Net.Mail;
using System.Text.RegularExpressions;

namespace Moongate.Server.Services.Accounts;

internal static partial class PublicRegistrationValidator
{
    [GeneratedRegex("^[A-Za-z0-9._-]{3,30}$", RegexOptions.CultureInvariant)]
    private static partial Regex UsernameRegex();

    public static string NormalizeUsername(string? username)
        => username?.Trim() ?? string.Empty;

    public static string NormalizeEmail(string? email)
        => email?.Trim() ?? string.Empty;

    public static bool IsUsernameValid(string username)
        => UsernameRegex().IsMatch(username);

    public static bool IsPasswordValid(string? password)
        => password is { Length: >= 8 and <= 30 }
           && password.All(character => character is >= ' ' and <= '~');

    public static bool IsEmailValid(string email)
        => MailAddress.TryCreate(email, out _);
}
