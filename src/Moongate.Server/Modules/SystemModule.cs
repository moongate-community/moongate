using Moongate.Core.Server.Attributes.Scripts;
using Moongate.Core.Server.Interfaces.Services;

namespace Moongate.Server.Modules;

[ScriptModule("system")]
public class SystemModule
{

    private readonly IEventLoopService _eventLoopService;

    public SystemModule(IEventLoopService eventLoopService)
    {
        _eventLoopService = eventLoopService;
    }

    [ScriptFunction("Get server time")]
    public string GetServerTime()
    {
        return DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
    }

    [ScriptFunction("Get server uptime")]
    public string GetServerUptime()
    {
        var uptime = DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime();
        return $"{uptime.Days} days, {uptime.Hours} hours, {uptime.Minutes} minutes, {uptime.Seconds} seconds";
    }

    [ScriptFunction("Delay via event loop")]
    public void Delay(int milliseconds)
    {
        if (milliseconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(milliseconds), "Delay time must be non-negative.");
        }


        var exit = false;

        while (exit == false)
        {
            Task.Delay(10).Wait();
            milliseconds -= 10;

            if (milliseconds <= 0)
            {
                exit = true;
            }
        }
    }

    [ScriptFunction("Test class add")]
    public void TestClassAdd(object classz)
    {

    }


}
