using TeamOps.Core.Security;
using Xunit;

namespace TeamOps.Tests;

public sealed class UserSessionTests
{
    [Fact]
    public void AssignedProfile_AutoSelectsFirstAuthorizedSector_AndRejectsAggregate()
    {
        var access = Context("AssignedSectors", 10, 20);
        var session = new UserSession(access,
        [
            new SectorAccessOption(10, "A", "A", "A"),
            new SectorAccessOption(20, "B", "B", "B")
        ]);

        Assert.Equal(10, session.SelectedSectorId);
        session.SelectSector(20);
        Assert.Equal(20, session.SelectedSectorId);
        Assert.Throws<UnauthorizedAccessException>(() => session.SelectSector(null));
        Assert.Throws<UnauthorizedAccessException>(() => session.SelectSector(999));
    }

    [Fact]
    public void OrganizationProfile_CanUseAggregateOrDescendantSector()
    {
        var access = Context("OrganizationTree", 10, 20);
        var session = new UserSession(access,
        [
            new SectorAccessOption(10, "A", "A", "A"),
            new SectorAccessOption(20, "B", "B", "B")
        ]);

        Assert.Null(session.SelectedSectorId);
        session.SelectSector(20);
        Assert.Equal(20, session.SelectedSectorId);
        session.SelectSector(null);
        Assert.Null(session.SelectedSectorId);
    }

    private static UserAccessContext Context(string scopeMode, params int[] sectorIds) => new()
    {
        UserId = 1,
        ScopeMode = scopeMode,
        AuthorizedSectorIds = sectorIds.ToHashSet()
    };
}
