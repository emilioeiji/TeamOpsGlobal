using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Dapper;
using TeamOps.Data.Db;
using TeamOps.UI.Forms.Models;

namespace TeamOps.UI.Services
{
    public sealed class GBareruCapacityForecastService
    {
        private const string GBareruSectorCode = "G-BARERU";
        private const string EciiCode = "ECII";
        private const string BunkatsuCode = "BUNKATSU";
        private const string DcsCode = "DCS";
        private const double DefaultBreakMinutes = 65d;
        private static readonly string[] BreakCodes = { "KYUKEI", "BREAK", "INTERVALO", "休憩" };

        private readonly SqliteConnectionFactory _factory;

        public GBareruCapacityForecastService(SqliteConnectionFactory factory)
        {
            _factory = factory;
        }

        public GBareruCapacityForecastDto BuildForecast(ProductionDashboardFilter filter, ProductionDashboardDto dashboard)
        {
            var watch = Stopwatch.StartNew();
            var result = new GBareruCapacityForecastDto();
            var gBareruSectorId = ResolveGBareruSectorId();

            if (gBareruSectorId <= 0)
            {
                result.Message = "Setor G-Bareru nao configurado.";
                WriteDiagnostic(result.Message);
                result.CalculationMs = watch.ElapsedMilliseconds;
                return result;
            }

            if (filter.SectorId > 0 && filter.SectorId != gBareruSectorId)
            {
                result.Message = "Previsao disponivel apenas para G-Bareru.";
                WriteDiagnostic(result.Message);
                result.CalculationMs = watch.ElapsedMilliseconds;
                return result;
            }

            var gBareruAreas = dashboard.Areas
                .Where(area => area.SectorId == gBareruSectorId)
                .Where(area => filter.LocalId <= 0 || area.LocalId == filter.LocalId)
                .ToList();

            if (gBareruAreas.Count == 0)
            {
                result.Message = "Sem area G-Bareru no filtro atual.";
                WriteDiagnostic(result.Message);
                result.CalculationMs = watch.ElapsedMilliseconds;
                return result;
            }

            var areaForecasts = new List<AreaForecastComputation>(gBareruAreas.Count);

            foreach (var area in gBareruAreas)
            {
                var times = LoadProcedureTimes(gBareruSectorId, area.LocalId ?? 0);
                var areaForecast = BuildAreaForecast(area, times);
                areaForecasts.Add(areaForecast);
                result.Areas.Add(areaForecast.Forecast);
            }

            var availableAreas = areaForecasts
                .Where(area => string.IsNullOrWhiteSpace(area.Forecast.Message))
                .ToList();

            if (availableAreas.Count == 0)
            {
                result.Message = string.Join(
                    " | ",
                    areaForecasts
                        .Select(area => area.Forecast.Message)
                        .Where(message => !string.IsNullOrWhiteSpace(message))
                        .Distinct(StringComparer.OrdinalIgnoreCase));
                WriteDiagnostic($"[ProductionMonitor][ForecastSummary] Sem previsao: Motivo: {result.Message}");
                result.CalculationMs = watch.ElapsedMilliseconds;
                return result;
            }

            result.IsAvailable = true;
            result.PeopleCount = availableAreas.Sum(area => area.Forecast.PeopleCount);
            result.AvailableMinutes = Math.Round(availableAreas.Sum(area => area.AvailableMinutes), 1);
            result.ForecastCapacity = Math.Round(availableAreas.Sum(area => area.Forecast.ForecastCapacity), 1);
            result.ForecastKadouritsuPercent = Math.Round(availableAreas.Average(area => area.Forecast.ForecastKadouritsuPercent), 1);
            result.RealKadouritsuPercent = Math.Round(availableAreas.Average(area => area.Forecast.RealKadouritsuPercent), 1);
            result.DifferencePercent = Math.Round(result.RealKadouritsuPercent - result.ForecastKadouritsuPercent, 1);
            result.EciiMinutes = ResolveAggregateMinutes(availableAreas.Select(area => area.EciiMinutes));
            result.BunkatsuMinutes = ResolveAggregateMinutes(availableAreas.Select(area => area.BunkatsuMinutes));
            result.DcsMinutes = ResolveAggregateMinutes(availableAreas.Select(area => area.DcsMinutes));
            result.Block1Minutes = Math.Round(result.EciiMinutes + result.BunkatsuMinutes, 1);
            result.Block2Minutes = result.DcsMinutes;

            var first = availableAreas[0].Forecast;
            result.CycleMode = availableAreas.Select(area => area.Forecast.CycleMode).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1
                ? first.CycleMode
                : "mixed";
            result.CycleMinutes = Math.Round(availableAreas.Average(area => area.Forecast.CycleMinutes), 1);
            result.Bottleneck = ResolveBottleneck(result.Block1Minutes, result.Block2Minutes);
            WriteDiagnostic(
                $"[ProductionMonitor][ForecastSummary] Local={(filter.LocalId > 0 ? filter.LocalId.ToString(CultureInfo.InvariantCulture) : "all")} Pessoas={result.PeopleCount} ECII={FormatMinutes(result.EciiMinutes)} BUNKATSU={FormatMinutes(result.BunkatsuMinutes)} DCS={FormatMinutes(result.DcsMinutes)} TempoDeCiclo={FormatMinutes(result.CycleMinutes)} CapacidadePrevista={FormatMinutes(result.ForecastCapacity)} KadouritsuPrevisto={FormatMinutes(result.ForecastKadouritsuPercent)}");
            result.CalculationMs = watch.ElapsedMilliseconds;
            return result;
        }

