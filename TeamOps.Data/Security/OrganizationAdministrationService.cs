using Dapper;
using TeamOps.Core.Security;
using TeamOps.Data.Db;

namespace TeamOps.Data.Security
{
    public sealed class OrganizationAdministrationService
    {
        private static readonly HashSet<string> ProtectedCodes = new(StringComparer.OrdinalIgnoreCase) { "5500", "5552" };
        private readonly SqliteConnectionFactory _factory;
        private readonly AdministrativeScopeService _scope;

        public OrganizationAdministrationService(SqliteConnectionFactory factory)
        {
            _factory = factory;
            _scope = new AdministrativeScopeService(factory);
        }

        public int AddUnit(int actorUserId, OrganizationUnitSaveRequest request)
        {
            _scope.DemandGlobal(actorUserId, PermissionActions.Create);
            Validate(request.Code, request.NamePt);
            using var conn = _factory.CreateOpenConnection();
            using var transaction = conn.BeginTransaction();
            DemandParentUnit(conn, transaction, request.ParentId, null);
            DemandUniqueCode(conn, transaction, request.Code, null);
            var id = conn.ExecuteScalar<int>(@"
                INSERT INTO OrganizationNodes (ParentId, NodeType, Code, NamePt, NameJp, IsActive)
                VALUES (@ParentId, 'Unit', @Code, @NamePt, @NameJp, @IsActive);
                SELECT last_insert_rowid();", Normalize(request), transaction);
            WriteAudit(conn, transaction, actorUserId, "OrganizationUnitCreated", "OrganizationNode", id, request.Code);
            transaction.Commit();
            return id;
        }

        public void UpdateUnit(int actorUserId, int nodeId, OrganizationUnitSaveRequest request)
        {
            _scope.DemandGlobal(actorUserId, PermissionActions.Edit);
            Validate(request.Code, request.NamePt);
            using var conn = _factory.CreateOpenConnection();
            using var transaction = conn.BeginTransaction();
            var existingCode = conn.ExecuteScalar<string?>(
                "SELECT Code FROM OrganizationNodes WHERE Id = @nodeId AND NodeType = 'Unit';",
                new { nodeId }, transaction) ?? throw new InvalidOperationException("Unidade organizacional nao encontrada.");
            if (ProtectedCodes.Contains(existingCode) && !existingCode.Equals(request.Code.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Os codigos estruturais 5500 e 5552 nao podem ser alterados.");
            DemandParentUnit(conn, transaction, request.ParentId, nodeId);
            DemandUniqueCode(conn, transaction, request.Code, nodeId);
            if (!request.IsActive)
                DemandEmptyUnit(conn, transaction, nodeId);
            var values = Normalize(request);
            if (conn.Execute(@"
                UPDATE OrganizationNodes
                SET ParentId = @ParentId, Code = @Code, NamePt = @NamePt, NameJp = @NameJp,
                    IsActive = @IsActive, UpdatedAt = CURRENT_TIMESTAMP
                WHERE Id = @nodeId AND NodeType = 'Unit';",
                new { values.ParentId, values.Code, values.NamePt, values.NameJp, values.IsActive, nodeId }, transaction) != 1)
                throw new InvalidOperationException("Unidade organizacional nao encontrada.");
            WriteAudit(conn, transaction, actorUserId, "OrganizationUnitUpdated", "OrganizationNode", nodeId, request.Code);
            transaction.Commit();
        }

        public int AddSector(int actorUserId, OrganizationSectorSaveRequest request)
        {
            _scope.DemandGlobal(actorUserId, PermissionActions.Create);
            Validate(request.Code, request.NamePt);
            using var conn = _factory.CreateOpenConnection();
            using var transaction = conn.BeginTransaction();
            DemandParentUnit(conn, transaction, request.ParentUnitId, null, required: true);
            DemandUniqueCode(conn, transaction, request.Code, null);
            var normalized = Normalize(request);
            var nodeId = conn.ExecuteScalar<int>(@"
                INSERT INTO OrganizationNodes (ParentId, NodeType, Code, NamePt, NameJp, IsActive)
                VALUES (@ParentUnitId, 'Sector', @Code, @NamePt, @NameJp, @IsActive);
                SELECT last_insert_rowid();", normalized, transaction);
            var sectorId = conn.ExecuteScalar<int>(@"
                INSERT INTO Sectors (Code, NamePt, NameJp, OrganizationNodeId, IsActive)
                VALUES (@Code, @NamePt, @NameJp, @nodeId, @IsActive);
                SELECT last_insert_rowid();",
                new { normalized.Code, normalized.NamePt, normalized.NameJp, nodeId, normalized.IsActive }, transaction);
            WriteAudit(conn, transaction, actorUserId, "OrganizationSectorCreated", "Sector", sectorId, request.Code);
            transaction.Commit();
            return sectorId;
        }

        public void UpdateSector(int actorUserId, int sectorId, OrganizationSectorSaveRequest request)
        {
            _scope.DemandGlobal(actorUserId, PermissionActions.Edit);
            Validate(request.Code, request.NamePt);
            using var conn = _factory.CreateOpenConnection();
            using var transaction = conn.BeginTransaction();
            var nodeId = conn.ExecuteScalar<int?>(
                "SELECT OrganizationNodeId FROM Sectors WHERE Id = @sectorId;", new { sectorId }, transaction)
                ?? throw new InvalidOperationException("Setor organizacional nao encontrado.");
            DemandParentUnit(conn, transaction, request.ParentUnitId, nodeId, required: true);
            DemandUniqueCode(conn, transaction, request.Code, nodeId);
            var normalized = Normalize(request);
            if (conn.Execute(@"
                UPDATE OrganizationNodes
                SET ParentId = @ParentUnitId, Code = @Code, NamePt = @NamePt, NameJp = @NameJp,
                    IsActive = @IsActive, UpdatedAt = CURRENT_TIMESTAMP
                WHERE Id = @nodeId AND NodeType = 'Sector';
                UPDATE Sectors
                SET Code = @Code, NamePt = @NamePt, NameJp = @NameJp, IsActive = @IsActive
                WHERE Id = @sectorId AND OrganizationNodeId = @nodeId;",
                new { normalized.ParentUnitId, normalized.Code, normalized.NamePt, normalized.NameJp, normalized.IsActive, nodeId, sectorId }, transaction) != 2)
                throw new InvalidOperationException("Nao foi possivel sincronizar o setor e o no organizacional.");
            WriteAudit(conn, transaction, actorUserId, "OrganizationSectorUpdated", "Sector", sectorId, request.Code);
            transaction.Commit();
        }

        public void DeactivateUnit(int actorUserId, int nodeId)
        {
            _scope.DemandGlobal(actorUserId, PermissionActions.Delete);
            using var conn = _factory.CreateOpenConnection();
            using var transaction = conn.BeginTransaction();
            var code = conn.ExecuteScalar<string?>(
                "SELECT Code FROM OrganizationNodes WHERE Id = @nodeId AND NodeType = 'Unit';", new { nodeId }, transaction)
                ?? throw new InvalidOperationException("Unidade organizacional nao encontrada.");
            if (ProtectedCodes.Contains(code))
                throw new InvalidOperationException("As unidades estruturais 5500 e 5552 nao podem ser desativadas.");
            DemandEmptyUnit(conn, transaction, nodeId);
            conn.Execute("UPDATE OrganizationNodes SET IsActive = 0, UpdatedAt = CURRENT_TIMESTAMP WHERE Id = @nodeId;", new { nodeId }, transaction);
            WriteAudit(conn, transaction, actorUserId, "OrganizationUnitDeactivated", "OrganizationNode", nodeId, code);
            transaction.Commit();
        }

        public void DeactivateSector(int actorUserId, int sectorId)
        {
            _scope.DemandGlobal(actorUserId, PermissionActions.Delete);
            using var conn = _factory.CreateOpenConnection();
            using var transaction = conn.BeginTransaction();
            var item = conn.QuerySingleOrDefault<SectorReference>(@"
                SELECT Id, OrganizationNodeId, Code FROM Sectors WHERE Id = @sectorId;",
                new { sectorId }, transaction) ?? throw new InvalidOperationException("Setor organizacional nao encontrado.");
            if (conn.Execute(@"
                UPDATE Sectors SET IsActive = 0 WHERE Id = @sectorId;
                UPDATE OrganizationNodes SET IsActive = 0, UpdatedAt = CURRENT_TIMESTAMP WHERE Id = @nodeId;",
                new { sectorId, nodeId = item.OrganizationNodeId }, transaction) != 2)
                throw new InvalidOperationException("Nao foi possivel desativar o setor e o no organizacional.");
            WriteAudit(conn, transaction, actorUserId, "OrganizationSectorDeactivated", "Sector", sectorId, item.Code);
            transaction.Commit();
        }

        private static void DemandParentUnit(System.Data.IDbConnection conn, System.Data.IDbTransaction transaction,
            int? parentId, int? currentNodeId, bool required = false)
        {
            if (!parentId.HasValue || parentId.Value <= 0)
            {
                if (required) throw new InvalidOperationException("Selecione a unidade superior.");
                return;
            }
            if (parentId == currentNodeId || conn.ExecuteScalar<long>(
                    "SELECT EXISTS(SELECT 1 FROM OrganizationNodes WHERE Id = @parentId AND NodeType = 'Unit' AND IsActive = 1);",
                    new { parentId }, transaction) != 1)
                throw new InvalidOperationException("Unidade superior invalida.");
        }

        private static void DemandUniqueCode(System.Data.IDbConnection conn, System.Data.IDbTransaction transaction,
            string code, int? currentNodeId)
        {
            if (conn.ExecuteScalar<long>(@"
                    SELECT EXISTS(SELECT 1 FROM OrganizationNodes
                    WHERE Code = @code COLLATE NOCASE AND (@currentNodeId IS NULL OR Id <> @currentNodeId));",
                    new { code = code.Trim(), currentNodeId }, transaction) == 1)
                throw new InvalidOperationException("Ja existe um no organizacional com este codigo.");
        }

        private static void DemandEmptyUnit(System.Data.IDbConnection conn, System.Data.IDbTransaction transaction, int nodeId)
        {
            if (conn.ExecuteScalar<long>(@"
                    SELECT EXISTS(SELECT 1 FROM OrganizationNodes WHERE ParentId = @nodeId AND IsActive = 1);",
                    new { nodeId }, transaction) == 1)
                throw new InvalidOperationException("Desative ou mova os nos filhos antes de desativar a unidade.");
        }

        private static void Validate(string code, string namePt)
        {
            if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Informe o codigo organizacional.");
            if (string.IsNullOrWhiteSpace(namePt)) throw new ArgumentException("Informe o nome em portugues.");
        }

        private static NormalizedUnit Normalize(OrganizationUnitSaveRequest request) => new(
            request.ParentId > 0 ? request.ParentId : null,
            request.Code.Trim().ToUpperInvariant(),
            request.NamePt.Trim(),
            (request.NameJp ?? string.Empty).Trim(),
            request.IsActive ? 1 : 0);

        private static NormalizedSector Normalize(OrganizationSectorSaveRequest request) => new(
            request.ParentUnitId,
            request.Code.Trim().ToUpperInvariant(),
            request.NamePt.Trim(),
            (request.NameJp ?? string.Empty).Trim(),
            request.IsActive ? 1 : 0);

        private static void WriteAudit(System.Data.IDbConnection conn, System.Data.IDbTransaction transaction,
            int actorUserId, string eventType, string targetType, int targetId, string details) =>
            conn.Execute(@"INSERT INTO SecurityAudit (ActorUserId, EventType, TargetType, TargetId, Details)
                           VALUES (@actorUserId, @eventType, @targetType, @targetId, @details);",
                new { actorUserId, eventType, targetType, targetId = targetId.ToString(), details }, transaction);

        private sealed class SectorReference
        {
            public int Id { get; set; }
            public int OrganizationNodeId { get; set; }
            public string Code { get; set; } = string.Empty;
        }

        private sealed record NormalizedUnit(int? ParentId, string Code, string NamePt, string NameJp, int IsActive);
        private sealed record NormalizedSector(int ParentUnitId, string Code, string NamePt, string NameJp, int IsActive);
    }

    public sealed record OrganizationUnitSaveRequest(int? ParentId, string Code, string NamePt, string? NameJp, bool IsActive);
    public sealed record OrganizationSectorSaveRequest(int ParentUnitId, string Code, string NamePt, string? NameJp, bool IsActive);
}
