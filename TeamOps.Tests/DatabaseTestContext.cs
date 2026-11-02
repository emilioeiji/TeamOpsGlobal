using TeamOps.Config;
using TeamOps.Data.Db;

namespace TeamOps.Tests;

internal sealed class DatabaseTestContext : IDisposable
{
    private readonly string _directory;

    public DatabaseTestContext()
    {
        _directory = Path.Combine(Path.GetTempPath(), "TeamOps.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        DatabasePath = Path.Combine(_directory, "teamops-v2.db");
        Settings = new DbSettings(DatabasePath);
        new DbInitializer(Settings).EnsureCreated();
        Factory = new SqliteConnectionFactory(Settings);
    }

    public string DatabasePath { get; }
    public DbSettings Settings { get; }
    public SqliteConnectionFactory Factory { get; }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }
}