        private int ResolveGBareruSectorId()
        {
            using var conn = _factory.CreateOpenConnection();
            return conn.QuerySingleOrDefault<int>(
                @"
                    SELECT Id
                    FROM Sectors
                    WHERE IsActive = 1
                      AND Code = @Code COLLATE NOCASE
                    ORDER BY Id
                    LIMIT 1;",
                new { Code = GBareruSectorCode });
        }

        private Dictionary<string, double> LoadProcedureTimes(int sectorId, int localId)
        {
            using var conn = _factory.CreateOpenConnection();

            var rows = conn.Query<ProcedureTimeRow>(
                @"
                    SELECT
                        upper(trim(ProcedureCode)) AS ProcedureCode,
                        StandardMinutes,
                        COALESCE(LocalId, 0) AS LocalId
                    FROM ProductionProcedureTimes
                    WHERE SectorId = @SectorId
                      AND COALESCE(IsActive, 1) = 1
                      AND upper(trim(ProcedureCode)) IN @Codes
                      AND (COALESCE(LocalId, 0) = 0 OR COALESCE(LocalId, 0) = @LocalId)
                    ORDER BY COALESCE(LocalId, 0) DESC;",
                new
                {
                    SectorId = sectorId,
                    LocalId = localId,
                    Codes = new[] { EciiCode, BunkatsuCode, DcsCode }.Concat(BreakCodes).ToArray()
                })
                .ToList();

            return rows
                .GroupBy(row => row.ProcedureCode, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().StandardMinutes, StringComparer.OrdinalIgnoreCase);
        }

