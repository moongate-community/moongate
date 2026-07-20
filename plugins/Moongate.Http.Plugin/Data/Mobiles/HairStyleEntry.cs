namespace Moongate.Http.Plugin.Data.Mobiles;

/// <summary>A selectable hair or facial-hair style: item graphic id, display hex and name.</summary>
public sealed record HairStyleEntry(int Style, string Hex, string Name, bool Facial);
