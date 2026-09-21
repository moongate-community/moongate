namespace Moongate.Persistence.Migrations.Data.Migrations;

/// <summary>The immutable identity and checksum recorded when a SQL migration commits.</summary>
public sealed record AppliedMigration
{
    /// <summary>Gets the component-relative script identity.</summary>
    public string Name { get; }

    /// <summary>Gets the applied SQL SHA-256 checksum.</summary>
    public string Checksum { get; }

    /// <summary>Creates an immutable history entry.</summary>
    public AppliedMigration(string name, string checksum)
    {
        Name = name;
        Checksum = checksum;
    }
}
