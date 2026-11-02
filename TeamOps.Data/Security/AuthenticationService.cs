using System.Globalization;
using TeamOps.Core.Common;
using TeamOps.Core.Entities;
using TeamOps.Core.Security;
using TeamOps.Data.Db;

namespace TeamOps.Data.Security
{
    public sealed class AuthenticationService
    {
        public const int MaximumFailedAttempts = 5;
        public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

        private readonly SqliteConnectionFactory _factory;
        private readonly Func<DateTime> _utcNow;

        public AuthenticationService(SqliteConnectionFactory factory, Func<DateTime>? utcNow = null)
        {
            _factory = factory;
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        public AuthenticationResult Authenticate(string login, string password, string? workstation = null)
        {
            var normalizedLogin = (login ?? string.Empty).Trim();
            if (normalizedLogin.Length == 0 || string.IsNullOrEmpty(password))
                return AuthenticationResult.Failure("invalid_credentials");

            using var conn = _factory.CreateOpenConnection();
            using var transaction = conn.BeginTransaction();

            User? user = null;
            string passwordHash = string.Empty;
            using (var select = conn.CreateCommand())
            {
                select.Transaction = transaction;
                select.CommandText = @"
                    SELECT Id, Login, CodigoFJ, Name, PasswordHash, AccessLevel, CreatedAt,
                           ProfileId, ReferenceNodeId, IsActive, FailedLoginAttempts,
                           LockedUntil, SessionVersion
                    FROM Users
                    WHERE Login = @login COLLATE NOCASE;";
                select.Parameters.AddWithValue("@login", normalizedLogin);
                using var reader = select.ExecuteReader();
                if (reader.Read())
                {
                    passwordHash = reader.GetString(4);
                    user = new User
                    {
                        Id = reader.GetInt32(0),
                        Login = reader.GetString(1),
                        CodigoFJ = reader.IsDBNull(2) ? null : reader.GetString(2),
                        Name = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                        PasswordHash = passwordHash,
                        AccessLevel = (AccessLevel)reader.GetInt32(5),
                        CreatedAt = reader.GetDateTime(6),
                        ProfileId = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                        ReferenceNodeId = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                        IsActive = reader.GetInt32(9) == 1,
                        FailedLoginAttempts = reader.GetInt32(10),
                        LockedUntilUtc = ParseUtc(reader.IsDBNull(11) ? null : reader.GetString(11)),
                        SessionVersion = reader.GetInt32(12)
                    };
                }
            }

            var now = _utcNow();
            if (user == null || !user.IsActive)
            {
                WriteAudit(conn, transaction, user?.Id, normalizedLogin, "LoginFailed", workstation, "Invalid credentials or inactive user");
                transaction.Commit();
                return AuthenticationResult.Failure("invalid_credentials");
            }

            if (user.LockedUntilUtc.HasValue && user.LockedUntilUtc.Value > now)
            {
                WriteAudit(conn, transaction, user.Id, normalizedLogin, "Locked", workstation, null);
                transaction.Commit();
                return AuthenticationResult.Failure("locked", user.LockedUntilUtc);
            }

            if (!BCrypt.Net.BCrypt.Verify(password, passwordHash))
            {
                var attempts = user.FailedLoginAttempts + 1;
                var lockedUntil = attempts >= MaximumFailedAttempts ? now.Add(LockoutDuration) : (DateTime?)null;
                using var failed = conn.CreateCommand();
                failed.Transaction = transaction;
                failed.CommandText = @"
                    UPDATE Users
                    SET FailedLoginAttempts = @attempts,
                        LockedUntil = @lockedUntil
                    WHERE Id = @userId;";
                failed.Parameters.AddWithValue("@attempts", attempts);
                failed.Parameters.AddWithValue("@lockedUntil", lockedUntil.HasValue ? FormatUtc(lockedUntil.Value) : DBNull.Value);
                failed.Parameters.AddWithValue("@userId", user.Id);
                failed.ExecuteNonQuery();
                WriteAudit(conn, transaction, user.Id, normalizedLogin,
                    lockedUntil.HasValue ? "Locked" : "LoginFailed", workstation, $"FailedAttempts={attempts}");
                transaction.Commit();
                return AuthenticationResult.Failure(lockedUntil.HasValue ? "locked" : "invalid_credentials", lockedUntil);
            }

            using (var success = conn.CreateCommand())
            {
                success.Transaction = transaction;
                success.CommandText = @"
                    UPDATE Users
                    SET FailedLoginAttempts = 0,
                        LockedUntil = NULL,
                        LastLoginAt = @now
                    WHERE Id = @userId;";
                success.Parameters.AddWithValue("@now", FormatUtc(now));
                success.Parameters.AddWithValue("@userId", user.Id);
                success.ExecuteNonQuery();
            }

            WriteAudit(conn, transaction, user.Id, normalizedLogin, "LoginSucceeded", workstation, null);
            transaction.Commit();
            user.FailedLoginAttempts = 0;
            user.LockedUntilUtc = null;
            return AuthenticationResult.Success(user);
        }

        private static void WriteAudit(
            Microsoft.Data.Sqlite.SqliteConnection conn,
            Microsoft.Data.Sqlite.SqliteTransaction transaction,
            int? userId,
            string login,
            string eventType,
            string? workstation,
            string? details)
        {
            using var audit = conn.CreateCommand();
            audit.Transaction = transaction;
            audit.CommandText = @"
                INSERT INTO AuthenticationAudit (UserId, Login, EventType, Workstation, Details)
                VALUES (@userId, @login, @eventType, @workstation, @details);";
            audit.Parameters.AddWithValue("@userId", userId ?? (object)DBNull.Value);
            audit.Parameters.AddWithValue("@login", login);
            audit.Parameters.AddWithValue("@eventType", eventType);
            audit.Parameters.AddWithValue("@workstation", workstation ?? (object)DBNull.Value);
            audit.Parameters.AddWithValue("@details", details ?? (object)DBNull.Value);
            audit.ExecuteNonQuery();
        }

        private static string FormatUtc(DateTime value) =>
            value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

        private static DateTime? ParseUtc(string? value) =>
            DateTime.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
                ? parsed
                : null;
    }
}
