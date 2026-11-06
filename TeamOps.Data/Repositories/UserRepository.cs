using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using TeamOps.Core.Common;
using TeamOps.Core.Entities;
using TeamOps.Data.Db;

namespace TeamOps.Data.Repositories
{
    public sealed class UserRepository(SqliteConnectionFactory factory)
    {
        private readonly SqliteConnectionFactory _factory = factory;

        // 🔹 Busca usuário pelo login
        public User? GetByLogin(string login)
        {
            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT Id, Login, CodigoFJ, Name, PasswordHash, CreatedAt,
                       ProfileId, ReferenceNodeId, IsActive, SessionVersion
                FROM Users
                WHERE Login = @login";
            cmd.Parameters.AddWithValue("@login", login);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;

            return new User
            {
                Id = reader.GetInt32(0),
                Login = reader.GetString(1),
                CodigoFJ = reader.IsDBNull(2) ? null : reader.GetString(2),
                Name = reader.IsDBNull(3) ? "" : reader.GetString(3),
                PasswordHash = reader.GetString(4),
                CreatedAt = reader.GetDateTime(5),
                ProfileId = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                ReferenceNodeId = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                IsActive = reader.GetInt32(8) == 1,
                SessionVersion = reader.GetInt32(9)
            };
        }

        // 🔹 Insere novo usuário
        public int Add(User user)
        {
            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Users (Login, CodigoFJ, Name, PasswordHash, ProfileId,
                                   ReferenceNodeId, IsActive)
                VALUES (@login, @codFJ, @name, @pass, @profileId,
                        @referenceNodeId, @isActive);
                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@login", user.Login);
            cmd.Parameters.AddWithValue("@codFJ", user.CodigoFJ ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@name", user.Name);
            cmd.Parameters.AddWithValue("@pass", user.PasswordHash);
            cmd.Parameters.AddWithValue("@profileId", user.ProfileId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@referenceNodeId", user.ReferenceNodeId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@isActive", user.IsActive ? 1 : 0);
            return checked((int)(long)cmd.ExecuteScalar()!);
        }

        // 🔹 Retorna todos os usuários
        public List<User> GetAll()
        {
            var users = new List<User>();

            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT Id, Login, CodigoFJ, Name, PasswordHash, CreatedAt,
                       ProfileId, ReferenceNodeId, IsActive, SessionVersion
                FROM Users";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                users.Add(new User
                {
                    Id = reader.GetInt32(0),
                    Login = reader.GetString(1),
                    CodigoFJ = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Name = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    PasswordHash = reader.GetString(4),
                    CreatedAt = reader.GetDateTime(5),
                    ProfileId = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                    ReferenceNodeId = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                    IsActive = reader.GetInt32(8) == 1,
                    SessionVersion = reader.GetInt32(9)
                });
            }

            return users;
        }

        // Atualiza somente dados de identidade. Permissoes sao administradas pelo UserAccessRepository.
        public void Update(User user)
        {
            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE Users 
                SET PasswordHash = @pass,
                    Name = @name,
                    PasswordChangedAt = CURRENT_TIMESTAMP,
                    SessionVersion = SessionVersion + 1
                WHERE Login = @login";
            cmd.Parameters.AddWithValue("@pass", user.PasswordHash);
            cmd.Parameters.AddWithValue("@name", user.Name);
            cmd.Parameters.AddWithValue("@login", user.Login);
            cmd.ExecuteNonQuery();
        }
    }
}
