using Dapper;
using TeamOps.Data.Db;

namespace TeamOps.Services
{
    public static class SectorFeatureCodes
    {
        public const string ProductionGBareruImport = "production.import.gbareru";
        public const string ProductionDadImport = "production.import.dad";
        public const string ProductionCapacityForecast = "production.capacity-forecast";
        public const string SobraDePeca = "sobra-de-peca";

        public static IReadOnlyList<string> All { get; } =
        [
            ProductionGBareruImport,
            ProductionDadImport,
            ProductionCapacityForecast,
            SobraDePeca
        ];
    }

    public sealed class SectorCapabilityService
    {
        private readonly SqliteConnectionFactory _factory;

        public SectorCapabilityService(SqliteConnectionFactory factory)
        {
            _factory = factory;
        }

        public int ResolveRequired(string featureCode)
        {
            var sectorId = TryResolve(featureCode);
            return sectorId ?? throw new InvalidOperationException(
                $"Nenhum setor ativo esta configurado para a capacidade '{featureCode}'.");
        }

        public int? TryResolve(string featureCode)
        {
            if (string.IsNullOrWhiteSpace(featureCode))
                throw new ArgumentException("Informe a capacidade setorial.", nameof(featureCode));

            using var conn = _factory.CreateOpenConnection();
            return conn.QuerySingleOrDefault<int?>(@"
                SELECT capability.SectorId
                FROM SectorCapabilities capability
                JOIN Sectors sector ON sector.Id = capability.SectorId AND sector.IsActive = 1
                JOIN OrganizationNodes node ON node.Id = sector.OrganizationNodeId AND node.IsActive = 1
                WHERE capability.FeatureCode = @featureCode COLLATE NOCASE
                  AND capability.IsActive = 1
                LIMIT 1;", new { featureCode = featureCode.Trim() });
        }
    }
}
