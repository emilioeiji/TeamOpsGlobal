using Dapper;
using TeamOps.Data.Db;

namespace TeamOps.Services
{
    public sealed class SectorCatalogService
    {
        private readonly SqliteConnectionFactory _factory;

        public SectorCatalogService(SqliteConnectionFactory factory)
        {
            _factory = factory;
        }

        public int ResolveRequired(string code)
        {
            var normalized = Normalize(code);
            var sector = LoadActiveSectors()
                .FirstOrDefault(item => Normalize(item.Code) == normalized);
            return sector?.Id
                ?? throw new InvalidOperationException($"Setor ativo nao configurado: {code}.");
        }

        public int ResolveFilter(string value)
        {
            var normalized = Normalize(value);
            if (normalized is "" or "ALL" or "*")
                return 0;

            var sectors = LoadActiveSectors();
            if (int.TryParse(normalized, out var sectorId))
            {
                return sectors.Any(item => item.Id == sectorId)
                    ? sectorId
                    : throw new InvalidOperationException($"Setor ativo nao encontrado: {value}.");
            }

            var sector = sectors.FirstOrDefault(item =>
                Normalize(item.Code) == normalized
                || Normalize(item.NamePt) == normalized
                || Normalize(item.NameJp) == normalized);
            return sector?.Id
                ?? throw new InvalidOperationException($"Setor ativo nao encontrado: {value}.");
        }

        public string GetLabel(int sectorId)
        {
            var sector = LoadActiveSectors().FirstOrDefault(item => item.Id == sectorId);
            if (sector == null)
                return $"Setor {sectorId}";

            return string.IsNullOrWhiteSpace(sector.NamePt) ? sector.Code : sector.NamePt;
        }

        private IReadOnlyList<SectorCatalogItem> LoadActiveSectors()
        {
            using var conn = _factory.CreateOpenConnection();
            return conn.Query<SectorCatalogItem>(@"
                SELECT Id, COALESCE(Code, '') AS Code,
                       COALESCE(NamePt, '') AS NamePt,
                       COALESCE(NameJp, '') AS NameJp
                FROM Sectors
                WHERE IsActive = 1
                ORDER BY Id;").ToList();
        }

        private static string Normalize(string? value) =>
            new((value ?? string.Empty)
                .Where(char.IsLetterOrDigit)
                .Select(char.ToUpperInvariant)
                .ToArray());

        private sealed class SectorCatalogItem
        {
            public int Id { get; set; }
            public string Code { get; set; } = string.Empty;
            public string NamePt { get; set; } = string.Empty;
            public string NameJp { get; set; } = string.Empty;
        }
    }
}
