using Moongate.UO.Data.Ids;

namespace Moongate.Server.Data.Internal.Cursors;

public record PendingCursorObject(long SessionId, Serial CursorId, Action<PendingCursorCallback> Callback, DateTime Expiration);
