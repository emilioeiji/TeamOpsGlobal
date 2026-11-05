using Dapper;
using Microsoft.Data.Sqlite;
using TeamOps.Data.Security;
using Xunit;

namespace TeamOps.Tests;

public sealed class OperationalRegressionTests
{
    private const string StrongPassword = "TeamOps#2026!Secure";

    [Fact]
    public void EveryConnectionEnforcesForeignKeysAndBusyTimeout()
    {
        using var context = new DatabaseTestContext();

        using var first = context.Factory.CreateOpenConnection();
        using var second = context.Factory.CreateOpenConnection();

        Assert.Equal(1L, first.ExecuteScalar<long>("PRAGMA foreign_keys;"));
        Assert.Equal(1L, second.ExecuteScalar<long>("PRAGMA foreign_keys;"));
        Assert.Equal(30_000L, first.ExecuteScalar<long>("PRAGMA busy_timeout;"));
        Assert.Equal(30_000L, second.ExecuteScalar<long>("PRAGMA busy_timeout;"));
    }

    [Fact]
    public async Task ConcurrentWriterWaitsForTransientLockAndThenCommits()
    {
        using var context = new DatabaseTestContext();
        using var blocker = context.Factory.CreateOpenConnection();
        blocker.Execute("CREATE TABLE ConcurrencyProbe (Id INTEGER PRIMARY KEY, Value TEXT NOT NULL);");

        using var transaction = blocker.BeginTransaction();
        blocker.Execute(
            "INSERT INTO ConcurrencyProbe (Value) VALUES ('first');",
            transaction: transaction);

        var competingWrite = Task.Run(() =>
        {
            using var connection = context.Factory.CreateOpenConnection();
            return connection.Execute("INSERT INTO ConcurrencyProbe (Value) VALUES ('second');");
        });

        await Task.Delay(150);
        Assert.False(competingWrite.IsCompleted);

        transaction.Commit();
        Assert.Equal(1, await competingWrite.WaitAsync(TimeSpan.FromSeconds(5)));

        using var verification = context.Factory.CreateOpenConnection();
        Assert.Equal(2, verification.ExecuteScalar<int>("SELECT COUNT(*) FROM ConcurrencyProbe;"));
    }

    [Fact]
    public void FailedTransactionRollsBackEveryWrite()
    {
        using var context = new DatabaseTestContext();
        using var connection = context.Factory.CreateOpenConnection();
        connection.Execute("CREATE TABLE TransactionProbe (Id INTEGER PRIMARY KEY, Value TEXT NOT NULL UNIQUE);");

        Assert.Throws<SqliteException>(() =>
        {
            using var transaction = connection.BeginTransaction();
            connection.Execute("INSERT INTO TransactionProbe (Value) VALUES ('same');", transaction: transaction);
            connection.Execute("INSERT INTO TransactionProbe (Value) VALUES ('same');", transaction: transaction);
            transaction.Commit();
        });

        Assert.Equal(0, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM TransactionProbe;"));
    }

    [Fact]
    public async Task ConcurrentInvalidLoginsLockAccountWithoutLosingAuditEvents()
    {
        using var context = new DatabaseTestContext();
        var now = new DateTime(2026, 11, 5, 0, 0, 0, DateTimeKind.Utc);
        new FirstRunProvisioningService(context.Factory)
            .ProvisionDirector("concurrent.director", "Concurrent Director", StrongPassword);

        var attempts = Enumerable.Range(0, AuthenticationService.MaximumFailedAttempts)
            .Select(_ => Task.Run(() => new AuthenticationService(context.Factory, () => now)
                .Authenticate("concurrent.director", "wrong-password", "TEST-PC")))
            .ToArray();

        var results = await Task.WhenAll(attempts).WaitAsync(TimeSpan.FromSeconds(15));

        Assert.All(results, result => Assert.False(result.Succeeded));
        Assert.Contains(results, result => result.ErrorCode == "locked");

        var locked = new AuthenticationService(context.Factory, () => now)
            .Authenticate("concurrent.director", StrongPassword, "TEST-PC");
        Assert.False(locked.Succeeded);
        Assert.Equal("locked", locked.ErrorCode);

        using var connection = context.Factory.CreateOpenConnection();
        Assert.Equal(AuthenticationService.MaximumFailedAttempts + 1,
            connection.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM AuthenticationAudit WHERE Login = 'concurrent.director';"));
    }
}
