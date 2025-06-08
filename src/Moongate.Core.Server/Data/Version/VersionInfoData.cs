namespace Moongate.Core.Server.Data.Version;

public record VersionInfoData(
    string AppName,
    string CodeName,
    string Version,
    string GitHash,
    string Branch,
    string BuildDate
);
