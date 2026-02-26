namespace Moongate.Server.PacketHandlers.Generators.Data.Internal;

internal sealed class FileLoaderRegistrationModel : IEquatable<FileLoaderRegistrationModel>
{
    public string LoaderTypeName { get; }

    public int Order { get; }

    public FileLoaderRegistrationModel(string loaderTypeName, int order)
    {
        LoaderTypeName = loaderTypeName;
        Order = order;
    }

    public bool Equals(FileLoaderRegistrationModel? other)
        => other is not null &&
           Order == other.Order &&
           string.Equals(LoaderTypeName, other.LoaderTypeName, StringComparison.Ordinal);

    public override bool Equals(object? obj)
        => obj is FileLoaderRegistrationModel other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            return ((LoaderTypeName != null ? LoaderTypeName.GetHashCode() : 0) * 397) ^ Order.GetHashCode();
        }
    }
}
