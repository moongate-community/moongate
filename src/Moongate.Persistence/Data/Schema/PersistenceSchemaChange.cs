using Moongate.Persistence.Types.Persistence;

namespace Moongate.Persistence.Data.Schema;

/// <summary>Describes generated PostgreSQL DDL for one persistence module.</summary>
public sealed class PersistenceSchemaChange
{
    /// <summary>Gets the database target affected by the change.</summary>
    public PersistenceDatabaseTarget Target { get; }

    /// <summary>Gets the stable module identifier that owns the change.</summary>
    public string ModuleId { get; }

    /// <summary>Gets the FreeSql-generated PostgreSQL DDL.</summary>
    public string Ddl { get; }

    /// <summary>Creates a schema change description.</summary>
    /// <param name="target">The affected database target.</param>
    /// <param name="moduleId">The owning persistence module.</param>
    /// <param name="ddl">The generated PostgreSQL DDL.</param>
    public PersistenceSchemaChange(PersistenceDatabaseTarget target, string moduleId, string ddl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ddl);

        Target = target;
        ModuleId = moduleId;
        Ddl = ddl;
    }
}
