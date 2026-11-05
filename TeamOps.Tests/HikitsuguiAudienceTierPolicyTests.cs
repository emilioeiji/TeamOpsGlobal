using TeamOps.Data.Security;
using Xunit;

namespace TeamOps.Tests;

public sealed class HikitsuguiAudienceTierPolicyTests
{
    [Theory]
    [InlineData(1, "operators", true)]
    [InlineData(1, "leaders", false)]
    [InlineData(1, "masv", false)]
    [InlineData(2, "leaders", true)]
    [InlineData(2, "masv", false)]
    [InlineData(3, "masv", true)]
    [InlineData(6, "todos", true)]
    [InlineData(6, "invalid", false)]
    public void CanFilter_UsesSchemaV2Hierarchy(int rank, string audience, bool expected)
    {
        Assert.Equal(expected, HikitsuguiAudienceTierPolicy.CanFilter(rank, audience));
    }

    [Fact]
    public void DemandCanFilter_RejectsManipulatedMasvFilterForKl()
    {
        Assert.Throws<UnauthorizedAccessException>(() =>
            HikitsuguiAudienceTierPolicy.DemandCanFilter(2, "masv"));
    }
}
