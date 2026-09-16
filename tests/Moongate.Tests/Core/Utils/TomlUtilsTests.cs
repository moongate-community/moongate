using System.Text.Json;
using Moongate.Core.Utils;
using Moongate.Tests.Support.Serialization.Data;
using Moongate.Tests.TestSupport.Directories;
using Tomlyn;
using Tomlyn.Model;

namespace Moongate.Tests.Core.Utils;

public sealed class TomlUtilsTests
{
    private const string SettingsToml = """
        ServerName = "Città di Luna"
        Enabled = false
        Tags = ["roleplay", "Italia"]

        [Network]
        Host = "127.0.0.1"
        Port = 2594
        """;

    [Fact]
    public void Deserialize_ConfigurationText_ReadsNestedValuesAndUnicode()
    {
        var settings = TomlUtils.Deserialize<TomlTestSettings>(SettingsToml);

        Assert.NotNull(settings);
        Assert.Equal("Città di Luna", settings.ServerName);
        Assert.False(settings.Enabled);
        Assert.Equal(["roleplay", "Italia"], settings.Tags);
        Assert.Equal("127.0.0.1", settings.Network.Host);
        Assert.Equal(2594, settings.Network.Port);
    }

    [Theory]
    [InlineData("")]
    [InlineData("# Empty configuration\n")]
    public void Deserialize_EmptyDocument_KeepsModelDefaults(string toml)
    {
        var settings = TomlUtils.Deserialize<TomlTestSettings>(toml);

        Assert.NotNull(settings);
        Assert.Equal("Moongate", settings.ServerName);
        Assert.Equal(2593, settings.Network.Port);
    }

    [Fact]
    public void Serialize_CustomNamingPolicy_DoesNotChangeSubsequentDefaults()
    {
        var settings = new TomlTestSettings { ServerName = "Luna" };
        var options = new TomlSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

        var customized = TomlUtils.Serialize(settings, options);
        var standard = TomlUtils.Serialize(settings);

        Assert.Contains("server_name = \"Luna\"", customized);
        Assert.Contains("[network]", customized);
        Assert.Contains("ServerName = \"Luna\"", standard);
        Assert.DoesNotContain("server_name", standard);
        Assert.Equal("Britannia", TomlUtils.Deserialize<TomlTestSettings>("server_name = \"Britannia\"", options)!.ServerName);
    }

    [Fact]
    public void Deserialize_MalformedDocument_PreservesTomlDiagnostics()
    {
        var exception = Assert.Throws<TomlException>(() => TomlUtils.Deserialize<TomlTestSettings>("ServerName = ["));

        Assert.NotNull(exception.Line);
    }

    [Fact]
    public void TextOperations_NullInput_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => TomlUtils.Serialize<TomlTestSettings>(null!));
        Assert.Throws<ArgumentNullException>(() => TomlUtils.Deserialize<TomlTestSettings>(null!));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeserializeFromFile_Utf8Document_AppliesCallerOptions(bool asynchronous)
    {
        using var directory = new TemporaryDirectory();
        var path = directory.CreateFile("settings.toml", "server_name = \"Città di Luna\"\n[network]\nport = 4000\n");
        var options = new TomlSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

        var settings = asynchronous
            ? await TomlUtils.DeserializeFromFileAsync<TomlTestSettings>(path, options)
            : TomlUtils.DeserializeFromFile<TomlTestSettings>(path, options);

        Assert.NotNull(settings);
        Assert.Equal("Città di Luna", settings.ServerName);
        Assert.Equal(4000, settings.Network.Port);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SerializeToFile_CreatesParentsAndOverwritesUtf8Document(bool asynchronous)
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "nested", "settings.toml");
        var settings = new TomlTestSettings { ServerName = "Città di Luna" };
        var options = new TomlSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

        if (asynchronous)
        {
            await TomlUtils.SerializeToFileAsync(settings, path, options);
        }
        else
        {
            TomlUtils.SerializeToFile(settings, path, options);
        }

        var table = TomlSerializer.Deserialize<TomlTable>(File.ReadAllText(path))!;
        Assert.Equal("Città di Luna", table["server_name"]);
        Assert.Equal(2593L, Assert.IsType<TomlTable>(table["network"])["port"]);

        File.AppendAllText(path, "\n# Previous trailing content\n");
        if (asynchronous)
        {
            await TomlUtils.SerializeToFileAsync(settings, path);
        }
        else
        {
            TomlUtils.SerializeToFile(settings, path);
        }

        Assert.DoesNotContain("Previous trailing content", File.ReadAllText(path));
        Assert.False(File.ReadAllBytes(path).AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeserializeFromFile_MissingFile_PreservesFileNotFoundException(bool asynchronous)
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "missing.toml");

        if (asynchronous)
        {
            await Assert.ThrowsAsync<FileNotFoundException>(() => TomlUtils.DeserializeFromFileAsync<TomlTestSettings>(path));
        }
        else
        {
            Assert.Throws<FileNotFoundException>(() => TomlUtils.DeserializeFromFile<TomlTestSettings>(path));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SerializeToFile_SerializationFails_PreservesExistingFile(bool asynchronous)
    {
        using var directory = new TemporaryDirectory();
        var path = directory.CreateFile("settings.toml", SettingsToml);

        if (asynchronous)
        {
            await Assert.ThrowsAsync<TomlException>(() => TomlUtils.SerializeToFileAsync("Not a TOML table", path));
        }
        else
        {
            Assert.Throws<TomlException>(() => TomlUtils.SerializeToFile("Not a TOML table", path));
        }

        Assert.Equal(SettingsToml, File.ReadAllText(path));
    }

    [Fact]
    public async Task FileOperations_PreCanceledToken_DoesNotTouchTheFilesystem()
    {
        using var directory = new TemporaryDirectory();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var parent = Path.Combine(directory.Path, "nested");
        var path = Path.Combine(parent, "settings.toml");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            TomlUtils.SerializeToFileAsync(new TomlTestSettings(), path, cancellationToken: cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            TomlUtils.DeserializeFromFileAsync<TomlTestSettings>(path, cancellationToken: cancellation.Token));

        Assert.False(Directory.Exists(parent));
    }
}
