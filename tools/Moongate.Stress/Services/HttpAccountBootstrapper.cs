using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Moongate.Stress.Data;
using Moongate.Stress.Interfaces;

namespace Moongate.Stress.Services;

public sealed class HttpAccountBootstrapper : IAccountBootstrapper
{
    private readonly HttpClient _httpClient;
    private readonly StressRunOptions _options;

    public HttpAccountBootstrapper(HttpClient httpClient, StressRunOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task EnsureUsersAsync(CancellationToken cancellationToken = default)
    {
        var jwtToken = await TryAuthenticateAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(jwtToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwtToken);
        }

        var existingUserNames = await GetExistingUserNamesAsync(cancellationToken);

        for (var i = 1; i <= _options.Clients; i++)
        {
            var username = BuildUsername(_options.UserPrefix, i);

            if (existingUserNames.Contains(username))
            {
                continue;
            }

            await CreateUserAsync(username, cancellationToken);
        }
    }

    public static string BuildUsername(string prefix, int index)
        => $"{prefix}_{index:0000}";

    private async Task<string?> TryAuthenticateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.AdminUsername) || string.IsNullOrWhiteSpace(_options.AdminPassword))
        {
            return null;
        }

        var payload = JsonSerializer.Serialize(
            new
            {
                username = _options.AdminUsername,
                password = _options.AdminPassword
            }
        );

        using var request = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("/auth/login", request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"HTTP auth failed ({(int)response.StatusCode}): {await response.Content.ReadAsStringAsync(cancellationToken)}"
            );
        }

        var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var token = TryExtractToken(rawJson);

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("HTTP auth succeeded but no token was returned.");
        }

        return token;
    }

    private async Task<HashSet<string>> GetExistingUserNamesAsync(CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync("/api/users/", cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized ||
            response.StatusCode == HttpStatusCode.Forbidden)
        {
            var authHint = string.IsNullOrWhiteSpace(_options.AdminUsername) ||
                           string.IsNullOrWhiteSpace(_options.AdminPassword)
                               ? "Missing admin credentials. Pass --admin-username and --admin-password."
                               : "Admin credentials were provided but were rejected.";
            throw new InvalidOperationException(
                $"Cannot query existing users ({(int)response.StatusCode}). {authHint}"
            );
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Cannot query existing users ({(int)response.StatusCode}): {await response.Content.ReadAsStringAsync(cancellationToken)}"
            );
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var users = await JsonSerializer.DeserializeAsync<List<HttpUserDto>>(stream, cancellationToken: cancellationToken);

        if (users is null)
        {
            return [];
        }

        return users.Select(static user => user.Username)
                    .Where(static username => !string.IsNullOrWhiteSpace(username))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private async Task CreateUserAsync(string username, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(
            new
            {
                username,
                password = _options.UserPassword,
                email = $"{username}@stress.local",
                role = _options.UserRole
            }
        );

        using var request = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("/api/users/", request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized ||
            response.StatusCode == HttpStatusCode.Forbidden)
        {
            var authHint = string.IsNullOrWhiteSpace(_options.AdminUsername) ||
                           string.IsNullOrWhiteSpace(_options.AdminPassword)
                               ? "Missing admin credentials. Pass --admin-username and --admin-password."
                               : "Admin credentials were provided but were rejected.";
            throw new InvalidOperationException(
                $"Cannot create user '{username}' ({(int)response.StatusCode}). {authHint}"
            );
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Cannot create user '{username}' ({(int)response.StatusCode}): {await response.Content.ReadAsStringAsync(cancellationToken)}"
            );
        }
    }

    private sealed class HttpUserDto
    {
        public string Username { get; set; } = string.Empty;
    }

    private static string? TryExtractToken(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return null;
        }

        using var document = JsonDocument.Parse(rawJson);

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (TryGetStringProperty(document.RootElement, "AccessToken", out var accessToken) ||
            TryGetStringProperty(document.RootElement, "accessToken", out accessToken) ||
            TryGetStringProperty(document.RootElement, "token", out accessToken))
        {
            return accessToken;
        }

        return null;
    }

    private static bool TryGetStringProperty(JsonElement element, string propertyName, out string? value)
    {
        value = null;

        if (!element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        var raw = property.GetString();

        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        value = raw;

        return true;
    }
}
