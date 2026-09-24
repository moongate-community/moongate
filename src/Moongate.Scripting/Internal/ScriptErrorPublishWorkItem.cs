using Moongate.Scripting.Data.Events;
using Moongate.Scripting.Data.Scripts;
using Moongate.Server.Core.Interfaces.GameLoop;
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Scripting.Internal;

/// <summary>Publishes a ScriptErrorEvent from the loop thread, outside any coroutine resume.</summary>
internal sealed class ScriptErrorPublishWorkItem : IGameLoopWorkItem
{
    private readonly IEventBusService _eventBus;
    private readonly ScriptErrorInfo _error;

    public ScriptErrorPublishWorkItem(IEventBusService eventBus, ScriptErrorInfo error)
    {
        _eventBus = eventBus;
        _error = error;
    }

    public void Execute()
        => _ = _eventBus.PublishAsync(new ScriptErrorEvent(_error));
}
