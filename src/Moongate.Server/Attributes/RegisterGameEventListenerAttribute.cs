namespace Moongate.Server.Attributes;

/// <summary>
/// Declares that a type should be auto-registered and auto-subscribed as a game event listener during bootstrap.
/// This attribute is consumed by the game-event-listener source generator.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class RegisterGameEventListenerAttribute : Attribute
{
    public int Priority { get; }

    public RegisterGameEventListenerAttribute(int priority = 200) => Priority = priority;
}
