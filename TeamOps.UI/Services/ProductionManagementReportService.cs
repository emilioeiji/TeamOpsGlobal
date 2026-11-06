using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Dapper;
using TeamOps.Data.Db;
using TeamOps.Data.Repositories;
using TeamOps.Services;
using TeamOps.UI.Forms.Models;

namespace TeamOps.UI.Services
{
    public sealed class ProductionManagementReportService
    {
        private readonly SqliteConnectionFactory _factory;
        private readonly ProductionAnalyticsService _analytics;
        private readonly int[] _authorizedSectorIds;

        public ProductionManagementReportService(
            SqliteConnectionFactory factory,
            IEnumerable<int> authorizedSectorIds)
        {
            _factory = factory;
            _analytics = new ProductionAnalyticsService(factory);
            _authorizedSectorIds = authorizedSectorIds
                .Where(id => id > 0)
                .Distinct()
                .ToArray();

            if (_authorizedSectorIds.Length == 0)
                throw new UnauthorizedAccessException("Usuario sem setores autorizados para relatorios.");
        }

        public ProductionManagementInitPayload GetInitialPayload()
        {
            using var conn = _factory.CreateOpenConnection();
            TeamOps.Data.Db.ProductionSchemaMigrator.Ensure(conn);
            new HaidaiModuleService(_factory).EnsureSchema();

            return new ProductionManagementInitPayload(
                QueryLookup(conn, "Shifts").ToList(),
                QueryLookup(conn, "Sectors")
                    .Where(item => _authorizedSectorIds.Contains(item.Id))
                    .ToList(),
                QueryLookup(conn, "Groups").ToList(),
                conn.Query<ProductionManagementLocalOption>(
                    @"
                        SELECT
                            Id,
                            SectorId,
                            COALESCE(NamePt, '') AS NamePt,
                            COALESCE(NULLIF(NameJp, ''), NamePt, '') AS NameJp
                        FROM Locals
                        WHERE SectorId IN @AuthorizedSectorIds
                        ORDER BY SectorId, NamePt;",
                    new { AuthorizedSectorIds = _authorizedSectorIds }).ToList(),
                conn.Query<ProductionManagementMachineOption>(
                    @"
                        SELECT
                            Id,
                            COALESCE(SectorId, 0) AS SectorId,
                            COALESCE(LocalId, 0) AS LocalId,
                            COALESCE(MachineCode, '') AS MachineCode,
                            COALESCE(NamePt, '') AS NamePt,
                            COALESCE(NULLIF(NameJp, ''), NamePt, '') AS NameJp
                        FROM Machines
                        WHERE COALESCE(IsActive, 1) = 1
                          AND SectorId IN @AuthorizedSectorIds
                        ORDER BY COALESCE(MachineCode, NamePt), Id;",
                    new { AuthorizedSectorIds = _authorizedSectorIds }).ToList(),
                conn.Query<ProductionManagementOperatorOption>(
                    @"
                        SELECT
                            CodigoFJ,
                            ShiftId,
                            SectorId,
                            GroupId,
                            COALESCE(IsLeader, 0) AS IsLeader,
                            COALESCE(Status, 1) AS IsActive,
                            COALESCE(NULLIF(NameRomanji, ''), CodigoFJ) AS NamePt,
                            COALESCE(NULLIF(NameNihongo, ''), COALESCE(NULLIF(NameRomanji, ''), CodigoFJ)) AS NameJp
                        FROM Operators
                        WHERE SectorId IN @AuthorizedSectorIds
                        ORDER BY NameRomanji, CodigoFJ;",
                    new { AuthorizedSectorIds = _authorizedSectorIds }).ToList(),
                QueryPartCodes(conn, _authorizedSectorIds).ToList());
        }

