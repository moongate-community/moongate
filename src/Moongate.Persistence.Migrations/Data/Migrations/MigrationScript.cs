using Moongate.Persistence.Migrations.Types.Migrations;
namespace Moongate.Persistence.Migrations.Data.Migrations;

/// <summary>A reviewed SQL migration identified by target, component and immutable filename.</summary>
public sealed record MigrationScript(MigrationTarget Target, string Component, string FileName, int Sequence, string Sql, string Checksum)
{
    /// <summary>Gets the stable component-relative script identity.</summary>
    public string Name => $"{Component}/{FileName}";
}
