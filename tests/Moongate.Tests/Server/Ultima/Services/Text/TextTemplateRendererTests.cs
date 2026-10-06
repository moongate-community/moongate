using Moongate.Server.Ultima.Services.Text;
using Moongate.Server.Ultima.Types.Text;

namespace Moongate.Tests.Server.Ultima.Services.Text;

public sealed class TextTemplateRendererTests
{
    [Fact]
    public void Render_DocumentGrammar_ExpandsOnceAndPreservesLiteralDollars()
    {
        var values = new Dictionary<string, string>
        {
            ["player_name"] = "Pippo", ["contact_name"] = "$server_name", ["server_name"] = "Moongate"
        };

        Assert.Equal(
            "Pippo / Pippo / $ / $server_name",
            TextTemplateRenderer.Render(
                "$player_name / ${player_name} / $$ / $contact_name",
                values,
                TextTemplateSyntaxType.Document
            )
        );
        Assert.Equal(
            "Pippo_suffix",
            TextTemplateRenderer.Render("${player_name}_suffix", values, TextTemplateSyntaxType.Document)
        );
        Assert.Throws<KeyNotFoundException>(() => TextTemplateRenderer.Render(
                "$player_name_suffix",
                values,
                TextTemplateSyntaxType.Document
            )
        );
    }

    [Fact]
    public void Render_DollarsAndUnicode_AreLiteralOutsideTokens()
    {
        Assert.Equal(
            "è $ $player_name ${player_name}",
            TextTemplateRenderer.Render(
                "è $$ $$player_name $${player_name}",
                new Dictionary<string, string>(),
                TextTemplateSyntaxType.Document
            )
        );
    }

    [Fact]
    public async Task RenderAsync_MotdGrammar_PreservesBareTokensAndResolverBehavior()
    {
        var calls = 0;
        var result = await TextTemplateRenderer.RenderAsync(
            "$player_name $$ ${player_name} ${player_name}",
            async (_, token) =>
            {
                await Task.Delay(1, token);
                calls++;
                return "${server_name}";
            },
            TextTemplateSyntaxType.Motd,
            CancellationToken.None
        );

        Assert.Equal("$player_name $$ ${server_name} ${server_name}", result);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task RenderAsync_Cancellation_StopsExpansion()
    {
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await TextTemplateRenderer.RenderAsync(
                "${player_name}",
                (_, _) => ValueTask.FromResult("Pippo"),
                TextTemplateSyntaxType.Motd,
                canceled.Token
            )
        );
    }

    [Theory]
    [InlineData("player_name", true)]
    [InlineData("name2", true)]
    [InlineData("", false)]
    [InlineData("Player", false)]
    [InlineData("name-x", false)]
    public void IsValidName_OnlySnakeCaseNames_AreAccepted(string name, bool valid)
    {
        Assert.Equal(valid, TextTemplateTokens.IsValidName(name));
    }
}
