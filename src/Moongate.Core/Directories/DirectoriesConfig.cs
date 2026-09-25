using Moongate.Core.Extensions.Strings;

namespace Moongate.Core.Directories;

public class DirectoriesConfig
{
    private static readonly char[] PathSegmentSeparators = ['/', '\\'];

    private readonly string[] _directories;

    public string Root { get; }

    public string this[string directoryType] => GetPath(directoryType);

    public string this[Enum directoryType] => GetPath(directoryType.ToString());

    public DirectoriesConfig(string rootDirectory, string[] directories)
    {
        _directories = directories;
        Root = rootDirectory;

        Init();
    }

    public void CreateDirectoryIfNotExists(string directoryType)
    {
        var path = GetPath(directoryType);

        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    public string GetPath<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        return GetPath(Enum.GetName(value));
    }

    public string GetPath(string directoryType)
    {
        // directoryType may name nested segments with '/' or '\', regardless of the host OS
        // (callers write "templates/test/test" the same way on Linux and on Windows). Each
        // segment is snake_cased on its own, then Path.Combine joins them with the platform's
        // own separator: snake-casing the whole string first would let '/' or '\' survive
        // untouched inside the result, since WordSplitter does not treat either as a boundary.
        var segments = directoryType
                       .Split(PathSegmentSeparators, StringSplitOptions.RemoveEmptyEntries)
                       .Select(segment => segment.ToSnakeCase())
                       .ToArray();

        var path = Path.Combine([Root, .. segments]);

        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        return path;
    }

    public override string ToString()
    {
        return Root;
    }

    private void Init()
    {
        if (!Directory.Exists(Root))
        {
            Directory.CreateDirectory(Root);
        }

        var directoryTypes = _directories.ToList();

        foreach (var path in directoryTypes.Select(GetPath)
                                           .Where(path => !Directory.Exists(path)))
        {
            Directory.CreateDirectory(path);
        }
    }
}
