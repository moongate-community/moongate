using Moongate.Server.Interfaces.Internal.Console;

namespace Moongate.Tests.TestSupport.Console;

internal sealed class ScriptedConsoleKeySource : IConsoleKeySource
{
    private readonly Queue<ConsoleKeyInfo> _keys = new();

    /// <summary>
    ///     When set, the next <see cref="ReadKey" /> throws this exception instead of returning a key.
    /// </summary>
    public Exception? ThrowOnNextRead { get; set; }

    public bool KeyAvailable
    {
        get
        {
            lock (_keys)
            {
                return ThrowOnNextRead is not null || _keys.Count > 0;
            }
        }
    }

    public void Enqueue(char character)
    {
        Enqueue(new ConsoleKeyInfo(character, ConsoleKey.A, false, false, false));
    }

    public void Enqueue(ConsoleKey key)
    {
        Enqueue(new ConsoleKeyInfo('\0', key, false, false, false));
    }

    public void Enqueue(ConsoleKeyInfo key)
    {
        lock (_keys)
        {
            _keys.Enqueue(key);
        }
    }

    public void EnqueueText(string text)
    {
        foreach (var character in text)
        {
            Enqueue(character);
        }
    }

    public ConsoleKeyInfo ReadKey()
    {
        lock (_keys)
        {
            if (ThrowOnNextRead is { } exception)
            {
                ThrowOnNextRead = null;

                throw exception;
            }

            return _keys.Dequeue();
        }
    }
}
