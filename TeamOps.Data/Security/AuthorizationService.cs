using TeamOps.Core.Security;
using TeamOps.Data.Db;

namespace TeamOps.Data.Security
{
    public sealed class AuthorizationService
    {
        private readonly SqliteConnectionFactory _factory;

        public AuthorizationService(SqliteConnectionFactory factory)
        {
            _factory = factory;
        }

        public UserAccessContext GetContext(int userId)
        {
            using var conn = _factory.CreateOpenConnection();
            using var userCommand = conn.CreateCommand();
            userCommand.CommandText = @"
                SELECT u.Id, COALESCE(u.CodigoFJ, ''), p.Code, p.HierarchyRank,
                       p.ScopeMode, u.ReferenceNodeId
                FROM Users u
                JOIN AccessProfiles p ON p.Id = u.ProfileId AND p.IsActive = 1
                JOIN OrganizationNodes reference ON reference.Id = u.ReferenceNodeId AND reference.IsActive = 1
                WHERE u.Id = @userId AND u.IsActive = 1;";
            userCommand.Parameters.AddWithValue("@userId", userId);

            int referenceNodeId;
            string codigoFJ;
            string profileCode;
            int hierarchyRank;
            string scopeMode;
            using (var reader = userCommand.ExecuteReader())
            {
                if (!reader.Read())
                    throw new UnauthorizedAccessException("Usuario inativo ou sem configuracao de acesso valida.");

                codigoFJ = reader.GetString(1);
                profileCode = reader.GetString(2);
                hierarchyRank = reader.GetInt32(3);
                scopeMode = reader.GetString(4);
                referenceNodeId = reader.GetInt32(5);
            }

            var sectorIds = scopeMode == "OrganizationTree"
                ? GetTreeSectorIds(conn, referenceNodeId)
                : GetAssignedSectorIds(conn, userId);

            return new UserAccessContext
            {
                UserId = userId,
                CodigoFJ = codigoFJ,
                ProfileCode = profileCode,
                HierarchyRank = hierarchyRank,
                ScopeMode = scopeMode,
                ReferenceNodeId = referenceNodeId,
                AuthorizedSectorIds = sectorIds
            };
        }

        public bool IsAllowed(int userId, string moduleCode, string actionCode, int? sectorId = null)
        {
            var context = GetContext(userId);
            if (sectorId.HasValue && !context.AuthorizedSectorIds.Contains(sectorId.Value))
                return false;

            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT EXISTS (
                    SELECT 1
                    FROM Users u
                    JOIN ModulePermissions permission ON permission.ProfileId = u.ProfileId
                    WHERE u.Id = @userId
                      AND permission.ModuleCode = @moduleCode COLLATE NOCASE
                      AND permission.ActionCode = @actionCode COLLATE NOCASE
                      AND permission.IsAllowed = 1
                );";
            cmd.Parameters.AddWithValue("@userId", userId);
            cmd.Parameters.AddWithValue("@moduleCode", Normalize(moduleCode));
            cmd.Parameters.AddWithValue("@actionCode", Normalize(actionCode));
            return (long)cmd.ExecuteScalar()! == 1;
        }

        public IReadOnlyList<SectorAccessOption> GetAuthorizedSectors(UserAccessContext context)
        {
            if (context.AuthorizedSectorIds.Count == 0)
                return [];

            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            var parameterNames = context.AuthorizedSectorIds
                .Select((_, index) => $"@sector{index}")
                .ToArray();
            cmd.CommandText = $@"
                SELECT Id, COALESCE(Code, ''), NamePt, NameJp
                FROM Sectors
                WHERE IsActive = 1 AND Id IN ({string.Join(",", parameterNames)})
                ORDER BY NamePt, Id;";
            var index = 0;
            foreach (var sectorId in context.AuthorizedSectorIds)
                cmd.Parameters.AddWithValue(parameterNames[index++], sectorId);

            var result = new List<SectorAccessOption>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                result.Add(new SectorAccessOption(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
            return result;
        }

        public void Demand(int userId, string moduleCode, string actionCode, int? sectorId = null)
        {
            if (!IsAllowed(userId, moduleCode, actionCode, sectorId))
                throw new UnauthorizedAccessException(
                    $"Acesso negado para {moduleCode}:{actionCode} no escopo solicitado.");
        }

        public void DemandSectorOrAggregate(int userId, string moduleCode, string actionCode, int sectorId)
        {
            if (sectorId > 0)
            {
                Demand(userId, moduleCode, actionCode, sectorId);
                return;
            }

            var context = GetContext(userId);
            if (!string.Equals(context.ScopeMode, "OrganizationTree", StringComparison.Ordinal))
                throw new UnauthorizedAccessException("Este perfil deve informar um setor explicito.");

            Demand(userId, moduleCode, actionCode);
        }

        private static HashSet<int> GetTreeSectorIds(Microsoft.Data.Sqlite.SqliteConnection conn, int referenceNodeId)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                WITH RECURSIVE scope(Id) AS (
                    SELECT Id FROM OrganizationNodes WHERE Id = @referenceNodeId AND IsActive = 1
                    UNION ALL
                    SELECT child.Id
                    FROM OrganizationNodes child
                    JOIN scope parent ON child.ParentId = parent.Id
                    WHERE child.IsActive = 1
                )
                SELECT s.Id
                FROM Sectors s
                JOIN scope ON scope.Id = s.OrganizationNodeId
                WHERE s.IsActive = 1;";
            cmd.Parameters.AddWithValue("@referenceNodeId", referenceNodeId);
            return ReadIds(cmd);
        }

        private static HashSet<int> GetAssignedSectorIds(Microsoft.Data.Sqlite.SqliteConnection conn, int userId)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT assignment.SectorId
                FROM UserSectorAssignments assignment
                JOIN Sectors sector ON sector.Id = assignment.SectorId AND sector.IsActive = 1
                JOIN OrganizationNodes node ON node.Id = sector.OrganizationNodeId AND node.IsActive = 1
                WHERE assignment.UserId = @userId;";
            cmd.Parameters.AddWithValue("@userId", userId);
            return ReadIds(cmd);
        }

        private static HashSet<int> ReadIds(Microsoft.Data.Sqlite.SqliteCommand cmd)
        {
            var ids = new HashSet<int>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                ids.Add(reader.GetInt32(0));
            return ids;
        }

        private static string Normalize(string value) => (value ?? string.Empty).Trim().ToLowerInvariant();
    }
}
