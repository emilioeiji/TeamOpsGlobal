using TeamOps.Data.Repositories;
using TeamOps.UI.Services;
using Xunit;

namespace TeamOps.Tests;

public sealed class HaidaiSectorTopologyTests
{
    [Fact]
    public void MissingGdad_DoesNotTreatSectorIdThreeAsSharedGdad()
    {
        using var context = new DatabaseTestContext();
        var service = new HaidaiModuleService(context.Factory);

        var payload = service.GetInitialPayload(0, 0);

        Assert.NotNull(payload.SharedSectorTopology.GBareruSectorId);
        Assert.NotNull(payload.SharedSectorTopology.DadSectorId);
        Assert.Null(payload.SharedSectorTopology.GdadSectorId);
        Assert.False(payload.SharedSectorTopology.IsEnabled);
    }

    [Fact]
    public void Gdad_IsResolvedByNormalizedCodeInsteadOfNumericId()
    {
        using var context = new DatabaseTestContext();
        var organizations = new OrganizationRepository(context.Factory);
        var unit = organizations.GetByCode("5552")!;
        var gdad = organizations.AddSector("G-DAD", "GDAD", "GDAD", unit.Id);
        var service = new HaidaiModuleService(context.Factory);

        var payload = service.GetInitialPayload(0, 0);

        Assert.NotEqual(3, gdad.Id);
        Assert.Equal(gdad.Id, payload.SharedSectorTopology.GdadSectorId);
        Assert.True(payload.SharedSectorTopology.IsEnabled);
    }
}
