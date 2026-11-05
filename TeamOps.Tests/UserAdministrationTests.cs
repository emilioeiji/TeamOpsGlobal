using Dapper;
using TeamOps.Core.Security;
using TeamOps.Data.Repositories;
using TeamOps.Data.Security;
using Xunit;

namespace TeamOps.Tests;

public sealed class UserAdministrationTests
{
    private const string StrongPassword = "TeamOps#2026!Secure";

    [Fact]
    public void Director_CreatesKlWithExplicitSectorScope()
    {
        using var context = new DatabaseTestContext();
        var directorId = ProvisionDirector(context);
        var service = new UserAdministrationService(context.Factory);
        var unit = new OrganizationRepository(context.Factory).GetByCode("5552")!;
        var dadId = GetSectorId(context, "DAD");
        CreateOperator(context, "FJ-KL-001", dadId);

        var userId = service.CreateUser(directorId,
            new UserAdministrationSaveRequest("fj.kl.001", "FJ-KL-001", "KL Teste", "KL", unit.Id, [dadId], true),
            StrongPassword);

        var access = new AuthorizationService(context.Factory).GetContext(userId);
        Assert.Equal("KL", access.ProfileCode);
        Assert.Equal([dadId], access.AuthorizedSectorIds);
        var payload = service.GetPayload(directorId);
        Assert.Contains(payload.Users, item => item.Id == userId && item.SectorIds.SequenceEqual([dadId]));
        Assert.Contains(payload.Profiles, item => item.Code == "KL");
        Assert.True(new AuthenticationService(context.Factory)
            .Authenticate("fj.kl.001", StrongPassword).Succeeded);
    }

    [Fact]
    public void Manager_CannotManageDirectorOrAssignOutsideReferenceTree()
    {
        using var context = new DatabaseTestContext();
        var directorId = ProvisionDirector(context);
        var service = new UserAdministrationService(context.Factory);
        var organizations = new OrganizationRepository(context.Factory);
        var root = organizations.GetByCode("5500")!;
        var unit = organizations.GetByCode("5552")!;
        CreateOperator(context, "FJ-MGR-01", GetSectorId(context, "DAD"));
        var managerId = service.CreateUser(directorId,
            new UserAdministrationSaveRequest("manager.5552", "FJ-MGR-01", "Manager 5552", "MANAGER", unit.Id, [], true),
            StrongPassword);
        var externalUnitId = organizations.AddUnit("5552-2", "Unidade externa", "", root.Id);
        var externalSector = organizations.AddSector("EXT", "Externo", "", externalUnitId);

        Assert.Throws<UnauthorizedAccessException>(() => service.UpdateUser(managerId, directorId,
            new UserAdministrationSaveRequest("initial.director", null, "Director", "DIRECTOR", root.Id, [], true)));
        Assert.Throws<UnauthorizedAccessException>(() => service.CreateUser(managerId,
            new UserAdministrationSaveRequest("outside.kl", "FJ-OUT-01", "Outside KL", "KL", unit.Id, [externalSector.Id], true),
            StrongPassword));
    }

    [Fact]
    public void PasswordReset_RequiresStrongPassword_InvalidatesSessionAndAudits()
    {
        using var context = new DatabaseTestContext();
        var directorId = ProvisionDirector(context);
        var service = new UserAdministrationService(context.Factory);
        var unit = new OrganizationRepository(context.Factory).GetByCode("5552")!;
        CreateOperator(context, "FJ-OP-001", GetSectorId(context, "IAS"));
        var userId = service.CreateUser(directorId,
            new UserAdministrationSaveRequest("operator.001", "FJ-OP-001", "Operator Teste", "OP", unit.Id,
                [GetSectorId(context, "IAS")], true), StrongPassword);
        var before = GetSessionVersion(context, userId);

        Assert.Throws<ArgumentException>(() => service.ResetPassword(directorId, userId, "weak"));
        service.ResetPassword(directorId, userId, "Changed#2026!Secure");

        Assert.Equal(before + 1, GetSessionVersion(context, userId));
        using var conn = context.Factory.CreateOpenConnection();
        Assert.Equal(1, conn.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM AuthenticationAudit WHERE UserId = @userId AND EventType = 'PasswordReset';",
            new { userId }));
        Assert.Equal(1, conn.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM SecurityAudit WHERE ActorUserId = @directorId AND TargetId = @targetId AND EventType = 'PasswordReset';",
            new { directorId, targetId = userId.ToString() }));
    }

    [Fact]
    public void NonAdministrator_CannotReadAdministrationPayload()
    {
        using var context = new DatabaseTestContext();
        var directorId = ProvisionDirector(context);
        var service = new UserAdministrationService(context.Factory);
        var unit = new OrganizationRepository(context.Factory).GetByCode("5552")!;
        CreateOperator(context, "FJ-OP-002", GetSectorId(context, "DAD"));
        var opId = service.CreateUser(directorId,
            new UserAdministrationSaveRequest("operator.002", "FJ-OP-002", "Operator Dois", "OP", unit.Id,
                [GetSectorId(context, "DAD")], true), StrongPassword);

        Assert.Throws<UnauthorizedAccessException>(() => service.GetPayload(opId));
    }

    private static int ProvisionDirector(DatabaseTestContext context) =>
        new FirstRunProvisioningService(context.Factory)
            .ProvisionDirector("initial.director", "Initial Director", StrongPassword);

    private static int GetSectorId(DatabaseTestContext context, string code)
    {
        using var conn = context.Factory.CreateOpenConnection();
        return conn.ExecuteScalar<int>("SELECT Id FROM Sectors WHERE Code = @code COLLATE NOCASE;", new { code });
    }

    private static int GetSessionVersion(DatabaseTestContext context, int userId)
    {
        using var conn = context.Factory.CreateOpenConnection();
        return conn.ExecuteScalar<int>("SELECT SessionVersion FROM Users WHERE Id = @userId;", new { userId });
    }

    private static void CreateOperator(DatabaseTestContext context, string codigoFJ, int sectorId)
    {
        using var conn = context.Factory.CreateOpenConnection();
        conn.Execute(@"
            INSERT OR IGNORE INTO Shifts (Id, NamePt, NameJp) VALUES (1, 'Turno 1', '1');
            INSERT OR IGNORE INTO Groups (Id, NamePt, NameJp) VALUES (1, 'Grupo 1', '1');
            INSERT INTO Operators
                (CodigoFJ, NameRomanji, NameNihongo, ShiftId, GroupId, SectorId, StartDate, Trainer, Status)
            VALUES
                (@codigoFJ, @codigoFJ, @codigoFJ, 1, 1, @sectorId, date('now'), 0, 1);",
            new { codigoFJ, sectorId });
    }
}
