namespace Moongate.Server.Core.Interfaces.Config;

/// <summary>
///     A section of <c>config/moongate.toml</c> that checks its own values when it is added with
///     <c>AddConfig</c>.
/// </summary>
public interface IConfigSection
{
    /// <summary>
    ///     Validates the section before server services begin startup; throws when a value is not allowed.
    /// </summary>
    void Validate();
}
