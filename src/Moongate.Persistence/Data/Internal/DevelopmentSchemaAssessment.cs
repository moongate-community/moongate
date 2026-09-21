namespace Moongate.Persistence.Data.Internal;

internal sealed record DevelopmentSchemaAssessment(string Ddl, bool RequiresReview, bool HasExistingTables);
