using Microsoft.Data.Sqlite;
using TeamOps.Core.Security;
using TeamOps.Data.Db;

namespace TeamOps.Data.Security;

public sealed class OperatorAccessService
{
    private readonly SqliteConnectionFactory _factory;

    public OperatorAccessService(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public int ProvisionOrUpdate(
        string codigoFJ,
        string displayName,
        int primarySectorId,
        IEnumerable<int> sectorIds,
        string operatorType,
        string? password,
        bool isActive)
    {
        var normalizedFj = (codigoFJ ?? string.Empty).Trim().ToUpperInvariant();
        var normalizedType = string.Equals(operatorType, "Relief", StringComparison.OrdinalIgnoreCase)
            ? "Relief"
            : "Regular";
        var sectors = sectorIds.Where(id => id > 0).Append(primarySectorId).Distinct().ToArray();

        if (normalizedFj.Length == 0) throw new ArgumentException("Informe o Codigo FJ.");
        if (primarySectorId <= 0 || sectors.Length == 0) throw new ArgumentException("Informe o setor principal.");
        if (normalizedType == "Regular" && sectors.Length != 1)
            throw new InvalidOperationException("Operador Regular deve possuir somente o setor principal.");
        if (normalizedType == "Relief" && sectors.Length < 2)
            throw new InvalidOperationException("Operador Relief deve possuir ao menos dois setores.");

        using var conn = _factory.CreateOpenConnection();
        using var transaction = conn.BeginTransaction();
        DemandActiveSectors(conn, transaction, sectors);

        var user = FindUser(conn, transaction, normalizedFj);
        int userId;
        if (user == null)
        {
            var internalCredential = string.IsNullOrWhiteSpace(password)
                ? $"{Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))}aA1"
                : password;
            if (!string.IsNullOrWhiteSpace(password))
                PasswordPolicy.DemandStrong(password);
            var profileId = Scalar<int>(conn, transaction,
                "SELECT Id FROM AccessProfiles WHERE Code = 'OP' COLLATE NOCASE AND IsActive = 1;");
            var rootId = OrganizationRootResolver.ResolveRequired(conn);
            var hash = BCrypt.Net.BCrypt.HashPassword(internalCredential, workFactor: 12);
            using var insert = conn.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = @"
                INSERT INTO Users
                    (Login, CodigoFJ, Name, PasswordHash, ProfileId, ReferenceNodeId,
                     IsActive, PasswordChangedAt, SessionVersion)
                VALUES
                    (@login, @codigoFJ, @name, @passwordHash, @profileId, @rootId,
                     @isActive, CURRENT_TIMESTAMP, 1);
                SELECT last_insert_rowid();";
            insert.Parameters.AddWithValue("@login", normalizedFj);
            insert.Parameters.AddWithValue("@codigoFJ", normalizedFj);
            insert.Parameters.AddWithValue("@name", displayName.Trim());
            insert.Parameters.AddWithValue("@passwordHash", hash);
            insert.Parameters.AddWithValue("@profileId", profileId);
            insert.Parameters.AddWithValue("@rootId", rootId);
            insert.Parameters.AddWithValue("@isActive", isActive ? 1 : 0);
            userId = checked((int)(long)insert.ExecuteScalar()!);
        }
        else
        {
            userId = user.Value.Id;
            string? newHash = null;
            if (!string.IsNullOrWhiteSpace(password))
            {
                PasswordPolicy.DemandStrong(password);
                newHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
            }

            using var update = conn.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = @"
                UPDATE Users
                SET Name = @name,
                    IsActive = @isActive,
                    PasswordHash = COALESCE(@passwordHash, PasswordHash),
                    PasswordChangedAt = CASE WHEN @passwordHash IS NULL THEN PasswordChangedAt ELSE CURRENT_TIMESTAMP END,
                    SessionVersion = SessionVersion + 1
                WHERE Id = @id;";
            update.Parameters.AddWithValue("@name", displayName.Trim());
            update.Parameters.AddWithValue("@isActive", isActive ? 1 : 0);
            update.Parameters.AddWithValue("@passwordHash", newHash ?? (object)DBNull.Value);
            update.Parameters.AddWithValue("@id", userId);
            update.ExecuteNonQuery();
        }

        ReplaceOperatorSectors(conn, transaction, normalizedFj, primarySectorId, sectors);
        SyncAssignedUserSectors(conn, transaction, userId, sectors, isActive);
        transaction.Commit();
        return userId;
    }

    public IReadOnlyList<OperatorSectorAccess> GetSectors(string codigoFJ)
    {
        using var conn = _factory.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT s.Id, s.Code, s.NamePt, s.NameJp, assignment.IsPrimary
            FROM OperatorSectorAssignments assignment
            JOIN Sectors s ON s.Id = assignment.SectorId AND s.IsActive = 1
            JOIN Operators o ON o.CodigoFJ = assignment.OperatorCodigoFJ AND o.Status = 1
            WHERE assignment.OperatorCodigoFJ = @codigoFJ COLLATE NOCASE
            ORDER BY assignment.IsPrimary DESC, s.NamePt, s.Id;";
        cmd.Parameters.AddWithValue("@codigoFJ", (codigoFJ ?? string.Empty).Trim());
        using var reader = cmd.ExecuteReader();
        var result = new List<OperatorSectorAccess>();
        while (reader.Read())
            result.Add(new(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetInt32(4) == 1));
        return result;
    }

    public bool HasIdentity(string codigoFJ)
    {
        using var conn = _factory.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT EXISTS(SELECT 1 FROM Users WHERE CodigoFJ = @fj COLLATE NOCASE);";
        cmd.Parameters.AddWithValue("@fj", (codigoFJ ?? string.Empty).Trim());
        return Convert.ToInt64(cmd.ExecuteScalar()) == 1;
    }

    public void Revoke(string codigoFJ)
    {
        using var conn = _factory.CreateOpenConnection();
        using var transaction = conn.BeginTransaction();
        var fj = (codigoFJ ?? string.Empty).Trim();
        Execute(conn, transaction, @"
            DELETE FROM UserSectorAssignments
            WHERE UserId IN (SELECT Id FROM Users WHERE CodigoFJ = @fj COLLATE NOCASE);",
            ("@fj", fj));
        Execute(conn, transaction, @"
            UPDATE Users SET IsActive = 0, SessionVersion = SessionVersion + 1
            WHERE CodigoFJ = @fj COLLATE NOCASE;",
            ("@fj", fj));
        transaction.Commit();
    }

    private static void ReplaceOperatorSectors(SqliteConnection conn, SqliteTransaction transaction, string fj, int primary, int[] sectors)
    {
        Execute(conn, transaction, "DELETE FROM OperatorSectorAssignments WHERE OperatorCodigoFJ = @fj;", ("@fj", fj));
        foreach (var sectorId in sectors)
            Execute(conn, transaction,
                "INSERT INTO OperatorSectorAssignments (OperatorCodigoFJ, SectorId, IsPrimary) VALUES (@fj, @sectorId, @isPrimary);",
                ("@fj", fj), ("@sectorId", sectorId), ("@isPrimary", sectorId == primary ? 1 : 0));
    }

    private static void SyncAssignedUserSectors(
        SqliteConnection conn,
        SqliteTransaction transaction,
        int userId,
        int[] sectors,
        bool isActive)
    {
        var scopeMode = Scalar<string>(conn, transaction, @"
            SELECT p.ScopeMode FROM Users u JOIN AccessProfiles p ON p.Id = u.ProfileId WHERE u.Id = @userId;",
            ("@userId", userId));
        if (!string.Equals(scopeMode, "AssignedSectors", StringComparison.Ordinal)) return;
        Execute(conn, transaction, "DELETE FROM UserSectorAssignments WHERE UserId = @userId;", ("@userId", userId));
        if (!isActive) return;
        foreach (var sectorId in sectors)
            Execute(conn, transaction,
                "INSERT INTO UserSectorAssignments (UserId, SectorId) VALUES (@userId, @sectorId);",
                ("@userId", userId), ("@sectorId", sectorId));
    }

    private static void DemandActiveSectors(SqliteConnection conn, SqliteTransaction transaction, int[] sectors)
    {
        foreach (var sectorId in sectors)
        {
            var exists = Scalar<long>(conn, transaction,
                "SELECT EXISTS(SELECT 1 FROM Sectors WHERE Id = @sectorId AND IsActive = 1);", ("@sectorId", sectorId));
            if (exists != 1) throw new InvalidOperationException("Setor inativo ou inexistente.");
        }
    }

    private static (int Id, string ProfileCode)? FindUser(SqliteConnection conn, SqliteTransaction transaction, string fj)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = @"
            SELECT u.Id, COALESCE(p.Code, '')
            FROM Users u LEFT JOIN AccessProfiles p ON p.Id = u.ProfileId
            WHERE u.CodigoFJ = @fj COLLATE NOCASE
            LIMIT 1;";
        cmd.Parameters.AddWithValue("@fj", fj);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? (reader.GetInt32(0), reader.GetString(1)) : null;
    }

    private static T Scalar<T>(SqliteConnection conn, SqliteTransaction transaction, string sql, params (string, object)[] parameters)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = sql;
        foreach (var parameter in parameters) cmd.Parameters.AddWithValue(parameter.Item1, parameter.Item2);
        return (T)Convert.ChangeType(cmd.ExecuteScalar()!, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void Execute(SqliteConnection conn, SqliteTransaction transaction, string sql, params (string, object)[] parameters)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = sql;
        foreach (var parameter in parameters) cmd.Parameters.AddWithValue(parameter.Item1, parameter.Item2);
        cmd.ExecuteNonQuery();
    }
}

public sealed record OperatorSectorAccess(int Id, string Code, string NamePt, string NameJp, bool IsPrimary);
