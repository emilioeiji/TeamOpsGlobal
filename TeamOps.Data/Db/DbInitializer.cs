// Project: TeamOps.Data
// File: Db/DbInitializer.cs
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using TeamOps.Config;

namespace TeamOps.Data.Db
{
    public sealed class DbInitializer
    {
        private readonly DbSettings _settings;

        public DbInitializer(DbSettings settings)
        {
            _settings = settings;
        }

        public void EnsureCreated()
        {
            var dbPath = _settings.DatabasePath;
            var dbDir = Path.GetDirectoryName(dbPath)!;
            try
            {
                Directory.CreateDirectory(dbDir);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                throw new InvalidOperationException(
                    $"Nao foi possivel acessar a pasta local do banco SQLite: {dbDir}. " +
                    "Verifique o caminho e as permissoes de leitura e escrita.", ex);
            }

            using var conn = new SqliteConnection(_settings.ConnectionString);
            conn.Open();

            using (var pragma = conn.CreateCommand())
            {
                pragma.CommandText = "PRAGMA foreign_keys = OFF; PRAGMA busy_timeout = 30000;";
                pragma.ExecuteNonQuery();
            }

            DemandCleanOrVersionedDatabase(conn);
            EnsureMigrationTable(conn);

            foreach (var migration in GetMigrations())
                ApplyMigration(conn, migration);

            using (var constraints = conn.CreateCommand())
            {
                constraints.CommandText = "PRAGMA foreign_keys = ON;";
                constraints.ExecuteNonQuery();
            }

            using (var check = conn.CreateCommand())
            {
                check.CommandText = "PRAGMA foreign_key_check;";
                using var violations = check.ExecuteReader();
                if (violations.Read())
                    throw new InvalidOperationException(
                        $"Schema V2 invalido: violacao de chave estrangeira em {violations.GetString(0)}.");
            }

        }

        private static void EnsureMigrationTable(SqliteConnection conn)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS SchemaMigrations (
                    Version INTEGER PRIMARY KEY,
                    Name TEXT NOT NULL UNIQUE,
                    AppliedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                );";
            cmd.ExecuteNonQuery();
        }

        private static IReadOnlyList<MigrationDefinition> GetMigrations() =>
        [
            new(1, "Initial operational schema", "TeamOps.Data.Migrations.InitialSchema.sql"),
            new(2, "Organization hierarchy", "TeamOps.Data.Migrations.V2_0002_Organization.sql"),
            new(3, "Identity and authorization", "TeamOps.Data.Migrations.V2_0003_IdentityAuthorization.sql"),
            new(4, "Hikitsugui audiences", "TeamOps.Data.Migrations.V2_0004_HikitsuguiAudiences.sql"),
            new(5, "Tasks sector scope", "TeamOps.Data.Migrations.V2_0005_TasksSectorScope.sql"),
            new(6, "Paid leave permission", "TeamOps.Data.Migrations.V2_0006_PaidLeavePermission.sql"),
            new(7, "Feature module schemas", "TeamOps.Data.Migrations.V2_0007_FeatureModules.sql"),
            new(8, "Production monitor schema", "TeamOps.Data.Migrations.V2_0008_ProductionMonitor.sql"),
            new(9, "Haidai sector topology", "TeamOps.Data.Migrations.V2_0009_HaidaiSectorTopology.sql"),
            new(10, "Sector capabilities", "TeamOps.Data.Migrations.V2_0010_SectorCapabilities.sql"),
            new(11, "Security integrity", "TeamOps.Data.Migrations.V2_0011_SecurityIntegrity.sql"),
            new(12, "Dynamic Haidai display", "TeamOps.Data.Migrations.V2_0012_HaidaiDynamicDisplay.sql")
        ];

        private static void DemandCleanOrVersionedDatabase(SqliteConnection conn)
        {
            using var migrationTable = conn.CreateCommand();
            migrationTable.CommandText = @"
                SELECT COUNT(*) FROM sqlite_master
                WHERE type = 'table' AND name = 'SchemaMigrations' COLLATE NOCASE;";
            if ((long)migrationTable.ExecuteScalar()! > 0)
                return;

            using var applicationTables = conn.CreateCommand();
            applicationTables.CommandText = @"
                SELECT COUNT(*) FROM sqlite_master
                WHERE type = 'table'
                  AND name NOT LIKE 'sqlite_%';";
            if ((long)applicationTables.ExecuteScalar()! > 0)
                throw new InvalidOperationException(
                    "Banco anterior ao Schema V2 detectado. Esta versao requer uma instalacao limpa. " +
                    "Configure um arquivo SQLite novo ou remova o banco antigo apos realizar o backup necessario.");
        }

        private static void ApplyMigration(SqliteConnection conn, MigrationDefinition migration)
        {
            using var check = conn.CreateCommand();
            check.CommandText = "SELECT COUNT(*) FROM SchemaMigrations WHERE Version = @version;";
            check.Parameters.AddWithValue("@version", migration.Version);
            if ((long)check.ExecuteScalar()! > 0)
                return;

            var assembly = typeof(DbInitializer).Assembly;
            using var stream = assembly.GetManifestResourceStream(migration.ResourceName)
                ?? throw new InvalidOperationException($"Migration incorporada nao encontrada: {migration.ResourceName}");
            using var reader = new StreamReader(stream);
            var sql = reader.ReadToEnd();

            using var transaction = conn.BeginTransaction();

            using var cmd = conn.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();

            using var record = conn.CreateCommand();
            record.Transaction = transaction;
            record.CommandText = @"
                INSERT INTO SchemaMigrations (Version, Name)
                VALUES (@version, @name);";
            record.Parameters.AddWithValue("@version", migration.Version);
            record.Parameters.AddWithValue("@name", migration.Name);
            record.ExecuteNonQuery();

            transaction.Commit();
        }

        private sealed record MigrationDefinition(int Version, string Name, string ResourceName);
    }
}
