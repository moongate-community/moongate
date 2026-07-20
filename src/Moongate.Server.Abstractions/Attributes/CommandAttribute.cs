using Moongate.Core.Types;
using Moongate.Server.Abstractions.Types;

namespace Moongate.Server.Abstractions.Attributes;

/// <summary>
/// Declares an <see cref="Interfaces.Commands.ICommand" />'s dispatch name(s), minimum
/// <see cref="AccountLevelType" />, and help text. This is <b>declarative metadata for tooling</b>
/// (documentation generation and/or a future source generator that emits the registrations) — it is
/// <b>not</b> scanned at runtime. The dispatcher indexes commands from the explicit
/// <c>RegisterCommand</c> calls (see <see cref="Extensions.CommandRegistrationExtensions" />), so the
/// attribute and the registration currently state the same metadata until a generator unifies them.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class CommandAttribute : Attribute
{
    public string Name { get; }

    public AccountLevelType MinLevel { get; }

    public string Description { get; }

    public CommandSourceType Sources { get; set; } = CommandSourceType.InGame;

    /// <param name="name">
    /// May be pipe-delimited ("broadcast|bc") to register aliases; the first token is the
    /// canonical name.
    /// </param>
    public CommandAttribute(string name, AccountLevelType minLevel, string description)
    {
        Name = name;
        MinLevel = minLevel;
        Description = description;
    }
}
