using TeamOps.Core.Common;
using TeamOps.Core.Entities;
using TeamOps.Core.Security;
using TeamOps.Data.Repositories;
using TeamOps.Data.Security;
using Xunit;

namespace TeamOps.Tests;

public sealed class AuthorizationTests
{
    [Fact]
    public void AssignedProfile_CannotRequestAggregateScope()
    {
        using var context = new DatabaseTestContext();
        var userId = CreateUser(context, "GL", "5552", "DAD", "G-BARERU");
        var authorization = new AuthorizationService(context.Factory);

        Assert.Throws<UnauthorizedAccessException>(() =>
            authorization.DemandSectorOrAggregate(userId, ModuleCodes.Haidai, PermissionActions.View, 0));
        authorization.DemandSectorOrAggregate(
            userId,
            ModuleCodes.Haidai,
            PermissionActions.View,
            GetSectorId(context, "DAD"));
    }

    [Fact]
    public void OrganizationProfile_CanRequestAggregateScope()
    {
        using var context = new DatabaseTestContext();
        var userId = CreateUser(context, "MANAGER", "5552");

        new AuthorizationService(context.Factory).DemandSectorOrAggregate(
            userId,
            ModuleCodes.Haidai,
            PermissionActions.View,
            0);
    }
    [Theory]
    [InlineData("DIRECTOR", "5500", 4)]
    [InlineData("MANAGER", "5552", 4)]
    [InlineData("SV", "5552", 4)]
    public void OrganizationProfiles_SeeOnlyReferenceTree(string profile, string referenceCode, int expectedSectors)
    {
        using var context = new DatabaseTestContext();
        var userId = CreateUser(context, profile, referenceCode);

        var access = new AuthorizationService(context.Factory).GetContext(userId);

        Assert.Equal(expectedSectors, access.AuthorizedSectorIds.Count);
    }

    [Theory]
    [InlineData("GL", new[] { "G-BARERU", "DAD" })]
    [InlineData("KL", new[] { "DAD" })]
    [InlineData("OP", new[] { "IAS" })]
    public void AssignedProfiles_SeeOnlyExplicitSectors(string profile, string[] sectorCodes)
    {
        using var context = new DatabaseTestContext();
        var userId = CreateUser(context, profile, "5552", sectorCodes);

        var access = new AuthorizationService(context.Factory).GetContext(userId);
        var expected = sectorCodes.Select(code => GetSectorId(context, code)).OrderBy(x => x).ToArray();

        Assert.Equal(expected, access.AuthorizedSectorIds.OrderBy(x => x).ToArray());
    }

    [Fact]
    public void FunctionalPermission_AndSectorScope_AreBothRequired()
    {
        using var context = new DatabaseTestContext();
        var glId = CreateUser(context, "GL", "5552", "DAD");
        var klId = CreateUser(context, "KL", "5552", "DAD");
        var opId = CreateUser(context, "OP", "5552", "IAS");
        var dadId = GetSectorId(context, "DAD");
        var gBareruId = GetSectorId(context, "G-BARERU");
        var authorization = new AuthorizationService(context.Factory);

        Assert.True(authorization.IsAllowed(glId, "production", PermissionActions.View, dadId));
        Assert.False(authorization.IsAllowed(glId, "production", PermissionActions.View, gBareruId));
        Assert.False(authorization.IsAllowed(klId, "production", PermissionActions.View, dadId));
        Assert.True(authorization.IsAllowed(opId, ModuleCodes.Hikitsugui, PermissionActions.View, GetSectorId(context, "IAS")));
        Assert.False(authorization.IsAllowed(opId, ModuleCodes.AccessControl, PermissionActions.View));
    }

