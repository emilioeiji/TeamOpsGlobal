using Dapper;
using TeamOps.Data.Db;

namespace TeamOps.Services
{
    internal sealed record ProductionSectorIds(int GBareru, int Dad)
    {
        public static ProductionSectorIds Load(SqliteConnectionFactory factory)
        {
            using var conn = factory.CreateOpenConnection();
            var ids = conn.Query<SectorCodeRow>(
                    @"SELECT Id, Code
                      FROM Sectors
                      WHERE IsActive = 1
                        AND Code IN ('G-BARERU', 'DAD') COLLATE NOCASE;")
                .ToDictionary(row => row.Code, row => row.Id, StringComparer.OrdinalIgnoreCase);

            if (!ids.TryGetValue("G-BARERU", out var gBareruId))
                throw new InvalidOperationException("Setor G-BARERU nao configurado.");
            if (!ids.TryGetValue("DAD", out var dadId))
                throw new InvalidOperationException("Setor DAD nao configurado.");

            return new ProductionSectorIds(gBareruId, dadId);
        }

        private sealed class SectorCodeRow
        {
            public int Id { get; set; }
            public string Code { get; set; } = string.Empty;
        }
    }
}
