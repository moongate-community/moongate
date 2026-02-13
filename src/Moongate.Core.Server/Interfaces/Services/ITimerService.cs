using Moongate.Core.Server.Interfaces.Services.Base;

namespace Moongate.Core.Server.Interfaces.Services;

public interface ITimerService : IMoongateAutostartService
{
    string RegisterTimer(string name, double intervalInMs, Action callback, double delayInMs = 0, bool repeat = false);

    void UnregisterAllTimers();

    void UnregisterTimer(string timerId);
}
