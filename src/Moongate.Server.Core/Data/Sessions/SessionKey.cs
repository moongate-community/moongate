namespace Moongate.Server.Core.Data.Sessions;

/// <summary>
///     The typed key of one value stored on a <see cref="GameSession" />. The key is the instance, not its name:
///     declare it once, in a static field, and share that field. Two keys with the same name are two different
///     values.
/// </summary>
/// <typeparam name="T">
///     The type of the value the key stores.
/// </typeparam>
public sealed class SessionKey<T>
{
    /// <summary>
    ///     Gets the name shown in diagnostics.
    /// </summary>
    public string Name { get; }

    /// <summary>
    ///     Gets the value a session returns while nothing has been set for this key.
    /// </summary>
    public T Default { get; }

    public SessionKey(string name, T defaultValue = default!)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Default = defaultValue;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Name;
    }
}
