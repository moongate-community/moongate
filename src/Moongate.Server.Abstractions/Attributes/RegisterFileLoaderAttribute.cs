namespace Moongate.Server.Attributes;

/// <summary>
/// Marks a file loader for source-generated registration in <c>BootstrapFileLoaderRegistration</c>.
/// The <see cref="Order" /> value controls execution sequence (lower runs first).
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class RegisterFileLoaderAttribute : Attribute
{
    /// <summary>
    /// Gets the execution order for this loader. Lower values run first.
    /// </summary>
    public int Order { get; }

    /// <summary>
    /// Initializes a new attribute instance with the specified execution order.
    /// </summary>
    /// <param name="order">Execution order for the annotated file loader.</param>
    public RegisterFileLoaderAttribute(int order)
    {
        Order = order;
    }
}