        public ProductionManagementReportPayload BuildReport(ProductionManagementReportFilter filter, string userFj)
        {
            var totalWatch = Stopwatch.StartNew();
            var loadProductionWatch = Stopwatch.StartNew();
            var start = ParseDate(filter.StartDateIso, DateTime.Today.AddDays(-29)).Date;
            var end = ParseDate(filter.EndDateIso, DateTime.Today).Date;
            if (end < start)
            {
                (start, end) = (end, start);
            }

            DemandSector(filter.SectorId);

            using var conn = _factory.CreateOpenConnection();
            TeamOps.Data.Db.ProductionSchemaMigrator.Ensure(conn);
            new HaidaiModuleService(_factory).EnsureSchema();

            var shifts = conn.Query<LookupRow>(
                "SELECT Id, COALESCE(NamePt, '') AS NamePt, COALESCE(NULLIF(NameJp, ''), NamePt, '') AS NameJp FROM Shifts ORDER BY Id;")
                .ToList();
            if (filter.ShiftId > 0)
            {
                shifts = shifts.Where(item => item.Id == filter.ShiftId).ToList();
            }

            var dailyDashboards = new List<ProductionDashboardSnapshot>();
            var sectorsToQuery = filter.SectorId > 0
                ? new[] { filter.SectorId }
                : _authorizedSectorIds;
            for (var day = start; day <= end; day = day.AddDays(1))
            {
                foreach (var sectorId in sectorsToQuery)
                {
                    foreach (var shift in shifts)
                    {
                        var dashboard = _analytics.BuildDashboard(new ProductionDashboardFilter
                        {
                            Date = day,
                            ShiftId = shift.Id,
                            SectorId = sectorId,
                            LocalId = filter.LocalId,
                            MachineId = filter.MachineId
                        });

                        if (!string.IsNullOrWhiteSpace(filter.PartCode)
                            && !DashboardHasPartCode(dashboard, filter.PartCode))
                        {
                            continue;
                        }

                        dailyDashboards.Add(new ProductionDashboardSnapshot(day, shift, dashboard));
                    }
                }
            }
            loadProductionWatch.Stop();

            var loadOperatorsWatch = Stopwatch.StartNew();
            var operators = LoadOperators(conn, filter, _authorizedSectorIds).ToList();
            loadOperatorsWatch.Stop();

            var loadPresenceWatch = Stopwatch.StartNew();
            var scheduleRows = LoadScheduleRows(conn, start, end, filter, _authorizedSectorIds).ToList();
            var scheduleByOperator = scheduleRows
                .GroupBy(item => item.OperatorCodigoFJ, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
            var presenceRows = LoadPresenceRows(conn, start, end, operators.Select(item => item.CodigoFJ).ToArray()).ToList();
            var presenceByOperator = BuildPresenceByOperator(presenceRows, scheduleByOperator, start, end);
            loadPresenceWatch.Stop();

            var buildChartsWatch = Stopwatch.StartNew();
            var operatorRows = BuildOperatorRows(operators, dailyDashboards, presenceByOperator, scheduleByOperator, filter).ToList();
            var machineRows = BuildMachineRows(dailyDashboards, filter).ToList();
            var sectorRows = BuildSectorRows(dailyDashboards).ToList();
            var shiftComparison = BuildShiftComparison(dailyDashboards);
            var groupComparison = BuildGroupComparison(operatorRows, filter.GroupAId, filter.GroupBId);
            var dailyTrend = BuildDailyTrend(dailyDashboards).ToList();
            var rankings = BuildRankings(operatorRows, sectorRows, machineRows);
            var alerts = BuildAlerts(operatorRows).ToList();
            buildChartsWatch.Stop();

            var buildReportWatch = Stopwatch.StartNew();
            var summary = BuildSummary(operatorRows, machineRows, sectorRows);
            buildReportWatch.Stop();
            totalWatch.Stop();

            var performance = new ProductionManagementPerformance(
                loadProductionWatch.ElapsedMilliseconds,
                loadOperatorsWatch.ElapsedMilliseconds,
                loadPresenceWatch.ElapsedMilliseconds,
                buildChartsWatch.ElapsedMilliseconds,
                buildReportWatch.ElapsedMilliseconds,
                totalWatch.ElapsedMilliseconds);

            LogAudit(conn, userFj, filter, operatorRows.Count, totalWatch.ElapsedMilliseconds);

            return new ProductionManagementReportPayload(
                start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                summary,
                operatorRows,
                rankings,
                shiftComparison,
                groupComparison,
                dailyTrend,
                sectorRows,
                machineRows,
                BuildPresenceCrossing(operatorRows).ToList(),
                alerts,
                performance);
        }

        private static IEnumerable<ProductionManagementLookupItem> QueryLookup(System.Data.IDbConnection conn, string table)
        {
            return conn.Query<ProductionManagementLookupItem>(
                $@"
                    SELECT
                        Id,
                        COALESCE(NamePt, '') AS NamePt,
                        COALESCE(NULLIF(NameJp, ''), NamePt, '') AS NameJp
                    FROM {table}
                    ORDER BY NamePt;");
        }

        private static IEnumerable<string> QueryPartCodes(
            System.Data.IDbConnection conn,
            int[] authorizedSectorIds)
        {
            return conn.Query<string>(
                @"
                    SELECT DISTINCT PartCode
                    FROM (
                        SELECT COALESCE(ecs.PartCode, '') AS PartCode
                        FROM Ec2MachineCurrentState ecs
                        JOIN Machines m ON m.Id = ecs.MachineId
                        WHERE m.SectorId IN @AuthorizedSectorIds
                        UNION ALL
                        SELECT COALESCE(me.RecipeName, '') AS PartCode
                        FROM MachineEvents me
                        JOIN Machines m ON m.Id = me.MachineId
                        WHERE m.SectorId IN @AuthorizedSectorIds
                    )
                    WHERE trim(PartCode) <> ''
                    ORDER BY PartCode
                    LIMIT 500;",
                new { AuthorizedSectorIds = authorizedSectorIds });
        }

        private static IEnumerable<OperatorRow> LoadOperators(
            System.Data.IDbConnection conn,
            ProductionManagementReportFilter filter,
            int[] authorizedSectorIds)
        {
            return conn.Query<OperatorRow>(
                @"
                    SELECT
                        o.CodigoFJ,
                        COALESCE(NULLIF(o.NameRomanji, ''), o.CodigoFJ) AS NamePt,
                        COALESCE(NULLIF(o.NameNihongo, ''), COALESCE(NULLIF(o.NameRomanji, ''), o.CodigoFJ)) AS NameJp,
                        o.ShiftId,
                        COALESCE(sh.NamePt, '') AS ShiftNamePt,
                        COALESCE(NULLIF(sh.NameJp, ''), sh.NamePt, '') AS ShiftNameJp,
                        o.SectorId,
                        COALESCE(sec.NamePt, '') AS SectorNamePt,
                        COALESCE(NULLIF(sec.NameJp, ''), sec.NamePt, '') AS SectorNameJp,
                        o.GroupId,
                        COALESCE(g.NamePt, '') AS GroupNamePt,
                        COALESCE(NULLIF(g.NameJp, ''), g.NamePt, '') AS GroupNameJp,
                        COALESCE(o.Status, 1) AS IsActive,
                        COALESCE(o.IsLeader, 0) AS IsLeader
                    FROM Operators o
                    LEFT JOIN Shifts sh ON sh.Id = o.ShiftId
                    LEFT JOIN Sectors sec ON sec.Id = o.SectorId
                    LEFT JOIN Groups g ON g.Id = o.GroupId
                    WHERE (@OnlyActive = 0 OR COALESCE(o.Status, 1) = 1)
                      AND o.SectorId IN @AuthorizedSectorIds
                      AND (@ShiftId <= 0 OR o.ShiftId = @ShiftId)
                      AND (@SectorId <= 0 OR o.SectorId = @SectorId)
                      AND (@GroupId <= 0 OR o.GroupId = @GroupId)
                      AND (@OperatorCode = '' OR o.CodigoFJ = @OperatorCode)
                      AND (@LeaderCode = '' OR o.CodigoFJ = @LeaderCode OR COALESCE(o.IsLeader, 0) = 1)
                    ORDER BY o.NameRomanji, o.CodigoFJ;",
                new
                {
                    OnlyActive = filter.OnlyActive ? 1 : 0,
                    filter.ShiftId,
                    filter.SectorId,
                    filter.GroupId,
                    OperatorCode = filter.OperatorCode ?? string.Empty,
                    LeaderCode = filter.LeaderCode ?? string.Empty,
                    AuthorizedSectorIds = authorizedSectorIds
                });
        }

        private static IEnumerable<PresenceRow> LoadPresenceRows(System.Data.IDbConnection conn, DateTime start, DateTime end, string[] operatorCodes)
        {
            if (operatorCodes.Length == 0)
            {
                return Array.Empty<PresenceRow>();
            }

            return conn.Query<PresenceRow>(
                @"
                    SELECT
                        CodigoFJ,
                        date(Date) AS Day,
                        MIN(Date) AS FirstPresence,
                        MAX(Date) AS LastPresence
                    FROM OperatorPresence
                    WHERE CodigoFJ IN @OperatorCodes
                      AND date(Date) BETWEEN date(@Start) AND date(@End)
                    GROUP BY CodigoFJ, date(Date);",
                new
                {
                    OperatorCodes = operatorCodes,
                    Start = start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    End = end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                });
        }

        private static IEnumerable<ScheduleRow> LoadScheduleRows(
            System.Data.IDbConnection conn,
            DateTime start,
            DateTime end,
            ProductionManagementReportFilter filter,
            int[] authorizedSectorIds)
        {
            return conn.Query<ScheduleRow>(
                @"
                    SELECT
                        ha.OperatorCodigoFJ,
                        date(ha.ScheduleDate) AS Day,
                        ha.ShiftId,
                        ha.SectorId,
                        ha.LocalId,
                        COALESCE(ha.IsHolidayWork, 0) AS IsHolidayWork
                    FROM HaidaiAssignments ha
                    WHERE date(ha.ScheduleDate) BETWEEN date(@Start) AND date(@End)
                      AND COALESCE(ha.IsLineupActive, 1) = 1
                      AND ha.SectorId IN @AuthorizedSectorIds
                      AND (@ShiftId <= 0 OR ha.ShiftId = @ShiftId)
                      AND (@SectorId <= 0 OR ha.SectorId = @SectorId)
                      AND (@LocalId <= 0 OR ha.LocalId = @LocalId)
                      AND (@OperatorCode = '' OR ha.OperatorCodigoFJ = @OperatorCode);",
                new
                {
                    Start = start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    End = end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    filter.ShiftId,
                    filter.SectorId,
                    filter.LocalId,
                    OperatorCode = filter.OperatorCode ?? string.Empty,
                    AuthorizedSectorIds = authorizedSectorIds
                });
        }

        private void DemandSector(int sectorId)
        {
            if (sectorId > 0 && !_authorizedSectorIds.Contains(sectorId))
                throw new UnauthorizedAccessException("Setor fora do escopo autorizado.");
        }

        private static Dictionary<string, PresenceAccumulator> BuildPresenceByOperator(
            IEnumerable<PresenceRow> rows,
            IReadOnlyDictionary<string, List<ScheduleRow>> scheduleByOperator,
            DateTime start,
            DateTime end)
        {
            var map = new Dictionary<string, PresenceAccumulator>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                if (!map.TryGetValue(row.CodigoFJ, out var acc))
                {
                    acc = new PresenceAccumulator();
                    map[row.CodigoFJ] = acc;
                }

                acc.PresentDays++;
                var first = ParseDateTime(row.FirstPresence);
                var last = ParseDateTime(row.LastPresence);
                if (first.HasValue && last.HasValue && last.Value > first.Value)
                {
                    acc.WorkedHours += Math.Max(0, (last.Value - first.Value).TotalHours);
                    acc.OvertimeHours += Math.Max(0, (last.Value - first.Value).TotalHours - 8d);
                }

                if (ParseDate(row.Day, start).DayOfWeek == DayOfWeek.Sunday)
                {
                    acc.WorkedSundays++;
                }
            }

            foreach (var code in scheduleByOperator.Keys)
            {
                if (!map.ContainsKey(code))
                {
                    map[code] = new PresenceAccumulator();
                }
            }

            var periodDays = Math.Max(1, (end.Date - start.Date).Days + 1);
            foreach (var pair in map)
            {
                var acc = pair.Value;
                var scheduledDays = scheduleByOperator.TryGetValue(pair.Key, out var schedules)
                    ? schedules.Select(item => item.Day).Distinct(StringComparer.OrdinalIgnoreCase).Count()
                    : 0;
                var denominator = scheduledDays > 0 ? scheduledDays : periodDays;
                acc.ScheduledDays = denominator;
                acc.PresencePercent = Math.Round((acc.PresentDays / (double)Math.Max(1, denominator)) * 100d, 1);
            }

            return map;
        }

        private static IEnumerable<ProductionManagementOperatorRow> BuildOperatorRows(
            IReadOnlyList<OperatorRow> operators,
            IReadOnlyList<ProductionDashboardSnapshot> dashboards,
            IReadOnlyDictionary<string, PresenceAccumulator> presenceByOperator,
            IReadOnlyDictionary<string, List<ScheduleRow>> scheduleByOperator,
            ProductionManagementReportFilter filter)
        {
            var operatorProduction = dashboards
                .SelectMany(snapshot => snapshot.Dashboard.OperatorRanking.Select(row => new { snapshot.Shift.Id, Row = row }))
                .GroupBy(item => item.Row.OperatorCodigoFJ, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => new
                    {
                        Production = group.Sum(item => item.Row.EstimatedRunningMinutes),
                        Kadouritsu = Average(group.Select(item => item.Row.EstimatedKadouritsuPercent)),
                        Full = group.Sum(item => item.Row.FullCoverageDays),
                        Partial = group.Sum(item => item.Row.PartialCoverageDays)
                    },
                    StringComparer.OrdinalIgnoreCase);

            var rows = new List<ProductionManagementOperatorRow>();
            foreach (var op in operators)
            {
                operatorProduction.TryGetValue(op.CodigoFJ, out var prod);
                presenceByOperator.TryGetValue(op.CodigoFJ, out var presence);
                scheduleByOperator.TryGetValue(op.CodigoFJ, out var schedules);
                presence ??= new PresenceAccumulator();
                schedules ??= new List<ScheduleRow>();
                var production = prod?.Production ?? 0;
                if (filter.OnlyProduction && production <= 0)
                {
                    continue;
                }
                var kadouritsu = Math.Round(prod?.Kadouritsu ?? 0, 1);
                var meta = kadouritsu > 0 ? Math.Round((production * 100d) / kadouritsu, 1) : production;
                var productionPercent = kadouritsu;

                rows.Add(new ProductionManagementOperatorRow(
                    op.CodigoFJ,
                    op.NamePt,
                    op.NameJp,
                    op.GroupId,
                    op.GroupNamePt,
                    op.GroupNameJp,
                    op.ShiftId,
                    op.ShiftNamePt,
                    op.ShiftNameJp,
                    op.SectorId,
                    op.SectorNamePt,
                    op.SectorNameJp,
                    Math.Round(production, 1),
                    Math.Round(meta, 1),
                    productionPercent,
                    kadouritsu,
                    Math.Round(presence.WorkedHours, 1),
                    Math.Round(presence.OvertimeHours, 1),
                    presence.WorkedSundays,
                    Math.Round(presence.PresencePercent, 1),
                    Math.Max(0, presence.ScheduledDays - presence.PresentDays),
                    0));
            }

            var rank = 1;
            foreach (var row in rows.OrderByDescending(item => item.Production).ThenBy(item => item.OperatorNamePt, StringComparer.OrdinalIgnoreCase))
            {
                row.Ranking = rank++;
            }

            return rows
                .OrderBy(item => item.Ranking)
                .ToList();
        }

        private static IEnumerable<ProductionManagementMachineRow> BuildMachineRows(IReadOnlyList<ProductionDashboardSnapshot> dashboards, ProductionManagementReportFilter filter)
        {
            return dashboards
                .SelectMany(snapshot => snapshot.Dashboard.Machines)
                .Where(machine => string.IsNullOrWhiteSpace(filter.PartCode)
                    || (machine.PartCode ?? string.Empty).Contains(filter.PartCode, StringComparison.OrdinalIgnoreCase)
                    || (machine.Ec2PartCode ?? string.Empty).Contains(filter.PartCode, StringComparison.OrdinalIgnoreCase)
                    || (machine.RecipeName ?? string.Empty).Contains(filter.PartCode, StringComparison.OrdinalIgnoreCase))
                .GroupBy(machine => machine.MachineId)
                .Select(group =>
                {
                    var first = group.First();
                    var total = group.Sum(item => item.TotalMinutes);
                    var running = group.Sum(item => item.RunningMinutes);
                    var stopped = group.Sum(item => item.StoppedMinutes + item.InactiveMinutes + item.ErrorMinutes);
                    return new ProductionManagementMachineRow(
                        first.MachineId,
                        first.MachineCode,
                        first.MachineNamePt,
                        first.SectorNamePt,
                        first.LocalNamePt,
                        Math.Round(running, 1),
                        Math.Round(total, 1),
                        total <= 0 ? 0 : Math.Round((running / total) * 100d, 1),
                        Math.Round(running / 60d, 1),
                        Math.Round(stopped / 60d, 1),
                        first.PartCode);
                })
                .OrderByDescending(item => item.Production)
                .ToList();
        }

        private static IEnumerable<ProductionManagementSectorRow> BuildSectorRows(IReadOnlyList<ProductionDashboardSnapshot> dashboards)
        {
            return dashboards
                .SelectMany(snapshot => snapshot.Dashboard.Areas)
                .GroupBy(area => $"{area.SectorId}|{area.SectorNamePt}", StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    var total = group.Sum(item => item.TotalMinutes);
                    var production = group.Sum(item => item.RunningMinutes);
                    return new ProductionManagementSectorRow(
                        group.First().SectorId ?? 0,
                        string.IsNullOrWhiteSpace(group.First().SectorNamePt) ? "-" : group.First().SectorNamePt,
                        Math.Round(production, 1),
                        Math.Round(total, 1),
                        total <= 0 ? 0 : Math.Round((production / total) * 100d, 1),
                        group.Sum(item => item.ScheduledOperatorCount),
                        group.Sum(item => item.MachineCount),
                        Average(group.Select(item => item.ProductionPercent)));
                })
                .OrderByDescending(item => item.Production)
                .ToList();
        }

