namespace Moongate.Abstractions.Types;

/// <summary>
/// Named constants for Moongate hosted-service startup priorities.
/// Lower values start earlier; services with the same priority start in registration order.
/// </summary>
public static class ServicePriority
{
    public const int Persistence = 110;
    public const int FileLoader = 120;
    public const int GameLoop = 130;
    public const int CharacterPositionPersistence = 130;
    public const int CommandSystem = 131;
    public const int ConsoleCommand = 132;
    public const int MetricsCollection = 135;
    public const int GameEventScriptBridge = 140;
    public const int Network = 150;
    public const int ScriptEngine = 150;
    public const int EventListener = 200;
    public const int HttpServer = 200;
}
