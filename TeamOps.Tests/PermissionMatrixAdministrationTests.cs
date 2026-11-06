using Dapper;
using TeamOps.Core.Security;
using TeamOps.Data.Repositories;
using TeamOps.Data.Security;
using Xunit;

namespace TeamOps.Tests;

public sealed class PermissionMatrixAdministrationTests
{
    private const string StrongPassword = "TeamOps#2026!Secure";

    [Fact]
    public void Director_CanReplaceProfilePermissions_AndInvalidateProfileSessions()
    {
        using var context = new DatabaseTestContext();
        var directorId = new FirstRunProvisioningService(context.Factory)
            .ProvisionDirector("initial.director", "Initial Director", StrongPassword);
        var opUserId = CreateProfileUser(context, "OP", "operator.matrix");
        var authorization = new AuthorizationService(context.Factory);
        var service = new PermissionMatrixAdministrationService(context.Factory);

        Assert.True(authorization.IsAllowed(opUserId, ModuleCodes.Hikitsugui, PermissionActions.View));
        var beforeVersion = SessionVersion(context, opUserId);

        service.SaveProfilePermissions(directorId, "OP",
        [
            new(ModuleCodes.Dashboard, PermissionActions.View, true),
            new(ModuleCodes.PaidLeave, PermissionActions.View, true)
        ]);

        Assert.False(authorization.IsAllowed(opUserId, ModuleCodes.Hikitsugui, PermissionActions.View));
        Assert.True(authorization.IsAllowed(opUserId, ModuleCodes.PaidLeave, PermissionActions.View));
        Assert.Equal(beforeVersion + 1, SessionVersion(context, opUserId));
        using var conn = context.Factory.CreateOpenConnection();
        Assert.Equal(1, conn.ExecuteScalar<int>(@"
            SELECT COUNT(*) FROM SecurityAudit
            WHERE EventType = 'PermissionMatrixUpdated'
              AND TargetType = 'AccessProfile'
              AND TargetId = 'OP';"));
    }

    [Fact]
    public void Manager_CannotChangeGlobalPermissionMatrix()
    {
        using var context = new DatabaseTestContext();
        var directorId = new FirstRunProvisioningService(context.Factory)
            .ProvisionDirector("initial.director", "Initial Director", StrongPassword);
        var unit = new OrganizationRepository(context.Factory).GetByCode("5552")!;
        var managerId = new UserAdministrationService(context.Factory).CreateUser(
            directorId,
            new UserAdministrationSaveRequest(
                "manager.5552", null, "Manager 5552", "MANAGER", unit.Id, [], true),
            StrongPassword);
        var service = new PermissionMatrixAdministrationService(context.Factory);

        Assert.Throws<UnauthorizedAccessException>(() =>
            service.SaveProfilePermissions(managerId, "OP",
                [new(ModuleCodes.Dashboard, PermissionActions.View, true)]));
    }

    private static int CreateProfileUser(DatabaseTestContext context, string profileCode, string login)
    {
        using var conn = context.Factory.CreateOpenConnection();
        return conn.ExecuteScalar<int>(@"
            INSERT INTO Users
                (Login, Name, PasswordHash, ProfileId, ReferenceNodeId, IsActive, SessionVersion)
            SELECT @login, @login, @hash, profile.Id, unit.Id, 1, 1
            FROM AccessProfiles profile
            CROSS JOIN OrganizationNodes unit
            WHERE profile.Code = @profileCode AND unit.Code = '5552';
            SELECT last_insert_rowid();",
            new
            {
                login,
                profileCode,
                hash = BCrypt.Net.BCrypt.HashPassword(StrongPassword)
            });
    }

    private static int SessionVersion(DatabaseTestContext context, int userId)
    {
        using var conn = context.Factory.CreateOpenConnection();
        return conn.ExecuteScalar<int>(
            "SELECT SessionVersion FROM Users WHERE Id = @userId;", new { userId });
    }
}