        private static ProductionManagementShiftComparison BuildShiftComparison(IReadOnlyList<ProductionDashboardSnapshot> dashboards)
        {
            var points = dashboards
                .GroupBy(item => item.Day)
                .OrderBy(group => group.Key)
                .Select(group => new ProductionManagementShiftTrendPoint(
                    group.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    Average(group.Where(item => !IsNightShift(item.Shift.NamePt, item.Shift.NameJp)).Select(item => item.Dashboard.ProductionPercent)),
                    Average(group.Where(item => IsNightShift(item.Shift.NamePt, item.Shift.NameJp)).Select(item => item.Dashboard.ProductionPercent))))
                .ToList();

            return new ProductionManagementShiftComparison(points);
        }

        private static bool IsNightShift(string namePt, string nameJp)
        {
            return (namePt ?? string.Empty).Contains("yakin", StringComparison.OrdinalIgnoreCase)
                || (namePt ?? string.Empty).Contains("noite", StringComparison.OrdinalIgnoreCase)
                || (nameJp ?? string.Empty).Contains("夜", StringComparison.OrdinalIgnoreCase);
        }

        private static ProductionManagementGroupComparison BuildGroupComparison(IEnumerable<ProductionManagementOperatorRow> rows, int groupAId, int groupBId)
        {
            return new ProductionManagementGroupComparison(
                BuildGroupMetric(rows.Where(item => groupAId <= 0 || item.GroupId == groupAId)),
                BuildGroupMetric(rows.Where(item => groupBId <= 0 || item.GroupId == groupBId)));
        }

