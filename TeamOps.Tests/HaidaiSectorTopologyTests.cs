using Dapper;
using TeamOps.Data.Repositories;
using TeamOps.UI.Services;
using Xunit;

namespace TeamOps.Tests;

public sealed class HaidaiSectorTopologyTests
{
    [Fact]
    public void MissingConfiguration_DoesNotInferSharedSectorFromNumericIdsOrCodes()
    {
        using var context = new DatabaseTestContext();
        var service = new HaidaiModuleService(context.Factory);

        var payload = service.GetInitialPayload(0, 0);

        Assert.Null(payload.SharedSectorTopology.SharedSectorId);
        Assert.Empty(payload.SharedSectorTopology.MemberSectorIds);
        Assert.False(payload.SharedSectorTopology.IsEnabled);
    }

    [Fact]
    public void SharedTopology_IsResolvedFromDatabaseConfiguration_NotSectorCodes()
    {
        using var context = new DatabaseTestContext();
        var organizations = new OrganizationRepository(context.Factory);
        var unit = organizations.GetByCode("5552")!;
        var gdad = organizations.AddSector("G-DAD", "GDAD", "GDAD", unit.Id);
        using (var conn = context.Factory.CreateOpenConnection())
        {
            var memberIds = conn.Query<int>(
                "SELECT Id FROM Sectors WHERE Code IN ('G-BARERU', 'DAD') ORDER BY Id;").ToArray();
            var groupId = conn.ExecuteScalar<int>(@"
                INSERT INTO HaidaiSharedSectorGroups (Name, SharedSectorId)
                VALUES ('Teste', @sharedSectorId);
                SELECT last_insert_rowid();", new { sharedSectorId = gdad.Id });
            foreach (var memberId in memberIds)
            {
                conn.Execute(@"
                    INSERT INTO HaidaiSharedSectorMembers (GroupId, MemberSectorId)
                    VALUES (@groupId, @memberId);", new { groupId, memberId });
            }

            conn.Execute(@"
                UPDATE Sectors SET Code = 'SHARED-CUSTOM' WHERE Id = @sharedSectorId;
                UPDATE Sectors SET Code = 'MEMBER-' || Id WHERE Id IN @memberIds;",
                new { sharedSectorId = gdad.Id, memberIds });
        }
        var service = new HaidaiModuleService(context.Factory);

        var payload = service.GetInitialPayload(0, 0);

        Assert.NotEqual(3, gdad.Id);
        Assert.Equal(gdad.Id, payload.SharedSectorTopology.SharedSectorId);
        Assert.Equal(2, payload.SharedSectorTopology.MemberSectorIds.Count);
        Assert.True(payload.SharedSectorTopology.IsEnabled);
    }
}
