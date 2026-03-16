using Moongate.Abstractions.Interfaces.Services.Base;
using Moongate.Server.Data.Session;

namespace Moongate.Server.Interfaces.Services.Interaction;

public interface IHelpRequestService : IMoongateService
{
    Task OpenAsync(GameSession session, CancellationToken cancellationToken = default);
}
