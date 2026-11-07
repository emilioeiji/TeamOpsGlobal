using TeamOps.Core.Security;
using TeamOps.Data.Db;

namespace TeamOps.Data.Security
{
    public sealed class HikitsuguiAudienceService
    {
        private readonly SqliteConnectionFactory _factory;
        private readonly AuthorizationService _authorization;

        public HikitsuguiAudienceService(SqliteConnectionFactory factory)
        {
            _factory = factory;
            _authorization = new AuthorizationService(factory);
        }

        public void SetSectorAudience(int actorUserId, int hikitsuguiId, IEnumerable<int> sectorIds)
        {
            var targets = sectorIds.Distinct().ToArray();
            DemandCanCreateForSectors(actorUserId, targets);

            ReplaceAudience(hikitsuguiId, "Sector", targets, null);
        }

        public void DemandCanCreateForSectors(int actorUserId, IEnumerable<int> sectorIds)
        {
            var targets = sectorIds.Distinct().ToArray();
            if (targets.Length == 0)
                throw new ArgumentException("Informe ao menos um setor para a audiencia.", nameof(sectorIds));

            foreach (var sectorId in targets)
                _authorization.Demand(actorUserId, ModuleCodes.Hikitsugui, PermissionActions.Create, sectorId);
        }

        public void SetOrganizationAudience(int actorUserId, int hikitsuguiId, int organizationNodeId)
        {
            if (!GetTargetableOrganizationUnits(actorUserId).Any(unit => unit.Id == organizationNodeId))
                throw new UnauthorizedAccessException("Unidade organizacional fora do escopo autorizado.");

            var targetSectors = GetOrganizationSectorIds(organizationNodeId);
            if (targetSectors.Count == 0)
                throw new InvalidOperationException("A unidade nao possui setores ativos.");

            DemandAllSectors(actorUserId, PermissionActions.Create, targetSectors);
            ReplaceAudience(hikitsuguiId, "Organization", [], organizationNodeId);
        }

        public void SetGeneralAudience(int actorUserId, int hikitsuguiId)
        {
            using var conn = _factory.CreateOpenConnection();
            var rootId = OrganizationRootResolver.ResolveRequired(conn);
            var targetSectors = GetOrganizationSectorIds(rootId);
            if (targetSectors.Count == 0)
                throw new InvalidOperationException("A organizacao nao possui setores ativos.");

            DemandAllSectors(actorUserId, PermissionActions.Create, targetSectors);
            ReplaceAudience(hikitsuguiId, "General", [], null);
        }

        public IReadOnlyList<HikitsuguiOrganizationAudienceOption> GetTargetableOrganizationUnits(int actorUserId)
        {
            var context = _authorization.GetContext(actorUserId);
            if (!_authorization.IsAllowed(actorUserId, ModuleCodes.Hikitsugui, PermissionActions.Create))
                return [];

            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                WITH RECURSIVE reference_scope(Id) AS (
                    SELECT Id
                    FROM OrganizationNodes
                    WHERE Id = @referenceNodeId AND IsActive = 1
                    UNION ALL
                    SELECT child.Id
                    FROM OrganizationNodes child
                    JOIN reference_scope parent ON child.ParentId = parent.Id
                    WHERE child.IsActive = 1
                )
                SELECT unit.Id,
                       unit.Code,
                       COALESCE(NULLIF(unit.NamePt, ''), unit.Code) AS NamePt,
                       COALESCE(NULLIF(unit.NameJp, ''), unit.NamePt, unit.Code) AS NameJp
                FROM OrganizationNodes unit
                JOIN reference_scope scope ON scope.Id = unit.Id
                WHERE unit.NodeType = 'Unit' AND unit.IsActive = 1
                ORDER BY unit.Code, unit.Id;";
            cmd.Parameters.AddWithValue("@referenceNodeId", context.ReferenceNodeId);

            var result = new List<HikitsuguiOrganizationAudienceOption>();
            using var reader = cmd.ExecuteReader();
            var units = new List<HikitsuguiOrganizationAudienceOption>();
            while (reader.Read())
            {
                units.Add(new HikitsuguiOrganizationAudienceOption(
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3)));
            }

            foreach (var unit in units)
            {
                var sectorIds = GetOrganizationSectorIds(unit.Id);
                if (sectorIds.Count > 0 && sectorIds.All(context.AuthorizedSectorIds.Contains))
                    result.Add(unit);
            }

            return result;
        }

        public bool CanView(int userId, int hikitsuguiId)
        {
            var context = _authorization.GetContext(userId);
            if (!_authorization.IsAllowed(userId, ModuleCodes.Hikitsugui, PermissionActions.View))
                return false;

            var audience = GetAudience(hikitsuguiId);
            if (audience.Count == 0)
                return false;

            return audience[0].Type switch
            {
                "Sector" => audience.Any(item => item.SectorId.HasValue
                    && context.AuthorizedSectorIds.Contains(item.SectorId.Value)),
                "Organization" => GetOrganizationSectorIds(audience[0].OrganizationNodeId!.Value)
                    .Any(context.AuthorizedSectorIds.Contains),
                "General" => context.AuthorizedSectorIds.Count > 0,
                _ => false
            };
        }

        public void DemandCanView(int userId, int hikitsuguiId)
        {
            if (!CanView(userId, hikitsuguiId))
                throw new UnauthorizedAccessException("Hikitsugui fora do escopo autorizado.");
        }

        public void DemandCanEdit(int userId, int hikitsuguiId)
        {
            var targetSectors = ResolveAudienceSectorIds(hikitsuguiId);
            if (targetSectors.Count == 0)
                throw new UnauthorizedAccessException("Hikitsugui sem audiencia valida.");
            DemandAllSectors(userId, PermissionActions.Edit, targetSectors);
        }

