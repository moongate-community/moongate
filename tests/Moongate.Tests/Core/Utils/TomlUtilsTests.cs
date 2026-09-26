using System.Text.Json;
using Moongate.Core.Utils;
using Moongate.Tests.Support.Serialization.Data;
using Moongate.Tests.Support.Serialization.Toml;
using Moongate.Tests.TestSupport.Directories;
using Tomlyn;
using Tomlyn.Model;

namespace Moongate.Tests.Core.Utils;

public sealed class TomlUtilsTests
{
    private const string SettingsToml = """
                                        server_name = "Città di Luna"
                                        enabled = false
                                        tags = ["roleplay", "Italia"]

                                        [network]
                                        host = "127.0.0.1"
                                        port = 2594
                                        """;

    [Theory, InlineData(false), InlineData(true)]
    public async Task DeserializeFromFile_MissingFile_PreservesFileNotFoundException(bool asynchronous)
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "missing.toml");

        if (asynchronous)
        {
            await Assert.ThrowsAsync<FileNotFoundException>(() => TomlUtils.DeserializeFromFileAsync<TomlTestSettings>(path)
            );
        }
        else
        {
            Assert.Throws<FileNotFoundException>(() => TomlUtils.DeserializeFromFile<TomlTestSettings>(path));
        }
    }

    [Theory, InlineData(false, false), InlineData(true, false), InlineData(false, true), InlineData(true, true)]
    public async Task DeserializeFromFile_Utf8Document_UsesDefaultOrCustomNaming(bool asynchronous, bool customNaming)
    {
        using var directory = new TemporaryDirectory();
        var key = customNaming ? "serverName" : "server_name";
        var path = directory.CreateFile("settings.toml", $"{key} = \"Città di Luna\"\n[network]\nport = 4000\n");
        var options = customNaming
            ? new TomlSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
            : null;

        var settings = asynchronous
            ? await TomlUtils.DeserializeFromFileAsync<TomlTestSettings>(path, options)
            : TomlUtils.DeserializeFromFile<TomlTestSettings>(path, options);

        Assert.NotNull(settings);
        Assert.Equal("Città di Luna", settings.ServerName);
        Assert.Equal(4000, settings.Network.Port);
    }

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

    [Theory, InlineData(""), InlineData("# Empty configuration\n")]
    public void Deserialize_EmptyDocument_KeepsModelDefaults(string toml)
    {
        var settings = TomlUtils.Deserialize<TomlTestSettings>(toml);

        Assert.NotNull(settings);
        Assert.Equal("Moongate", settings.ServerName);
        Assert.Equal(2593, settings.Network.Port);
    }

    [Fact]
    public void Deserialize_MalformedDocument_PreservesTomlDiagnostics()
    {
        var exception = Assert.Throws<TomlException>(() => TomlUtils.Deserialize<TomlTestSettings>("server_name = ["));

        Assert.NotNull(exception.Line);
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
            TomlUtils.SerializeToFileAsync(new TomlTestSettings(), path, cancellationToken: cancellation.Token)
        );
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            TomlUtils.DeserializeFromFileAsync<TomlTestSettings>(path, cancellationToken: cancellation.Token)
        );

        Assert.False(Directory.Exists(parent));
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task SerializeToFile_CreatesParentsAndOverwritesUtf8Document(bool asynchronous)
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "nested", "settings.toml");
        var settings = new TomlTestSettings { ServerName = "Città di Luna" };
        var options = new TomlSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        if (asynchronous)
        {
            await TomlUtils.SerializeToFileAsync(settings, path);
        }
        else
        {
            TomlUtils.SerializeToFile(settings, path);
        }

        var table = TomlSerializer.Deserialize<TomlTable>(File.ReadAllText(path))!;
        Assert.Equal("Città di Luna", table["server_name"]);
        Assert.Equal(2593L, Assert.IsType<TomlTable>(table["network"])["port"]);

        File.AppendAllText(path, "\n# Previous trailing content\n");

        if (asynchronous)
        {
            await TomlUtils.SerializeToFileAsync(settings, path, options);
        }
        else
        {
            TomlUtils.SerializeToFile(settings, path, options);
        }

        var overwritten = File.ReadAllText(path);
        Assert.DoesNotContain("Previous trailing content", overwritten);
        Assert.Contains("serverName = \"Città di Luna\"", overwritten);
        Assert.DoesNotContain("server_name", overwritten);
        Assert.False(File.ReadAllBytes(path).AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }));
    }

    [Theory, InlineData(false), InlineData(true)]
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
    public void Serialize_CustomNamingPolicy_DoesNotChangeSubsequentDefaults()
    {
        var settings = new TomlTestSettings { ServerName = "Luna" };
        var options = new TomlSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        var customized = TomlUtils.Serialize(settings, options);
        var standard = TomlUtils.Serialize(settings);

        Assert.Contains("serverName = \"Luna\"", customized);
        Assert.Contains("server_name = \"Luna\"", standard);
        Assert.Contains("[network]", standard);
        Assert.DoesNotContain("serverName", standard);
        Assert.Equal(
            "Britannia",
            TomlUtils.Deserialize<TomlTestSettings>("serverName = \"Britannia\"", options)!.ServerName
        );
    }

    [Fact]
    public void TextOperations_NullInput_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => TomlUtils.Serialize<TomlTestSettings>(null!));
        Assert.Throws<ArgumentNullException>(() => TomlUtils.Deserialize<TomlTestSettings>(null!));
    }

    [Fact]
    public void AddTomlConverter_MakesTheConverterAvailableWithoutExplicitOptions()
    {
        try
        {
            TomlUtils.AddTomlConverter(new RecordingTomlConverter());

            var value = TomlUtils.Serialize(new MarkerHolder());

            Assert.Equal("value = true", value.Trim());
        }
        finally
        {
            TomlUtils.RemoveTomlConverter<RecordingTomlConverter>();
        }
    }

    [Fact]
    public void AddTomlConverter_CalledTwiceWithTheSameType_RegistersOnce()
    {
        try
        {
            TomlUtils.AddTomlConverter(new RecordingTomlConverter());
            TomlUtils.AddTomlConverter(new RecordingTomlConverter());

            Assert.Single(TomlUtils.GetTomlConverters().OfType<RecordingTomlConverter>());
        }
        finally
        {
            TomlUtils.RemoveTomlConverter<RecordingTomlConverter>();
        }
    }

    [Fact]
    public void RemoveTomlConverter_RemovesEveryConverterOfThatType_AndReportsWhetherAnyWasRemoved()
    {
        TomlUtils.AddTomlConverter(new RecordingTomlConverter());

        var removed = TomlUtils.RemoveTomlConverter<RecordingTomlConverter>();
        var removedAgain = TomlUtils.RemoveTomlConverter<RecordingTomlConverter>();

        Assert.True(removed);
        Assert.False(removedAgain);
        Assert.Empty(TomlUtils.GetTomlConverters().OfType<RecordingTomlConverter>());
    }

    [Fact]
    public void ExplicitOptions_AreNeverExtendedWithRegisteredConverters()
    {
        try
        {
            TomlUtils.AddTomlConverter(new RecordingTomlConverter());
            var options = new TomlSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

            // Without the converter, Tomlyn falls back to walking Marker's (zero) public properties,
            // producing an empty nested table rather than the converter's "true" — proving the explicit
            // options object never picked up what AddTomlConverter registered globally.
            var value = TomlUtils.Serialize(new MarkerHolder(), options);

            Assert.DoesNotContain("true", value, StringComparison.Ordinal);
        }
        finally
        {
            TomlUtils.RemoveTomlConverter<RecordingTomlConverter>();
        }
    }

    [Fact]
    public void RemoveTomlConverter_WhileOthersRead_NeverHidesAnotherConverter()
    {
        TomlUtils.AddTomlConverter(new RegistryTomlConverter<StableRegistryMarker>());
        var missing = 0;
        using var stop = new CancellationTokenSource();

        var reader = Task.Run(() =>
            {
                while (!stop.IsCancellationRequested)
                {
                    if (!TomlUtils.GetTomlConverters().OfType<RegistryTomlConverter<StableRegistryMarker>>().Any())
                    {
                        Interlocked.Increment(ref missing);
                    }
                }
            }
        );

        for (var i = 0; i < 20000; i++)
        {
            TomlUtils.AddTomlConverter(new RecordingTomlConverter());
            TomlUtils.RemoveTomlConverter<RecordingTomlConverter>();
        }

        stop.Cancel();
        reader.Wait();

        Assert.Equal(0, missing);
    }

    [Fact]
    public void AddTomlConverter_SameTypeFromManyThreads_RegistersOnce()
    {
        try
        {
            for (var round = 0; round < 200; round++)
            {
                TomlUtils.RemoveTomlConverter<RegistryTomlConverter<ParallelRegistryMarker>>();
                Parallel.For(0, 16, _ => TomlUtils.AddTomlConverter(new RegistryTomlConverter<ParallelRegistryMarker>()));

                Assert.Single(TomlUtils.GetTomlConverters().OfType<RegistryTomlConverter<ParallelRegistryMarker>>());
            }
        }
        finally
        {
            TomlUtils.RemoveTomlConverter<RegistryTomlConverter<ParallelRegistryMarker>>();
        }
    }
}