        private static AreaForecastComputation BuildAreaForecast(
            ProductionAreaSummaryDto area,
            IReadOnlyDictionary<string, double> times)
        {
            var forecast = new GBareruCapacityAreaForecastDto
            {
                LocalId = area.LocalId,
                LocalNamePt = area.LocalNamePt,
                LocalNameJp = area.LocalNameJp,
                RealKadouritsuPercent = area.ProductionPercent
            };

            var computation = new AreaForecastComputation
            {
                Forecast = forecast
            };

            var peopleCount = ResolvePeopleCount(area);
            forecast.PeopleCount = peopleCount;
            computation.BreakMinutes = ResolveBreakMinutes(times);
            computation.GrossAvailableMinutes = ResolveAvailableMinutes(area);
            computation.AvailableMinutes = Math.Max(0, computation.GrossAvailableMinutes - computation.BreakMinutes);

            if (!TryResolveProcedureTime(times, EciiCode, out var ecii)
                || !TryResolveProcedureTime(times, BunkatsuCode, out var bunkatsu)
                || !TryResolveProcedureTime(times, DcsCode, out var dcs))
            {
                forecast.Message = BuildMissingTimesMessage(times);
                LogAreaForecast(computation, area, forecast.Message);
                return computation;
            }

            computation.EciiMinutes = ecii;
            computation.BunkatsuMinutes = bunkatsu;
            computation.DcsMinutes = dcs;

            if (peopleCount <= 0)
            {
                forecast.Message = "Sem previsao: Motivo: nenhuma alocacao Haidai encontrada.";
                LogAreaForecast(computation, area, forecast.Message);
                return computation;
            }

            if (peopleCount > 2)
            {
                forecast.Message = "Sem previsao: Motivo: mais de 2 pessoas no local.";
                LogAreaForecast(computation, area, forecast.Message);
                return computation;
            }

            var block1 = ecii + bunkatsu;
            var block2 = dcs;
            var totalWork = block1 + block2;
            var cycle = peopleCount == 1
                ? totalWork
                : Math.Max(block1, block2);
            var available = computation.AvailableMinutes;
            var availabilityRatio = computation.GrossAvailableMinutes <= 0
                ? 0
                : Math.Min(1d, available / computation.GrossAvailableMinutes);

            forecast.CycleMode = peopleCount == 1 ? "1 pessoa" : "2 pessoas";
            forecast.CycleMinutes = Math.Round(cycle, 1);
            forecast.ForecastCapacity = cycle <= 0 ? 0 : Math.Round(available / cycle, 1);
            forecast.ForecastKadouritsuPercent = cycle <= 0
                ? 0
                : Math.Round(Math.Min(100d, (totalWork / (cycle * peopleCount)) * 100d * availabilityRatio), 1);

            LogAreaForecast(computation, area, string.Empty);
            return computation;
        }

        private static double ResolveAvailableMinutes(ProductionAreaSummaryDto? area)
        {
            if (area == null)
            {
                return 0;
            }

            var countedMachines = Math.Max(0, area.MachineCount - area.MachinesIgnored);
            return countedMachines <= 0
                ? Math.Round(area.TotalMinutes, 1)
                : Math.Round(area.TotalMinutes / countedMachines, 1);
        }

        private static string ResolveBottleneck(double block1, double block2)
        {
            if (Math.Abs(block1 - block2) < 0.01)
            {
                return "Balanceado";
            }

            return block1 > block2 ? "ECII+Bunkatsu" : "DCS";
        }

        private static bool TryResolveProcedureTime(
            IReadOnlyDictionary<string, double> times,
            string procedureCode,
            out double minutes)
        {
            if (times.TryGetValue(procedureCode, out minutes)
                && double.IsFinite(minutes)
                && minutes > 0)
            {
                return true;
            }

            minutes = 0;
            return false;
        }

        private static string BuildMissingTimesMessage(IReadOnlyDictionary<string, double> times)
        {
            var missing = new List<string>(3);
            if (!TryResolveProcedureTime(times, EciiCode, out _))
            {
                missing.Add(EciiCode);
            }

            if (!TryResolveProcedureTime(times, BunkatsuCode, out _))
            {
                missing.Add(BunkatsuCode);
            }

            if (!TryResolveProcedureTime(times, DcsCode, out _))
            {
                missing.Add(DcsCode);
            }

            return missing.Count == 0
                ? "Sem previsao: Motivo: tempos invalidos em Admin > Tempos de Procedimento do setor G-Bareru."
                : $"Sem previsao: Motivo: nenhum tempo cadastrado valido para {string.Join(", ", missing)} em Admin > Tempos de Procedimento do setor G-Bareru.";
        }