        private static ProductionManagementGroupMetric BuildGroupMetric(IEnumerable<ProductionManagementOperatorRow> rows)
        {
            var list = rows.ToList();
            var production = list.Sum(item => item.Production);
            var meta = list.Sum(item => item.Meta);
            return new ProductionManagementGroupMetric(
                list.FirstOrDefault()?.GroupNamePt ?? "-",
                Math.Round(production, 1),
                Math.Round(meta, 1),
                meta <= 0 ? 0 : Math.Round((production / meta) * 100d, 1),
                Math.Round(list.Sum(item => item.OvertimeHours), 1),
                list.Sum(item => item.Absences));
        }

        private static IEnumerable<ProductionManagementDailyTrend> BuildDailyTrend(IReadOnlyList<ProductionDashboardSnapshot> dashboards)
        {
            return dashboards
                .GroupBy(item => item.Day)
                .Select(group =>
                {
                    var production = group.Sum(item => item.Dashboard.Machines.Sum(machine => machine.RunningMinutes));
                    var meta = group.Sum(item => item.Dashboard.Machines.Sum(machine => machine.TotalMinutes));
                    return new ProductionManagementDailyTrend(
                        group.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        Math.Round(production, 1),
                        Math.Round(meta, 1),
                        meta <= 0 ? 0 : Math.Round((production / meta) * 100d, 1));
                })
                .OrderBy(item => item.Day);
        }

