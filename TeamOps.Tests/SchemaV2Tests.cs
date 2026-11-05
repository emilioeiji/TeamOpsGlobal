using TeamOps.Data.Repositories;
using Xunit;

namespace TeamOps.Tests;

public sealed class SchemaV2Tests
{
    [Fact]
    public void EmptyDatabase_IsCreatedAndMigrationsAreIdempotent()
    {
        using var context = new DatabaseTestContext();

        new TeamOps.Data.Db.DbInitializer(context.Settings).EnsureCreated();

        using var conn = context.Factory.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT group_concat(Version, ',') FROM (SELECT Version FROM SchemaMigrations ORDER BY Version);";
        Assert.Equal("1,2,3,4,5,6", cmd.ExecuteScalar()?.ToString());

        cmd.CommandText = "PRAGMA foreign_key_check;";
        using var violations = cmd.ExecuteReader();
        Assert.False(violations.Read());
    }

    [Fact]
    public void Tasks_RequireAnActiveSector()
    {
        using var context = new DatabaseTestContext();
        using var conn = context.Factory.CreateOpenConnection();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Tasks') WHERE name = 'SectorId';";
        Assert.Equal(1L, cmd.ExecuteScalar());

        cmd.CommandText = @"
            INSERT INTO Tasks
                (Description, DueDate, ShiftId, Status, CreatedByCodigoFJ, SectorId)
            VALUES
                ('Sem setor', date('now'), 1, 'pending', 'UNKNOWN', NULL);";
        var error = Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() => cmd.ExecuteNonQuery());
        Assert.Contains("Task requer um setor ativo", error.Message);
    }

    [Fact]
    public void InitialOrganization_HasUnitsSectorsAndProfiles()
    {
        using var context = new DatabaseTestContext();
        var repository = new OrganizationRepository(context.Factory);

        var root = repository.GetByCode("5500");
        var unit = repository.GetByCode("5552");
        var descendants = repository.GetDescendants(unit!.Id);

        Assert.NotNull(root);
        Assert.Equal(root!.Id, unit.ParentId);
        Assert.Equal(new[] { "APL", "DAD", "G-BARERU", "IAS" },
            descendants.Where(x => x.NodeType == TeamOps.Core.Common.OrganizationNodeType.Sector)
                .Select(x => x.Code).OrderBy(x => x).ToArray());

        using var conn = context.Factory.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM AccessProfiles;";
        Assert.Equal(6L, cmd.ExecuteScalar());
    }

    [Fact]
    public void UnitsAndSectors_CanBeCreatedWithoutCodeChanges()
    {
        using var context = new DatabaseTestContext();
        var repository = new OrganizationRepository(context.Factory);
        var root = repository.GetByCode("5500")!;

        var unitId = repository.AddUnit("5553", "Unidade 5553", "5553", root.Id);
        var sector = repository.AddSector("SETOR-A", "Setor A", "A", unitId);

        Assert.True(sector.Id > 0);
        Assert.Equal(unitId, repository.GetByCode("SETOR-A")!.ParentId);
        Assert.Contains(new SectorRepository(context.Factory).GetAll(), x => x.Code == "SETOR-A");
    }

    [Fact]
    public void OrganizationTree_RejectsCyclesAndSectorParents()
    {
        using var context = new DatabaseTestContext();
        var repository = new OrganizationRepository(context.Factory);
        var root = repository.GetByCode("5500")!;
        var unit = repository.GetByCode("5552")!;
        var dad = repository.GetByCode("DAD")!;

        Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() => repository.MoveUnit(root.Id, unit.Id));
        Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() => repository.AddUnit("INVALID", "Invalid", "", dad.Id));
    }
}
