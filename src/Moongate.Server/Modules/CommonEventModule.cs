using Moongate.Core.Server.Attributes.Scripts;
using Moongate.Core.Server.Interfaces.Services;
using Moongate.UO.Data.Events.Characters;

namespace Moongate.Server.Modules;

[ScriptModule("events")]
public class CommonEventModule
{
    private readonly IEventBusService _eventBusService;
    private readonly IScriptEngineService _scriptEngineService;

    public CommonEventModule(IEventBusService eventBusService, IScriptEngineService scriptEngineService)
    {
        _eventBusService = eventBusService;
        _scriptEngineService = scriptEngineService;
    }

    [ScriptFunction("Register handler for character created event")]
    public void OnCharacterCreated(Func<CharacterCreatedEvent, Task> handler)
    {
        _scriptEngineService.AddCallback(
            nameof(OnCharacterCreated),
            args =>
            {
                if (args.Length == 1 && args[0] is CharacterCreatedEvent characterCreatedEvent)
                {
                    handler(characterCreatedEvent);
                }
            }
        );
    }

    [ScriptFunction("Register handler for CharacterInGameEvent")]
    public void OnCharacterInGame(Func<CharacterInGameEvent, Task> handler)
    {
        _eventBusService.Subscribe(handler);
    }
}
