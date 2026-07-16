using SquidStd.Core.Interfaces.Events;

namespace Moongate.Server.Data.Events;

/// <summary>Raised once the UO client files have been located and the data directory is ready.</summary>
public sealed record FilesLoadedEvent(string Directory, int FileCount) : IEvent;
