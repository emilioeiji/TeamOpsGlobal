using Dapper;
using TeamOps.Core.Common;
using TeamOps.Core.Security;
using TeamOps.Data.Db;

namespace TeamOps.Data.Security
{
    public sealed class UserAdministrationService
    {
        private readonly SqliteConnectionFactory _factory;
        private readonly AuthorizationService _authorization;

        public UserAdministrationService(SqliteConnectionFactory factory)
        {
            _factory = factory;
            _authorization = new AuthorizationService(factory);
        }

        public UserAdministrationPayload GetPayload(int actorUserId)
        {
            var actor = DemandAdministrator(actorUserId);
            using var conn = _factory.CreateOpenConnection();
            var nodeIds = GetScopedNodeIds(conn, actor.ReferenceNodeId);
            var profiles = conn.Query<AccessProfileOption>(@"
                SELECT Id, Code, NamePt, NameJp, HierarchyRank, ScopeMode
                FROM AccessProfiles
                WHERE IsActive = 1 AND HierarchyRank < @actorRank
                ORDER BY HierarchyRank DESC;", new { actorRank = actor.HierarchyRank }).ToList();
            var nodes = nodeIds.Count == 0 ? [] : conn.Query<OrganizationNodeOption>(@"
                SELECT Id, ParentId, NodeType, Code, NamePt, NameJp
                FROM OrganizationNodes
                WHERE IsActive = 1 AND Id IN @nodeIds
                ORDER BY Code COLLATE NOCASE;", new { nodeIds }).ToList();
            var sectors = nodeIds.Count == 0 ? [] : conn.Query<SectorAdministrationOption>(@"
                SELECT Id, OrganizationNodeId, Code, NamePt, NameJp
                FROM Sectors
                WHERE IsActive = 1 AND OrganizationNodeId IN @nodeIds
                ORDER BY NamePt COLLATE NOCASE, Id;", new { nodeIds }).ToList();
            var users = QueryManageableUsers(conn, actor, nodeIds);
            return new UserAdministrationPayload(users, profiles, nodes, sectors);
        }

        public int CreateUser(int actorUserId, UserAdministrationSaveRequest request, string password)
        {
            var actor = DemandAdministrator(actorUserId);
            ValidateIdentity(request.Login, request.Name);
            PasswordPolicy.DemandStrong(password);
            using var conn = _factory.CreateOpenConnection();
            using var transaction = conn.BeginTransaction();
            var target = ValidateTargetConfiguration(conn, transaction, actor, request, null);
            var legacyLevel = ToLegacyAccessLevel(target.HierarchyRank);
            var hash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
            var userId = conn.ExecuteScalar<int>(@"
                INSERT INTO Users
                    (Login, CodigoFJ, Name, PasswordHash, AccessLevel, ProfileId, ReferenceNodeId,
                     IsActive, PasswordChangedAt, SessionVersion)
                VALUES
                    (@Login, @CodigoFJ, @Name, @hash, @legacyLevel, @profileId, @ReferenceNodeId,
                     @isActive, CURRENT_TIMESTAMP, 1);
                SELECT last_insert_rowid();",
                new
                {
                    Login = request.Login.Trim(),
                    CodigoFJ = NullIfWhiteSpace(request.CodigoFJ),
                    Name = request.Name.Trim(),
                    hash,
                    legacyLevel,
                    profileId = target.Id,
                    request.ReferenceNodeId,
                    isActive = request.IsActive ? 1 : 0
                }, transaction);
            ReplaceSectorAssignments(conn, transaction, userId, request.SectorIds, actorUserId, target.ScopeMode);
            WriteSecurityAudit(conn, transaction, actorUserId, "UserCreated", userId, Describe(request));
            transaction.Commit();
            return userId;
        }

        public void UpdateUser(int actorUserId, int targetUserId, UserAdministrationSaveRequest request)
        {
            var actor = DemandAdministrator(actorUserId);
            ValidateIdentity(request.Login, request.Name);
            using var conn = _factory.CreateOpenConnection();
            using var transaction = conn.BeginTransaction();
            DemandCanManage(conn, transaction, actor, targetUserId);
            var target = ValidateTargetConfiguration(conn, transaction, actor, request, targetUserId);
            var affected = conn.Execute(@"
                UPDATE Users
                SET CodigoFJ = @CodigoFJ,
                    Name = @Name,
                    AccessLevel = @legacyLevel,
                    ProfileId = @profileId,
                    ReferenceNodeId = @ReferenceNodeId,
                    IsActive = @isActive,
                    SessionVersion = SessionVersion + 1
                WHERE Id = @targetUserId AND Login = @Login COLLATE NOCASE;",
                new
                {
                    targetUserId,
                    Login = request.Login.Trim(),
                    CodigoFJ = NullIfWhiteSpace(request.CodigoFJ),
                    Name = request.Name.Trim(),
                    legacyLevel = ToLegacyAccessLevel(target.HierarchyRank),
                    profileId = target.Id,
                    request.ReferenceNodeId,
                    isActive = request.IsActive ? 1 : 0
                }, transaction);
            if (affected != 1)
                throw new InvalidOperationException("Usuario nao encontrado ou login divergente.");
            ReplaceSectorAssignments(conn, transaction, targetUserId, request.SectorIds, actorUserId, target.ScopeMode);
            WriteSecurityAudit(conn, transaction, actorUserId, "UserAccessConfigured", targetUserId, Describe(request));
            transaction.Commit();
        }

        public void ResetPassword(int actorUserId, int targetUserId, string password)
        {
            var actor = DemandAdministrator(actorUserId);
            PasswordPolicy.DemandStrong(password);
            using var conn = _factory.CreateOpenConnection();
            using var transaction = conn.BeginTransaction();
            DemandCanManage(conn, transaction, actor, targetUserId);
            var hash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
            if (conn.Execute(@"
                UPDATE Users
                SET PasswordHash = @hash,
                    PasswordChangedAt = CURRENT_TIMESTAMP,
                    FailedLoginAttempts = 0,
                    LockedUntil = NULL,
                    SessionVersion = SessionVersion + 1
                WHERE Id = @targetUserId;", new { hash, targetUserId }, transaction) != 1)
                throw new InvalidOperationException("Usuario nao encontrado.");
            conn.Execute(@"
                INSERT INTO AuthenticationAudit (UserId, Login, EventType, Workstation, Details)
                SELECT Id, Login, 'PasswordReset', @workstation, 'Administrative password reset'
                FROM Users WHERE Id = @targetUserId;",
                new { targetUserId, workstation = Environment.MachineName }, transaction);
            WriteSecurityAudit(conn, transaction, actorUserId, "PasswordReset", targetUserId, "Administrative password reset");
            transaction.Commit();
        }

        private UserAccessContext DemandAdministrator(int actorUserId)
        {
            _authorization.Demand(actorUserId, ModuleCodes.AccessControl, PermissionActions.Administer);
            return _authorization.GetContext(actorUserId);
        }

        private static List<int> GetScopedNodeIds(System.Data.IDbConnection conn, int referenceNodeId,
            System.Data.IDbTransaction? transaction = null) =>
            conn.Query<int>(@"
                WITH RECURSIVE scope(Id) AS (
                    SELECT Id FROM OrganizationNodes WHERE Id = @referenceNodeId AND IsActive = 1
                    UNION ALL
                    SELECT child.Id FROM OrganizationNodes child
                    JOIN scope parent ON child.ParentId = parent.Id
                    WHERE child.IsActive = 1
                ) SELECT Id FROM scope;", new { referenceNodeId }, transaction).ToList();

        private static List<UserAdministrationRow> QueryManageableUsers(
            System.Data.IDbConnection conn, UserAccessContext actor, IReadOnlyCollection<int> nodeIds) =>
            nodeIds.Count == 0 ? [] : conn.Query<UserAdministrationRow>(@"
                SELECT u.Id, u.Login, COALESCE(u.CodigoFJ, '') AS CodigoFJ, COALESCE(u.Name, '') AS Name,
                       p.Code AS ProfileCode, p.NamePt AS ProfileNamePt, p.NameJp AS ProfileNameJp,
                       u.ReferenceNodeId, reference.Code AS ReferenceCode, u.IsActive,
                       strftime('%Y-%m-%d %H:%M', u.CreatedAt) AS CreatedAt,
                       COALESCE(group_concat(assignment.SectorId), '') AS SectorIdsCsv
                FROM Users u
                JOIN AccessProfiles p ON p.Id = u.ProfileId AND p.IsActive = 1
                JOIN OrganizationNodes reference ON reference.Id = u.ReferenceNodeId
                LEFT JOIN UserSectorAssignments assignment ON assignment.UserId = u.Id
                WHERE p.HierarchyRank < @actorRank AND u.ReferenceNodeId IN @nodeIds
                GROUP BY u.Id
                ORDER BY p.HierarchyRank DESC, COALESCE(NULLIF(u.Name, ''), u.Login) COLLATE NOCASE;",
                new { actorRank = actor.HierarchyRank, nodeIds }).ToList();

        private static AccessProfileOption ValidateTargetConfiguration(
            System.Data.IDbConnection conn, System.Data.IDbTransaction transaction,
            UserAccessContext actor, UserAdministrationSaveRequest request, int? targetUserId)
        {
            var profile = conn.QuerySingleOrDefault<AccessProfileOption>(@"
                SELECT Id, Code, NamePt, NameJp, HierarchyRank, ScopeMode
                FROM AccessProfiles WHERE Code = @code COLLATE NOCASE AND IsActive = 1;",
                new { code = request.ProfileCode.Trim() }, transaction)
                ?? throw new InvalidOperationException("Perfil de acesso invalido.");
            if (profile.HierarchyRank >= actor.HierarchyRank)
                throw new UnauthorizedAccessException("Nao e permitido administrar perfil igual ou superior ao seu.");
            var scopedNodes = GetScopedNodeIds(conn, actor.ReferenceNodeId, transaction);
            if (!scopedNodes.Contains(request.ReferenceNodeId))
                throw new UnauthorizedAccessException("Referencia organizacional fora do seu escopo.");
            var nodeType = conn.ExecuteScalar<string?>(
                "SELECT NodeType FROM OrganizationNodes WHERE Id = @id AND IsActive = 1;",
                new { id = request.ReferenceNodeId }, transaction);
            if (profile.ScopeMode == "OrganizationTree" && nodeType != "Unit")
                throw new InvalidOperationException("Perfis de arvore devem referenciar uma unidade.");
            if (profile.ScopeMode == "AssignedSectors" && request.SectorIds.Count == 0)
                throw new InvalidOperationException("Selecione ao menos um setor para este perfil.");
            var allowedSectors = conn.Query<int>(@"
                SELECT Id FROM Sectors WHERE IsActive = 1 AND OrganizationNodeId IN @scopedNodes;",
                new { scopedNodes }, transaction).ToHashSet();
            if (request.SectorIds.Any(id => !allowedSectors.Contains(id)))
                throw new UnauthorizedAccessException("Setor fora do seu escopo administrativo.");
            var codigoFJ = NullIfWhiteSpace(request.CodigoFJ);
            if (profile.ScopeMode == "AssignedSectors" && codigoFJ == null)
                throw new InvalidOperationException("Codigo FJ obrigatorio para perfis operacionais.");
            if (codigoFJ != null && conn.ExecuteScalar<long>(
                    "SELECT EXISTS(SELECT 1 FROM Operators WHERE CodigoFJ = @codigoFJ COLLATE NOCASE AND Status = 1);",
                    new { codigoFJ }, transaction) != 1)
                throw new InvalidOperationException("Codigo FJ nao encontrado entre os operadores ativos.");
            var duplicate = conn.ExecuteScalar<long>(@"
                SELECT EXISTS(SELECT 1 FROM Users
                    WHERE (@targetUserId IS NULL OR Id <> @targetUserId)
                      AND (Login = @login COLLATE NOCASE
                           OR (@codigoFJ IS NOT NULL AND CodigoFJ = @codigoFJ COLLATE NOCASE)));",
                new { targetUserId, login = request.Login.Trim(), codigoFJ }, transaction);
            if (duplicate == 1)
                throw new InvalidOperationException("Login ou codigo FJ ja cadastrado.");
            return profile;
        }

        private static void DemandCanManage(System.Data.IDbConnection conn, System.Data.IDbTransaction transaction,
            UserAccessContext actor, int targetUserId)
        {
            var target = conn.QuerySingleOrDefault<ManagedUserReference>(@"
                SELECT p.HierarchyRank, u.ReferenceNodeId
                FROM Users u JOIN AccessProfiles p ON p.Id = u.ProfileId
                WHERE u.Id = @targetUserId;", new { targetUserId }, transaction);
            if (target == default || target.HierarchyRank >= actor.HierarchyRank
                || !GetScopedNodeIds(conn, actor.ReferenceNodeId, transaction).Contains(target.ReferenceNodeId))
                throw new UnauthorizedAccessException("Usuario fora do seu escopo administrativo.");
        }

        private static void ReplaceSectorAssignments(System.Data.IDbConnection conn, System.Data.IDbTransaction transaction,
            int userId, IReadOnlyCollection<int> sectorIds, int actorUserId, string scopeMode)
        {
            conn.Execute("DELETE FROM UserSectorAssignments WHERE UserId = @userId;", new { userId }, transaction);
            if (scopeMode != "AssignedSectors") return;
            foreach (var sectorId in sectorIds.Distinct())
                conn.Execute(@"INSERT INTO UserSectorAssignments (UserId, SectorId, AssignedByUserId)
                               VALUES (@userId, @sectorId, @actorUserId);",
                    new { userId, sectorId, actorUserId }, transaction);
        }

        private static void WriteSecurityAudit(System.Data.IDbConnection conn, System.Data.IDbTransaction transaction,
            int actorUserId, string eventType, int targetUserId, string details) =>
            conn.Execute(@"INSERT INTO SecurityAudit (ActorUserId, EventType, TargetType, TargetId, Details)
                           VALUES (@actorUserId, @eventType, 'User', @targetId, @details);",
                new { actorUserId, eventType, targetId = targetUserId.ToString(), details }, transaction);

        private static void ValidateIdentity(string login, string name)
        {
            if ((login ?? string.Empty).Trim().Length < 4)
                throw new ArgumentException("O login deve possuir pelo menos 4 caracteres.");
            if ((name ?? string.Empty).Trim().Length < 2)
                throw new ArgumentException("Informe o nome do usuario.");
        }

        private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        private static int ToLegacyAccessLevel(int rank) => rank switch
        {
            >= 5 => (int)AccessLevel.Admin,
            4 => (int)AccessLevel.Manager,
            3 => (int)AccessLevel.GL,
            2 => (int)AccessLevel.KL,
            _ => (int)AccessLevel.Basic
        };
        private static string Describe(UserAdministrationSaveRequest request) =>
            $"Profile={request.ProfileCode};Reference={request.ReferenceNodeId};Sectors={string.Join(',', request.SectorIds.Distinct())};Active={request.IsActive}";

        private sealed class ManagedUserReference
        {
            public int HierarchyRank { get; set; }
            public int ReferenceNodeId { get; set; }
        }
    }

    public sealed record UserAdministrationSaveRequest(string Login, string? CodigoFJ, string Name,
        string ProfileCode, int ReferenceNodeId, IReadOnlyCollection<int> SectorIds, bool IsActive);
    public sealed record UserAdministrationPayload(IReadOnlyList<UserAdministrationRow> Users,
        IReadOnlyList<AccessProfileOption> Profiles, IReadOnlyList<OrganizationNodeOption> OrganizationNodes,
        IReadOnlyList<SectorAdministrationOption> Sectors);
    public sealed class UserAdministrationRow
    {
        public int Id { get; set; }
        public string Login { get; set; } = string.Empty;
        public string CodigoFJ { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ProfileCode { get; set; } = string.Empty;
        public string ProfileNamePt { get; set; } = string.Empty;
        public string ProfileNameJp { get; set; } = string.Empty;
        public int ReferenceNodeId { get; set; }
        public string ReferenceCode { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string CreatedAt { get; set; } = string.Empty;
        public string SectorIdsCsv { get; set; } = string.Empty;
        public int[] SectorIds => SectorIdsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
    }
    public sealed class AccessProfileOption
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string NamePt { get; set; } = string.Empty;
        public string NameJp { get; set; } = string.Empty;
        public int HierarchyRank { get; set; }
        public string ScopeMode { get; set; } = string.Empty;
    }
    public sealed class OrganizationNodeOption
    {
        public int Id { get; set; }
        public int? ParentId { get; set; }
        public string NodeType { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string NamePt { get; set; } = string.Empty;
        public string NameJp { get; set; } = string.Empty;
    }
    public sealed class SectorAdministrationOption
    {
        public int Id { get; set; }
        public int OrganizationNodeId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string NamePt { get; set; } = string.Empty;
        public string NameJp { get; set; } = string.Empty;
    }
}
