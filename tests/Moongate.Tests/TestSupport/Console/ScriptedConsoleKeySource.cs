using Moongate.Server.Interfaces.Internal.Console;

namespace Moongate.Tests.TestSupport.Console;

internal sealed class ScriptedConsoleKeySource : IConsoleKeySource
{
    private readonly Queue<ConsoleKeyInfo> _keys = new();

    public bool KeyAvailable
    {
        get
        {
            lock (_keys)
            {
                return _keys.Count > 0;
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

    public void EnqueueText(string text)
    {
        foreach (var character in text)
        {
            Enqueue(character);
        }
    }

    public void Enqueue(ConsoleKeyInfo key)
    {
        lock (_keys)
        {
            _keys.Enqueue(key);
        }
    }

    public ConsoleKeyInfo ReadKey()
    {
        lock (_keys)
        {
            return _keys.Dequeue();
        }
    }
}
