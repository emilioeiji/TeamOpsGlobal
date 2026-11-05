using Dapper;
using Microsoft.Data.Sqlite;
using TeamOps.Config;
using TeamOps.Data.Db;
using Xunit;

namespace TeamOps.Tests;

public sealed class LegacyDatabaseMigrationTests
{
    [Fact]
    public void ExistingDatabaseWithoutV2ColumnsMigratesWithoutLosingLegacySectors()
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
                    CREATE TABLE Operators (
                        CodigoFJ TEXT PRIMARY KEY,
                        NameRomanji TEXT NOT NULL,
                        NameNihongo TEXT NOT NULL,
                        ShiftId INTEGER NOT NULL,
                        GroupId INTEGER NOT NULL,
                        SectorId INTEGER NOT NULL,
                        StartDate DATE NOT NULL,
                        Trainer INTEGER NOT NULL DEFAULT 0,
                        Status INTEGER NOT NULL DEFAULT 1
                    );

                    CREATE TABLE Sectors (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        NamePt TEXT NOT NULL,
                        NameJp TEXT NOT NULL
                    );

                    INSERT INTO Sectors (Id, NamePt, NameJp) VALUES
                        (1, 'G-Bareru', 'G-Bareru'),
                        (2, 'DAD', 'DAD');

                    INSERT INTO Operators
                        (CodigoFJ, NameRomanji, NameNihongo, ShiftId, GroupId, SectorId, StartDate, Trainer, Status)
                    VALUES
                        ('FJ-LEGACY', 'Legacy Operator', 'Legacy Operator', 1, 1, 1, '2020-01-01', 0, 1);
                ");
            }

            new DbInitializer(settings).EnsureCreated();
            new DbInitializer(settings).EnsureCreated();

            using var migrated = new SqliteConnection(settings.ConnectionString);
            migrated.Open();
            Assert.Equal(1, migrated.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM pragma_table_info('Operators') WHERE name = 'BadgeCode';"));
            Assert.Equal(1, migrated.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM pragma_table_info('Sectors') WHERE name = 'OrganizationNodeId';"));
            Assert.Equal("G-BARERU", migrated.ExecuteScalar<string>("SELECT Code FROM Sectors WHERE Id = 1;"));
            Assert.Equal("DAD", migrated.ExecuteScalar<string>("SELECT Code FROM Sectors WHERE Id = 2;"));
            Assert.Equal(1, migrated.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM Operators WHERE CodigoFJ = 'FJ-LEGACY' AND SectorId = 1;"));
            Assert.Equal(2, migrated.ExecuteScalar<int>(@"
                SELECT COUNT(*)
                FROM Sectors sector
                JOIN OrganizationNodes node ON node.Id = sector.OrganizationNodeId
                WHERE sector.Id IN (1, 2) AND node.Code = sector.Code COLLATE NOCASE;"));
            Assert.Equal(5, migrated.ExecuteScalar<int>("SELECT COUNT(*) FROM SchemaMigrations;"));
            Assert.Equal(1, migrated.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'UX_Sectors_Code';"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }
}
