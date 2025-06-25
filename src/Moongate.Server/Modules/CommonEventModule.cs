using Moongate.Core.Server.Attributes.Scripts;
using Moongate.Core.Server.Interfaces.Services;
using Moongate.UO.Data.Events.Characters;

namespace Moongate.Server.Modules;

[ScriptModule("events")]
public class CommonEventModule
{
    private readonly IEventBusService _eventBusService;

    public CommonEventModule(IEventBusService eventBusService)
    {
        _eventBusService = eventBusService;
    }

    [ScriptFunction("Register handler for CharacterInGameEvent")]
    public void OnCharacterInGame(Func<CharacterInGameEvent, Task> handler)
    {
        _eventBusService.Subscribe(handler);
    }
}