    [Fact]
    public void DashboardModules_UseTheCentralPermissionMatrix()
    {
        using var context = new DatabaseTestContext();
        var opId = CreateUser(context, "OP", "5552", "IAS");
        var klId = CreateUser(context, "KL", "5552", "DAD");
        var glId = CreateUser(context, "GL", "5552", "DAD");
        var authorization = new AuthorizationService(context.Factory);

        Assert.True(authorization.IsAllowed(opId, ModuleCodes.Hikitsugui, PermissionActions.View));
        Assert.True(authorization.IsAllowed(opId, ModuleCodes.PaidLeave, PermissionActions.View));
        Assert.False(authorization.IsAllowed(opId, ModuleCodes.FollowUp, PermissionActions.View));
        Assert.True(authorization.IsAllowed(klId, ModuleCodes.FollowUp, PermissionActions.View));
        Assert.False(authorization.IsAllowed(klId, ModuleCodes.Production, PermissionActions.View));
        Assert.True(authorization.IsAllowed(glId, ModuleCodes.Production, PermissionActions.View));
    }

    [Fact]
    public void SectorAssignment_OutsideReferenceTree_IsRejected()
    {
        using var context = new DatabaseTestContext();
        var userId = CreateUnconfiguredUser(context);
        var dadNode = new OrganizationRepository(context.Factory).GetByCode("DAD")!;
        var gBareruId = GetSectorId(context, "G-BARERU");

        Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() =>
            new UserAccessRepository(context.Factory).Configure(userId, "KL", dadNode.Id, [gBareruId]));
    }

    [Fact]
    public void CorruptReferenceCannotExpandAssignedSectorScope()
    {
        using var context = new DatabaseTestContext();
        var userId = CreateUser(context, "KL", "5552", "DAD");
        var organizations = new OrganizationRepository(context.Factory);
        var root = organizations.GetByCode("5500")!;
        var externalUnitId = organizations.AddUnit("EXTERNAL", "External", "", root.Id);

        using (var conn = context.Factory.CreateOpenConnection())
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "UPDATE Users SET ReferenceNodeId = @externalUnitId WHERE Id = @userId;";
            cmd.Parameters.AddWithValue("@externalUnitId", externalUnitId);
            cmd.Parameters.AddWithValue("@userId", userId);
            cmd.ExecuteNonQuery();
        }

        var access = new AuthorizationService(context.Factory).GetContext(userId);
        Assert.Empty(access.AuthorizedSectorIds);
    }

    private static int CreateUser(DatabaseTestContext context, string profile, string referenceCode, params string[] sectorCodes)
    {
        var userId = CreateUnconfiguredUser(context);
        var reference = new OrganizationRepository(context.Factory).GetByCode(referenceCode)!;
        var sectorIds = sectorCodes.Select(code => GetSectorId(context, code));
        new UserAccessRepository(context.Factory).Configure(userId, profile, reference.Id, sectorIds);
        return userId;
    }

    private static int CreateUnconfiguredUser(DatabaseTestContext context)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var codigoFJ = $"FJ{suffix}";
        var sectorId = GetSectorId(context, "DAD");

        using (var conn = context.Factory.CreateOpenConnection())
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                INSERT OR IGNORE INTO Shifts (Id, NamePt, NameJp) VALUES (1, 'Turno 1', '1');
                INSERT OR IGNORE INTO Groups (Id, NamePt, NameJp) VALUES (1, 'Grupo 1', '1');
                INSERT INTO Operators
                    (CodigoFJ, NameRomanji, NameNihongo, ShiftId, GroupId, SectorId, StartDate, Trainer, Status)
                VALUES
                    (@codigoFJ, @codigoFJ, @codigoFJ, 1, 1, @sectorId, date('now'), 0, 1);";
            cmd.Parameters.AddWithValue("@codigoFJ", codigoFJ);
            cmd.Parameters.AddWithValue("@sectorId", sectorId);
            cmd.ExecuteNonQuery();
        }

        var repository = new UserRepository(context.Factory);
        return repository.Add(new User
        {
            Login = codigoFJ,
            CodigoFJ = codigoFJ,
            Name = codigoFJ,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("test-password")
        });
    }

    private static int GetSectorId(DatabaseTestContext context, string code)
    {
        using var conn = context.Factory.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id FROM Sectors WHERE Code = @code COLLATE NOCASE;";
        cmd.Parameters.AddWithValue("@code", code);
        return Convert.ToInt32(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }
}
