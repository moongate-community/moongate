using FreeSql;

namespace Moongate.Persistence.Internal;

/// <summary>
///     Keeps every <see cref="DateTime" /> column in UTC. The columns are <c>timestamp</c> without a time zone, which
///     Npgsql reads back with <see cref="DateTimeKind.Unspecified" />; .NET then treats those values as local time.
///     Values are converted to UTC before they are written and marked as UTC when they are read.
/// </summary>
internal static class UtcDateTimeConvention
{
    public static void Apply(IFreeSql orm)
    {
        orm.Aop.AuditValue += (_, args) =>
        {
            if (args.Value is DateTime value && value.Kind == DateTimeKind.Local)
            {
                args.Value = value.ToUniversalTime();
            }
        };

        orm.Aop.AuditDataReader += (_, args) =>
        {
            if (args.Value is DateTime value && value.Kind != DateTimeKind.Utc)
            {
                args.Value = DateTime.SpecifyKind(value, DateTimeKind.Utc);
            }
        };
    }
}
