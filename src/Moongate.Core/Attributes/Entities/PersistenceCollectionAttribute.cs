namespace Moongate.Core.Attributes.Entities;

/// <summary>Declares the persistence collection that stores an entity type.</summary>
/// <remarks>
/// The collection name is storage identity: it names the files on disk and has to stay stable for
/// the life of the data. Declaring it here rather than deriving it from the type name keeps a class
/// rename from pointing the server at a different collection and orphaning what is already stored.
/// The persistence layer validates the name when the collection is registered.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class PersistenceCollectionAttribute : Attribute
{
    /// <summary>Gets the name of the collection that stores the annotated entity.</summary>
    public string Name { get; }

    public PersistenceCollectionAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
    }
}
