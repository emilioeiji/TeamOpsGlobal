using Dapper;
using TeamOps.Services;
using Xunit;

namespace TeamOps.Tests;

public sealed class SectorCatalogTests
{
    [Fact]
    public void ResolvesActiveSectorsByCodeNameAliasAndActualId()
    {
        using var context = new DatabaseTestContext();
        var catalog = new SectorCatalogService(context.Factory);
        using var conn = context.Factory.CreateOpenConnection();
        var dadId = conn.ExecuteScalar<int>("SELECT Id FROM Sectors WHERE Code = 'DAD';");
        var gBareruId = conn.ExecuteScalar<int>("SELECT Id FROM Sectors WHERE Code = 'G-BARERU';");

        Assert.Equal(dadId, catalog.ResolveRequired("dad"));
        Assert.Equal(dadId, catalog.ResolveFilter("DAD"));
        Assert.Equal(dadId, catalog.ResolveFilter(dadId.ToString()));
        Assert.Equal(gBareruId, catalog.ResolveFilter("gbareru"));
        Assert.Equal("DAD", catalog.GetLabel(dadId));
        Assert.Equal(0, catalog.ResolveFilter("all"));
    }

    [Fact]
    public void NewlyCreatedSectorIsResolvedWithoutCodeChanges()
    {
        using var context = new DatabaseTestContext();
        using var conn = context.Factory.CreateOpenConnection();
        var unitId = conn.ExecuteScalar<int>(
            "SELECT Id FROM OrganizationNodes WHERE Code = '5552';");
        var nodeId = conn.ExecuteScalar<int>(@"
            INSERT INTO OrganizationNodes (ParentId, NodeType, Code, NamePt, NameJp)
            VALUES (@unitId, 'Sector', 'NEW-SECTOR', 'Setor Novo', '新しい');
            SELECT last_insert_rowid();", new { unitId });
        var sectorId = conn.ExecuteScalar<int>(@"
            INSERT INTO Sectors (Code, NamePt, NameJp, OrganizationNodeId, IsActive)
            VALUES ('NEW-SECTOR', 'Setor Novo', '新しい', @nodeId, 1);
            SELECT last_insert_rowid();", new { nodeId });

        var catalog = new SectorCatalogService(context.Factory);

        Assert.Equal(sectorId, catalog.ResolveFilter("NEW-SECTOR"));
        Assert.Equal(sectorId, catalog.ResolveFilter("Setor Novo"));
        Assert.Equal("Setor Novo", catalog.GetLabel(sectorId));
    }

    [Fact]
    public void InactiveOrUnknownSectorIsRejected()
    {
        using var context = new DatabaseTestContext();
        using var conn = context.Factory.CreateOpenConnection();
        conn.Execute("UPDATE Sectors SET IsActive = 0 WHERE Code = 'APL';");
        var catalog = new SectorCatalogService(context.Factory);

        Assert.Throws<InvalidOperationException>(() => catalog.ResolveFilter("APL"));
        Assert.Throws<InvalidOperationException>(() => catalog.ResolveFilter("UNKNOWN"));
    }
}
