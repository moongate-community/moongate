namespace Moongate.Persistence.Migrations.Data.Migrations;

/// <summary>The immutable identity and checksum recorded when a SQL migration commits.</summary>
public sealed record AppliedMigration(string Name, string Checksum);
