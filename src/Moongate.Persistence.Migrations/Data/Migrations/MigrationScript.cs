using Moongate.Persistence.Migrations.Types.Migrations;

namespace Moongate.Persistence.Migrations.Data.Migrations;

/// <summary>A reviewed SQL migration identified by target, component and immutable filename.</summary>
public sealed record MigrationScript
{
    /// <summary>Gets the database target.</summary>
    public MigrationTarget Target { get; }

    /// <summary>Gets the stable component ID.</summary>
    public string Component { get; }

    /// <summary>Gets the immutable numbered filename.</summary>
    public string FileName { get; }

    /// <summary>Gets the component-local sequence.</summary>
    public int Sequence { get; }

    /// <summary>Gets normalized UTF-8 SQL text.</summary>
    public string Sql { get; }

    /// <summary>Gets the normalized SQL SHA-256 checksum.</summary>
    public string Checksum { get; }

    /// <summary>Gets the stable component-relative script identity.</summary>
    public string Name => $"{Component}/{FileName}";

    /// <summary>Creates an immutable migration description.</summary>
    public MigrationScript(
        MigrationTarget target,
        string component,
        string fileName,
        int sequence,
        string sql,
        string checksum
    )
    {
        Target = target;
        Component = component;
        FileName = fileName;
        Sequence = sequence;
        Sql = sql;
        Checksum = checksum;
    }
}
