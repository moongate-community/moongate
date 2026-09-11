using Moongate.Ultima.Graphics;
using Moongate.Ultima.Helpers;
using Moongate.Ultima.Types;

namespace Moongate.Ultima.Tiles;

/// <summary>
/// Represents land tile data.
/// <seealso cref="ItemData" />
/// <seealso cref="LandData" />
/// </summary>
public struct LandData
{
    public unsafe LandData(NewLandTileDataMul mulStruct)
    {
        TextureId = mulStruct.texID;
        Flags = (TileFlagType)mulStruct.flags;
        Name = TileDataHelpers.ReadNameString(mulStruct.name);
    }

    public unsafe LandData(OldLandTileDataMul mulStruct)
    {
        TextureId = mulStruct.texID;
        Flags = (TileFlagType)mulStruct.flags;
        Name = TileDataHelpers.ReadNameString(mulStruct.name);
    }

    /// <summary>
    /// Gets the name of this land tile.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets the texture id of this land tile.
    /// </summary>
    public ushort TextureId { get; set; }

    /// <summary>
    /// Gets a bitfield representing the 32 individual flags of this land tile.
    /// </summary>
    public TileFlagType Flags { get; set; }

    public void ReadData(string[] split)
    {
        var i = 1;
        Name = split[i++];
        TextureId = (ushort)TileDataHelpers.ConvertStringToInt(split[i++]);

        Flags = 0;
        int temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.Background;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.Weapon;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.Transparent;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.Translucent;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.Wall;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.Damaging;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.Impassable;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.Wet;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.Unknown1;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.Surface;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.Bridge;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.Generic;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.Window;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.NoShoot;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.ArticleA;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.ArticleAn;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.ArticleThe;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.Foliage;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.PartialHue;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.NoHouse;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.Map;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.Container;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.Wearable;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.LightSource;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.Animation;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.HoverOver;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.NoDiagonal;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.Armor;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.Roof;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.Door;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.StairBack;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.StairRight;
        }

        // Read new flags if file format support them
        if (!Art.IsUOAHS())
        {
            return;
        }

        // CSV may have been exported from an older client version that did not include extended HSA flags.
        // Any missing flags default to 0, which is already set above.
        if (i >= split.Length)
        {
            return;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.AlphaBlend;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.UseNewArt;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.ArtUsed;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.Unused8;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.NoShadow;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.PixelBleed;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.PlayAnimOnce;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.MultiMovable;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.Unused10;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.Unused11;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.Unused12;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.Unused13;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.Unused14;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.Unused15;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.Unused16;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.Unused17;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.Unused18;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.Unused19;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.Unused20;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.Unused21;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.Unused22;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.Unused23;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.Unused24;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.Unused25;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.Unused26;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.Unused27;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.Unused28;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.Unused29;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.Unused30;
        }

        temp = Convert.ToByte(split[i++]);

        if (temp != 0)
        {
            Flags |= TileFlagType.Unused31;
        }

        temp = Convert.ToByte(split[i]);

        if (temp != 0)
        {
            Flags |= TileFlagType.Unused32;
        }
    }
}
