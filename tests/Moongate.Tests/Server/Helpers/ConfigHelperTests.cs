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
    public void Load_ApiDefaults_PersistsDisabledListenerInSnakeCase()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "moongate.toml");
        var config = ConfigHelper.Load(path);
        Assert.False(config.Api.Enabled);
        Assert.Equal(2594, config.Api.Port);
        var document = TomlSerializer.Deserialize<TomlTable>(File.ReadAllText(path))!;
        var api = Assert.IsType<TomlTable>(document["api"]);
        Assert.Equal(false, api["enabled"]);
        Assert.Equal(false, api["auto_generate_certificate"]);
        Assert.Equal(new[] { "localhost" }, Assert.IsType<TomlArray>(api["certificate_dns_names"]).Cast<string>());
        Assert.Equal(new[] { "127.0.0.1", "::1" }, Assert.IsType<TomlArray>(api["certificate_ip_addresses"]).Cast<string>());
        Assert.Equal(2594L, api["port"]);
        Assert.Equal("0.0.0.0", api["listen_address"]);
        Assert.Equal("MOONGATE_API_CERTIFICATE_PASSWORD", api["certificate_password_environment_variable"]);
    }

    [Fact]
    public void Load_ExistingFileWithoutApi_PreservesFileAndDefaultsToDisabled()
    {
        using var directory = new TemporaryDirectory();
        const string toml = "[network]\ngame_port = 4001\n";
        var path = directory.CreateFile("moongate.toml", toml);
        var config = ConfigHelper.Load(path);
        Assert.False(config.Api.Enabled);
        Assert.Equal(2594, config.Api.Port);
        Assert.Equal(toml, File.ReadAllText(path));
    }

    [Fact]
    public void Load_ApiOverrides_ReadsEndpointTlsAndPeerPermissions()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.CreateFile(
            "moongate.toml",
            """
            [api]
            enabled = true
            listen_address = "::1"
            port = 4002
            auto_generate_certificate = true
            certificate_dns_names = ["realm.internal"]
            certificate_ip_addresses = ["10.0.0.12"]
            certificate_path = "certs/server.pfx"
            certificate_password_environment_variable = "TEST_API_PASSWORD"
            trusted_root_paths = ["certs/root.pem"]
            [[api.peers]]
            certificate_sha256 = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF"
            peer_id = "admin"
            allowed_operations = [100, 65535]
            """
        );
        var api = ConfigHelper.Load(path).Api;
        Assert.True(api.Enabled);
        Assert.Equal("::1", api.ListenAddress);
        Assert.Equal(4002, api.Port);
        Assert.True(api.AutoGenerateCertificate);
        Assert.Equal(["realm.internal"], api.CertificateDnsNames);
        Assert.Equal(["10.0.0.12"], api.CertificateIpAddresses);
        Assert.Equal("certs/server.pfx", api.CertificatePath);
        Assert.Equal("TEST_API_PASSWORD", api.CertificatePasswordEnvironmentVariable);
        Assert.Equal(["certs/root.pem"], api.TrustedRootPaths);
        var peer = Assert.Single(api.Peers);
        Assert.Equal("admin", peer.PeerId);
        Assert.Equal(new ushort[] { 100, 65535 }, peer.AllowedOperations.OperationIds);
    }

    [Fact]
    public void Load_EnabledApiWithoutTls_RejectsBeforeStartup()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.CreateFile("moongate.toml", "[api]\nenabled = true\n");
        Assert.Throws<InvalidOperationException>(() => ConfigHelper.Load(path));
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
    public void Validate_NullWorldSaveSection_RejectsBeforeServerStartup()
    {
        var config = new MoongateServerConfig { WorldSave = null! };
        Assert.Throws<InvalidOperationException>(config.Validate);
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

    [Fact]
    public void Load_NonPositiveStringCap_RejectsBeforeServerStartup()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.CreateFile("moongate.toml", "[scripting]\nmax_string_length = 0\n");
        Assert.Throws<ArgumentOutOfRangeException>(() => ConfigHelper.Load(path));
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

    [Fact]
    public void Validate_BlankScriptingBootstrapFile_RejectsBeforeServerStartup()
    {
        var config = new MoongateServerConfig { Scripting = { BootstrapFile = " " } };
        Assert.Throws<InvalidOperationException>(config.Validate);
    }
}
