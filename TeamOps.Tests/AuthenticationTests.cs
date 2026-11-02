using TeamOps.Core.Security;
using TeamOps.Data.Security;
using Xunit;

namespace TeamOps.Tests;

public sealed class AuthenticationTests
{
    private const string StrongPassword = "TeamOps#2026!Secure";

    [Fact]
    public void EmptyDatabase_HasNoDefaultCredentials_AndRequiresProvisioning()
    {
        using var context = new DatabaseTestContext();
        var provisioning = new FirstRunProvisioningService(context.Factory);

        Assert.True(provisioning.IsProvisioningRequired());
        using var conn = context.Factory.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Users;";
        Assert.Equal(0L, cmd.ExecuteScalar());
    }

    [Fact]
    public void FirstDirector_ChoosesCredentials_AndProvisioningRunsOnce()
    {
        using var context = new DatabaseTestContext();
        var provisioning = new FirstRunProvisioningService(context.Factory);

        var userId = provisioning.ProvisionDirector("initial.director", "Initial Director", StrongPassword);

        Assert.False(provisioning.IsProvisioningRequired());
        Assert.Throws<InvalidOperationException>(() =>
            provisioning.ProvisionDirector("second.director", "Second Director", StrongPassword));

        var authenticated = new AuthenticationService(context.Factory)
            .Authenticate("initial.director", StrongPassword, "TEST-PC");
        Assert.True(authenticated.Succeeded);
        Assert.Equal(userId, authenticated.User!.Id);

        var scope = new AuthorizationService(context.Factory).GetContext(userId);
        Assert.Equal("DIRECTOR", scope.ProfileCode);
        Assert.Equal(4, scope.AuthorizedSectorIds.Count);
    }

    [Fact]
    public void FiveInvalidAttempts_LockAccountAndWriteAudit()
    {
        using var context = new DatabaseTestContext();
        var now = new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc);
        new FirstRunProvisioningService(context.Factory)
            .ProvisionDirector("secure.director", "Secure Director", StrongPassword);
        var authentication = new AuthenticationService(context.Factory, () => now);

        for (var attempt = 1; attempt <= AuthenticationService.MaximumFailedAttempts; attempt++)
        {
            var result = authentication.Authenticate("secure.director", "wrong-password", "TEST-PC");
            Assert.False(result.Succeeded);
        }

        var locked = authentication.Authenticate("secure.director", StrongPassword, "TEST-PC");
        Assert.False(locked.Succeeded);
        Assert.Equal("locked", locked.ErrorCode);
        Assert.Equal(now.Add(AuthenticationService.LockoutDuration), locked.LockedUntilUtc);

        using var conn = context.Factory.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM AuthenticationAudit WHERE Login = 'secure.director';";
        Assert.Equal(6L, cmd.ExecuteScalar());
    }

    [Fact]
    public void InactiveUser_CannotAuthenticate()
    {
        using var context = new DatabaseTestContext();
        var userId = new FirstRunProvisioningService(context.Factory)
            .ProvisionDirector("inactive.director", "Inactive Director", StrongPassword);
        using (var conn = context.Factory.CreateOpenConnection())
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "UPDATE Users SET IsActive = 0 WHERE Id = @id;";
            cmd.Parameters.AddWithValue("@id", userId);
            cmd.ExecuteNonQuery();
        }

        var result = new AuthenticationService(context.Factory)
            .Authenticate("inactive.director", StrongPassword);

        Assert.False(result.Succeeded);
        Assert.Equal("invalid_credentials", result.ErrorCode);
    }

    [Theory]
    [InlineData("short")]
    [InlineData("alllowercase123!")]
    [InlineData("ALLUPPERCASE123!")]
    [InlineData("NoNumbersHere!")]
    public void Provisioning_RejectsWeakPasswords(string password)
    {
        using var context = new DatabaseTestContext();
        Assert.Throws<ArgumentException>(() =>
            new FirstRunProvisioningService(context.Factory)
                .ProvisionDirector("initial.director", "Initial Director", password));
    }
}
