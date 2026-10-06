using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Ultima.HuePicking;

/// <summary>
///     Answers every picker at once with <see cref="Result" /> and records the graphic each one showed.
/// </summary>
public sealed class StubHuePickerService : IHuePickerService
{

    /// <summary>
    ///     Answers the pickers kept open with <paramref name="hue" />.
    /// </summary>
    public void Answer(int? hue)
    {
        foreach (var (session, callback) in _open.ToArray())
        {
            callback(session, hue);
        }

        _open.Clear();
    }

    /// <summary>
    ///     The answers handed in, in order.
    /// </summary>
    public List<(int PickerId, int Hue)> Completed { get; } = [];

    public bool TryComplete(GameSession session, int pickerId, int hue)
    {
        Completed.Add((pickerId, hue));

        return false;
    }

    public void OnSessionClosed(GameSession session)
    {
    }

    private readonly List<(GameSession Session, Action<GameSession, int?> Callback)> _open = [];

    /// <summary>
    ///     The hue every picker is answered with; null for a picker that was replaced or whose player left.
    /// </summary>
    public int? Result { get; set; }

    public List<int> Graphics { get; } = [];

    /// <summary>
    ///     True keeps every picker open until <see cref="Answer" />, as a player that takes its time.
    /// </summary>
    public bool Defer { get; set; }

    public void Begin(GameSession session, int graphic, Action<GameSession, int?> callback)
    {
        Graphics.Add(graphic);

        if (Defer)
        {
            _open.Add((session, callback));

            return;
        }

        callback(session, Result);
    }
}
