using System.Collections.Frozen;
using Moongate.Server.Ultima.Data.Motd;
using Moongate.Server.Ultima.Interfaces.Motd;

namespace Moongate.Server.Ultima.Services.Motd;

/// <summary>
///     Keeps plugin variable registrations stable after MOTD templates are validated.
/// </summary>
public sealed class MotdVariableRegistry : IMotdVariableRegistry
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Func<MotdContext, CancellationToken, ValueTask<string>>> _resolvers =
        new(StringComparer.Ordinal);

    private FrozenDictionary<string, Func<MotdContext, CancellationToken, ValueTask<string>>>? _frozen;

    public bool Contains(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        lock (_gate)
        {
            return _frozen is not null ? _frozen.ContainsKey(name) : _resolvers.ContainsKey(name);
        }
    }

    public void Freeze()
    {
        lock (_gate)
        {
            _frozen ??= _resolvers.ToFrozenDictionary(StringComparer.Ordinal);
        }
    }

    public void Register(string name, Func<MotdContext, CancellationToken, ValueTask<string>> resolver)
    {
        if (!IsValidName(name))
        {
            throw new ArgumentException("MOTD variable names must match [a-z][a-z0-9_]*.", nameof(name));
        }

        ArgumentNullException.ThrowIfNull(resolver);

        lock (_gate)
        {
            if (_frozen is not null)
            {
                throw new InvalidOperationException("MOTD variable registration is closed.");
            }

            if (!_resolvers.TryAdd(name, resolver))
            {
                throw new InvalidOperationException($"MOTD variable '{name}' is already registered.");
            }
        }
    }

    public ValueTask<string> ResolveAsync(string name, MotdContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(context);
        Func<MotdContext, CancellationToken, ValueTask<string>> resolver;

        lock (_gate)
        {
            var found = _frozen is not null
                ? _frozen.TryGetValue(name, out resolver!)
                : _resolvers.TryGetValue(name, out resolver!);

            if (!found)
            {
                throw new KeyNotFoundException($"MOTD variable '{name}' is not registered.");
            }
        }

        return resolver(context, cancellationToken);
    }

    private static bool IsValidName(string? name)
    {
        if (string.IsNullOrEmpty(name) || name[0] is < 'a' or > 'z')
        {
            return false;
        }

        return name[1..].All(character =>
            character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_');
    }
}
