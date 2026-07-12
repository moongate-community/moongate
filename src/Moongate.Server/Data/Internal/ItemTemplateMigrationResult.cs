namespace Moongate.Server.Data.Internal;

public sealed record ItemTemplateMigrationResult(
    int StandardCount,
    int CustomCount,
    int FileCount,
    string BackupPath
);
