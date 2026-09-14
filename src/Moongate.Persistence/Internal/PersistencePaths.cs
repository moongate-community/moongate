namespace Moongate.Persistence.Internal;
public sealed class PersistencePaths
{
    public string DirectoryPath { get; }

    public string CollectionName { get; }

    public string Snapshot { get; }

    public string Journal { get; }

    public string Lock { get; }

    public PersistencePaths(string directory, string collectionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ValidateCollectionName(collectionName);
        DirectoryPath = Path.GetFullPath(directory);
        CollectionName = collectionName;
        Snapshot = Path.Combine(DirectoryPath, collectionName + ".snapshot.bin");
        Journal = Path.Combine(DirectoryPath, collectionName + ".journal.bin");
        Lock = Path.Combine(DirectoryPath, collectionName + ".lock");
    }

    public static void ValidateCollectionName(string collectionName)
    {
        if (collectionName is null || collectionName.Length is < 1 or > 64 ||
            collectionName.Any(c => c is not (>= 'a' and <= 'z') and not (>= '0' and <= '9') and not '_' and not '-'))
        {
            throw new ArgumentException("Collection names require 1..64 lowercase ASCII letters, digits, underscores or hyphens.", nameof(collectionName));
        }
    }
}
