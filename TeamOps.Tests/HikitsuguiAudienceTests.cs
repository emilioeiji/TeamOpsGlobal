using TeamOps.Core.Common;
using TeamOps.Core.Entities;
using TeamOps.Data.Repositories;
using TeamOps.Data.Security;
using Xunit;

namespace TeamOps.Tests;

public sealed class HikitsuguiAudienceTests
{
    [Fact]
    public void SectorAudience_IsVisibleOnlyInsideAssignedSector()
    {
        using var context = new DatabaseTestContext();
        var author = CreateUser(context, "GL", "5552", "DAD");
        var dadReader = CreateUser(context, "KL", "5552", "DAD");
        var otherReader = CreateUser(context, "KL", "5552", "G-BARERU");
        var messageId = CreateMessage(context, author);
        var service = new HikitsuguiAudienceService(context.Factory);

        service.SetSectorAudience(author, messageId, [SectorId(context, "DAD")]);

        Assert.True(service.CanView(dadReader, messageId));
        Assert.False(service.CanView(otherReader, messageId));
        Assert.Throws<UnauthorizedAccessException>(() => service.DemandCanView(otherReader, messageId));
    }

    [Fact]
    public void MultipleSectorAudience_IsVisibleForEachTargetButNotOthers()
    {
        using var context = new DatabaseTestContext();
        var author = CreateUser(context, "GL", "5552", "DAD", "G-BARERU");
        var dadReader = CreateUser(context, "OP", "5552", "DAD");
        var gBareruReader = CreateUser(context, "OP", "5552", "G-BARERU");
        var iasReader = CreateUser(context, "OP", "5552", "IAS");
        var messageId = CreateMessage(context, author);
        var service = new HikitsuguiAudienceService(context.Factory);

        service.SetSectorAudience(author, messageId,
            [SectorId(context, "DAD"), SectorId(context, "G-BARERU")]);

        Assert.True(service.CanView(dadReader, messageId));
        Assert.True(service.CanView(gBareruReader, messageId));
        Assert.False(service.CanView(iasReader, messageId));
    }

    [Fact]
    public void ProfileAudience_IsExplicitAndDoesNotInheritByHierarchy()
    {
        using var context = new DatabaseTestContext();
        var author = CreateUser(context, "GL", "5552", "DAD");
        var op = CreateUser(context, "OP", "5552", "DAD");
        var kl = CreateUser(context, "KL", "5552", "DAD");
        var gl = CreateUser(context, "GL", "5552", "DAD");
        var manager = CreateUser(context, "MANAGER", "5552");
        var supervisor = CreateUser(context, "SV", "5552");
        var director = CreateUser(context, "DIRECTOR", "5500");
        var messageId = CreateMessage(context, author);
        var service = new HikitsuguiAudienceService(context.Factory);

        service.SetSectorAudience(author, messageId, [SectorId(context, "DAD")]);
        service.SetProfileAudience(author, messageId, ["OP", "GL"]);

        Assert.True(service.CanView(op, messageId));
        Assert.True(service.CanView(gl, messageId));
        Assert.False(service.CanView(kl, messageId));
        Assert.False(service.CanView(manager, messageId));
        Assert.False(service.CanView(supervisor, messageId));
        Assert.False(service.CanView(director, messageId));
        Assert.Throws<UnauthorizedAccessException>(() => service.DemandCanView(manager, messageId));
    }

    [Fact]
    public void GlOnlyAudience_IsNotAutomaticallyVisibleToManager()
    {
        using var context = new DatabaseTestContext();
        var gl = CreateUser(context, "GL", "5552", "DAD");
        var manager = CreateUser(context, "MANAGER", "5552");
        var messageId = CreateMessage(context, gl);
        var service = new HikitsuguiAudienceService(context.Factory);

        service.SetSectorAudience(gl, messageId, [SectorId(context, "DAD")]);
        service.SetProfileAudience(gl, messageId, ["GL"]);

        Assert.True(service.CanView(gl, messageId));
        Assert.False(service.CanView(manager, messageId));
    }

    [Fact]
    public void GlAndManagerAudience_IsVisibleOnlyToThoseExplicitProfiles()
    {
        using var context = new DatabaseTestContext();
        var author = CreateUser(context, "GL", "5552", "DAD");
        var op = CreateUser(context, "OP", "5552", "DAD");
        var gl = CreateUser(context, "GL", "5552", "DAD");
        var manager = CreateUser(context, "MANAGER", "5552");
        var director = CreateUser(context, "DIRECTOR", "5500");
        var messageId = CreateMessage(context, author);
        var service = new HikitsuguiAudienceService(context.Factory);

        service.SetSectorAudience(author, messageId, [SectorId(context, "DAD")]);
        service.SetProfileAudience(author, messageId, ["GL", "MA"]);

        Assert.False(service.CanView(op, messageId));
        Assert.True(service.CanView(gl, messageId));
        Assert.True(service.CanView(manager, messageId));
        Assert.False(service.CanView(director, messageId));
    }

