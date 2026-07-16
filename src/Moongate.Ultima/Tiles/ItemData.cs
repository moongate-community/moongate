using Moongate.Ultima.Graphics;
using Moongate.Ultima.Helpers;
using Moongate.Ultima.Types;

namespace Moongate.Ultima.Tiles;

/// <summary>
/// Represents item tile data.
/// <seealso cref="TileData" />
/// <seealso cref="LandData" />
/// </summary>
public struct ItemData
{
    public unsafe ItemData(NewItemTileDataMul mulStruct)
    {
        Name = TileDataHelpers.ReadNameString(mulStruct.name);
        Flags = (TileFlagType)mulStruct.flags;
        Weight = mulStruct.weight;
        Quality = mulStruct.quality;
        Quantity = mulStruct.quantity;
        Value = mulStruct.value;
        Height = mulStruct.height;
        Animation = mulStruct.anim;
        Hue = mulStruct.hue;
        StackingOffset = mulStruct.stackingOffset;
        MiscData = mulStruct.miscData;
        Unk2 = mulStruct.unk2;
        Unk3 = mulStruct.unk3;
    }

    public unsafe ItemData(OldItemTileDataMul mulStruct)
    {
        Name = TileDataHelpers.ReadNameString(mulStruct.name);
        Flags = (TileFlagType)mulStruct.flags;
        Weight = mulStruct.weight;
        Quality = mulStruct.quality;
        Quantity = mulStruct.quantity;
        Value = mulStruct.value;
        Height = mulStruct.height;
        Animation = mulStruct.anim;
        Hue = mulStruct.hue;
        StackingOffset = mulStruct.stackingOffset;
        MiscData = mulStruct.miscData;
        Unk2 = mulStruct.unk2;
        Unk3 = mulStruct.unk3;
    }

    /// <summary>
    /// Gets the name of this item.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets the animation body index of this item.
    /// <seealso cref="Moongate.Ultima.Animation.Animations" />
    /// </summary>
    public short Animation { get; set; }

    /// <summary>
    /// Gets a bitfield representing the 32 individual flags of this item.
    /// <seealso cref="TileFlagType" />
    /// </summary>
    public TileFlagType Flags { get; set; }

    /// <summary>
    /// Whether or not this item is flagged as '<see cref="TileFlagType.Background" />'.
    /// <seealso cref="TileFlagType" />
    /// </summary>
    public bool Background => (Flags & TileFlagType.Background) != 0;

    /// <summary>
    /// Whether or not this item is flagged as '<see cref="TileFlagType.Bridge" />'.
    /// <seealso cref="TileFlagType" />
    /// </summary>
    public bool Bridge => (Flags & TileFlagType.Bridge) != 0;

    /// <summary>
    /// Whether or not this item is flagged as '<see cref="TileFlagType.Impassable" />'.
    /// <seealso cref="TileFlagType" />
    /// </summary>
    public bool Impassable => (Flags & TileFlagType.Impassable) != 0;

    /// <summary>
    /// Whether or not this item is flagged as '<see cref="TileFlagType.Surface" />'.
    /// <seealso cref="TileFlagType" />
    /// </summary>
    public bool Surface => (Flags & TileFlagType.Surface) != 0;

    /// <summary>
    /// Gets the weight of this item.
    /// </summary>
    public byte Weight { get; set; }

    /// <summary>
    /// Gets the 'quality' of this item. For wearable items, this will be the layer.
    /// </summary>
    public byte Quality { get; set; }

    /// <summary>
    /// Gets the 'quantity' of this item.
    /// </summary>
    public byte Quantity { get; set; }

    /// <summary>
    /// Gets the 'value' of this item.
    /// </summary>
    public byte Value { get; set; }

    /// <summary>
    /// Gets the Hue of this item.
    /// </summary>
    public byte Hue { get; set; }

    /// <summary>
    /// Gets the stackingOffset of this item. (If flag Generic)
    /// </summary>
    public byte StackingOffset { get; set; }

    /// <summary>
    /// Gets the height of this item.
    /// </summary>
    public byte Height { get; set; }

    /// <summary>
    /// Gets the MiscData of this item. (old UO Demo weapon template definition) (Unk1)
    /// </summary>
    public short MiscData { get; set; }

    /// <summary>
    /// Gets the unk2 of this item.
    /// </summary>
    public byte Unk2 { get; set; }

    /// <summary>
    /// Gets the unk3 of this item.
    /// </summary>
    public byte Unk3 { get; set; }

    /// <summary>
    /// Gets the 'calculated height' of this item. For <see cref="Bridge">bridges</see>, this will be:
    /// <c>(<see cref="Height" /> / 2)</c>.
    /// </summary>
    public int CalcHeight
    {
        get
        {
            if ((Flags & TileFlagType.Bridge) != 0)
            {
                return Height / 2;
            }

            return Height;
        }
    }

    /// <summary>
    /// Whether or not this item is wearable as '<see cref="TileFlagType.Wearable" />'.
    /// <seealso cref="TileFlagType" />
    /// </summary>
    public bool Wearable => (Flags & TileFlagType.Wearable) != 0;

    public void ReadData(string[] split)
    {
        var i = 1;
        Name = split[i++];
        Weight = Convert.ToByte(split[i++]);
        Quality = Convert.ToByte(split[i++]);
        Animation = (short)TileDataHelpers.ConvertStringToInt(split[i++]);
        Height = Convert.ToByte(split[i++]);
        Hue = Convert.ToByte(split[i++]);
        Quantity = Convert.ToByte(split[i++]);
        StackingOffset = Convert.ToByte(split[i++]);
        MiscData = Convert.ToInt16(split[i++]);
        Unk2 = Convert.ToByte(split[i++]);
        Unk3 = Convert.ToByte(split[i++]);

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
