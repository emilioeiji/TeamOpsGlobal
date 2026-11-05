using TeamOps.Core.Common;
using TeamOps.Core.Entities;
using TeamOps.Core.Security;
using TeamOps.Data.Repositories;
using TeamOps.Data.Security;
using Xunit;

namespace TeamOps.Tests;

public sealed class PaidLeaveAuthorizationTests
{
    [Fact]
    public void RequestIdManipulation_CannotCrossAssignedSector()
    {
        using var context = new DatabaseTestContext();
        var dadOperator = CreateOperator(context, "FJ-DAD", "DAD");
        var iasOperator = CreateOperator(context, "FJ-IAS", "IAS");
        var dadUser = CreateUser(context, "GL", "5552", dadOperator, "DAD");
        var dadRequest = CreateRequest(context, dadOperator);
        var iasRequest = CreateRequest(context, iasOperator);
        var authorization = new PaidLeaveAuthorizationService(context.Factory);

        authorization.DemandRequest(dadUser, PermissionActions.View, dadRequest);
        Assert.Throws<UnauthorizedAccessException>(() =>
            authorization.DemandRequest(dadUser, PermissionActions.View, iasRequest));
        Assert.Throws<UnauthorizedAccessException>(() =>
            authorization.DemandRequest(dadUser, PermissionActions.View, int.MaxValue));
    }

    [Fact]
    public void CreateAndMove_RequireEveryTargetOperatorToBeInScope()
    {
        using var context = new DatabaseTestContext();
        var dadOperator = CreateOperator(context, "FJ-DAD", "DAD");
        var iasOperator = CreateOperator(context, "FJ-IAS", "IAS");
        var dadUser = CreateUser(context, "KL", "5552", dadOperator, "DAD");
        var requestId = CreateRequest(context, dadOperator);
        var authorization = new PaidLeaveAuthorizationService(context.Factory);

        authorization.DemandOperator(dadUser, PermissionActions.Create, dadOperator);
        authorization.DemandRequestMove(dadUser, PermissionActions.Edit, requestId, dadOperator);
        Assert.Throws<UnauthorizedAccessException>(() =>
            authorization.DemandOperator(dadUser, PermissionActions.Create, iasOperator));
        Assert.Throws<UnauthorizedAccessException>(() =>
            authorization.DemandRequestMove(dadUser, PermissionActions.Edit, requestId, iasOperator));
    }

    [Fact]
    public void Delete_RequiresFunctionalPermissionInAdditionToSectorScope()
    {
        using var context = new DatabaseTestContext();
        var dadOperator = CreateOperator(context, "FJ-DAD", "DAD");
        var klUser = CreateUser(context, "KL", "5552", dadOperator, "DAD");
        var glUser = CreateUser(context, "GL", "5552", "FJ-GL", "DAD");
        var requestId = CreateRequest(context, dadOperator);
        var authorization = new PaidLeaveAuthorizationService(context.Factory);

        Assert.Throws<UnauthorizedAccessException>(() =>
            authorization.DemandRequest(klUser, PermissionActions.Delete, requestId));
        authorization.DemandRequest(glUser, PermissionActions.Delete, requestId);
    }

    private static string CreateOperator(DatabaseTestContext context, string codigoFJ, string sectorCode)
    {
        var sectorId = GetSectorId(context, sectorCode);
        using var conn = context.Factory.CreateOpenConnection();
        conn.ExecuteNonQuery(@"
            INSERT OR IGNORE INTO Shifts (Id, NamePt, NameJp) VALUES (1, 'Turno 1', '1');
            INSERT OR IGNORE INTO Groups (Id, NamePt, NameJp) VALUES (1, 'Grupo 1', '1');");
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Operators
                (CodigoFJ, NameRomanji, NameNihongo, ShiftId, GroupId, SectorId, StartDate, Trainer, Status)
            VALUES
                (@codigoFJ, @codigoFJ, @codigoFJ, 1, 1, @sectorId, date('now'), 0, 1);";
        cmd.Parameters.AddWithValue("@codigoFJ", codigoFJ);
        cmd.Parameters.AddWithValue("@sectorId", sectorId);
        cmd.ExecuteNonQuery();
        return codigoFJ;
    }

    private static int CreateUser(
        DatabaseTestContext context,
        string profile,
        string referenceCode,
        string codigoFJ,
        params string[] sectorCodes)
    {
        using (var conn = context.Factory.CreateOpenConnection())
        using (var exists = conn.CreateCommand())
        {
            exists.CommandText = "SELECT COUNT(*) FROM Operators WHERE CodigoFJ = @codigoFJ;";
            exists.Parameters.AddWithValue("@codigoFJ", codigoFJ);
            if ((long)exists.ExecuteScalar()! == 0)
                CreateOperator(context, codigoFJ, sectorCodes[0]);
        }

        var userId = new UserRepository(context.Factory).Add(new User
        {
            Login = codigoFJ,
            CodigoFJ = codigoFJ,
            Name = codigoFJ,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("test-password"),
            AccessLevel = AccessLevel.Basic
        });
        var reference = new OrganizationRepository(context.Factory).GetByCode(referenceCode)!;
        var sectorIds = sectorCodes.Select(code => GetSectorId(context, code));
        new UserAccessRepository(context.Factory).Configure(userId, profile, reference.Id, sectorIds);
        return userId;
    }

    private static int CreateRequest(DatabaseTestContext context, string codigoFJ)
    {
        using var conn = context.Factory.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO AcompYukyu
                (OperatorCodigoFJ, RequestDate, AuthorizedByCodigoFJ, Notes, TodokeMotivoId)
            VALUES
                (@codigoFJ, date('now'), @codigoFJ, '', 1);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@codigoFJ", codigoFJ);
        return Convert.ToInt32((long)cmd.ExecuteScalar()!);
    }

    private static int GetSectorId(DatabaseTestContext context, string code)
    {
        using var conn = context.Factory.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id FROM Sectors WHERE Code = @code COLLATE NOCASE;";
        cmd.Parameters.AddWithValue("@code", code);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }
}

internal static class SqliteTestExtensions
{
    public static void ExecuteNonQuery(this Microsoft.Data.Sqlite.SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
