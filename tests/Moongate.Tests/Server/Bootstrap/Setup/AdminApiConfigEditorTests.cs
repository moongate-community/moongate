using Moongate.Core.Utils;
using Moongate.Server.Bootstrap.Internal.Setup;
using Moongate.Server.Data.Config;

namespace Moongate.Tests.Server.Bootstrap.Setup;

public sealed class AdminApiConfigEditorTests
{
    [Theory, InlineData("\n"), InlineData("\r\n")]
    public void EnableGeneratedCertificate_ExistingTable_PreservesCommentsAndOtherTables(string newline)
    {
        var source = string.Join(
            newline,
            "# Configuration",
            "[admin_api] # Admin",
            "'enabled' = false # switch",
            "listen_address = '0.0.0.0'",
            "port = 2590",
            "[redis]",
            "connection_string = '${REDIS}'",
            ""
        );
        var result = AdminApiConfigEditor.EnableGeneratedCertificate(source);
        Assert.Contains("'enabled' = true # switch" + newline, result);
        Assert.Contains("[admin_api] # Admin" + newline, result);
        Assert.EndsWith("[redis]" + newline + "connection_string = '${REDIS}'" + newline, result);
        Assert.DoesNotContain("\n", result.Replace(newline, ""));
        var config = TomlUtils.Deserialize<MoongateServerConfig>(result)!;
        Assert.True(config.AdminApi.Enabled);
        Assert.Equal("certificates/admin.pfx", config.AdminApi.CertificatePath);
        Assert.Equal("0.0.0.0", config.AdminApi.ListenAddress);
    }

    [Theory, InlineData("# Empty"), InlineData("[admin_api]"), InlineData("[admin_api] # Last comment")]
    public void EnableGeneratedCertificate_MissingValues_AppendsValidTlsConfiguration(string source)
    {
        var result = AdminApiConfigEditor.EnableGeneratedCertificate(source);
        var config = TomlUtils.Deserialize<MoongateServerConfig>(result)!;
        Assert.True(config.AdminApi.Enabled);
        Assert.False(config.AdminApi.AllowInsecureLoopback);
        Assert.Equal("certificates/admin.pfx", config.AdminApi.CertificatePath);
    }

    [Theory, InlineData("[invalid"), InlineData("admin_api = { enabled = false }"), InlineData("admin_api.enabled = false")]
    public void EnableGeneratedCertificate_UnsupportedConfig_ReportsFailure(string source)
        => Assert.Throws<InvalidDataException>(() => AdminApiConfigEditor.EnableGeneratedCertificate(source));

    [Fact]
    public void EnableGeneratedCertificate_CustomIdentity_RefusesReplacement()
        => Assert.Throws<InvalidOperationException>(
            () => AdminApiConfigEditor.EnableGeneratedCertificate("[admin_api]\ncertificate_path = 'operator.pfx'\n")
        );
}
