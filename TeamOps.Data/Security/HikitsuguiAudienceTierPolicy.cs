namespace TeamOps.Data.Security
{
    public static class HikitsuguiAudienceTierPolicy
    {
        public static bool CanFilter(int hierarchyRank, string? audience)
        {
            return (audience ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "operador" or "operators" => hierarchyRank >= 1,
                "lider" or "leaders" => hierarchyRank >= 2,
                "masv" => hierarchyRank >= 3,
                "todos" or "all" => hierarchyRank >= 1,
                _ => false
            };
        }

        public static void DemandCanFilter(int hierarchyRank, string? audience)
        {
            if (!CanFilter(hierarchyRank, audience))
                throw new UnauthorizedAccessException("Perfil sem acesso ao publico solicitado.");
        }
    }
}
