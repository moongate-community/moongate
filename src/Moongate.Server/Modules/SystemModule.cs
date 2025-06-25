using Moongate.Core.Server.Attributes.Scripts;

namespace Moongate.Server.Modules;

[ScriptModule("system")]
public class SystemModule
{
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

    [ScriptFunction("Delay")]
    public void Delay(int milliseconds)
    {
        if (milliseconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(milliseconds), "Delay time must be non-negative.");
        }

        Task.Delay(milliseconds).Wait();
    }
}
