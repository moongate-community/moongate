using System.Text;
using Moongate.UO.Data.Items;
using YamlDotNet.Serialization;

namespace Moongate.Server.Services.Items;

public static class ItemTemplateYamlSerializer
{
    private static readonly ISerializer Serializer = new SerializerBuilder()
                                                    .ConfigureDefaultValuesHandling(
                                                        DefaultValuesHandling.OmitNull |
                                                        DefaultValuesHandling.OmitDefaults |
                                                        DefaultValuesHandling.OmitEmptyCollections
                                                    )
                                                    .DisableAliases()
                                                    .Build();

    public static void SerializeToFile(string file, IReadOnlyList<ItemTemplate> templates)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
        File.WriteAllText(file, Serializer.Serialize(templates.ToArray()), Encoding.UTF8);
    }
}