    [Fact]
    public void OrganizationAudience_RequiresPublisherToControlEveryDescendantSector()
    {
        using var context = new DatabaseTestContext();
        var manager = CreateUser(context, "MANAGER", "5552");
        var gl = CreateUser(context, "GL", "5552", "DAD");
        var iasReader = CreateUser(context, "OP", "5552", "IAS");
        var unitId = new OrganizationRepository(context.Factory).GetByCode("5552")!.Id;
        var managerMessage = CreateMessage(context, manager);
        var glMessage = CreateMessage(context, gl);
        var service = new HikitsuguiAudienceService(context.Factory);

        service.SetOrganizationAudience(manager, managerMessage, unitId);

        Assert.True(service.CanView(iasReader, managerMessage));
        Assert.Throws<UnauthorizedAccessException>(() =>
            service.SetOrganizationAudience(gl, glMessage, unitId));
    }

    [Fact]
    public void OrganizationAudienceOptions_ListOnlyUnitsFullyControlledByPublisher()
    {
        using var context = new DatabaseTestContext();
        var director = CreateUser(context, "DIRECTOR", "5500");
        var manager = CreateUser(context, "MANAGER", "5552");
        var gl = CreateUser(context, "GL", "5552", "DAD");
        var service = new HikitsuguiAudienceService(context.Factory);

        Assert.Contains(service.GetTargetableOrganizationUnits(director), unit => unit.Code == "5500");
        Assert.Contains(service.GetTargetableOrganizationUnits(director), unit => unit.Code == "5552");
        Assert.Contains(service.GetTargetableOrganizationUnits(manager), unit => unit.Code == "5552");
        Assert.DoesNotContain(service.GetTargetableOrganizationUnits(manager), unit => unit.Code == "5500");
        Assert.Empty(service.GetTargetableOrganizationUnits(gl));
    }

    [Fact]
    public void GeneralAudience_CanBePublishedOnlyByOrganizationWideUser()
    {
        using var context = new DatabaseTestContext();
        var director = CreateUser(context, "DIRECTOR", "5500");
        var gl = CreateUser(context, "GL", "5552", "DAD");
        var operatorUser = CreateUser(context, "OP", "5552", "APL");
        var generalMessage = CreateMessage(context, director);
        var unauthorizedMessage = CreateMessage(context, gl);
        var service = new HikitsuguiAudienceService(context.Factory);

        using (var conn = context.Factory.CreateOpenConnection())
        using (var rename = conn.CreateCommand())
        {
            rename.CommandText = "UPDATE OrganizationNodes SET Code = 'CORPORATE' WHERE ParentId IS NULL;";
            rename.ExecuteNonQuery();
        }

        service.SetGeneralAudience(director, generalMessage);

        Assert.True(service.CanView(operatorUser, generalMessage));
        Assert.True(service.IsTargetedToSector(generalMessage, SectorId(context, "APL")));
        Assert.Throws<UnauthorizedAccessException>(() => service.SetGeneralAudience(gl, unauthorizedMessage));
    }

    [Fact]
    public void MessageWithoutAudience_IsDeniedEvenWhenIdExists()
    {
        using var context = new DatabaseTestContext();
        var user = CreateUser(context, "GL", "5552", "DAD");
        var messageId = CreateMessage(context, user);

        Assert.False(new HikitsuguiAudienceService(context.Factory).CanView(user, messageId));
    }

    [Fact]
    public void OrganizationAudience_ResolvesSectorFiltersFromTheTree()
    {
        using var context = new DatabaseTestContext();
        var manager = CreateUser(context, "MANAGER", "5552");
        var messageId = CreateMessage(context, manager);
        var unitId = new OrganizationRepository(context.Factory).GetByCode("5552")!.Id;
        var service = new HikitsuguiAudienceService(context.Factory);

        service.SetOrganizationAudience(manager, messageId, unitId);

        Assert.True(service.IsTargetedToSector(messageId, SectorId(context, "DAD")));
        Assert.True(service.IsTargetedToSector(messageId, SectorId(context, "IAS")));
    }

    [Fact]
    public void Delete_RequiresDeletePermissionAcrossTheWholeAudience()
    {
        using var context = new DatabaseTestContext();
        var author = CreateUser(context, "GL", "5552", "DAD");
        var operatorUser = CreateUser(context, "OP", "5552", "DAD");
        var messageId = CreateMessage(context, author);
        var service = new HikitsuguiAudienceService(context.Factory);
        service.SetSectorAudience(author, messageId, [SectorId(context, "DAD")]);

        service.DemandCanDelete(author, messageId);
        Assert.Throws<UnauthorizedAccessException>(() => service.DemandCanDelete(operatorUser, messageId));
    }

