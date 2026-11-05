using TeamOps.Core.Security;
using TeamOps.Data.Db;

namespace TeamOps.Data.Security
{
    public sealed class AdministrativeScopeService
    {
        private readonly AuthorizationService _authorization;

        public AdministrativeScopeService(SqliteConnectionFactory factory)
        {
            _authorization = new AuthorizationService(factory);
        }

        public AdministrativeScope GetScope(int userId, string action = PermissionActions.Administer)
        {
            _authorization.Demand(userId, ModuleCodes.Organization, action);
            var context = _authorization.GetContext(userId);
            return new AdministrativeScope(
                context.UserId,
                context.ProfileCode,
                context.HierarchyRank,
                context.AuthorizedSectorIds);
        }

        public void DemandSector(int userId, int sectorId, string action)
        {
            if (sectorId <= 0)
                throw new UnauthorizedAccessException("O cadastro deve estar associado a um setor autorizado.");
            _authorization.Demand(userId, ModuleCodes.Organization, action, sectorId);
        }

        public void DemandGlobal(int userId, string action)
        {
            var scope = GetScope(userId, action);
            if (!scope.CanManageGlobalCatalogs)
                throw new UnauthorizedAccessException("Somente Diretor pode administrar catalogos globais.");
        }
    }

    public sealed record AdministrativeScope(
        int UserId,
        string ProfileCode,
        int HierarchyRank,
        IReadOnlySet<int> AuthorizedSectorIds)
    {
        public bool CanManageGlobalCatalogs => HierarchyRank >= 6;
        public bool ContainsSector(int sectorId) => AuthorizedSectorIds.Contains(sectorId);
    }
}
