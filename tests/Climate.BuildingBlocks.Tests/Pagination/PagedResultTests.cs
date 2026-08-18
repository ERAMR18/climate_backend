using Climate.SharedKernel.Pagination;

namespace Climate.BuildingBlocks.Tests.Pagination;

public sealed class PagedResultTests
{
    [Fact]
    public void ConstructorCalculatesNavigationState()
    {
        var page = new PagedResult<int>([21, 22], pageNumber: 2, pageSize: 20, totalCount: 45);

        Assert.Equal(3, page.TotalPages);
        Assert.True(page.HasPreviousPage);
        Assert.True(page.HasNextPage);
    }

    [Theory]
    [InlineData(0, 20, 1)]
    [InlineData(1, 0, 1)]
    [InlineData(1, 20, -1)]
    public void ConstructorRejectsInvalidArguments(int pageNumber, int pageSize, long totalCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PagedResult<int>([], pageNumber, pageSize, totalCount));
    }
}
