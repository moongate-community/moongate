using Moongate.UO.Data.Ids;

namespace Moongate.UO.Data.MegaCliloc;

/// <summary>
/// Represents a cliloc entry for a specific object
/// </summary>
public class MegaClilocEntry
{
    /// <summary>
    /// Serial of the object/creature
    /// </summary>
    public Serial Serial { get; set; }

    /// <summary>
    /// List of properties/clilocs for this object
    /// </summary>
    public List<MegaClilocProperty> Properties { get; set; } = new();

    public MegaClilocEntry()
    {
    }

    public MegaClilocEntry(Serial serial, string name)
    {
        Serial = serial;

        Properties.Add(new MegaClilocProperty(Serial.Value, name));
    }

    public void AddProperty(uint clilocId, string? text = null)
    {
        Properties.Add(new MegaClilocProperty(clilocId, text));
    }
}
