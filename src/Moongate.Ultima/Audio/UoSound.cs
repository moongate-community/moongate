namespace Moongate.Ultima.Audio;

public sealed class UoSound
{
    public readonly byte[] Buffer;
    public string Name;
    public int Id;

    public UoSound(string name, int id, byte[] buff)
    {
        Name = name;
        Id = id;
        Buffer = buff;
    }
}
