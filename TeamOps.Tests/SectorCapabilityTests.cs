using Dapper;
using TeamOps.Data.Repositories;
using TeamOps.Services;
using Xunit;

namespace TeamOps.Tests;

public sealed class SectorCapabilityTests
{
    [Fact]
    public void CapabilityResolution_DoesNotDependOnTheSectorCode()
    {
        using var context = new DatabaseTestContext();
        var service = new SectorCapabilityService(context.Factory);
        var originalId = service.ResolveRequired(SectorFeatureCodes.SobraDePeca);

        using var conn = context.Factory.CreateOpenConnection();
        conn.Execute("UPDATE Sectors SET Code = 'CUSTOM-PROCESS' WHERE Id = @originalId;", new { originalId });

        Assert.Equal(originalId, service.ResolveRequired(SectorFeatureCodes.SobraDePeca));
    }

    [Fact]
    public void CapabilityCanBeReassignedToANewSectorWithoutChangingCode()
    {
        using var context = new DatabaseTestContext();
        var organizations = new OrganizationRepository(context.Factory);
        var unit = organizations.GetByCode("5552")!;
        var newSector = organizations.AddSector("PROCESS-X", "Processo X", "Processo X", unit.Id);

        using (var conn = context.Factory.CreateOpenConnection())
        using (var transaction = conn.BeginTransaction())
        {
            conn.Execute(@"
                UPDATE SectorCapabilities
                SET IsActive = 0, UpdatedAt = CURRENT_TIMESTAMP
                WHERE FeatureCode = @featureCode;
                INSERT INTO SectorCapabilities (SectorId, FeatureCode)
                VALUES (@sectorId, @featureCode);",
                new
                {
                    sectorId = newSector.Id,
                    featureCode = SectorFeatureCodes.SobraDePeca
                }, transaction);
            transaction.Commit();
        }

        Assert.Equal(newSector.Id,
            new SectorCapabilityService(context.Factory).ResolveRequired(SectorFeatureCodes.SobraDePeca));
    }
}
