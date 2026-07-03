using System;
using Moongate.Ultima.Helpers;

using Moongate.Ultima.Types;

namespace Moongate.Ultima;

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
        Flags = (TileFlag)mulStruct.flags;
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
        Flags = (TileFlag)mulStruct.flags;
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
    /// <seealso cref="Animations" />
    /// </summary>
    public short Animation { get; set; }

    /// <summary>
    /// Gets a bitfield representing the 32 individual flags of this item.
    /// <seealso cref="TileFlag" />
    /// </summary>
    public TileFlag Flags { get; set; }

    /// <summary>
    /// Whether or not this item is flagged as '<see cref="TileFlag.Background" />'.
    /// <seealso cref="TileFlag" />
    /// </summary>
    public bool Background
    {
        get { return (Flags & TileFlag.Background) != 0; }
    }

    /// <summary>
    /// Whether or not this item is flagged as '<see cref="TileFlag.Bridge" />'.
    /// <seealso cref="TileFlag" />
    /// </summary>
    public bool Bridge
    {
        get { return (Flags & TileFlag.Bridge) != 0; }
    }

    /// <summary>
    /// Whether or not this item is flagged as '<see cref="TileFlag.Impassable" />'.
    /// <seealso cref="TileFlag" />
    /// </summary>
    public bool Impassable
    {
        get { return (Flags & TileFlag.Impassable) != 0; }
    }

    /// <summary>
    /// Whether or not this item is flagged as '<see cref="TileFlag.Surface" />'.
    /// <seealso cref="TileFlag" />
    /// </summary>
    public bool Surface
    {
        get { return (Flags & TileFlag.Surface) != 0; }
    }

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
    /// Gets the 'calculated height' of this item. For <see cref="Bridge">bridges</see>, this will be: <c>(<see cref="Height" /> / 2)</c>.
    /// </summary>
    public int CalcHeight
    {
        get
        {
            if ((Flags & TileFlag.Bridge) != 0)
            {
                return Height / 2;
            }

            return Height;
        }
    }

    /// <summary>
    /// Whether or not this item is wearable as '<see cref="TileFlag.Wearable" />'.
    /// <seealso cref="TileFlag" />
    /// </summary>
    public bool Wearable
    {
        get { return (Flags & TileFlag.Wearable) != 0; }
    }

    public void ReadData(string[] split)
    {
        int i = 1;
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
            Flags |= TileFlag.Background;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.Weapon;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.Transparent;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.Translucent;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.Wall;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.Damaging;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.Impassable;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.Wet;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.Unknown1;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.Surface;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.Bridge;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.Generic;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.Window;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.NoShoot;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.ArticleA;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.ArticleAn;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.ArticleThe;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.Foliage;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.PartialHue;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.NoHouse;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.Map;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.Container;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.Wearable;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.LightSource;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.Animation;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.HoverOver;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.NoDiagonal;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.Armor;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.Roof;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.Door;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.StairBack;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.StairRight;
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
            Flags |= TileFlag.AlphaBlend;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.UseNewArt;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.ArtUsed;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.Unused8;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.NoShadow;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.PixelBleed;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.PlayAnimOnce;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.MultiMovable;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.Unused10;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.Unused11;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.Unused12;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.Unused13;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.Unused14;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.Unused15;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.Unused16;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.Unused17;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.Unused18;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.Unused19;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.Unused20;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.Unused21;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.Unused22;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.Unused23;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.Unused24;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.Unused25;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.Unused26;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.Unused27;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.Unused28;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.Unused29;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.Unused30;
        }

        temp = Convert.ToByte(split[i++]);
        if (temp != 0)
        {
            Flags |= TileFlag.Unused31;
        }

        temp = Convert.ToByte(split[i]);
        if (temp != 0)
        {
            Flags |= TileFlag.Unused32;
        }
    }
}
