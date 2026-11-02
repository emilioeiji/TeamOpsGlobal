using TeamOps.Data.Db;

namespace TeamOps.Data.Repositories
{
    public sealed class UserAccessRepository
    {
        private readonly SqliteConnectionFactory _factory;

        public UserAccessRepository(SqliteConnectionFactory factory)
        {
            _factory = factory;
        }

        public void Configure(int userId, string profileCode, int referenceNodeId, IEnumerable<int> sectorIds, int? actorUserId = null)
        {
            var distinctSectorIds = sectorIds.Distinct().ToArray();
            using var conn = _factory.CreateOpenConnection();
            using var transaction = conn.BeginTransaction();

            using var update = conn.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = @"
                UPDATE Users
                SET ProfileId = (SELECT Id FROM AccessProfiles WHERE Code = @profileCode COLLATE NOCASE AND IsActive = 1),
                    ReferenceNodeId = @referenceNodeId,
                    SessionVersion = SessionVersion + 1
                WHERE Id = @userId
                  AND EXISTS (SELECT 1 FROM AccessProfiles WHERE Code = @profileCode COLLATE NOCASE AND IsActive = 1)
                  AND EXISTS (SELECT 1 FROM OrganizationNodes WHERE Id = @referenceNodeId AND IsActive = 1);";
            update.Parameters.AddWithValue("@profileCode", profileCode.Trim());
            update.Parameters.AddWithValue("@referenceNodeId", referenceNodeId);
            update.Parameters.AddWithValue("@userId", userId);
            if (update.ExecuteNonQuery() != 1)
                throw new InvalidOperationException("Usuario, perfil ou referencia organizacional invalida.");

            using var delete = conn.CreateCommand();
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM UserSectorAssignments WHERE UserId = @userId;";
            delete.Parameters.AddWithValue("@userId", userId);
            delete.ExecuteNonQuery();

            foreach (var sectorId in distinctSectorIds)
            {
                using var insert = conn.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText = @"
                    INSERT INTO UserSectorAssignments (UserId, SectorId, AssignedByUserId)
                    VALUES (@userId, @sectorId, @actorUserId);";
                insert.Parameters.AddWithValue("@userId", userId);
                insert.Parameters.AddWithValue("@sectorId", sectorId);
                insert.Parameters.AddWithValue("@actorUserId", actorUserId ?? (object)DBNull.Value);
                insert.ExecuteNonQuery();
            }

            using var audit = conn.CreateCommand();
            audit.Transaction = transaction;
            audit.CommandText = @"
                INSERT INTO SecurityAudit (ActorUserId, EventType, TargetType, TargetId, Details)
                VALUES (@actorUserId, 'UserAccessConfigured', 'User', @targetId, @details);";
            audit.Parameters.AddWithValue("@actorUserId", actorUserId ?? (object)DBNull.Value);
            audit.Parameters.AddWithValue("@targetId", userId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            audit.Parameters.AddWithValue("@details", $"Profile={profileCode};Reference={referenceNodeId};Sectors={string.Join(',', distinctSectorIds)}");
            audit.ExecuteNonQuery();

            transaction.Commit();
        }
    }
}
