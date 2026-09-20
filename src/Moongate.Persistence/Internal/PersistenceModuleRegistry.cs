using System.Text.RegularExpressions;
using Moongate.Core.Interfaces.Entities;
using Moongate.Persistence.Data.Internal;
using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Types.Persistence;

namespace Moongate.Persistence.Internal;

internal sealed partial class PersistenceModuleRegistry
{
    private const int MaximumModuleIdLength = 128;
    private readonly List<IPersistenceModule> _modules = [];
    private readonly HashSet<Type> _entities = [];
    private bool _frozen;

    public void RegisterModule(IPersistenceModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        ThrowIfFrozen();
        _modules.Add(module);
    }

    public void RegisterEntity(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        ThrowIfFrozen();
        if (!_entities.Add(entityType))
        {
            throw new InvalidOperationException($"Persistence entity '{entityType.FullName}' is registered more than once.");
        }
    }

    public PersistenceModuleRegistrySnapshot ValidateAndFreeze(IReadOnlyCollection<PostgreSqlDatabase> databases)
    {
        ArgumentNullException.ThrowIfNull(databases);
        ThrowIfFrozen();
        _frozen = true;

        var orderedModules = _modules
            .OrderBy(module => module.DatabaseTarget)
            .ToArray();
        ValidateModuleDeclarations(orderedModules);
        var owners = ValidateOwnership(orderedModules);
        var databaseByTarget = databases.ToDictionary(database => database.Target);
        foreach (var targetGroup in orderedModules.GroupBy(module => module.DatabaseTarget))
        {
            if (!databaseByTarget.TryGetValue(targetGroup.Key, out var database))
            {
                throw new InvalidOperationException($"Persistence target '{targetGroup.Key}' is required by a module but is not configured.");
            }

            ValidateFinalMappings(database, targetGroup, owners);
        }

        var registrations = orderedModules
            .Select(module => new PersistenceModuleRegistration(
                module,
                module.EntityTypes.OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray()))
            .ToArray();

        return new PersistenceModuleRegistrySnapshot(registrations, owners);
    }

    public IReadOnlyList<PersistenceDatabaseTarget> GetDatabaseTargets()
    {
        ThrowIfFrozen();
        return _modules
            .Select(module => module.DatabaseTarget)
            .Distinct()
            .Order()
            .ToArray();
    }

