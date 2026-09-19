using Moongate.Core.Utils;
using Moongate.Server.Core.Types.Hosting;
using Moongate.Server.Data.Config;
using Moongate.Server.Helpers;
using Moongate.Tests.TestSupport.Directories;
using Tomlyn;
using Tomlyn.Model;

namespace Moongate.Tests.Server.Helpers;

public sealed class ConfigHelperTests
{
    [Fact]
    public void Load_MissingFile_CreatesParentsAndPersistsModelDefaultsInSnakeCase()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "config", "moongate.toml");
        var defaults = new MoongateServerConfig();

        var config = ConfigHelper.Load(path);

        Assert.Equal(ServerMode.Standalone, config.Mode);
        Assert.Equivalent(defaults, config);
        var document = TomlSerializer.Deserialize<TomlTable>(File.ReadAllText(path))!;
        Assert.Equal("standalone", document["mode"]);
        var shard = Assert.IsType<TomlTable>(document["shard"]);
        var network = Assert.IsType<TomlTable>(document["network"]);
        var diagnostics = Assert.IsType<TomlTable>(document["diagnostics"]);
        Assert.Equal(defaults.Shard.ShardName, shard["shard_name"]);
        Assert.Equal((long)defaults.Network.GamePort, network["game_port"]);
        Assert.Equal(defaults.Network.ListenAddress, network["listen_address"]);
        Assert.Equal(defaults.Network.EnablePingServer, network["enable_ping_server"]);
        Assert.Equal(defaults.Diagnostics.Enabled, diagnostics["enabled"]);
        Assert.Equal((long)defaults.Diagnostics.IntervalSeconds, diagnostics["interval_seconds"]);
        Assert.Equal(defaults.Diagnostics.LogMetrics, diagnostics["log_metrics"]);
    }

    [Fact]
    public void Load_ExistingFile_ReadsCustomValuesWithoutRewritingIt()
    {
        using var directory = new TemporaryDirectory();
        const string toml = """
            # Keep this operator comment.
            [shard]
            shard_name = "Città di Luna"

            [network]
            game_port = 4000
            listen_address = "127.0.0.1"
            enable_ping_server = false
            """;
        var path = directory.CreateFile("moongate.toml", toml);

        var config = ConfigHelper.Load(path);

        Assert.Equal("Città di Luna", config.Shard.ShardName);
        Assert.Equal(4000, config.Network.GamePort);
        Assert.Equal("127.0.0.1", config.Network.ListenAddress);
        Assert.False(config.Network.EnablePingServer);
        Assert.Equal(toml, File.ReadAllText(path));
    }

    [Fact]
    public void Load_PartialFile_KeepsDefaultsForOmittedSettings()
    {
        using var directory = new TemporaryDirectory();
        const string toml = "[network]\ngame_port = 4001\n";
        var path = directory.CreateFile("moongate.toml", toml);
        var defaults = new MoongateServerConfig();

        var config = ConfigHelper.Load(path);

        Assert.Equivalent(defaults.Shard, config.Shard);
        Assert.Equal(ServerMode.Standalone, config.Mode);
        Assert.Equal(4001, config.Network.GamePort);
        Assert.Equal(defaults.Network.ListenAddress, config.Network.ListenAddress);
        Assert.Equal(defaults.Network.EnablePingServer, config.Network.EnablePingServer);
        Assert.True(config.Diagnostics.Enabled);
        Assert.Equal(TimeSpan.FromSeconds(5), config.Diagnostics.ToOptions().Interval);
        Assert.False(config.Diagnostics.LogMetrics);
        Assert.Equal(toml, File.ReadAllText(path));
    }

    [Theory, InlineData("[network"), InlineData("[network]\ngame_port = \"invalid\"\n")]
    public void Load_InvalidFile_PropagatesTomlErrorAndPreservesFile(string toml)
    {
        using var directory = new TemporaryDirectory();
        var path = directory.CreateFile("moongate.toml", toml);

        Assert.Throws<TomlException>(() => ConfigHelper.Load(path));

        Assert.Equal(toml, File.ReadAllText(path));
    }

    [Fact]
    public void Load_ParentIsAFile_PropagatesIoErrorWithoutReturningUnsavedDefaults()
    {
        using var directory = new TemporaryDirectory();
        var parent = directory.CreateFile("config", "Existing data");

        Assert.ThrowsAny<IOException>(() => ConfigHelper.Load(Path.Combine(parent, "moongate.toml")));

        Assert.Equal("Existing data", File.ReadAllText(parent));
    }

    [Theory,
     InlineData("login", true, false),
     InlineData("game", false, true),
     InlineData("standalone", true, true)]
    public void Load_ServerMode_EnablesExpectedFlags(string mode, bool login, bool game)
    {
        using var directory = new TemporaryDirectory();
        var toml = $"mode = \"{mode}\"\n";
        var path = directory.CreateFile("moongate.toml", toml);

        var config = ConfigHelper.Load(path);

        Assert.Equal(login, config.Mode.HasFlag(ServerMode.Login));
        Assert.Equal(game, config.Mode.HasFlag(ServerMode.Game));
        Assert.Equal(toml, File.ReadAllText(path));
    }

    [Theory,
     InlineData(ServerMode.Login, "login"),
     InlineData(ServerMode.Game, "game"),
     InlineData(ServerMode.Login | ServerMode.Game, "standalone")]
    public void Load_SerializedServerMode_RoundTripsReadableName(ServerMode mode, string name)
    {
        using var directory = new TemporaryDirectory();
        var toml = TomlUtils.Serialize(new MoongateServerConfig { Mode = mode });
        var document = TomlSerializer.Deserialize<TomlTable>(toml)!;
        var path = directory.CreateFile("moongate.toml", toml);

        Assert.Equal(name, document["mode"]);
        Assert.Equal(mode, ConfigHelper.Load(path).Mode);
    }

    [Theory,
     InlineData("\"none\""),
     InlineData("\"invalid\""),
     InlineData("\"\""),
     InlineData("0"),
     InlineData("4"),
     InlineData("true")]
    public void Load_InvalidServerMode_RejectsAndPreservesFile(string value)
    {
        using var directory = new TemporaryDirectory();
        var toml = $"mode = {value}\n";
        var path = directory.CreateFile("moongate.toml", toml);

        Assert.Throws<TomlException>(() => ConfigHelper.Load(path));

        Assert.Equal(toml, File.ReadAllText(path));
    }

    [Fact]
    public void Load_WorldSaveDefaults_WriteSnakeCaseAndMapToOptions()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "moongate.toml");
        var config = ConfigHelper.Load(path);
        var worldSave = Assert.IsType<TomlTable>(TomlSerializer.Deserialize<TomlTable>(File.ReadAllText(path))!["world_save"]);
        Assert.Equal(true, worldSave["enabled"]);
        Assert.Equal(300L, worldSave["interval_seconds"]);
        Assert.Equal(true, worldSave["backups_enabled"]);
        Assert.Equal(5L, worldSave["backup_retention_count"]);
        var options = config.WorldSave.ToOptions();
        Assert.True(options.Enabled);
        Assert.True(options.BackupsEnabled);
        Assert.Equal(TimeSpan.FromSeconds(300), options.Interval);
        Assert.Equal(5, options.BackupRetentionCount);
    }

    [Fact]
    public void Load_WorldSaveOverrides_MapsConfiguredValues()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.CreateFile("moongate.toml", """
            [world_save]
            enabled = false
            interval_seconds = 45
            backups_enabled = false
            backup_retention_count = 2
            """);
        var options = ConfigHelper.Load(path).WorldSave.ToOptions();
        Assert.False(options.Enabled);
        Assert.False(options.BackupsEnabled);
        Assert.Equal(TimeSpan.FromSeconds(45), options.Interval);
        Assert.Equal(2, options.BackupRetentionCount);
    }

    [Theory, InlineData(0, 5), InlineData(-1, 5), InlineData(300, 0), InlineData(300, -1)]
    public void Load_NonPositiveWorldSaveSettings_RejectsBeforeServerStartup(int interval, int retention)
    {
        using var directory = new TemporaryDirectory();
        var path = directory.CreateFile("moongate.toml",
            $"[world_save]\nenabled = false\ninterval_seconds = {interval}\nbackup_retention_count = {retention}\n");
        Assert.Throws<ArgumentOutOfRangeException>(() => ConfigHelper.Load(path));
    }

    [Fact]
    public void Validate_NullWorldSaveSection_RejectsBeforeServerStartup()
    {
        var config = new MoongateServerConfig { WorldSave = null! };
        Assert.Throws<InvalidOperationException>(config.Validate);
    }

}
