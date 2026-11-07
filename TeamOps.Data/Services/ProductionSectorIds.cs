using TeamOps.Data.Db;

namespace TeamOps.Services
{
    internal sealed record ProductionSectorIds(int GBareru, int Dad)
    {
        public static ProductionSectorIds Load(SqliteConnectionFactory factory)
        {
            var capabilities = new SectorCapabilityService(factory);
            return new ProductionSectorIds(
                capabilities.ResolveRequired(SectorFeatureCodes.ProductionGBareruImport),
                capabilities.ResolveRequired(SectorFeatureCodes.ProductionDadImport));
        }
    }
}
