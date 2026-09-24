using Moongate.Persistence.Internal;

namespace Moongate.Persistence.Tests.Modules;

public sealed class SchemaSqlReaderTests
{
    [Fact]
    public void TryRead_QuotedSemicolonsAndNestedComments_DoNotHideStatements()
    {
        Assert.True(
            SchemaSqlReader.TryRead(
                "/* one /* nested */ comment */ COMMENT ON TABLE \"auth\".\"accounts\" IS 'DROP; it''s text'; -- tail\rDROP TABLE \"auth\".\"accounts\";",
                out var statements
            )
        );
        Assert.Equal(2, statements.Count);
        Assert.Equal("COMMENT", statements[0].Tokens[0]);
        Assert.Equal("DROP", statements[1].Tokens[0]);
    }

    [Theory, InlineData("SELECT $body$ unsafe $body$;"), InlineData("SELECT 'unterminated"), InlineData("/* unterminated"),
     InlineData("SELECT E'back\\slash';")]
    public void TryRead_UnsupportedOrUnterminatedConstructs_FailsClosed(string sql)
        => Assert.False(SchemaSqlReader.TryRead(sql, out _));
}
