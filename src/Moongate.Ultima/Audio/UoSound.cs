namespace Moongate.Ultima.Audio;

public sealed class UoSound
{
    public string Name;
    public int Id;
    public readonly byte[] Buffer;

    public UoSound(string name, int id, byte[] buff)
    {
        Name = name;
        Id = id;
        Buffer = buff;
    }
}
