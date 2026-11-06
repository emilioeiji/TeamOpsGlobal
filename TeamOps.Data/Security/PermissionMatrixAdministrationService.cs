using Dapper;
using TeamOps.Core.Security;
using TeamOps.Data.Db;

namespace TeamOps.Data.Security
{
    public sealed class PermissionMatrixAdministrationService
    {
        private readonly SqliteConnectionFactory _factory;
        private readonly AuthorizationService _authorization;

        public PermissionMatrixAdministrationService(SqliteConnectionFactory factory)
        {
            _factory = factory;
            _authorization = new AuthorizationService(factory);
        }

        public PermissionMatrixPayload GetPayload(int actorUserId)
        {
            var actor = DemandDirector(actorUserId);
            using var conn = _factory.CreateOpenConnection();
            var profiles = conn.Query<PermissionProfileOption>(@"
                SELECT Id, Code, NamePt, NameJp, HierarchyRank
                FROM AccessProfiles
                WHERE IsActive = 1 AND HierarchyRank < @actorRank
                ORDER BY HierarchyRank DESC;", new { actorRank = actor.HierarchyRank }).ToList();
            var profileIds = profiles.Select(item => item.Id).ToArray();
            var permissions = profileIds.Length == 0
                ? []
                : conn.Query<ModulePermissionOption>(@"
                    SELECT profile.Code AS ProfileCode,
                           permission.ModuleCode,
                           permission.ActionCode,
                           permission.IsAllowed
                    FROM ModulePermissions permission
                    JOIN AccessProfiles profile ON profile.Id = permission.ProfileId
                    WHERE permission.ProfileId IN @profileIds
                    ORDER BY profile.HierarchyRank DESC, permission.ModuleCode, permission.ActionCode;",
                    new { profileIds }).ToList();
            return new PermissionMatrixPayload(profiles, permissions);
        }

        public void SaveProfilePermissions(int actorUserId, string profileCode,
            IReadOnlyCollection<ModulePermissionSelection> permissions)
        {
            var actor = DemandDirector(actorUserId);
            var normalizedProfile = (profileCode ?? string.Empty).Trim();
            if (normalizedProfile.Length == 0)
                throw new ArgumentException("Selecione um perfil de acesso.", nameof(profileCode));

            var normalized = permissions
                .Where(item => item.IsAllowed)
                .Select(item => new ModulePermissionSelection(
                    (item.ModuleCode ?? string.Empty).Trim().ToLowerInvariant(),
                    (item.ActionCode ?? string.Empty).Trim().ToLowerInvariant(),
                    true))
                .ToList();
            if (normalized.Any(item => !ModuleCodes.All.Contains(item.ModuleCode)
                    || !PermissionActions.All.Contains(item.ActionCode)))
                throw new ArgumentException("A matriz contem modulo ou acao invalida.", nameof(permissions));
            if (normalized.Count != normalized.Select(item => (item.ModuleCode, item.ActionCode)).Distinct().Count())
                throw new ArgumentException("A matriz contem permissoes duplicadas.", nameof(permissions));

            using var conn = _factory.CreateOpenConnection();
            using var transaction = conn.BeginTransaction();
            var profile = conn.QuerySingleOrDefault<PermissionProfileOption>(@"
                SELECT Id, Code, NamePt, NameJp, HierarchyRank
                FROM AccessProfiles
                WHERE Code = @code COLLATE NOCASE AND IsActive = 1;",
                new { code = normalizedProfile }, transaction)
                ?? throw new InvalidOperationException("Perfil de acesso nao encontrado.");
            if (profile.HierarchyRank >= actor.HierarchyRank)
                throw new UnauthorizedAccessException("Nao e permitido alterar perfil igual ou superior ao Diretor atual.");

            conn.Execute("DELETE FROM ModulePermissions WHERE ProfileId = @profileId;",
                new { profileId = profile.Id }, transaction);
            foreach (var permission in normalized)
            {
                conn.Execute(@"
                    INSERT INTO ModulePermissions (ProfileId, ModuleCode, ActionCode, IsAllowed)
                    VALUES (@profileId, @ModuleCode, @ActionCode, 1);",
                    new { profileId = profile.Id, permission.ModuleCode, permission.ActionCode }, transaction);
            }

            conn.Execute(@"
                UPDATE Users
                SET SessionVersion = SessionVersion + 1
                WHERE ProfileId = @profileId;", new { profileId = profile.Id }, transaction);
            conn.Execute(@"
                INSERT INTO SecurityAudit (ActorUserId, EventType, TargetType, TargetId, Details)
                VALUES (@actorUserId, 'PermissionMatrixUpdated', 'AccessProfile', @profileCode, @details);",
                new
                {
                    actorUserId,
                    profileCode = profile.Code,
                    details = string.Join(';', normalized.Select(item => $"{item.ModuleCode}:{item.ActionCode}"))
                }, transaction);
            transaction.Commit();
        }

        private UserAccessContext DemandDirector(int actorUserId)
        {
            _authorization.Demand(actorUserId, ModuleCodes.AccessControl, PermissionActions.Administer);
            var actor = _authorization.GetContext(actorUserId);
            if (actor.HierarchyRank != 6)
                throw new UnauthorizedAccessException("Somente Diretor pode alterar a matriz global de permissoes.");
            return actor;
        }
    }

    public sealed record PermissionMatrixPayload(
        IReadOnlyList<PermissionProfileOption> Profiles,
        IReadOnlyList<ModulePermissionOption> Permissions);

    public sealed class PermissionProfileOption
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string NamePt { get; set; } = string.Empty;
        public string NameJp { get; set; } = string.Empty;
        public int HierarchyRank { get; set; }
    }

    public sealed class ModulePermissionOption
    {
        public string ProfileCode { get; set; } = string.Empty;
        public string ModuleCode { get; set; } = string.Empty;
        public string ActionCode { get; set; } = string.Empty;
        public bool IsAllowed { get; set; }
    }

    public sealed record ModulePermissionSelection(string ModuleCode, string ActionCode, bool IsAllowed);
}