        private static ProductionManagementRankings BuildRankings(
            IReadOnlyList<ProductionManagementOperatorRow> operators,
            IReadOnlyList<ProductionManagementSectorRow> sectors,
            IReadOnlyList<ProductionManagementMachineRow> machines)
        {
            return new ProductionManagementRankings(
                operators.Take(20).Select((item, index) => RankingFrom(index + 1, item.OperatorNamePt, item.Production, item.Meta)).ToList(),
                sectors.Take(20).Select((item, index) => RankingFrom(index + 1, item.SectorNamePt, item.Production, item.Meta)).ToList(),
                operators.GroupBy(item => item.GroupNamePt)
                    .Select(group => new { Name = group.Key, Production = group.Sum(item => item.Production), Meta = group.Sum(item => item.Meta) })
                    .OrderByDescending(item => item.Production)
                    .Take(20)
                    .Select((item, index) => RankingFrom(index + 1, item.Name, item.Production, item.Meta))
                    .ToList(),
                machines.Take(20).Select((item, index) => RankingFrom(index + 1, item.MachineCode, item.Production, item.Meta)).ToList());
        }

        private static ProductionManagementRankingItem RankingFrom(int rank, string name, double production, double meta)
        {
            return new ProductionManagementRankingItem(
                rank,
                string.IsNullOrWhiteSpace(name) ? "-" : name,
                Math.Round(production, 1),
                Math.Round(meta, 1),
                meta <= 0 ? 0 : Math.Round((production / meta) * 100d, 1),
                Math.Round(production - meta, 1));
        }