        public void DemandCanDelete(int userId, int hikitsuguiId)
        {
            var targetSectors = ResolveAudienceSectorIds(hikitsuguiId);
            if (targetSectors.Count == 0)
                throw new UnauthorizedAccessException("Hikitsugui sem audiencia valida.");
            DemandAllSectors(userId, PermissionActions.Delete, targetSectors);
        }

        public bool IsTargetedToSector(int hikitsuguiId, int sectorId)
        {
            return sectorId > 0 && ResolveAudienceSectorIds(hikitsuguiId).Contains(sectorId);
        }

        public IReadOnlySet<int> GetVisibleMessageIds(int userId, DateTime start, DateTime end)
        {
            var result = new HashSet<int>();
            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT Id FROM Hikitsugui
                WHERE Date >= @start AND Date < @end
                ORDER BY Date DESC, Id DESC;";
            cmd.Parameters.AddWithValue("@start", start);
            cmd.Parameters.AddWithValue("@end", end);
            using var reader = cmd.ExecuteReader();
            var candidates = new List<int>();
            while (reader.Read())
                candidates.Add(reader.GetInt32(0));

            foreach (var id in candidates)
            {
                if (CanView(userId, id))
                    result.Add(id);
            }
            return result;
        }

        private void DemandAllSectors(int userId, string action, IReadOnlyCollection<int> sectorIds)
        {
            foreach (var sectorId in sectorIds)
                _authorization.Demand(userId, ModuleCodes.Hikitsugui, action, sectorId);
        }

        private void ReplaceAudience(int hikitsuguiId, string type, IReadOnlyCollection<int> sectorIds, int? organizationNodeId)
        {
            using var conn = _factory.CreateOpenConnection();
            using var transaction = conn.BeginTransaction();

            using (var exists = conn.CreateCommand())
            {
                exists.Transaction = transaction;
                exists.CommandText = "SELECT EXISTS (SELECT 1 FROM Hikitsugui WHERE Id = @id);";
                exists.Parameters.AddWithValue("@id", hikitsuguiId);
                if ((long)exists.ExecuteScalar()! != 1)
                    throw new InvalidOperationException("Hikitsugui nao encontrado.");
            }

            using (var delete = conn.CreateCommand())
            {
                delete.Transaction = transaction;
                delete.CommandText = "DELETE FROM HikitsuguiAudiences WHERE HikitsuguiId = @id;";
                delete.Parameters.AddWithValue("@id", hikitsuguiId);
                delete.ExecuteNonQuery();
            }

            var rows = type == "Sector" ? sectorIds.Cast<int?>().ToArray() : new int?[] { null };
            foreach (var sectorId in rows)
            {
                using var insert = conn.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText = @"
                    INSERT INTO HikitsuguiAudiences
                        (HikitsuguiId, AudienceType, SectorId, OrganizationNodeId)
                    VALUES (@hikitsuguiId, @type, @sectorId, @organizationNodeId);";
                insert.Parameters.AddWithValue("@hikitsuguiId", hikitsuguiId);
                insert.Parameters.AddWithValue("@type", type);
                insert.Parameters.AddWithValue("@sectorId", sectorId ?? (object)DBNull.Value);
                insert.Parameters.AddWithValue("@organizationNodeId", organizationNodeId ?? (object)DBNull.Value);
                insert.ExecuteNonQuery();
            }

            transaction.Commit();
        }

        private List<AudienceRow> GetAudience(int hikitsuguiId)
        {
            var result = new List<AudienceRow>();
            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT AudienceType, SectorId, OrganizationNodeId
                FROM HikitsuguiAudiences WHERE HikitsuguiId = @id;";
            cmd.Parameters.AddWithValue("@id", hikitsuguiId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new AudienceRow(
                    reader.GetString(0),
                    reader.IsDBNull(1) ? null : reader.GetInt32(1),
                    reader.IsDBNull(2) ? null : reader.GetInt32(2)));
            }
            return result;
        }

        private HashSet<int> ResolveAudienceSectorIds(int hikitsuguiId)
        {
            var audience = GetAudience(hikitsuguiId);
            if (audience.Count == 0)
                return [];
            if (audience[0].Type == "Sector")
                return audience.Where(item => item.SectorId.HasValue).Select(item => item.SectorId!.Value).ToHashSet();
            if (audience[0].Type == "Organization")
                return GetOrganizationSectorIds(audience[0].OrganizationNodeId!.Value);

            using var conn = _factory.CreateOpenConnection();
            return GetOrganizationSectorIds(OrganizationRootResolver.ResolveRequired(conn));
        }

        private HashSet<int> GetOrganizationSectorIds(int organizationNodeId)
        {
            var result = new HashSet<int>();
            using var conn = _factory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                WITH RECURSIVE descendants(Id) AS (
                    SELECT Id FROM OrganizationNodes WHERE Id = @nodeId AND NodeType = 'Unit' AND IsActive = 1
                    UNION ALL
                    SELECT child.Id FROM OrganizationNodes child
                    JOIN descendants parent ON child.ParentId = parent.Id
                    WHERE child.IsActive = 1
                )
                SELECT sector.Id FROM Sectors sector
                JOIN descendants ON descendants.Id = sector.OrganizationNodeId
                WHERE sector.IsActive = 1;";
            cmd.Parameters.AddWithValue("@nodeId", organizationNodeId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                result.Add(reader.GetInt32(0));
            return result;
        }

        private sealed record AudienceRow(string Type, int? SectorId, int? OrganizationNodeId);
    }

    public sealed record HikitsuguiOrganizationAudienceOption(
        int Id,
        string Code,
        string NamePt,
        string NameJp);
}
