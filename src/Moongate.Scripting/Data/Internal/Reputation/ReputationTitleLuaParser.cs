using Moongate.UO.Data.Data.Reputation;
using MoonSharp.Interpreter;

namespace Moongate.Scripting.Data.Internal.Reputation;

internal static class ReputationTitleLuaParser
{
    private const string HonorificsKey = "honorifics";
    private const string MaleKey = "male";
    private const string FemaleKey = "female";
    private const string FameBucketsKey = "fame_buckets";
    private const string KarmaBucketsKey = "karma_buckets";
    private const string MaxFameKey = "max_fame";
    private const string MaxKarmaKey = "max_karma";
    private const string TitleKey = "title";

    public static bool TryParse(Table table, out ReputationTitleConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(table);

        configuration = ReputationTitleConfiguration.Default;

        if (!TryGetTable(table, HonorificsKey, out var honorificsTable) ||
            !TryGetString(honorificsTable, MaleKey, out var maleHonorific) ||
            !TryGetString(honorificsTable, FemaleKey, out var femaleHonorific) ||
            !TryGetArrayTable(table, FameBucketsKey, out var fameBucketTables))
        {
            return false;
        }

        var fameBuckets = new List<ReputationFameBucket>(fameBucketTables.Count);
        var previousMaxFame = int.MinValue;

        foreach (var fameBucketTable in fameBucketTables)
        {
            if (!TryGetInteger(fameBucketTable, MaxFameKey, out var maxFame) ||
                !TryGetArrayTable(fameBucketTable, KarmaBucketsKey, out var karmaBucketTables) ||
                karmaBucketTables.Count == 0 ||
                maxFame < previousMaxFame)
            {
                return false;
            }

            previousMaxFame = maxFame;

            var karmaBuckets = new List<ReputationKarmaBucket>(karmaBucketTables.Count);
            var previousMaxKarma = int.MinValue;

            foreach (var karmaBucketTable in karmaBucketTables)
            {
                if (!TryGetInteger(karmaBucketTable, MaxKarmaKey, out var maxKarma) ||
                    !TryGetString(karmaBucketTable, TitleKey, out var title) ||
                    maxKarma < previousMaxKarma)
                {
                    return false;
                }

                previousMaxKarma = maxKarma;
                karmaBuckets.Add(new(maxKarma, title));
            }

            fameBuckets.Add(new(maxFame, karmaBuckets));
        }

        configuration = ReputationTitleConfiguration.Create(
            new(maleHonorific, femaleHonorific),
            fameBuckets
        );

        return true;
    }

    private static bool TryGetArrayTable(Table table, string key, out List<Table> values)
    {
        values = [];

        if (!TryGetTable(table, key, out var arrayTable))
        {
            return false;
        }

        for (var index = 1;; index++)
        {
            var value = arrayTable.Get(index);

            if (value.Type == DataType.Nil)
            {
                break;
            }

            if (value.Type != DataType.Table || value.Table is null)
            {
                return false;
            }

            values.Add(value.Table);
        }

        return values.Count > 0;
    }

    private static bool TryGetInteger(Table table, string key, out int value)
    {
        value = 0;
        var dynValue = table.Get(key);

        if (dynValue.Type != DataType.Number)
        {
            return false;
        }

        value = (int)dynValue.Number;

        return true;
    }

    private static bool TryGetString(Table table, string key, out string value)
    {
        value = string.Empty;
        var dynValue = table.Get(key);

        if (dynValue.Type != DataType.String || dynValue.String is null)
        {
            return false;
        }

        value = dynValue.String;

        return true;
    }

    private static bool TryGetTable(Table table, string key, out Table value)
    {
        value = null!;
        var dynValue = table.Get(key);

        if (dynValue.Type != DataType.Table || dynValue.Table is null)
        {
            return false;
        }

        value = dynValue.Table;

        return true;
    }
}
