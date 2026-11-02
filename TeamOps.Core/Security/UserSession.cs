namespace TeamOps.Core.Security
{
    public sealed class UserSession
    {
        public UserSession(UserAccessContext access, IReadOnlyList<SectorAccessOption> sectors)
        {
            Access = access;
            Sectors = sectors;
            CanUseAggregateScope = string.Equals(access.ScopeMode, "OrganizationTree", StringComparison.Ordinal);
            SelectedSectorId = sectors.Count == 1 || !CanUseAggregateScope && sectors.Count > 0
                ? sectors[0].Id
                : null;
        }

        public UserAccessContext Access { get; }
        public IReadOnlyList<SectorAccessOption> Sectors { get; }
        public bool CanUseAggregateScope { get; }
        public int? SelectedSectorId { get; private set; }

        public void SelectSector(int? sectorId)
        {
            if (!sectorId.HasValue)
            {
                if (!CanUseAggregateScope)
                    throw new UnauthorizedAccessException("Este perfil deve selecionar um setor explicito.");
                SelectedSectorId = null;
                return;
            }

            if (!Access.AuthorizedSectorIds.Contains(sectorId.Value))
                throw new UnauthorizedAccessException("Setor fora do escopo autorizado.");

            SelectedSectorId = sectorId.Value;
        }
    }
}
