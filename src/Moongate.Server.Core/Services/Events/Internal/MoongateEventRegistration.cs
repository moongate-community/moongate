namespace Moongate.Server.Core.Services.Events.Internal;

internal sealed class MoongateEventRegistration
{
    public Delegate Handler { get; }

    public MoongateEventRegistration(Delegate handler)
    {
        Handler = handler;
    }
}
