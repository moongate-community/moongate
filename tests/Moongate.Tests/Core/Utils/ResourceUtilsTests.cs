using System.Reflection;
using Moongate.Core.Utils;
using Moongate.Tests.TestSupport.Directories;

namespace Moongate.Tests.Core.Utils;

public sealed class ResourceUtilsTests
{
    private const string FullResourceName = "Moongate.Tests.TestSupport.Resources.Nested.sample.txt";
    private const string ResourcePath = "TestSupport/Resources/Nested/sample.txt";
    private static readonly Assembly _assembly = typeof(ResourceUtilsTests).Assembly;

    [Fact]
    public void ConvertResourceNameToPath_ValidName_PreservesFileExtension()
    {
        var result = ResourceUtils.ConvertResourceNameToPath(FullResourceName, "Moongate.Tests");

        Assert.Equal(Path.Combine("TestSupport", "Resources", "Nested", "sample.txt"), result);
    }

    [Theory, InlineData("Other.Root.file.txt", "Moongate.Tests"), InlineData("Moongate.Tests.NoExtension", "Moongate.Tests")]
    public void ConvertResourceNameToPath_InvalidName_ThrowsArgumentException(string resourceName, string prefix)
    {
        Assert.Throws<ArgumentException>(() => ResourceUtils.ConvertResourceNameToPath(resourceName, prefix));
    }

    [Fact]
    public void EmbeddedNameAndComponents_AreConvertedUsingDocumentedSeparators()
    {
        Assert.Equal(
            "Nested/sample/txt",
            ResourceUtils.EmbeddedNameToPath(FullResourceName, "Moongate.Tests.TestSupport.Resources")
        );
        Assert.Equal(
            Path.Combine("TestSupport", "Resources", "Nested"),
            ResourceUtils.GetDirectoryPathFromResourceName(FullResourceName, "Moongate.Tests")
        );
        Assert.Equal("sample.txt", ResourceUtils.GetFileNameFromResourceName(FullResourceName));
        Assert.Equal("sample.txt", ResourceUtils.GetFileNameFromResourcePath(FullResourceName));
    }

    [Theory, InlineData("file.txt"), InlineData("extensionless")]
    public void ResourceComponentHelpers_NamesWithoutDirectory_ReturnEmptyDirectoryAndOriginalFileName(string name)
    {
        Assert.Equal("", ResourceUtils.GetDirectoryPathFromResourceName(name));
        Assert.Equal(name, ResourceUtils.GetFileNameFromResourceName(name));
    }

    [Fact]
    public void EmbeddedResourceReaders_FullAndPartialNames_ReturnFixtureContent()
    {
        var expected = "Moongate embedded fixture.\n";

        Assert.Equal(expected, ResourceUtils.GetEmbeddedResourceString(_assembly, FullResourceName));
        Assert.Equal(expected, ResourceUtils.ReadEmbeddedResource(ResourcePath, _assembly));
        Assert.Equal(
            expected,
            System.Text.Encoding.UTF8.GetString(
                ResourceUtils.GetEmbeddedResourceByteArray(_assembly, ResourcePath).Span
            )
        );
        Assert.Equal(
            expected,
            System.Text.Encoding.UTF8.GetString(
                ResourceUtils.GetEmbeddedResourceContent(ResourcePath, _assembly)
            )
        );

        using var stream = ResourceUtils.GetEmbeddedResourceStream(_assembly, ResourcePath);
        using var reader = new StreamReader(stream);
        Assert.Equal(expected, reader.ReadToEnd());
    }

    [Theory, InlineData("Nested/sample.txt"), InlineData("Nested\\sample.txt")]
    public void GetEmbeddedResourceContent_PartialPath_ResolvesSuffixWithEitherSeparator(string path)
    {
        var bytes = ResourceUtils.GetEmbeddedResourceContent(path, _assembly);

        Assert.Equal("Moongate embedded fixture.\n", System.Text.Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public void EmbeddedResourceQueries_FilterByDirectoryAndReturnFileNameWithExtension()
    {
        Assert.Contains(FullResourceName, ResourceUtils.GetEmbeddedResourceNames(_assembly));
        Assert.Equal(
            [FullResourceName],
            ResourceUtils.GetEmbeddedResourceNames(_assembly, "TestSupport/Resources/Nested")
        );
        Assert.Equal(
            ["sample.txt"],
            ResourceUtils.GetEmbeddedResourceFileNames(_assembly, "TestSupport/Resources/Nested")
        );
    }

    [Theory,
     InlineData("sample.txt", "sample.txt"),
     InlineData("README", "README"),
     InlineData("Moongate.Tests.Resources.Nested.sample.txt", "sample.txt")]
    public void GetFileNameFromResourcePath_CommonResourceNames_ReturnFileNameWithExtension(
        string resourceName,
        string expected
    )
    {
        Assert.Equal(expected, ResourceUtils.GetFileNameFromResourcePath(resourceName));
    }

    [Fact]
    public void CopyEmbeddedToDirectory_CreatesTreeAndCopiesBytes()
    {
        using var destination = new TemporaryDirectory();
        var target = Path.Combine(destination.Path, "copy");

        ResourceUtils.CopyEmbeddedToDirectory(_assembly, target);

        Assert.Equal(
            "Moongate embedded fixture.\n",
            File.ReadAllText(Path.Combine(target, "TestSupport", "Resources", "Nested", "sample.txt"))
        );
    }

    [Fact]
    public void CopyEmbeddedToDirectory_DestinationIsExistingFile_ThrowsIOException()
    {
        using var destination = new TemporaryDirectory();
        var file = destination.CreateFile("occupied");

        Assert.Throws<IOException>(() => ResourceUtils.CopyEmbeddedToDirectory(_assembly, file));
    }

    [Fact]
    public void EmbeddedResourceReaders_MissingResource_ThrowFileNotFoundException()
    {
        Assert.Throws<FileNotFoundException>(() => ResourceUtils.GetEmbeddedResourceByteArray(_assembly, "missing.txt"));
        Assert.Throws<FileNotFoundException>(() => ResourceUtils.GetEmbeddedResourceContent("missing.txt", _assembly));
        Assert.Throws<FileNotFoundException>(() => ResourceUtils.GetEmbeddedResourceStream(_assembly, "missing.txt"));
        Assert.Throws<FileNotFoundException>(() => ResourceUtils.ReadEmbeddedResource("missing.txt", _assembly));
    }

    [Fact]
    public void EmbeddedResourceReaders_NullRequiredArguments_ThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ResourceUtils.GetEmbeddedResourceByteArray(null!, FullResourceName));
        Assert.Throws<ArgumentNullException>(() => ResourceUtils.GetEmbeddedResourceByteArray(_assembly, null!));
        Assert.Throws<ArgumentNullException>(() => ResourceUtils.GetEmbeddedResourceStream(null!, FullResourceName));
        Assert.Throws<ArgumentNullException>(() => ResourceUtils.GetEmbeddedResourceStream(_assembly, null!));
        Assert.Throws<ArgumentNullException>(() => ResourceUtils.GetDirectoryPathFromResourceName(null!));
        Assert.Throws<ArgumentNullException>(() => ResourceUtils.GetFileNameFromResourceName(null!));
    }
}
