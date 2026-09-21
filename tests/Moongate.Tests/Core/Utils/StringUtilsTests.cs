using Moongate.Core.Utils;
using Moongate.Tests.TestSupport.Strings;

namespace Moongate.Tests.Core.Utils;

public class StringUtilsTests
{
    [Fact]
    public void CaseConversions_TurkishCurrentCulture_UseInvariantCasing()
    {
        using var culture = new CultureScope("tr-TR");

        Assert.Equal("iIndex", StringUtils.ToCamelCase("I_INDEX"));
        Assert.Equal("IIndex", StringUtils.ToPascalCase("i_index"));
        Assert.Equal("i_index", StringUtils.ToSnakeCase("I_INDEX"));
        Assert.Equal("I_INDEX", StringUtils.ToUpperSnakeCase("i index"));
        Assert.Equal("I index", StringUtils.ToSentenceCase("i INDEX"));
    }

    [Fact]
    public void ToCamelCase_LeadingSeparators_LowercasesFirstWord()
        => Assert.Equal("helloWorld", StringUtils.ToCamelCase("__hello world"));

    [Theory,
     InlineData(null, ""),
     InlineData("", ""),
     InlineData("É", "é"),
     InlineData("HTTPResponseID", "httpResponseId"),
     InlineData("API__RESPONSE--id ", "apiResponseId"),
     InlineData("CAFÉ_münchen", "caféMünchen"),
     InlineData("_ -\t", "")]
    public void ToCamelCase_WordsAndSeparators_ProducesCamelCase(string? input, string expected)
        => Assert.Equal(expected, StringUtils.ToCamelCase(input!));

    [Theory,
     InlineData(null, ""),
     InlineData("", ""),
     InlineData("É", "é"),
     InlineData("HTTPResponseID", "http.response.id"),
     InlineData("-- hello__WORLD \t ", "hello.world"),
     InlineData("CAFÉ münchen", "café.münchen"),
     InlineData("_ -\t", "")]
    public void ToDotCase_WordsAndSeparators_ProducesDotCase(string? input, string expected)
        => Assert.Equal(expected, StringUtils.ToDotCase(input!));

    [Theory,
     InlineData(null, ""),
     InlineData("", ""),
     InlineData("É", "é"),
     InlineData("HTTPResponseID", "http-response-id"),
     InlineData("-- hello__WORLD \t ", "hello-world"),
     InlineData("CAFÉ münchen", "café-münchen"),
     InlineData("_ -\t", "")]
    public void ToKebabCase_WordsAndSeparators_ProducesKebabCase(string? input, string expected)
        => Assert.Equal(expected, StringUtils.ToKebabCase(input!));

    [Theory,
     InlineData(null, ""),
     InlineData("", ""),
     InlineData("é", "É"),
     InlineData("HTTPResponseID", "HttpResponseId"),
     InlineData("-- hello__WORLD \t ", "HelloWorld"),
     InlineData("CAFÉ münchen", "CaféMünchen"),
     InlineData("_ -\t", "")]
    public void ToPascalCase_WordsAndSeparators_ProducesPascalCase(string? input, string expected)
        => Assert.Equal(expected, StringUtils.ToPascalCase(input!));

    [Theory,
     InlineData(null, ""),
     InlineData("", ""),
     InlineData("É", "é"),
     InlineData("HTTPResponseID", "http/response/id"),
     InlineData("-- hello__WORLD \t ", "hello/world"),
     InlineData("CAFÉ münchen", "café/münchen"),
     InlineData("_ -\t", "")]
    public void ToPathCase_WordsAndSeparators_ProducesPathCase(string? input, string expected)
        => Assert.Equal(expected, StringUtils.ToPathCase(input!));

    [Theory,
     InlineData(null, ""),
     InlineData("", ""),
     InlineData("é", "É"),
     InlineData("apiResponse", "Apiresponse"),
     InlineData(" _HELLO__WORLD-- ", "Hello world"),
     InlineData("ÉCOLE MÜNCHEN", "École münchen"),
     InlineData("_ -\t", "")]
    public void ToSentenceCase_SpaceSeparatedWords_CapitalizesOnlyFirstWord(string? input, string expected)
        => Assert.Equal(expected, StringUtils.ToSentenceCase(input!));

    [Theory,
     InlineData(null, ""),
     InlineData("", ""),
     InlineData("É", "é"),
     InlineData("HTTPResponseID", "http_response_id"),
     InlineData("-- hello__WORLD \t ", "hello_world"),
     InlineData("CAFÉ münchen", "café_münchen"),
     InlineData("_ -\t", "")]
    public void ToSnakeCase_WordsAndSeparators_ProducesSnakeCase(string? input, string expected)
        => Assert.Equal(expected, StringUtils.ToSnakeCase(input!));

    [Theory,
     InlineData(null, ""),
     InlineData("", ""),
     InlineData("é", "É"),
     InlineData("HTTPResponseID", "Http Response Id"),
     InlineData("-- hello__WORLD \t ", "Hello World"),
     InlineData("CAFÉ münchen", "Café München"),
     InlineData("_ -\t", "")]
    public void ToTitleCase_WordsAndSeparators_ProducesTitleCase(string? input, string expected)
        => Assert.Equal(expected, StringUtils.ToTitleCase(input!));

    [Theory,
     InlineData(null, ""),
     InlineData("", ""),
     InlineData("é", "É"),
     InlineData("HTTPResponseID", "Http-Response-Id"),
     InlineData("-- hello__WORLD \t ", "Hello-World"),
     InlineData("CAFÉ münchen", "Café-München"),
     InlineData("_ -\t", "")]
    public void ToTrainCase_WordsAndSeparators_ProducesTrainCase(string? input, string expected)
        => Assert.Equal(expected, StringUtils.ToTrainCase(input!));

    [Theory, InlineData("HTTPResponseID", "HTTP_RESPONSE_ID"), InlineData("café münchen", "CAFÉ_MÜNCHEN")]
    public void ToUpperSnakeCase_Words_UppercasesEachWord(string input, string expected)
        => Assert.Equal(expected, StringUtils.ToUpperSnakeCase(input));
}
