// Project: TeamOps.Config
// File: DbSettings.cs
using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace TeamOps.Config
{
    public sealed class DbSettings
    {
        public bool PortableMode { get; }
        public string DatabasePath { get; }
        public string ConnectionString { get; }

        // Exemplo: permitir Shared Cache e criação caso não exista
        // Microsoft.Data.Sqlite usa "Data Source=...", e aceita opções no connection string.
        public DbSettings(bool portableMode = false)
            : this(AppPaths.GetDatabasePath(portableMode), portableMode)
        {
        }

        public DbSettings(string databasePath, bool portableMode = false)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
                throw new ArgumentException("O caminho do banco deve ser informado.", nameof(databasePath));

            PortableMode = portableMode;
            DatabasePath = Path.GetFullPath(databasePath);

            ConnectionString = new SqliteConnectionStringBuilder
            {
                DataSource = DatabasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = true,
                DefaultTimeout = 30
            }.ToString();
        }

        public override string ToString() => $"DB: {DatabasePath} | Portable: {PortableMode}";
    }
}
