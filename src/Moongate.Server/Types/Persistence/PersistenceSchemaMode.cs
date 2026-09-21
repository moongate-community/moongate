namespace Moongate.Server.Types.Persistence;

/// <summary>Selects normal hosting or an administrative schema operation.</summary>
public enum PersistenceSchemaMode
{
    None,
    Preview,
    Generate,
    Apply
}
