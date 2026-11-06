using Dapper;
using TeamOps.Data.Repositories;
using TeamOps.Data.Security;
using Xunit;

namespace TeamOps.Tests;

public sealed class OrganizationAdministrationTests
{
    private const string StrongPassword = "TeamOps#2026!Secure";

    [Fact]
    public void Director_CreatesAndKeepsUnitAndSectorSynchronized()
    {
        using var context = new DatabaseTestContext();
        var directorId = ProvisionDirector(context);
        var service = new OrganizationAdministrationService(context.Factory);
        var root = new OrganizationRepository(context.Factory).GetByCode("5500")!;

        var unitId = service.AddUnit(directorId,
            new OrganizationUnitSaveRequest(root.Id, "5552-2", "Unidade 5552-2", "", true));
        var sectorId = service.AddSector(directorId,
            new OrganizationSectorSaveRequest(unitId, "NEW-SECTOR", "Novo Setor", "新", true));
        service.UpdateSector(directorId, sectorId,
            new OrganizationSectorSaveRequest(unitId, "NEW-SECTOR", "Setor Atualizado", "更新", true));

        using var conn = context.Factory.CreateOpenConnection();
        var synchronized = conn.ExecuteScalar<int>(@"
            SELECT COUNT(*) FROM Sectors s
            JOIN OrganizationNodes n ON n.Id = s.OrganizationNodeId
            WHERE s.Id = @sectorId AND s.Code = n.Code AND s.NamePt = n.NamePt
              AND s.NamePt = 'Setor Atualizado' AND n.ParentId = @unitId;",
            new { sectorId, unitId });
        Assert.Equal(1, synchronized);
        Assert.Throws<InvalidOperationException>(() => service.DeactivateUnit(directorId, unitId));

        service.DeactivateSector(directorId, sectorId);
        service.DeactivateUnit(directorId, unitId);

        Assert.Equal(0, conn.ExecuteScalar<int>("SELECT IsActive FROM Sectors WHERE Id = @sectorId;", new { sectorId }));
        Assert.Equal(0, conn.ExecuteScalar<int>("SELECT IsActive FROM OrganizationNodes WHERE Id = @unitId;", new { unitId }));
        Assert.Equal(5, conn.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM SecurityAudit WHERE ActorUserId = @directorId AND EventType LIKE 'Organization%';",
            new { directorId }));
    }

    [Fact]
    public void RootIdentityComesFromTheTree_NotFromItsCode()
    {
        using var context = new DatabaseTestContext();
        var directorId = ProvisionDirector(context);
        var service = new OrganizationAdministrationService(context.Factory);
        var root = new OrganizationRepository(context.Factory).GetByCode("5500")!;

        service.UpdateUnit(directorId, root.Id,
            new OrganizationUnitSaveRequest(null, "ROOT", "Raiz", "", true));

        using var conn = context.Factory.CreateOpenConnection();
        Assert.Equal(root.Id, conn.ExecuteScalar<int>(
            "SELECT Id FROM OrganizationNodes WHERE Code = 'ROOT';"));
        Assert.Throws<InvalidOperationException>(() => service.UpdateUnit(directorId, root.Id,
            new OrganizationUnitSaveRequest(root.Id, "ROOT", "Raiz", "", true)));
        Assert.Throws<InvalidOperationException>(() => service.DeactivateUnit(directorId, root.Id));
    }

    [Fact]
    public void UnitCannotBeMovedBelowItsOwnDescendant()
    {
        using var context = new DatabaseTestContext();
        var directorId = ProvisionDirector(context);
        var service = new OrganizationAdministrationService(context.Factory);
        var root = new OrganizationRepository(context.Factory).GetByCode("5500")!;
        var parentId = service.AddUnit(directorId,
            new OrganizationUnitSaveRequest(root.Id, "BRANCH", "Ramo", "", true));
        var childId = service.AddUnit(directorId,
            new OrganizationUnitSaveRequest(parentId, "BRANCH-1", "Sub-ramo", "", true));

        Assert.Throws<InvalidOperationException>(() => service.UpdateUnit(directorId, parentId,
            new OrganizationUnitSaveRequest(childId, "BRANCH", "Ramo", "", true)));
    }

    [Fact]
    public void ManagerCannotChangeOrganizationStructure()
    {
        using var context = new DatabaseTestContext();
        var directorId = ProvisionDirector(context);
        var unit = new OrganizationRepository(context.Factory).GetByCode("5552")!;
        var managerId = new UserAdministrationService(context.Factory).CreateUser(directorId,
            new UserAdministrationSaveRequest("manager.5552", null, "Manager 5552", "MANAGER", unit.Id, [], true),
            StrongPassword);

        Assert.Throws<UnauthorizedAccessException>(() =>
            new OrganizationAdministrationService(context.Factory).AddUnit(managerId,
                new OrganizationUnitSaveRequest(unit.Id, "FORBIDDEN", "Proibido", "", true)));
    }

    private static int ProvisionDirector(DatabaseTestContext context) =>
        new FirstRunProvisioningService(context.Factory)
            .ProvisionDirector("initial.director", "Initial Director", StrongPassword);
}