    [Fact]
    public void OperatorQuery_UsesExplicitAudienceInsteadOfSpecialLocalCodes()
    {
        using var context = new DatabaseTestContext();
        var dadUser = CreateUser(context, "OP", "5552", "DAD");
        var dadAuthor = CreateUser(context, "GL", "5552", "DAD");
        var otherAuthor = CreateUser(context, "GL", "5552", "G-BARERU");
        var dadMessage = CreateMessage(context, dadAuthor);
        var otherMessage = CreateMessage(context, otherAuthor);
        var localId = CreateLocal(context, "DAD");
        SetOperatorMessage(context, dadMessage, localId);
        SetOperatorMessage(context, otherMessage, localId);
        var service = new HikitsuguiAudienceService(context.Factory);
        service.SetSectorAudience(dadAuthor, dadMessage, [SectorId(context, "DAD")]);
        service.SetSectorAudience(otherAuthor, otherMessage, [SectorId(context, "G-BARERU")]);

        var rows = new HikitsuguiRepository(context.Factory)
            .GetForOperator(DateTime.Today.AddDays(-1), DateTime.Today.AddDays(1), dadUser, localId);

        Assert.Single(rows);
        Assert.Equal(dadMessage, rows[0].Id);
    }

    private static int CreateUser(DatabaseTestContext context, string profile, string referenceCode, params string[] sectors)
    {
        var organization = new OrganizationRepository(context.Factory);
        var reference = organization.GetByCode(referenceCode)!;
        var primarySectorId = SectorId(context, sectors.FirstOrDefault() ?? "DAD");
        var codigoFJ = $"FJ{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        using (var conn = context.Factory.CreateOpenConnection())
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                INSERT OR IGNORE INTO Shifts (Id, NamePt, NameJp) VALUES (1, 'Turno 1', '1');
                INSERT OR IGNORE INTO Groups (Id, NamePt, NameJp) VALUES (1, 'Grupo 1', '1');
                INSERT INTO Operators
                    (CodigoFJ, NameRomanji, NameNihongo, ShiftId, GroupId, SectorId, StartDate, Trainer, Status)
                VALUES (@fj, @fj, @fj, 1, 1, @sectorId, date('now'), 0, 1);";
            cmd.Parameters.AddWithValue("@fj", codigoFJ);
            cmd.Parameters.AddWithValue("@sectorId", primarySectorId);
            cmd.ExecuteNonQuery();
        }

        var userId = new UserRepository(context.Factory).Add(new User
        {
            Login = codigoFJ,
            CodigoFJ = codigoFJ,
            Name = codigoFJ,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("test-password")
        });
        new UserAccessRepository(context.Factory).Configure(
            userId,
            profile,
            reference.Id,
            sectors.Select(code => SectorId(context, code)));
        return userId;
    }

    private static int CreateMessage(DatabaseTestContext context, int userId)
    {
        using var conn = context.Factory.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT OR IGNORE INTO Categories (Id, NamePt, NameJp) VALUES (1, 'Geral', '一般');
            INSERT INTO Hikitsugui
                (Date, ShiftId, CreatorCodigoFJ, CategoryId, SectorId, Description)
            SELECT CURRENT_TIMESTAMP, 1, CodigoFJ, 1, NULL, 'Test message'
            FROM Users WHERE Id = @userId;
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@userId", userId);
        return checked((int)(long)cmd.ExecuteScalar()!);
    }

    private static int SectorId(DatabaseTestContext context, string code)
    {
        using var conn = context.Factory.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id FROM Sectors WHERE Code = @code COLLATE NOCASE;";
        cmd.Parameters.AddWithValue("@code", code);
        return Convert.ToInt32(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static int CreateLocal(DatabaseTestContext context, string sectorCode)
    {
        var sectorId = SectorId(context, sectorCode);
        using var conn = context.Factory.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Locals (NamePt, NameJp, SectorId)
            VALUES ('Local de teste', 'Test', @sectorId);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@sectorId", sectorId);
        return checked((int)(long)cmd.ExecuteScalar()!);
    }

    private static void SetOperatorMessage(DatabaseTestContext context, int messageId, int localId)
    {
        using var conn = context.Factory.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE Hikitsugui
            SET LocalId = @localId
            WHERE Id = @messageId;";
        cmd.Parameters.AddWithValue("@localId", localId);
        cmd.Parameters.AddWithValue("@messageId", messageId);
        cmd.ExecuteNonQuery();
    }
}
