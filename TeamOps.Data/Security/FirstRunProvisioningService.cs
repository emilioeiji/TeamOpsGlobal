using TeamOps.Core.Common;
using TeamOps.Data.Db;

namespace TeamOps.Data.Security
{
    public sealed class FirstRunProvisioningService
    {
        private readonly SqliteConnectionFactory _factory;

        public FirstRunProvisioningService(SqliteConnectionFactory factory)
        {
            _factory = factory;
        }

        public bool IsProvisioningRequired()
        {
            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT NOT EXISTS (SELECT 1 FROM Users);";
            return (long)cmd.ExecuteScalar()! == 1;
        }

        public int ProvisionDirector(string login, string name, string password)
        {
            var normalizedLogin = (login ?? string.Empty).Trim();
            var normalizedName = (name ?? string.Empty).Trim();
            Validate(normalizedLogin, normalizedName, password);
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);

            using var conn = _factory.CreateOpenConnection();
            using var transaction = conn.BeginTransaction(System.Data.IsolationLevel.Serializable);

            using (var existing = conn.CreateCommand())
            {
                existing.Transaction = transaction;
                existing.CommandText = "SELECT COUNT(*) FROM Users;";
                if ((long)existing.ExecuteScalar()! != 0)
                    throw new InvalidOperationException("O provisionamento inicial ja foi concluido.");
            }

            int userId;
            using (var insert = conn.CreateCommand())
            {
                insert.Transaction = transaction;
                insert.CommandText = @"
                    INSERT INTO Users
                        (Login, CodigoFJ, Name, PasswordHash, AccessLevel, ProfileId,
                         ReferenceNodeId, IsActive, PasswordChangedAt)
                    SELECT @login, NULL, @name, @passwordHash, @legacyAccessLevel,
                           profile.Id, root.Id, 1, CURRENT_TIMESTAMP
                    FROM AccessProfiles profile
                    CROSS JOIN OrganizationNodes root
                    WHERE profile.Code = 'DIRECTOR' AND root.Code = '5500';
                    SELECT last_insert_rowid();";
                insert.Parameters.AddWithValue("@login", normalizedLogin);
                insert.Parameters.AddWithValue("@name", normalizedName);
                insert.Parameters.AddWithValue("@passwordHash", passwordHash);
                insert.Parameters.AddWithValue("@legacyAccessLevel", (int)AccessLevel.Admin);
                userId = checked((int)(long)insert.ExecuteScalar()!);
                if (userId <= 0)
                    throw new InvalidOperationException("Perfil Diretor ou unidade 5500 nao foi encontrado.");
            }

            using (var audit = conn.CreateCommand())
            {
                audit.Transaction = transaction;
                audit.CommandText = @"
                    INSERT INTO SecurityAudit (ActorUserId, EventType, TargetType, TargetId, Details)
                    VALUES (@userId, 'InitialDirectorProvisioned', 'User', @targetId, 'Secure first-run provisioning');";
                audit.Parameters.AddWithValue("@userId", userId);
                audit.Parameters.AddWithValue("@targetId", userId.ToString(System.Globalization.CultureInfo.InvariantCulture));
                audit.ExecuteNonQuery();
            }

            transaction.Commit();
            return userId;
        }

        private static void Validate(string login, string name, string password)
        {
            if (login.Length < 4)
                throw new ArgumentException("O login deve possuir pelo menos 4 caracteres.", nameof(login));
            if (name.Length < 2)
                throw new ArgumentException("Informe o nome do administrador.", nameof(name));
            PasswordPolicy.DemandStrong(password);
        }
    }
}