    private static void ValidateModuleDeclarations(IReadOnlyList<IPersistenceModule> modules)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var schemas = new HashSet<(PersistenceDatabaseTarget Target, string Schema)>();
        foreach (var module in modules)
        {
            if (string.IsNullOrWhiteSpace(module.Id) || module.Id.Length > MaximumModuleIdLength ||
                !ModuleIdPattern().IsMatch(module.Id))
            {
                throw new InvalidOperationException(
                    $"Persistence module id '{module.Id}' must contain lowercase ASCII segments separated by '.', '_' or '-'.");
            }

            if (!ids.Add(module.Id))
            {
                throw new InvalidOperationException($"Persistence module id '{module.Id}' is duplicated.");
            }

            if (string.IsNullOrWhiteSpace(module.Schema) || !SchemaPattern().IsMatch(module.Schema))
            {
                throw new InvalidOperationException(
                    $"Persistence module '{module.Id}' schema '{module.Schema}' must be 1..63 lowercase snake_case characters and start with a letter.");
            }

            if (!schemas.Add((module.DatabaseTarget, module.Schema)))
            {
                throw new InvalidOperationException(
                    $"Persistence schema '{module.Schema}' is duplicated for target '{module.DatabaseTarget}'.");
            }

            if (!Enum.IsDefined(module.DatabaseTarget))
            {
                throw new InvalidOperationException(
                    $"Persistence module '{module.Id}' uses unsupported database target '{module.DatabaseTarget}'.");
            }

            if (module.EntityTypes is null || module.EntityTypes.Count == 0)
            {
                throw new InvalidOperationException($"Persistence module '{module.Id}' must declare at least one entity type.");
            }

            var declared = new HashSet<Type>();
            foreach (var entityType in module.EntityTypes)
            {
                if (entityType is null || !entityType.IsClass || entityType.IsAbstract || entityType.ContainsGenericParameters ||
                    !typeof(IMoongateEntity).IsAssignableFrom(entityType))
                {
                    throw new InvalidOperationException(
                        $"Persistence module '{module.Id}' owned type '{entityType?.FullName ?? "<null>"}' must be a concrete, closed class implementing {nameof(IMoongateEntity)}.");
                }

                if (!declared.Add(entityType))
                {
                    throw new InvalidOperationException(
                        $"Persistence module '{module.Id}' declares entity '{entityType.FullName}' more than once.");
                }
            }
        }
    }

    private Dictionary<Type, IPersistenceModule> ValidateOwnership(IReadOnlyList<IPersistenceModule> modules)
    {
        var owners = new Dictionary<Type, IPersistenceModule>();
        foreach (var module in modules)
        {
            foreach (var entityType in module.EntityTypes)
            {
                if (!_entities.Contains(entityType))
                {
                    throw new InvalidOperationException(
                        $"Persistence module '{module.Id}' owns entity '{entityType.FullName}', but that entity was not registered.");
                }

                if (!owners.TryAdd(entityType, module))
                {
                    throw new InvalidOperationException(
                        $"Persistence entity '{entityType.FullName}' is owned by both modules '{owners[entityType].Id}' and '{module.Id}'.");
                }
            }
        }

        foreach (var entityType in _entities.OrderBy(type => type.FullName, StringComparer.Ordinal))
        {
            if (!owners.ContainsKey(entityType))
            {
                throw new InvalidOperationException($"Persistence entity '{entityType.FullName}' has no module owner.");
            }
        }

        return owners;
    }

    private static void ValidateFinalMappings(
        PostgreSqlDatabase database,
        IEnumerable<IPersistenceModule> modules,
        IReadOnlyDictionary<Type, IPersistenceModule> owners
    )
    {
        var tables = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (var module in modules)
        {
            foreach (var entityType in module.EntityTypes.OrderBy(type => type.FullName, StringComparer.Ordinal))
            {
                var table = database.Orm.CodeFirst.GetTableByEntity(entityType);
                ValidateEntityIdentity(module, entityType, table);
                var separator = table.DbName.IndexOf('.');
                var schema = separator < 0 ? "" : table.DbName[..separator];
                var tableName = separator < 0 ? table.DbName : table.DbName[(separator + 1)..];
                if (!string.Equals(schema, module.Schema, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Persistence module '{module.Id}' owns entity '{entityType.FullName}' in schema '{module.Schema}', " +
                        $"but the final shared FreeSql mapping resolves to schema '{schema}'.");
                }

                if (!SchemaPattern().IsMatch(tableName))
                {
                    throw new InvalidOperationException(
                        $"Persistence entity '{entityType.FullName}' table '{table.DbName}' must use a lowercase snake_case table name.");
                }

                var tableKey = $"{schema}.{tableName}";
                if (!tables.TryAdd(tableKey, entityType))
                {
                    throw new InvalidOperationException(
                        $"Persistence table '{tableKey}' is duplicated by entities '{tables[tableKey].FullName}' and '{entityType.FullName}'.");
                }

                foreach (var column in table.ColumnsByCs.Values)
                {
                    var columnName = column.Attribute.Name;
                    if (!SchemaPattern().IsMatch(columnName))
                    {
                        throw new InvalidOperationException(
                            $"Persistence entity '{entityType.FullName}' column '{columnName}' must use a lowercase snake_case name.");
                    }
                }

                if (!ReferenceEquals(owners[entityType], module))
                {
                    throw new InvalidOperationException(
                        $"Persistence entity '{entityType.FullName}' final mapping is not owned by module '{module.Id}'.");
                }
            }
        }
    }

    private static void ValidateEntityIdentity(
        IPersistenceModule module,
        Type entityType,
        FreeSql.Internal.Model.TableInfo table
    )
    {
        if (!table.ColumnsByCs.TryGetValue(nameof(IMoongateEntity.Id), out var idColumn))
        {
            throw new InvalidOperationException(
                $"Persistence module '{module.Id}' entity '{entityType.FullName}' must map its public Serial Id property.");
        }

        if (idColumn.CsType != typeof(Moongate.Core.Primitives.Serial) ||
            table.Primarys.Length != 1 ||
            !ReferenceEquals(table.Primarys[0], idColumn))
        {
            throw new InvalidOperationException(
                $"Persistence module '{module.Id}' entity '{entityType.FullName}' must map Serial Id as its sole primary key.");
        }

        if (idColumn.Attribute.IsIdentity)
        {
            throw new InvalidOperationException(
                $"Persistence module '{module.Id}' entity '{entityType.FullName}' must keep Serial Id application-assigned, not database-generated.");
        }

        if (idColumn.Attribute.MapType != typeof(long))
        {
            throw new InvalidOperationException(
                $"Persistence module '{module.Id}' entity '{entityType.FullName}' must map Serial Id through CLR long storage.");
        }

        var databaseType = idColumn.Attribute.DbType.Trim();
        if (!string.Equals(databaseType, "int8", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(databaseType, "bigint", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Persistence module '{module.Id}' entity '{entityType.FullName}' must map Serial Id to PostgreSQL bigint/int8 storage, " +
                $"but the effective database type is '{databaseType}'.");
        }
    }

    private void ThrowIfFrozen()
    {
        if (_frozen)
        {
            throw new InvalidOperationException("Persistence registration is frozen.");
        }
    }

    [GeneratedRegex("^[a-z0-9]+(?:[._-][a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex ModuleIdPattern();

    [GeneratedRegex("^[a-z][a-z0-9_]{0,62}$", RegexOptions.CultureInvariant)]
    private static partial Regex SchemaPattern();
}
