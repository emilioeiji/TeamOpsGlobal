using Dapper;
using TeamOps.Data.Repositories;
using TeamOps.Data.Security;
using Xunit;

namespace TeamOps.Tests;

public sealed class LeaderAssignmentTests
{
    private const string StrongPassword = "TeamOps#2026!Secure";

    [Fact]
    public void Director_AssignsAndRemovesOperatorFromLeaderInSameSector()
    {
        using var context = new DatabaseTestContext();
        var directorId = ProvisionDirector(context);
        var dadId = SectorId(context, "DAD");
        AddOperator(context, "FJ-LEADER", dadId, isLeader: true);
        AddOperator(context, "FJ-OPERATOR", dadId);
        var repository = new LeaderAssignmentRepository(context.Factory);

        Assert.Contains(repository.GetLeaders(directorId), item => item.CodigoFJ == "FJ-LEADER");
        Assert.Contains(repository.GetAvailable(directorId, "FJ-LEADER"),
            item => item.CodigoFJ == "FJ-OPERATOR");

        repository.Add(directorId, "FJ-LEADER", "FJ-OPERATOR");

        Assert.Contains(repository.GetAssigned(directorId, "FJ-LEADER"),
            item => item.CodigoFJ == "FJ-OPERATOR");
        Assert.DoesNotContain(repository.GetAvailable(directorId, "FJ-LEADER"),
            item => item.CodigoFJ == "FJ-OPERATOR");

        repository.Remove(directorId, "FJ-LEADER", "FJ-OPERATOR");

        Assert.Empty(repository.GetAssigned(directorId, "FJ-LEADER"));
        using var conn = context.Factory.CreateOpenConnection();
        Assert.Equal(2, conn.ExecuteScalar<int>(@"
            SELECT COUNT(*) FROM SecurityAudit
            WHERE ActorUserId = @directorId AND TargetType = 'LeaderAssignment';",
            new { directorId }));
    }

    [Fact]
    public void Assignment_RejectsOperatorFromAnotherSector()
    {
        using var context = new DatabaseTestContext();
        var directorId = ProvisionDirector(context);
        AddOperator(context, "FJ-DAD-LEADER", SectorId(context, "DAD"), isLeader: true);
        AddOperator(context, "FJ-GB-OPERATOR", SectorId(context, "G-BARERU"));

        var exception = Assert.Throws<UnauthorizedAccessException>(() =>
            new LeaderAssignmentRepository(context.Factory)
                .Add(directorId, "FJ-DAD-LEADER", "FJ-GB-OPERATOR"));

        Assert.Contains("mesmo setor", exception.Message);
    }

    [Fact]
    public void NonLeaderOperator_CannotReceiveAssignments()
    {
        using var context = new DatabaseTestContext();
        var directorId = ProvisionDirector(context);
        var dadId = SectorId(context, "DAD");
        AddOperator(context, "FJ-NOT-LEADER", dadId);
        AddOperator(context, "FJ-OPERATOR", dadId);

        Assert.Throws<UnauthorizedAccessException>(() =>
            new LeaderAssignmentRepository(context.Factory)
                .Add(directorId, "FJ-NOT-LEADER", "FJ-OPERATOR"));
    }

    private static int ProvisionDirector(DatabaseTestContext context) =>
        new FirstRunProvisioningService(context.Factory)
            .ProvisionDirector("initial.director", "Initial Director", StrongPassword);

    private static int SectorId(DatabaseTestContext context, string code)
    {
        using var conn = context.Factory.CreateOpenConnection();
        return conn.ExecuteScalar<int>(
            "SELECT Id FROM Sectors WHERE Code = @code;", new { code });
    }

    private static void AddOperator(DatabaseTestContext context, string codigoFJ, int sectorId,
        bool isLeader = false)
    {
        using var conn = context.Factory.CreateOpenConnection();
        conn.Execute(@"
            INSERT OR IGNORE INTO Shifts (Id, NamePt, NameJp) VALUES (1, 'Turno 1', '1');
            INSERT OR IGNORE INTO Groups (Id, NamePt, NameJp) VALUES (1, 'Grupo 1', '1');
            INSERT INTO Operators
                (CodigoFJ, NameRomanji, NameNihongo, ShiftId, GroupId, SectorId,
                 StartDate, Trainer, Status, IsLeader)
            VALUES
                (@codigoFJ, @codigoFJ, @codigoFJ, 1, 1, @sectorId,
                 date('now'), 0, 1, @isLeader);",
            new { codigoFJ, sectorId, isLeader });
    }
}
