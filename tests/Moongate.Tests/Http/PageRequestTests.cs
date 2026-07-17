using Moongate.Http.Plugin.Data;

namespace Moongate.Tests.Http;

public class PageRequestTests
{
    [Fact]
    public void TryParse_NothingSupplied_UsesTheDefaults()
    {
        Assert.True(PageRequest.TryParse(null, null, null, out var request, out var error));

        Assert.Null(error);
        Assert.Equal(1, request.Page);
        Assert.Equal(PageRequest.DefaultPageSize, request.PageSize);
        Assert.Null(request.Search);
    }

    [Fact]
    public void TryParse_FirstPage_SkipsNothing()
    {
        Assert.True(PageRequest.TryParse("1", "25", null, out var request, out _));

        Assert.Equal(0, request.Skip);
    }

    [Fact]
    public void TryParse_Skip_IsOneBasedPageTurnedIntoAnOffset()
    {
        // The single place page-to-offset happens. Mixing 1-based and 0-based across layers is the classic
        // off-by-one, and it shows up as a quietly missing or repeated row, not as an error.
        Assert.True(PageRequest.TryParse("3", "25", null, out var request, out _));

        Assert.Equal(50, request.Skip);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    public void TryParse_BadPage_IsRejectedRatherThanClamped(string page)
    {
        // Serving page 1 to someone who asked for page 0 hides their bug instead of reporting it.
        Assert.False(PageRequest.TryParse(page, null, null, out _, out var error));
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("101")]
    [InlineData("5000")]
    [InlineData("abc")]
    public void TryParse_BadPageSize_IsRejectedRatherThanClamped(string pageSize)
    {
        // Silently capping 5000 to 100 leaves the caller believing it read everything.
        Assert.False(PageRequest.TryParse(null, pageSize, null, out _, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void TryParse_MaxPageSize_IsAllowed()
    {
        Assert.True(
            PageRequest.TryParse(null, PageRequest.MaxPageSize.ToString(), null, out var request, out _)
        );

        Assert.Equal(PageRequest.MaxPageSize, request.PageSize);
    }

    [Fact]
    public void TryParse_Search_IsTrimmed()
    {
        Assert.True(PageRequest.TryParse(null, null, "  tom  ", out var request, out _));

        Assert.Equal("tom", request.Search);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParse_BlankSearch_MeansNoFilter(string search)
    {
        // Null and blank must mean the same thing, or "?search=" behaves differently from omitting it.
        Assert.True(PageRequest.TryParse(null, null, search, out var request, out _));

        Assert.Null(request.Search);
    }

    [Fact]
    public void From_ComputesTheTotalPages()
    {
        PageRequest.TryParse("1", "25", null, out var request, out _);

        var response = PagedResponse<string>.From(["a"], 51, request);

        Assert.Equal(3, response.TotalPages); // 51 over 25 is three pages, not two
        Assert.Equal(51, response.Total);
        Assert.Equal(1, response.Page);
        Assert.Equal(25, response.PageSize);
    }

    [Fact]
    public void From_NoResults_IsZeroPages()
    {
        PageRequest.TryParse("1", "25", null, out var request, out _);

        var response = PagedResponse<string>.From([], 0, request);

        Assert.Equal(0, response.TotalPages);
        Assert.Empty(response.Items);
    }
}
