using Moongate.UO.Data.Mobiles.Templates;
using YamlDotNet.Serialization;

namespace Moongate.Server.Services.Mobiles;

/// <summary>Deserializes a YAML file into a flat list of <see cref="MobileTemplate" /> (keys are PascalCase).</summary>
internal static class MobileTemplateYamlDeserializer
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
                                                         .WithDuplicateKeyChecking()
                                                         .Build();

    public static MobileTemplate[] DeserializeFromFile(string file, string relativePath)
    {
        try
        {
            var yaml = File.ReadAllText(file);

            if (string.IsNullOrWhiteSpace(yaml))
            {
                return [];
            }

            return Deserializer.Deserialize<List<MobileTemplate>>(yaml)?.ToArray() ?? [];
        }
        catch (Exception exception)
        {
            throw new InvalidDataException(
                $"Failed to deserialize mobile template YAML '{relativePath}': {exception.Message}",
                exception
            );
        }
    }
}
