namespace Moongate.Server.Ultima.Commands.Internal;

/// <summary>
///     Runs an action for every report, on the reporting thread, unlike <see cref="Progress{T}" /> which posts it.
/// </summary>
internal sealed class ActionProgress<T> : IProgress<T>
{
    private readonly Action<T> _action;

    public ActionProgress(Action<T> action)
    {
        _action = action;
    }

    public void Report(T value)
    {
        _action(value);
    }
}
