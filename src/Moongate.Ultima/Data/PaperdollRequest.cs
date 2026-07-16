namespace Moongate.Ultima.Data;

/// <summary>Parametric paperdoll composition request; style value 0 means "none".</summary>
public sealed record PaperdollRequest
{
    public bool Female { get; init; }

    public ushort SkinHue { get; init; }

    public int HairStyle { get; init; }

    public ushort HairHue { get; init; }

    public int FacialHairStyle { get; init; }

    public ushort FacialHairHue { get; init; }

    public bool IncludeBackground { get; init; } = true;

    public IReadOnlyList<PaperdollEquipEntry> Equipment { get; init; } = [];
}
