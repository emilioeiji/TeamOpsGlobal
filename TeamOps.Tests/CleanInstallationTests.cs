using Dapper;
using Microsoft.Data.Sqlite;
using TeamOps.Config;
using TeamOps.Data.Db;
using Xunit;

namespace TeamOps.Tests;

public sealed class CleanInstallationTests
{
    [Fact]
    public void LegacyDatabaseWithoutV2HistoryIsRejectedWithoutModification()
    {
        var directory = Path.Combine(Path.GetTempPath(), "TeamOps.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "legacy.db");

        try
        {
            var settings = new DbSettings(databasePath);
            using (var connection = new SqliteConnection(settings.ConnectionString))
            {
                connection.Open();
                connection.Execute(@"
                    CREATE TABLE Sectors (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        NamePt TEXT NOT NULL,
                        NameJp TEXT NOT NULL
                    );
                    INSERT INTO Sectors (NamePt, NameJp) VALUES ('Legacy', 'Legacy');");
            }

            var error = Assert.Throws<InvalidOperationException>(() =>
                new DbInitializer(settings).EnsureCreated());

            Assert.Contains("instalacao limpa", error.Message, StringComparison.OrdinalIgnoreCase);
            using var unchanged = new SqliteConnection(settings.ConnectionString);
            unchanged.Open();
            Assert.Equal(1, unchanged.ExecuteScalar<int>("SELECT COUNT(*) FROM Sectors;"));
            Assert.Equal(0, unchanged.ExecuteScalar<int>(@"
                SELECT COUNT(*) FROM sqlite_master
                WHERE type = 'table' AND name = 'SchemaMigrations';"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void EmptyDatabaseCreatesOnlyTheVersionedSchema()
    {
        using var context = new DatabaseTestContext();
        using var conn = context.Factory.CreateOpenConnection();

        Assert.Equal(10, conn.ExecuteScalar<int>("SELECT COUNT(*) FROM SchemaMigrations;"));
        Assert.Equal(0, conn.ExecuteScalar<int>(@"
            SELECT COUNT(*) FROM sqlite_master
            WHERE type = 'table' AND name IN ('GroupLeaders', 'Assignments');"));
        Assert.Equal(0, conn.ExecuteScalar<int>(@"
            SELECT COUNT(*) FROM pragma_table_info('Users')
            WHERE name = 'AccessLevel';"));
        Assert.Equal(0, conn.ExecuteScalar<int>(@"
            SELECT COUNT(*) FROM pragma_table_info('Hikitsugui')
            WHERE name IN ('ForLeaders', 'ForOperators', 'ForMaSv');"));
        Assert.Equal(4, conn.ExecuteScalar<int>(@"
            SELECT COUNT(*) FROM sqlite_master
            WHERE type = 'table'
              AND name IN ('HaidaiAssignments', 'HaidaiMovements', 'MasterCards', 'MasterCardStatusHistory');"));
        Assert.Equal(2, conn.ExecuteScalar<int>(@"
            SELECT COUNT(*) FROM sqlite_master
            WHERE type = 'table'
              AND name IN ('HaidaiSharedSectorGroups', 'HaidaiSharedSectorMembers');"));
        Assert.Equal(1, conn.ExecuteScalar<int>(@"
            SELECT COUNT(*) FROM sqlite_master
            WHERE type = 'table' AND name = 'SectorCapabilities';"));
        Assert.Equal(4, conn.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM SectorCapabilities WHERE IsActive = 1;"));
        Assert.Equal(1, conn.ExecuteScalar<int>(@"
            SELECT COUNT(*) FROM pragma_table_info('Locals')
            WHERE name = 'ShortCode';"));
        Assert.Equal(2, conn.ExecuteScalar<int>(@"
            SELECT COUNT(*) FROM sqlite_master
            WHERE type = 'table'
              AND name IN ('ProductionPlanSnapshots', 'ProductionPlanRows');"));
        Assert.Equal(3, conn.ExecuteScalar<int>(@"
            SELECT COUNT(*) FROM pragma_table_info('Ec2MachineCurrentState')
            WHERE name IN ('ImportId', 'SourceType', 'IsStale');"));
        Assert.Equal(7, conn.ExecuteScalar<int>(@"
            SELECT COUNT(*) FROM MachineStatuses status
            JOIN Sectors sector ON sector.Id = status.SectorId
            WHERE sector.Code = 'DAD';"));
    }

    [Fact]
    public void InaccessibleDatabaseDirectoryReturnsActionableMessage()
    {
        var directory = Path.Combine(Path.GetTempPath(), "TeamOps.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var blockingFile = Path.Combine(directory, "not-a-directory");
        File.WriteAllText(blockingFile, "block");

        try
        {
            var settings = new DbSettings(Path.Combine(blockingFile, "teamops.db"));

            var error = Assert.Throws<InvalidOperationException>(() =>
                new DbInitializer(settings).EnsureCreated());

            Assert.Contains("pasta local do banco SQLite", error.Message);
            Assert.Contains("permissoes de leitura e escrita", error.Message);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }
}
