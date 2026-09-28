using Moongate.Server.Ultima.Data.Motd;

namespace Moongate.Server.Ultima.Interfaces.Motd;

/// <summary>
///     Registers MOTD variables before startup validation and resolves them for character entry.
/// </summary>
public interface IMotdVariableRegistry
{
    /// <summary>Returns whether a variable is registered.</summary>
    bool Contains(string name);

    /// <summary>Closes registration after all plugins have registered.</summary>
    void Freeze();

    /// <summary>Registers a uniquely named variable resolver.</summary>
    void Register(string name, Func<MotdContext, CancellationToken, ValueTask<string>> resolver);

    /// <summary>Resolves one variable for the current character entry.</summary>
    ValueTask<string> ResolveAsync(string name, MotdContext context, CancellationToken cancellationToken);
}
