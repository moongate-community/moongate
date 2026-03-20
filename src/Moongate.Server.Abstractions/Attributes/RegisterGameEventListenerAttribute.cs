namespace Moongate.Server.Attributes;

/// <summary>
/// Declares that a type should be auto-registered and auto-subscribed as a game event listener during bootstrap.
/// This attribute is consumed by the game-event-listener source generator.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class RegisterGameEventListenerAttribute : Attribute
{
    /// <summary>
    /// Gets the startup priority for listener services that are also host services.
    /// </summary>
    public int Priority { get; }

    /// <summary>
    /// Initializes a new attribute instance.
    /// </summary>
    /// <param name="priority">Startup priority for host-managed listeners.</param>
    public RegisterGameEventListenerAttribute(int priority = 200)
    {
        Priority = priority;
    }
}
