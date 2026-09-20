using Moongate.Core.Utils;
using Moongate.Server.Data.Config.Sections;
using Tomlyn;
using Tomlyn.Model;

namespace Moongate.Tests.Server.Data.Config.Sections;

public sealed class ApiPeerConfigTests
{
    [Theory, InlineData("[\"*\"]"), InlineData("[1, 100, 65535]"), InlineData("[]")]
    public void Serialize_AllowedOperations_RoundTripsWithoutChangingTheirMeaning(string operations)
    {
        var toml = $"allowed_operations = {operations}\npeer_id = \"realm\"\ncertificate_sha256 = \"{new string('A', 64)}\"\n";
        var peer = TomlUtils.Deserialize<ApiPeerConfig>(toml)!;
        peer.Validate();
        Assert.Equal("realm", peer.PeerId);
        var serialized = TomlUtils.Serialize(peer);
        var before = Assert.IsType<TomlArray>(TomlSerializer.Deserialize<TomlTable>(toml)!["allowed_operations"]);
        var after = Assert.IsType<TomlArray>(TomlSerializer.Deserialize<TomlTable>(serialized)!["allowed_operations"]);
        Assert.Equal(before.ToArray(), after.ToArray());
        TomlUtils.Deserialize<ApiPeerConfig>(serialized)!.Validate();
    }

    [Theory, InlineData("[\"*\", 100]"), InlineData("[100, \"*\"]"), InlineData("[\"*\", \"*\"]"),
     InlineData("[\"all\"]"), InlineData("[\"100\"]"), InlineData("[\"\"]"), InlineData("[0]"),
     InlineData("[-1]"), InlineData("[65536]"), InlineData("[1.5]"), InlineData("[true]"),
     InlineData("[[100]]"), InlineData("\"*\""), InlineData("100")]
    public void Validate_InvalidOperationPolicy_RejectsInsteadOfGrantingAll(string operations)
    {
        var toml = $"peer_id = \"realm\"\ncertificate_sha256 = \"{new string('A', 64)}\"\nallowed_operations = {operations}\n";
        var exception = Record.Exception(() => TomlUtils.Deserialize<ApiPeerConfig>(toml)!.Validate());
        Assert.True(exception is TomlException or InvalidOperationException, exception?.ToString() ?? "Invalid permissions were accepted.");
    }
}
