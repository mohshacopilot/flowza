using Flowza.Common.Paging;

namespace Flowza.BuildingBlocks.UnitTests.Common;

public sealed class PagingTests
{
    [Fact]
    public void T2_S7_default_page_is_the_first_page_of_25()
    {
        var request = new PageRequest();

        request.Page.ShouldBe(1);
        request.Size.ShouldBe(25);
        request.Skip.ShouldBe(0);
    }

    [Theory]
    [InlineData(0, 25, 1, 25)]
    [InlineData(-3, 25, 1, 25)]
    [InlineData(2, 0, 2, 1)]
    [InlineData(2, 500, 2, 100)]
    [InlineData(3, 10, 3, 10)]
    public void T2_S7_out_of_range_values_are_brought_into_range(int page, int size, int expectedPage, int expectedSize)
    {
        var request = new PageRequest(page, size);

        request.Page.ShouldBe(expectedPage);
        request.Size.ShouldBe(expectedSize);
    }

    [Fact]
    public void T2_S7_skip_counts_the_items_before_the_page()
    {
        new PageRequest(3, 25).Skip.ShouldBe(50);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(25, 1)]
    [InlineData(26, 2)]
    [InlineData(100, 4)]
    public void T2_S7_total_pages_round_up(int totalCount, int expectedPages)
    {
        var page = PagedResult.Create<string>([], new PageRequest(1, 25), totalCount);

        page.TotalPages.ShouldBe(expectedPages);
        page.Page.ShouldBe(1);
        page.Size.ShouldBe(25);
        page.TotalCount.ShouldBe(totalCount);
    }
}
