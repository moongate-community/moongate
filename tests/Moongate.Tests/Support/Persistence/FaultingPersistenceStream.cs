namespace Moongate.Tests.Support.Persistence;

internal sealed class FaultingPersistenceStream : Stream
{
    private readonly FaultingPersistenceFileSystem _fileSystem;

    public Stream Inner { get; }
    public override bool CanRead => Inner.CanRead;
    public override bool CanSeek => Inner.CanSeek;
    public override bool CanWrite => Inner.CanWrite;
    public override long Length => Inner.Length;
    public override long Position { get => Inner.Position; set => Inner.Position = value; }

    public FaultingPersistenceStream(Stream inner, FaultingPersistenceFileSystem fileSystem)
    {
        Inner = inner;
        _fileSystem = fileSystem;
    }

    public override void Flush() => Inner.Flush();
    public override int Read(byte[] buffer, int offset, int count) => Inner.Read(buffer, offset, count);
    public override int Read(Span<byte> buffer) => Inner.Read(buffer);
    public override long Seek(long offset, SeekOrigin origin) => Inner.Seek(offset, origin);
    public override void SetLength(long value) => Inner.SetLength(value);
    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (_fileSystem.FailWrite)
        {
            Inner.Write(buffer[..Math.Min(7, buffer.Length)]);
            Inner.Flush();
            throw new IOException("Injected partial write failure.");
        }
        Inner.Write(buffer);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Inner.Dispose();
        }

        base.Dispose(disposing);
    }
}
