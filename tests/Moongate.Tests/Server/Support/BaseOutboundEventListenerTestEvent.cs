using Moongate.Server.Data.Events;
using Moongate.Server.Data.Events.Base;

namespace Moongate.Tests.Server.Support;

public readonly record struct BaseOutboundEventListenerTestEvent(long Value) : IGameEvent;
