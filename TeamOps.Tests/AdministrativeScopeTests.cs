using TeamOps.Core.Security;
using TeamOps.Data.Repositories;
using TeamOps.Data.Security;
using Xunit;

namespace TeamOps.Tests;

public sealed class AdministrativeScopeTests
{
    private const string StrongPassword = "TeamOps#2026!Secure";

    [Fact]
    public void Director_CanManageGlobalCatalogs()
    {
        using var context = new DatabaseTestContext();
        var directorId = new FirstRunProvisioningService(context.Factory)
            .ProvisionDirector("initial.director", "Initial Director", StrongPassword);
        var service = new AdministrativeScopeService(context.Factory);

        service.DemandGlobal(directorId, PermissionActions.Administer);
        Assert.True(service.GetScope(directorId).CanManageGlobalCatalogs);
    }

    [Fact]
    public void Manager_IsRestrictedToReferenceTreeAndCannotManageGlobalCatalogs()
    {
        using var context = new DatabaseTestContext();
        var directorId = new FirstRunProvisioningService(context.Factory)
            .ProvisionDirector("initial.director", "Initial Director", StrongPassword);
        var unit = new OrganizationRepository(context.Factory).GetByCode("5552")!;
        var managerId = new UserAdministrationService(context.Factory).CreateUser(directorId,
            new UserAdministrationSaveRequest("manager.5552", null, "Manager 5552", "MANAGER", unit.Id, [], true),
            StrongPassword);
        var service = new AdministrativeScopeService(context.Factory);
        var scope = service.GetScope(managerId);
        var root = new OrganizationRepository(context.Factory).GetByCode("5500")!;
        var externalUnit = new OrganizationRepository(context.Factory)
            .AddUnit("5552-2", "Unidade 5552-2", "", root.Id);
        var externalSector = new OrganizationRepository(context.Factory)
            .AddSector("EXTERNAL", "Externo", "", externalUnit);

        Assert.False(scope.CanManageGlobalCatalogs);
        Assert.Equal(4, scope.AuthorizedSectorIds.Count);
        Assert.Throws<UnauthorizedAccessException>(() =>
            service.DemandGlobal(managerId, PermissionActions.Administer));
        foreach (var sectorId in scope.AuthorizedSectorIds)
            service.DemandSector(managerId, sectorId, PermissionActions.Edit);
        Assert.Throws<UnauthorizedAccessException>(() =>
            service.DemandSector(managerId, externalSector.Id, PermissionActions.Edit));
    }
}
