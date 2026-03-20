using System.Text.Json.Serialization;
using Moongate.Server.Data.Plugins;

namespace Moongate.Server.Json;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true),
 JsonSerializable(typeof(MoongatePluginManifest)),
 JsonSerializable(typeof(MoongatePluginDependencyManifest)),
 JsonSerializable(typeof(List<string>)),
 JsonSerializable(typeof(IReadOnlyList<string>)),
 JsonSerializable(typeof(List<MoongatePluginDependencyManifest>)),
 JsonSerializable(typeof(IReadOnlyList<MoongatePluginDependencyManifest>))]

/// <summary>
/// Represents the JSON context for plugin manifest parsing.
/// </summary>
public partial class MoongatePluginJsonContext : JsonSerializerContext;