        private static ProductionManagementSummary BuildSummary(
            IReadOnlyList<ProductionManagementOperatorRow> operators,
            IReadOnlyList<ProductionManagementMachineRow> machines,
            IReadOnlyList<ProductionManagementSectorRow> sectors)
        {
            var production = machines.Sum(item => item.Production);
            var meta = machines.Sum(item => item.Meta);
            return new ProductionManagementSummary(
                Math.Round(production, 1),
                Math.Round(meta, 1),
                meta <= 0 ? 0 : Math.Round((production / meta) * 100d, 1),
                Average(machines.Select(item => item.Kadouritsu)),
                operators.Count,
                machines.Count,
                Math.Round(operators.Sum(item => item.WorkedHours), 1),
                Math.Round(operators.Sum(item => item.OvertimeHours), 1),
                operators.Sum(item => item.WorkedSundays));
        }

        private static IEnumerable<ProductionManagementPresenceCrossRow> BuildPresenceCrossing(IEnumerable<ProductionManagementOperatorRow> operators)
        {
            return operators.Select(item => new ProductionManagementPresenceCrossRow(
                item.OperatorCode,
                item.OperatorNamePt,
                item.PresencePercent,
                item.Production,
                item.OvertimeHours,
                item.WorkedSundays,
                item.Absences,
                item.Production >= item.Meta && item.PresencePercent >= 82d ? "Produz muito" :
                item.ProductionPercent < 80d ? "Produz pouco" :
                item.PresencePercent < 82d ? "Falta muito" :
                item.OvertimeHours >= 45d ? "Faz muita hora extra" : "Normal"));
        }

        private static IEnumerable<ProductionManagementAlert> BuildAlerts(IEnumerable<ProductionManagementOperatorRow> operators)
        {
            foreach (var row in operators)
            {
                if (row.ProductionPercent < 70d)
                {
                    yield return new ProductionManagementAlert("critical", "Producao critica", row.OperatorNamePt, row.ProductionPercent);
                }
                else if (row.ProductionPercent < 80d)
                {
                    yield return new ProductionManagementAlert("warning", "Producao baixa", row.OperatorNamePt, row.ProductionPercent);
                }

                if (row.OvertimeHours >= 90d)
                {
                    yield return new ProductionManagementAlert("critical", "Horas extras 90h+", row.OperatorNamePt, row.OvertimeHours);
                }
                else if (row.OvertimeHours >= 45d)
                {
                    yield return new ProductionManagementAlert("warning", "Horas extras 45h+", row.OperatorNamePt, row.OvertimeHours);
                }

                if (row.PresencePercent < 82d)
                {
                    yield return new ProductionManagementAlert("critical", "Presenca abaixo de 82%", row.OperatorNamePt, row.PresencePercent);
                }
            }
        }

        private static bool DashboardHasPartCode(ProductionDashboardDto dashboard, string partCode)
        {
            return dashboard.Machines.Any(machine =>
                (machine.PartCode ?? string.Empty).Contains(partCode, StringComparison.OrdinalIgnoreCase)
                || (machine.Ec2PartCode ?? string.Empty).Contains(partCode, StringComparison.OrdinalIgnoreCase)
                || (machine.RecipeName ?? string.Empty).Contains(partCode, StringComparison.OrdinalIgnoreCase));
        }

        private static double Average(IEnumerable<double> values)
        {
            var list = values.Where(double.IsFinite).ToList();
            return list.Count == 0 ? 0 : Math.Round(list.Average(), 1);
        }

