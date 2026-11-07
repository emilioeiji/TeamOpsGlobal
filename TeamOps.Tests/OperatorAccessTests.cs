using TeamOps.Core.Entities;
using TeamOps.Data.Repositories;
using TeamOps.Data.Security;
using Xunit;

namespace TeamOps.Tests;

public sealed class OperatorAccessTests
{
    private const string Password = "Operador1";

    [Fact]
    public void RegisteringOperator_ProvisionsInternalIdentityWithoutPasswordInput()
    {
        using var context = new DatabaseTestContext();
        var dad = SectorId(context, "DAD");
        CreateOperator(context, "FJ90001", dad);

        var userId = new OperatorAccessService(context.Factory).ProvisionOrUpdate(
            "FJ90001", "Operador Teste", dad, [dad], "Regular", null, true);

        var authentication = new AuthenticationService(context.Factory);
        Assert.False(authentication.Authenticate("FJ90001", "errada").Succeeded);
        Assert.True(new OperatorAccessService(context.Factory).HasIdentity("FJ90001"));

        var access = new AuthorizationService(context.Factory).GetContext(userId);
        Assert.Equal("OP", access.ProfileCode);
        Assert.Equal([dad], access.AuthorizedSectorIds.OrderBy(id => id));
    }

    [Fact]
    public void ReliefOperator_HasOnlyExplicitSectors_AndRemovalRevokesAccess()
    {
        using var context = new DatabaseTestContext();
        var dad = SectorId(context, "DAD");
        var ias = SectorId(context, "IAS");
        CreateOperator(context, "FJ90002", dad);
        var service = new OperatorAccessService(context.Factory);

        var userId = service.ProvisionOrUpdate(
            "FJ90002", "Operador Relief", dad, [dad, ias], "Relief", null, true);

        Assert.Equal(2, service.GetSectors("FJ90002").Count);
        Assert.Contains(ias, new AuthorizationService(context.Factory).GetContext(userId).AuthorizedSectorIds);
        var iasMessage = CreateMessage(context, "FJ90002", ias);
        var audience = new HikitsuguiAudienceService(context.Factory);
        audience.SetSectorAudience(userId, iasMessage, [ias]);
        audience.SetProfileAudience(userId, iasMessage, ["OP"]);
        Assert.True(audience.CanViewForOperator(userId, "FJ90002", iasMessage));

        service.ProvisionOrUpdate(
            "FJ90002", "Operador Relief", dad, [dad], "Regular", null, true);

        Assert.Single(service.GetSectors("FJ90002"));
        Assert.DoesNotContain(ias, new AuthorizationService(context.Factory).GetContext(userId).AuthorizedSectorIds);
        Assert.False(audience.CanViewForOperator(userId, "FJ90002", iasMessage));
        Assert.Throws<UnauthorizedAccessException>(() =>
            audience.DemandCanViewForOperator(userId, "FJ90002", iasMessage));
    }

    [Fact]
    public void ReliefOperator_RequiresAtLeastTwoSectors()
    {
        using var context = new DatabaseTestContext();
        var dad = SectorId(context, "DAD");
        CreateOperator(context, "FJ90003", dad);

        Assert.Throws<InvalidOperationException>(() =>
            new OperatorAccessService(context.Factory).ProvisionOrUpdate(
                "FJ90003", "Relief Invalido", dad, [dad], "Relief", Password, true));
    }

    private static void CreateOperator(DatabaseTestContext context, string fj, int sectorId)
    {
        using (var conn = context.Factory.CreateOpenConnection())
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                INSERT OR IGNORE INTO Shifts (Id, NamePt, NameJp) VALUES (1, 'Turno 1', '1');
                INSERT OR IGNORE INTO Groups (Id, NamePt, NameJp) VALUES (1, 'Grupo 1', '1');";
            cmd.ExecuteNonQuery();
        }

        new OperatorRepository(context.Factory).Add(new Operator
        {
            CodigoFJ = fj,
            NameRomanji = fj,
            NameNihongo = fj,
            ShiftId = 1,
            GroupId = 1,
            SectorId = sectorId,
            StartDate = DateTime.Today,
            Status = true
        });
    }

    private static int SectorId(DatabaseTestContext context, string code)
    {
        using var conn = context.Factory.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id FROM Sectors WHERE Code = @code COLLATE NOCASE;";
        cmd.Parameters.AddWithValue("@code", code);
        return Convert.ToInt32(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static int CreateMessage(DatabaseTestContext context, string creatorFj, int sectorId)
    {
        using var conn = context.Factory.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT OR IGNORE INTO Categories (Id, NamePt, NameJp) VALUES (1, 'Geral', '一般');
            INSERT INTO Hikitsugui
                (Date, ShiftId, CreatorCodigoFJ, CategoryId, SectorId, Description)
            VALUES (CURRENT_TIMESTAMP, 1, @fj, 1, @sectorId, 'Teste de audiencia');
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@fj", creatorFj);
        cmd.Parameters.AddWithValue("@sectorId", sectorId);
        return checked((int)(long)cmd.ExecuteScalar()!);
    }
}
