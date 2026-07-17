namespace Moongate.Http.Plugin.Data.Mobiles;

/// <summary>One worn item to draw: the item template id and the already-resolved hue (0 = template's).</summary>
public sealed record MobileFigureEquipment(string ItemTemplateId, int Hue);
