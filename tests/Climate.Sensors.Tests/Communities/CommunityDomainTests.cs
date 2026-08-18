using Climate.Sensors.Domain.Communities;

namespace Climate.Sensors.Tests.Communities;

public sealed class CommunityDomainTests
{
    [Fact]
    public void CreateNormalizesOptionalDescription()
    {
        Community community=Community.Create(Guid.NewGuid()," Rural ","   ",10m,-90m,DateTimeOffset.UtcNow);
        Assert.Equal("Rural",community.Name); Assert.Null(community.Description); Assert.True(community.IsActive);
    }

    [Fact]
    public void UpdateCanDeactivateAndReplaceCoordinates()
    {
        Community community=Community.Create(Guid.NewGuid(),"Old",null,0m,0m,DateTimeOffset.UtcNow);
        community.Update(" New "," Description ",12m,-91m,false);
        Assert.Equal("New",community.Name); Assert.Equal("Description",community.Description); Assert.Equal(12m,community.Latitude); Assert.False(community.IsActive);
    }
}
