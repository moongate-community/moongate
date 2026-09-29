using DryIoc;
using Moongate.Server.Core.Data.Config;
using Moongate.Server.Core.Extensions;
using Moongate.Tests.TestSupport.Config;
using Moongate.Tests.TestSupport.Directories;
using Tomlyn;
using Tomlyn.Model;

namespace Moongate.Tests.Server.Core.Extensions;

public sealed class ConfigContainerExtensionsTests
{
    [Fact]
    public void AddConfig_APresentSection_ReadsItAndRegistersIt()
    {
        using var directory = new TemporaryDirectory();
        var container = Container(directory, "[sample]\ngreeting = \"hi\"\n\n[sample.limits]\nmax_count = 3\n");

        var config = container.AddConfig<SampleSection>("sample");

        Assert.Equal(("hi", 3), (config.Greeting, config.Limits.MaxCount));
        Assert.Same(config, container.Resolve<SampleSection>());
    }

    [Fact]
    public void AddConfig_AMissingSection_UsesTheDefaultsAndAppendsThemToTheFile()
    {
        using var directory = new TemporaryDirectory();
        const string original = "# operator comment\n[network]\ngame_port = 4001\n";
        var container = Container(directory, original);

        var config = container.AddConfig<SampleSection>("sample");

        Assert.Equal(("hello", 7), (config.Greeting, config.Limits.MaxCount));
        var text = File.ReadAllText(directory.Path + "/moongate.toml");
        Assert.StartsWith(original, text);
        var sample = Assert.IsType<TomlTable>(TomlSerializer.Deserialize<TomlTable>(text)!["sample"]);
        Assert.Equal("hello", sample["greeting"]);
        Assert.Equal(7L, Assert.IsType<TomlTable>(sample["limits"])["max_count"]);
    }

    [Fact]
    public void AddConfig_ACrlfFile_AppendsWithCrlf()
    {
        using var directory = new TemporaryDirectory();
        const string original = "[network]\r\ngame_port = 4001\r\n";
        var container = Container(directory, original);

        container.AddConfig<SampleSection>("sample");

        var text = File.ReadAllText(directory.Path + "/moongate.toml");
        Assert.StartsWith(original, text);
        Assert.DoesNotContain("\n", text.Replace("\r\n", ""));
    }

    [Fact]
    public void AddConfig_ASectionInDottedForm_IsReadAndNotAppended()
    {
        using var directory = new TemporaryDirectory();
        const string original = "sample.greeting = \"dotted\"\n";
        var container = Container(directory, original);

        var config = container.AddConfig<SampleSection>("sample");

        Assert.Equal("dotted", config.Greeting);
        Assert.Equal(original, File.ReadAllText(directory.Path + "/moongate.toml"));
    }

    [Theory, InlineData("sample = 5\n"), InlineData("[[sample]]\ngreeting = \"x\"\n")]
    public void AddConfig_ASectionThatIsNotATable_ThrowsAndLeavesTheFile(string toml)
    {
        using var directory = new TemporaryDirectory();
        var container = Container(directory, toml);

        var exception = Assert.Throws<InvalidOperationException>(() => container.AddConfig<SampleSection>("sample"));

        Assert.Contains("[sample]", exception.Message);
        Assert.Equal(toml, File.ReadAllText(directory.Path + "/moongate.toml"));
    }

    [Fact]
    public void AddConfig_AHostSectionName_Throws()
    {
        using var directory = new TemporaryDirectory();
        var container = Container(directory, "", "network");

        var exception = Assert.Throws<InvalidOperationException>(() => container.AddConfig<SampleSection>("network"));

        Assert.Contains("[network]", exception.Message);
    }

    [Fact]
    public void AddConfig_TheSameNameTwice_Throws()
    {
        using var directory = new TemporaryDirectory();
        var container = Container(directory, "");
        container.AddConfig<SampleSection>("sample");

        Assert.Throws<InvalidOperationException>(() => container.AddConfig<OtherSection>("sample"));
    }

    [Fact]
    public void AddConfig_AnInvalidSection_Throws()
    {
        using var directory = new TemporaryDirectory();
        var container = Container(directory, "[sample.limits]\nmax_count = 0\n");

        Assert.Throws<InvalidOperationException>(() => container.AddConfig<SampleSection>("sample"));
    }

    [Fact]
    public void AddConfig_AFileThatCannotBeWritten_UsesTheDefaults()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.CreateFile("moongate.toml", "");
        var container = new Container();
        // The document points at a directory, so appending to it fails.
        container.RegisterInstance(new ServerConfigDocument(directory.Path, new(), []));

        var config = container.AddConfig<SampleSection>("sample");

        Assert.Equal("hello", config.Greeting);
        Assert.Equal("", File.ReadAllText(path));
    }

    private static Container Container(TemporaryDirectory directory, string toml, params string[] reserved)
    {
        var path = directory.CreateFile("moongate.toml", toml);
        var container = new Container();
        container.RegisterInstance(new ServerConfigDocument(path, TomlSerializer.Deserialize<TomlTable>(toml)!, reserved));

        return container;
    }
}