        private static DateTime ParseDate(string value, DateTime fallback)
        {
            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                ? parsed.Date
                : fallback.Date;
        }

        private static DateTime? ParseDateTime(string value)
        {
            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                ? parsed
                : null;
        }

        private static void LogAudit(System.Data.IDbConnection conn, string userFj, ProductionManagementReportFilter filter, int count, long elapsedMs)
        {
            try
            {
                conn.Execute(
                    @"
                        INSERT INTO SystemLog
                        (Timestamp, UserFJ, Module, Action, TargetId, Details)
                        VALUES
                        (@timestamp, @userFj, 'ProductionManagementReport', 'Generate', NULL, @details);",
                    new
                    {
                        timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                        userFj,
                        details = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            filter.StartDateIso,
                            filter.EndDateIso,
                            filter.SectorId,
                            filter.ShiftId,
                            filter.GroupId,
                            filter.OperatorCode,
                            TotalRegistros = count,
                            TempoExecucao = elapsedMs
                        })
                    });
            }
            catch
            {
                // Auditoria nao deve interromper o relatorio gerencial.
            }
        }

        private sealed record ProductionDashboardSnapshot(DateTime Day, LookupRow Shift, ProductionDashboardDto Dashboard);

        private sealed class LookupRow
        {
            public int Id { get; set; }
            public string NamePt { get; set; } = string.Empty;
            public string NameJp { get; set; } = string.Empty;
        }

        private sealed class OperatorRow
        {
            public string CodigoFJ { get; set; } = string.Empty;
            public string NamePt { get; set; } = string.Empty;
            public string NameJp { get; set; } = string.Empty;
            public int ShiftId { get; set; }
            public string ShiftNamePt { get; set; } = string.Empty;
            public string ShiftNameJp { get; set; } = string.Empty;
            public int SectorId { get; set; }
            public string SectorNamePt { get; set; } = string.Empty;
            public string SectorNameJp { get; set; } = string.Empty;
            public int GroupId { get; set; }
            public string GroupNamePt { get; set; } = string.Empty;
            public string GroupNameJp { get; set; } = string.Empty;
        }

        private sealed class PresenceRow
        {
            public string CodigoFJ { get; set; } = string.Empty;
            public string Day { get; set; } = string.Empty;
            public string FirstPresence { get; set; } = string.Empty;
            public string LastPresence { get; set; } = string.Empty;
        }

        private sealed class ScheduleRow
        {
            public string OperatorCodigoFJ { get; set; } = string.Empty;
            public string Day { get; set; } = string.Empty;
        }

        private sealed class PresenceAccumulator
        {
            public int PresentDays { get; set; }
            public double WorkedHours { get; set; }
            public double OvertimeHours { get; set; }
            public int WorkedSundays { get; set; }
            public double PresencePercent { get; set; }
            public int ScheduledDays { get; set; }
        }
    }

    public sealed record ProductionManagementReportFilter(
        string StartDateIso,
        string EndDateIso,
        int SectorId,
        int LocalId,
        int ShiftId,
        int GroupId,
        int GroupAId,
        int GroupBId,
        string OperatorCode,
        int MachineId,
        string PartCode,
        string LeaderCode,
        bool OnlyActive,
        bool OnlyProduction);

    public sealed record ProductionManagementInitPayload(
        IReadOnlyList<ProductionManagementLookupItem> Shifts,
        IReadOnlyList<ProductionManagementLookupItem> Sectors,
        IReadOnlyList<ProductionManagementLookupItem> Groups,
        IReadOnlyList<ProductionManagementLocalOption> Locals,
        IReadOnlyList<ProductionManagementMachineOption> Machines,
        IReadOnlyList<ProductionManagementOperatorOption> Operators,
        IReadOnlyList<string> PartCodes);

    public sealed class ProductionManagementLookupItem
    {
        public int Id { get; set; }
        public string NamePt { get; set; } = string.Empty;
        public string NameJp { get; set; } = string.Empty;
    }

    public sealed class ProductionManagementLocalOption
    {
        public int Id { get; set; }
        public int SectorId { get; set; }
        public string NamePt { get; set; } = string.Empty;
        public string NameJp { get; set; } = string.Empty;
    }

    public sealed class ProductionManagementMachineOption
    {
        public int Id { get; set; }
        public int SectorId { get; set; }
        public int LocalId { get; set; }
        public string MachineCode { get; set; } = string.Empty;
        public string NamePt { get; set; } = string.Empty;
        public string NameJp { get; set; } = string.Empty;
    }

    public sealed class ProductionManagementOperatorOption
    {
        public string CodigoFJ { get; set; } = string.Empty;
        public int ShiftId { get; set; }
        public int SectorId { get; set; }
        public int GroupId { get; set; }
        public int IsLeader { get; set; }
        public int IsActive { get; set; }
        public string NamePt { get; set; } = string.Empty;
        public string NameJp { get; set; } = string.Empty;
    }
    public sealed record ProductionManagementReportPayload(
        string StartDateIso,
        string EndDateIso,
        ProductionManagementSummary Summary,
        IReadOnlyList<ProductionManagementOperatorRow> Operators,
        ProductionManagementRankings Rankings,
        ProductionManagementShiftComparison ShiftComparison,
        ProductionManagementGroupComparison GroupComparison,
        IReadOnlyList<ProductionManagementDailyTrend> DailyTrend,
        IReadOnlyList<ProductionManagementSectorRow> Sectors,
        IReadOnlyList<ProductionManagementMachineRow> Machines,
        IReadOnlyList<ProductionManagementPresenceCrossRow> PresenceCrossing,
        IReadOnlyList<ProductionManagementAlert> Alerts,
        ProductionManagementPerformance Performance);

    public sealed record ProductionManagementSummary(double ProductionTotal, double MetaTotal, double EfficiencyAverage, double KadouritsuAverage, int TotalOperators, int TotalMachines, double TotalWorkedHours, double TotalOvertimeHours, int TotalWorkedSundays);
    public sealed class ProductionManagementOperatorRow
    {
        public ProductionManagementOperatorRow(
            string operatorCode,
            string operatorNamePt,
            string operatorNameJp,
            int groupId,
            string groupNamePt,
            string groupNameJp,
            int shiftId,
            string shiftNamePt,
            string shiftNameJp,
            int sectorId,
            string sectorNamePt,
            string sectorNameJp,
            double production,
            double meta,
            double productionPercent,
            double kadouritsu,
            double workedHours,
            double overtimeHours,
            int workedSundays,
            double presencePercent,
            int absences,
            int ranking)
        {
            OperatorCode = operatorCode;
            OperatorNamePt = operatorNamePt;
            OperatorNameJp = operatorNameJp;
            GroupId = groupId;
            GroupNamePt = groupNamePt;
            GroupNameJp = groupNameJp;
            ShiftId = shiftId;
            ShiftNamePt = shiftNamePt;
            ShiftNameJp = shiftNameJp;
            SectorId = sectorId;
            SectorNamePt = sectorNamePt;
            SectorNameJp = sectorNameJp;
            Production = production;
            Meta = meta;
            ProductionPercent = productionPercent;
            Kadouritsu = kadouritsu;
            WorkedHours = workedHours;
            OvertimeHours = overtimeHours;
            WorkedSundays = workedSundays;
            PresencePercent = presencePercent;
            Absences = absences;
            Ranking = ranking;
        }

        public string OperatorCode { get; }
        public string OperatorNamePt { get; }
        public string OperatorNameJp { get; }
        public int GroupId { get; }
        public string GroupNamePt { get; }
        public string GroupNameJp { get; }
        public int ShiftId { get; }
        public string ShiftNamePt { get; }
        public string ShiftNameJp { get; }
        public int SectorId { get; }
        public string SectorNamePt { get; }
        public string SectorNameJp { get; }
        public double Production { get; }
        public double Meta { get; }
        public double ProductionPercent { get; }
        public double Kadouritsu { get; }
        public double WorkedHours { get; }
        public double OvertimeHours { get; }
        public int WorkedSundays { get; }
        public double PresencePercent { get; }
        public int Absences { get; }
        public int Ranking { get; set; }
    }
    public sealed record ProductionManagementRankingItem(int Rank, string Name, double Production, double Meta, double Percent, double Difference);
    public sealed record ProductionManagementRankings(IReadOnlyList<ProductionManagementRankingItem> Operators, IReadOnlyList<ProductionManagementRankingItem> Sectors, IReadOnlyList<ProductionManagementRankingItem> Groups, IReadOnlyList<ProductionManagementRankingItem> Machines);
    public sealed record ProductionManagementShiftTrendPoint(string Day, double DayKadouritsu, double NightKadouritsu);
    public sealed record ProductionManagementShiftComparison(IReadOnlyList<ProductionManagementShiftTrendPoint> Points);
    public sealed record ProductionManagementGroupMetric(string Name, double Production, double Meta, double Percent, double OvertimeHours, int Absenteeism);
    public sealed record ProductionManagementGroupComparison(ProductionManagementGroupMetric GroupA, ProductionManagementGroupMetric GroupB);
    public sealed record ProductionManagementDailyTrend(string Day, double Production, double Meta, double Percent);
    public sealed record ProductionManagementSectorRow(int SectorId, string SectorNamePt, double Production, double Meta, double Percent, int Operators, int Machines, double Kadouritsu);
    public sealed record ProductionManagementMachineRow(int MachineId, string MachineCode, string MachineNamePt, string SectorNamePt, string LocalNamePt, double Production, double Meta, double Kadouritsu, double RunningHours, double StoppedHours, string PartCode);
    public sealed record ProductionManagementPresenceCrossRow(string OperatorCode, string OperatorNamePt, double PresencePercent, double Production, double OvertimeHours, int WorkedSundays, int Absenteeism, string Insight);
    public sealed record ProductionManagementAlert(string Level, string Title, string Target, double Value);
    public sealed record ProductionManagementPerformance(long LoadProductionMs, long LoadOperatorsMs, long LoadPresenceMs, long LoadChartsMs, long BuildReportMs, long TotalMs);
}
