using Dapper;
using TeamOps.Data.Repositories;
using Xunit;

namespace TeamOps.Tests;

public sealed class LocalRepositoryTests
{
    [Fact]
    public void SelectionQueries_ExcludeInactiveLocations()
    {
        using var context = new DatabaseTestContext();
        int sectorId;
        int activeId;
        int inactiveId;

        using (var conn = context.Factory.CreateOpenConnection())
        {
            sectorId = conn.ExecuteScalar<int>("SELECT Id FROM Sectors ORDER BY Id LIMIT 1;");
            activeId = conn.ExecuteScalar<int>(@"
                INSERT INTO Locals (NamePt, NameJp, ShortCode, SectorId, IsActive)
                VALUES ('Local ativo teste', 'Active test', 'ACTIVE-TEST', @sectorId, 1);
                SELECT last_insert_rowid();", new { sectorId });
            inactiveId = conn.ExecuteScalar<int>(@"
                INSERT INTO Locals (NamePt, NameJp, ShortCode, SectorId, IsActive)
                VALUES ('Local inativo teste', 'Inactive test', 'INACTIVE-TEST', @sectorId, 0);
                SELECT last_insert_rowid();", new { sectorId });
        }

        var repository = new LocalRepository(context.Factory);
        var allIds = repository.GetAll().Select(local => local.Id).ToHashSet();
        var sectorIds = repository.GetBySector(sectorId).Select(local => local.Id).ToHashSet();
        var selectableIds = repository.GetLocalIdsBySector(sectorId).ToHashSet();

        Assert.Contains(activeId, allIds);
        Assert.DoesNotContain(inactiveId, allIds);
        Assert.Contains(activeId, sectorIds);
        Assert.DoesNotContain(inactiveId, sectorIds);
        Assert.Contains(activeId, selectableIds);
        Assert.DoesNotContain(inactiveId, selectableIds);
    }
}