        private static int ResolvePeopleCount(ProductionAreaSummaryDto area)
        {
            if (area.ScheduledOperatorCount > 0)
            {
                return area.ScheduledOperatorCount;
            }

            var names = area.ScheduledOperatorsPt
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();

            if (names.Count == 0)
            {
                names = area.ScheduledOperatorsJp
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .ToList();
            }

            return names
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
        }

        private static double ResolveBreakMinutes(IReadOnlyDictionary<string, double> times)
        {
            foreach (var code in BreakCodes)
            {
                if (TryResolveProcedureTime(times, code, out var minutes))
                {
                    return Math.Round(minutes, 1);
                }
            }

            return DefaultBreakMinutes;
        }

        private static double ResolveAggregateMinutes(IEnumerable<double> values)
        {
            var list = values
                .Where(value => double.IsFinite(value) && value > 0)
                .ToList();

            if (list.Count == 0)
            {
                return 0;
            }

            var first = list[0];
            return list.All(value => Math.Abs(value - first) < 0.01)
                ? Math.Round(first, 1)
                : Math.Round(list.Average(), 1);
        }

        private static string FormatMinutes(double value)
        {
            return value.ToString("0.0", CultureInfo.InvariantCulture);
        }

        private static void LogAreaForecast(
            AreaForecastComputation computation,
            ProductionAreaSummaryDto area,
            string reason)
        {
            var localName = string.IsNullOrWhiteSpace(area.LocalNamePt)
                ? area.LocalNameJp
                : area.LocalNamePt;

            if (!string.IsNullOrWhiteSpace(reason))
            {
                WriteDiagnostic(
                    $"[ProductionMonitor][Forecast] Local={localName} Pessoas={computation.Forecast.PeopleCount} ECII={FormatMinutes(computation.EciiMinutes)} BUNKATSU={FormatMinutes(computation.BunkatsuMinutes)} DCS={FormatMinutes(computation.DcsMinutes)} Kyukei={FormatMinutes(computation.BreakMinutes)} Disponivel={FormatMinutes(computation.AvailableMinutes)} DisponivelBruto={FormatMinutes(computation.GrossAvailableMinutes)} TempoDeCiclo={FormatMinutes(computation.Forecast.CycleMinutes)} CapacidadePrevista={FormatMinutes(computation.Forecast.ForecastCapacity)} KadouritsuPrevisto={FormatMinutes(computation.Forecast.ForecastKadouritsuPercent)} {reason}");
                return;
            }

            WriteDiagnostic(
                $"[ProductionMonitor][Forecast] Local={localName} Pessoas={computation.Forecast.PeopleCount} ECII={FormatMinutes(computation.EciiMinutes)} BUNKATSU={FormatMinutes(computation.BunkatsuMinutes)} DCS={FormatMinutes(computation.DcsMinutes)} Kyukei={FormatMinutes(computation.BreakMinutes)} Disponivel={FormatMinutes(computation.AvailableMinutes)} DisponivelBruto={FormatMinutes(computation.GrossAvailableMinutes)} TempoDeCiclo={FormatMinutes(computation.Forecast.CycleMinutes)} CapacidadePrevista={FormatMinutes(computation.Forecast.ForecastCapacity)} KadouritsuPrevisto={FormatMinutes(computation.Forecast.ForecastKadouritsuPercent)}");
        }

        private static void WriteDiagnostic(string message)
        {
            Debug.WriteLine(message);

            try
            {
                Console.WriteLine(message);
            }
            catch
            {
            }
        }

        private sealed class ProcedureTimeRow
        {
            public string ProcedureCode { get; set; } = string.Empty;
            public double StandardMinutes { get; set; }
            public int LocalId { get; set; }
        }

        private sealed class AreaForecastComputation
        {
            public GBareruCapacityAreaForecastDto Forecast { get; set; } = new();
            public double EciiMinutes { get; set; }
            public double BunkatsuMinutes { get; set; }
            public double DcsMinutes { get; set; }
            public double BreakMinutes { get; set; }
            public double GrossAvailableMinutes { get; set; }
            public double AvailableMinutes { get; set; }
        }
    }
}
