using Moongate.Core.Types;
using Moongate.Server.Abstractions.Types;

namespace Moongate.Server.Abstractions.Attributes;

/// <summary>
/// Declares an <see cref="Interfaces.Commands.ICommand" />'s dispatch name(s), minimum
/// <see cref="AccountLevelType" />, and help text. <paramref name="name" /> may be pipe-delimited
/// ("broadcast|bc") to register aliases; the first token is the canonical name.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class CommandAttribute : Attribute
{
    public string Name { get; }

    public AccountLevelType MinLevel { get; }

    public string Description { get; }

    public CommandSourceType Sources { get; set; } = CommandSourceType.InGame;

    public CommandAttribute(string name, AccountLevelType minLevel, string description)
    {
        Name = name;
        MinLevel = minLevel;
        Description = description;
    }
}
