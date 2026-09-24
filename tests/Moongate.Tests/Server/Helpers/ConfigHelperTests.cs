using Moongate.Core.Utils;
using Moongate.Server.Core.Types.Accounts;
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
    public void Load_Defaults_PersistsRedisWithoutInternalApiSection()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "moongate.toml");
        var config = ConfigHelper.Load(path);
        var document = TomlSerializer.Deserialize<TomlTable>(File.ReadAllText(path))!;
        Assert.False(document.ContainsKey("api"));
        var redis = Assert.IsType<TomlTable>(document["redis"]);
        Assert.Equal(config.Redis.ConnectionString, redis["connection_string"]);
        Assert.Equal(config.Redis.HandoffSecret, redis["handoff_secret"]);
        var network = Assert.IsType<TomlTable>(document["network"]);
        Assert.Equal(2593L, network["login_port"]);
        Assert.Equal(2595L, network["game_port"]);
    }

    [Fact]
    public void Load_RealmDirectory_RoundTripsSnakeCaseAndAccountType()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.CreateFile(
            "moongate.toml",
            """
            mode = "game"
            [realm_directory]
            realm_id = "realm-a"
            name = "Realm A"
            server_index = 7
            advertised_address = "127.0.0.1"
            advertised_port = 2593
            minimum_account_type = "game_master"
            heartbeat_interval_seconds = 5
            lease_duration_seconds = 15
            max_realms = 128
            """
        );

        var config = TomlUtils.DeserializeFromFile<MoongateServerConfig>(path)!;

        Assert.Equal("realm-a", config.RealmDirectory.RealmId);
        Assert.Equal("Realm A", config.RealmDirectory.Name);
        Assert.Equal(7, config.RealmDirectory.ServerIndex);
        Assert.Equal("127.0.0.1", config.RealmDirectory.AdvertisedAddress);
        Assert.Equal(AccountType.GameMaster, config.RealmDirectory.MinimumAccountType);
    }

    [Fact]
    public void Load_ExistingFileWithoutRedis_PreservesFileAndKeepsRedisDefaults()
    {
        using var directory = new TemporaryDirectory();
        const string toml = "[network]\ngame_port = 4001\n";
        var path = directory.CreateFile("moongate.toml", toml);
        var config = ConfigHelper.Load(path);
        Assert.Equal("$MOONGATE_REDIS_CONNECTION_STRING", config.Redis.ConnectionString);
        Assert.Equal(2593, config.Network.LoginPort);
        Assert.Equal(4001, config.Network.GamePort);
        Assert.Equal(toml, File.ReadAllText(path));
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
                            login_port = 4002
                            game_port = 4000
                            listen_address = "127.0.0.1"
                            enable_ping_server = false
                            """;
        var path = directory.CreateFile("moongate.toml", toml);

        var config = ConfigHelper.Load(path);

        Assert.Equal("Città di Luna", config.Shard.ShardName);
        Assert.Equal(4002, config.Network.LoginPort);
        Assert.Equal(4000, config.Network.GamePort);
        Assert.Equal("127.0.0.1", config.Network.ListenAddress);
        Assert.False(config.Network.EnablePingServer);
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

    [Theory, InlineData(0, 1000), InlineData(150000, 0), InlineData(150000, 200000)]
    public void Load_InvalidScriptingBudget_RejectsBeforeServerStartup(int resumeBudget, int hookInterval)
    {
        using var directory = new TemporaryDirectory();
        var path = directory.CreateFile(
            "moongate.toml",
            $"[scripting]\nmax_instructions_per_resume = {resumeBudget}\nhook_interval = {hookInterval}\n"
        );
        Assert.Throws<ArgumentOutOfRangeException>(() => ConfigHelper.Load(path));
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
    public void Load_NonPositiveStringCap_RejectsBeforeServerStartup()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.CreateFile("moongate.toml", "[scripting]\nmax_string_length = 0\n");
        Assert.Throws<ArgumentOutOfRangeException>(() => ConfigHelper.Load(path));
    }

    [Theory, InlineData(0), InlineData(-1)]
    public void Load_NonPositiveWorldSaveSettings_RejectsBeforeServerStartup(int interval)
    {
        using var directory = new TemporaryDirectory();
        var path = directory.CreateFile(
            "moongate.toml",
            $"[world_save]\nenabled = false\ninterval_seconds = {interval}\n"
        );
        Assert.Throws<ArgumentOutOfRangeException>(() => ConfigHelper.Load(path));
    }

    [Fact]
    public void Load_ParentIsAFile_PropagatesIoErrorWithoutReturningUnsavedDefaults()
    {
        using var directory = new TemporaryDirectory();
        var parent = directory.CreateFile("config", "Existing data");

        Assert.ThrowsAny<IOException>(() => ConfigHelper.Load(Path.Combine(parent, "moongate.toml")));

        Assert.Equal("Existing data", File.ReadAllText(parent));
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

    [Fact]
    public void Load_ScriptingDefaults_WriteSnakeCaseAndMapToOptions()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "moongate.toml");
        var config = ConfigHelper.Load(path);
        var scripting =
            Assert.IsType<TomlTable>(TomlSerializer.Deserialize<TomlTable>(File.ReadAllText(path))!["scripting"]);
        Assert.Equal("init.lua", scripting["bootstrap_file"]);
        Assert.Equal(150_000L, scripting["max_instructions_per_resume"]);
        Assert.Equal(10_000_000L, scripting["max_instructions_per_chunk"]);
        Assert.Equal(1_000L, scripting["hook_interval"]);
        Assert.Equal(true, scripting["write_definitions"]);
        Assert.Equal(16_777_216L, scripting["max_string_length"]);
        var options = config.Scripting.ToOptions(directory.Path);
        Assert.Equal("init.lua", options.BootstrapFile);
        Assert.Equal(150_000, options.MaxInstructionsPerResume);
        Assert.Equal(10_000_000, options.MaxInstructionsPerChunk);
        Assert.Equal(1_000, options.HookInterval);
        Assert.True(options.WriteDefinitions);
        Assert.Equal(16_777_216, options.MaxStringLength);
    }

    [Fact]
    public void Load_ScriptingOverrides_MapsConfiguredValues()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.CreateFile(
            "moongate.toml",
            """
            [scripting]
            bootstrap_file = "boot.lua"
            max_instructions_per_resume = 20000
            max_instructions_per_chunk = 500000
            hook_interval = 500
            write_definitions = false
            max_string_length = 1024
            """
        );
        var options = ConfigHelper.Load(path).Scripting.ToOptions(directory.Path);
        Assert.Equal("boot.lua", options.BootstrapFile);
        Assert.Equal(20_000, options.MaxInstructionsPerResume);
        Assert.Equal(500_000, options.MaxInstructionsPerChunk);
        Assert.Equal(500, options.HookInterval);
        Assert.False(options.WriteDefinitions);
        Assert.Equal(1024, options.MaxStringLength);
    }

    [Theory,
     InlineData(ServerMode.Login, "login"),
     InlineData(ServerMode.Game, "game"),
     InlineData(ServerMode.Login | ServerMode.Game, "standalone")]
    public void Deserialize_SerializedServerMode_RoundTripsReadableName(ServerMode mode, string name)
    {
        using var directory = new TemporaryDirectory();
        var toml = TomlUtils.Serialize(new MoongateServerConfig { Mode = mode });
        var document = TomlSerializer.Deserialize<TomlTable>(toml)!;
        var path = directory.CreateFile("moongate.toml", toml);

        Assert.Equal(name, document["mode"]);
        Assert.Equal(mode, TomlUtils.DeserializeFromFile<MoongateServerConfig>(path)!.Mode);
    }

    [Theory,
     InlineData("login", true, false),
     InlineData("game", false, true),
     InlineData("standalone", true, true)]
    public void Deserialize_ServerMode_EnablesExpectedFlags(string mode, bool login, bool game)
    {
        using var directory = new TemporaryDirectory();
        var toml = $"mode = \"{mode}\"\n";
        var path = directory.CreateFile("moongate.toml", toml);

        var config = TomlUtils.DeserializeFromFile<MoongateServerConfig>(path)!;

        Assert.Equal(login, config.Mode.HasFlag(ServerMode.Login));
        Assert.Equal(game, config.Mode.HasFlag(ServerMode.Game));
        Assert.Equal(toml, File.ReadAllText(path));
    }

    [Fact]
    public void Load_WorldSaveDefaults_WriteSnakeCaseAndMapToOptions()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "moongate.toml");
        var config = ConfigHelper.Load(path);
        var worldSave =
            Assert.IsType<TomlTable>(TomlSerializer.Deserialize<TomlTable>(File.ReadAllText(path))!["world_save"]);
        Assert.Equal(true, worldSave["enabled"]);
        Assert.Equal(300L, worldSave["interval_seconds"]);
        var options = config.WorldSave.ToOptions();
        Assert.True(options.Enabled);
        Assert.Equal(TimeSpan.FromSeconds(300), options.Interval);
    }

    [Fact]
    public void Load_WorldSaveOverrides_MapsConfiguredValues()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.CreateFile(
            "moongate.toml",
            """
            [world_save]
            enabled = false
            interval_seconds = 45
            """
        );
        var options = ConfigHelper.Load(path).WorldSave.ToOptions();
        Assert.False(options.Enabled);
        Assert.Equal(TimeSpan.FromSeconds(45), options.Interval);
    }

    [Fact]
    public void Validate_BlankScriptingBootstrapFile_RejectsBeforeServerStartup()
    {
        var config = new MoongateServerConfig { Scripting = { BootstrapFile = " " } };
        Assert.Throws<InvalidOperationException>(config.Validate);
    }

    [Fact]
    public void Validate_NullWorldSaveSection_RejectsBeforeServerStartup()
    {
        var config = new MoongateServerConfig { WorldSave = null! };
        Assert.Throws<InvalidOperationException>(config.Validate);
    }
}
