using Moongate.Core.Server.Interfaces.Services.Base;

namespace Moongate.Core.Server.Interfaces.Services;

public interface ISchedulerSystemService : IMoongateService
{
    Task<bool> IsJobRegistered(string name);
    Task PauseJob(string name);
    Task RegisterJob(string name, Func<Task> task, TimeSpan interval);
    Task ResumeJob(string name);
    Task UnregisterJob(string name);
}
