using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Dapper;
using TeamOps.Data.Db;
using TeamOps.UI.Forms.Models;
using TeamOps.UI.Services;

namespace TeamOps.Services
{
    public sealed class ProductionAnalyticsService
    {
        private const int HistoryDays = 7;

        private readonly SqliteConnectionFactory _factory;

        public ProductionAnalyticsService(SqliteConnectionFactory factory)
        {
            _factory = factory;
        }

        public ProductionShiftPeriod GetShiftPeriod(DateTime date, string shiftName)
        {
            var normalized = (shiftName ?? string.Empty).Trim().ToLowerInvariant();
            var isNightShift = IsYakinShiftHint(normalized);

            return isNightShift
                ? new ProductionShiftPeriod
                {
                    Start = date.Date.AddHours(20).AddMinutes(30),
                    End = date.Date.AddDays(1).AddHours(8).AddMinutes(35)
                }
                : new ProductionShiftPeriod
                {
                    Start = date.Date.AddHours(8).AddMinutes(30),
                    End = date.Date.AddHours(20).AddMinutes(35)
                };
        }

        public ProductionDashboardDto BuildDashboard(ProductionDashboardFilter filter)
        {
            var totalWatch = Stopwatch.StartNew();
            long queryMachinesMs = 0;
            long queryEventsMs = 0;
            long queryEc2Ms = 0;
            long buildMachinesMs = 0;
            long buildAreasMs = 0;

            using var conn = _factory.CreateOpenConnection();
            var haidaiService = new HaidaiModuleService(_factory);
            haidaiService.RefreshSectorTopology();

            var shifts = Measure(() => conn.Query<ShiftLookupRow>(
                @"
                    SELECT
                        Id,
                        COALESCE(NamePt, '') AS NamePt,
                        COALESCE(NULLIF(NameJp, ''), NamePt, '') AS NameJp
                    FROM Shifts
                    ORDER BY Id;").ToList(), out _);

            var shift = shifts.FirstOrDefault(item => item.Id == filter.ShiftId)
                ?? throw new InvalidOperationException("Turno nao encontrado para o monitor de producao.");

            var selectedShiftName = BuildShiftPeriodHint(shift.NamePt, shift.NameJp);
            var operationalDate = ResolveOperationalDate(filter.Date, selectedShiftName);
            var period = GetShiftPeriod(operationalDate, selectedShiftName);
            var dashboard = new ProductionDashboardDto
            {
                Period = period
            };

            var machines = Measure(() => conn.Query<MachineRow>(
                @"
                    SELECT
                        m.Id,
                        COALESCE(m.MachineCode, '') AS MachineCode,
                        COALESCE(m.LineCode, '') AS LineCode,
                        COALESCE(m.NamePt, '') AS MachineNamePt,
                        COALESCE(NULLIF(m.NameJp, ''), m.NamePt, '') AS MachineNameJp,
                        m.LocalId,
                        m.SectorId,
                        COALESCE(l.NamePt, '') AS LocalNamePt,
                        COALESCE(NULLIF(l.NameJp, ''), l.NamePt, '') AS LocalNameJp,
                        COALESCE(s.NamePt, '') AS SectorNamePt,
                        COALESCE(NULLIF(s.NameJp, ''), s.NamePt, '') AS SectorNameJp
                    FROM Machines m
                    LEFT JOIN Locals l ON l.Id = m.LocalId
                    LEFT JOIN Sectors s ON s.Id = m.SectorId
                    WHERE COALESCE(m.IsActive, 1) = 1
                      AND (@sectorId <= 0 OR m.SectorId = @sectorId)
                      AND (@localId <= 0 OR m.LocalId = @localId)
                      AND (@machineId <= 0 OR m.Id = @machineId)
                    ORDER BY
                        COALESCE(s.NamePt, ''),
                        COALESCE(l.NamePt, ''),
                        COALESCE(m.LineCode, ''),
                        COALESCE(m.MachineCode, ''),
                        m.Id;",
                    new
                    {
                        sectorId = filter.SectorId,
                        localId = filter.LocalId,
                        machineId = filter.MachineId
                    }).ToList(), out queryMachinesMs);

            if (machines.Count == 0)
            {
                WriteDashboardPerformanceLog(
                    filter,
                    totalWatch.ElapsedMilliseconds,
                    queryMachinesMs,
                    queryEventsMs,
                    queryEc2Ms,
                    buildMachinesMs,
                    buildAreasMs,
                    0,
                    0,
                    0);
                return dashboard;
            }

            var machineIds = machines.Select(machine => machine.Id).ToArray();
            var historyStartDate = operationalDate.AddDays(-(HistoryDays - 1));
            var rangeStart = historyStartDate.AddDays(-1);
            var rangeEnd = operationalDate.AddDays(2);

            var statusRows = conn.Query<StatusRow>(
                @"
                    SELECT
                        SectorId,
                        MachineId,
                        StatusCode,
                        COALESCE(StatusText, '') AS StatusText,
                        COALESCE(RecipeName, '') AS RecipeName,
                        COALESCE(LotNo, '') AS LotNo,
                        EventDateTime
                    FROM MachineCurrentStatus
                    WHERE MachineId IN @machineIds;",
                new
                {
                    machineIds
                }
            ).ToDictionary(row => row.MachineId);

            var events = Measure(() => conn.Query<EventRow>(
                @"
                    SELECT
                        SectorId,
                        MachineId,
                        StatusCode,
                        COALESCE(StatusText, '') AS StatusText,
                        COALESCE(RecipeName, '') AS RecipeName,
                        COALESCE(LotNo, '') AS LotNo,
                        EventDateTime
                    FROM MachineEvents
                    WHERE MachineId IN @machineIds
                      AND EventDateTime <= @rangeEnd
                      AND EventDateTime >= @rangeStart
                    ORDER BY MachineId, EventDateTime, Id;",
                new
                {
                    machineIds,
                    rangeStart = rangeStart.ToString("yyyy-MM-dd HH:mm:ss"),
                    rangeEnd = rangeEnd.ToString("yyyy-MM-dd HH:mm:ss")
                }
            ).ToList(), out queryEventsMs);

            var eventsByMachine = events
                .GroupBy(row => row.MachineId)
                .ToDictionary(group => group.Key, group => group.OrderBy(item => item.EventDateTime).ToList());

            var scheduleCurrentRows = Measure(() => conn.Query<ScheduleRow>(
                @"
                    SELECT
                        ha.ScheduleDate,
                        ha.LocalId,
                        COALESCE(ha.OperatorCodigoFJ, '') AS OperatorCodigoFJ,
                        COALESCE(op.NameRomanji, ha.OperatorCodigoFJ) AS OperatorNamePt,
                        COALESCE(NULLIF(op.NameNihongo, ''), op.NameRomanji, ha.OperatorCodigoFJ) AS OperatorNameJp
                    FROM HaidaiAssignments ha
                    LEFT JOIN Operators op ON op.CodigoFJ = ha.OperatorCodigoFJ
                    WHERE date(ha.ScheduleDate) = date(@scheduleDate)
                      AND ha.ShiftId = @shiftId
                      AND COALESCE(ha.IsLineupActive, 1) = 1
                      AND COALESCE(ha.LocalId, 0) > 0;",
                new
                {
                    scheduleDate = operationalDate.ToString("yyyy-MM-dd"),
                    shiftId = filter.ShiftId
                }
            ).ToList(), out _);

            var scheduleHistoryRows = Measure(() => conn.Query<ScheduleRow>(
                @"
                    SELECT
                        ha.ScheduleDate,
                        ha.LocalId,
                        COALESCE(ha.OperatorCodigoFJ, '') AS OperatorCodigoFJ,
                        COALESCE(op.NameRomanji, ha.OperatorCodigoFJ) AS OperatorNamePt,
                        COALESCE(NULLIF(op.NameNihongo, ''), op.NameRomanji, ha.OperatorCodigoFJ) AS OperatorNameJp
                    FROM HaidaiAssignments ha
                    LEFT JOIN Operators op ON op.CodigoFJ = ha.OperatorCodigoFJ
                    WHERE date(ha.ScheduleDate) BETWEEN date(@startDate) AND date(@endDate)
                      AND ha.ShiftId = @shiftId
                      AND COALESCE(ha.IsLineupActive, 1) = 1
                      AND COALESCE(ha.LocalId, 0) > 0;",
                new
                {
                    startDate = historyStartDate.ToString("yyyy-MM-dd"),
                    endDate = operationalDate.ToString("yyyy-MM-dd"),
                    shiftId = filter.ShiftId
                }
            ).ToList(), out _);

            var scheduleOperatorCodes = scheduleHistoryRows
                .Select(row => row.OperatorCodigoFJ)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var movementRows = scheduleOperatorCodes.Length == 0
                ? new List<MovementWindowRow>()
                : Measure(() => conn.Query<MovementWindowRow>(
                    @"
                    SELECT
                        date(ScheduleDate) AS Day,
                        COALESCE(OperatorCodigoFJ, '') AS OperatorCodigoFJ,
                        COALESCE(MovementType, '') AS MovementType,
                        COALESCE(EventTime, '') AS EventTime,
                        COALESCE(EventDateTime, '') AS EventDateTime,
                        COALESCE(ReplacementOperatorCodigoFJ, '') AS ReplacementOperatorCodigoFJ
                    FROM HaidaiMovements
                    WHERE (OperatorCodigoFJ IN @OperatorCodes
                        OR ReplacementOperatorCodigoFJ IN @OperatorCodes)
                      AND date(ScheduleDate) BETWEEN date(@startDate) AND date(@endDate)
                    ORDER BY date(ScheduleDate) DESC, COALESCE(EventTime, '') DESC, Id DESC;",
                    new
                    {
                        OperatorCodes = scheduleOperatorCodes,
                        startDate = historyStartDate.ToString("yyyy-MM-dd"),
                        endDate = operationalDate.ToString("yyyy-MM-dd")
                    }).ToList(), out _);

            var latestMovementByOperatorDay = movementRows
                .Where(item => string.IsNullOrWhiteSpace(item.ReplacementOperatorCodigoFJ))
                .GroupBy(
                    item => $"{item.OperatorCodigoFJ}|{item.Day}",
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.First(),
                    StringComparer.OrdinalIgnoreCase);

            var latestReplacementMovementByOperatorDay = movementRows
                .Where(item => !string.IsNullOrWhiteSpace(item.ReplacementOperatorCodigoFJ))
                .GroupBy(
                    item => $"{item.ReplacementOperatorCodigoFJ}|{item.Day}",
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.First(),
                    StringComparer.OrdinalIgnoreCase);

            var operatorsByLocal = scheduleCurrentRows
                .GroupBy(row => row.LocalId)
                .ToDictionary(group => group.Key, group => group.ToList());

            var statusDefinitions = conn.Query<StatusDefinitionRow>(
                @"
                    SELECT
                        SectorId,
                        StatusCode,
                        DisplayCode,
                        COALESCE(Classification, '') AS Classification,
                        COALESCE(NamePt, '') AS NamePt,
                        COALESCE(NULLIF(NameJp, ''), NamePt, '') AS NameJp,
                        COALESCE(ColorHex, '') AS ColorHex,
                        COALESCE(TextColorHex, '') AS TextColorHex
                    FROM MachineStatuses
                    WHERE COALESCE(IsActive, 1) = 1;"
            ).ToDictionary(row => BuildStatusKey(row.SectorId, row.StatusCode), StringComparer.OrdinalIgnoreCase);

            var ec2States = Measure(() => LoadEc2States(conn, machineIds), out queryEc2Ms);

            var latestKnownEventCandidates = statusRows.Values
                .Select(item => item.EventDateTime)
                .Concat(events.Select(item => item.EventDateTime))
                .Concat(ec2States.Values.Select(item => item.SnapshotAt))
                .ToList();

            DateTime? latestKnownEventTime = latestKnownEventCandidates.Count == 0
                ? null
                : latestKnownEventCandidates.Max();

            period = ResolveEffectivePeriod(period, latestKnownEventTime);
            dashboard.Period = period;

            var machineSummaries = Measure(() => BuildMachineSummaries(
                machines,
                statusRows,
                eventsByMachine,
                operatorsByLocal,
                ec2States,
                statusDefinitions,
                period), out buildMachinesMs);

            dashboard.Machines.AddRange(machineSummaries);

            var aggregate = Summarize(machineSummaries, "dashboard");
            dashboard.ProductionPercent = aggregate.ProductionPercent;
            dashboard.MachinesRunning = aggregate.MachinesRunning;
            dashboard.MachinesStopped = aggregate.MachinesStopped;
            dashboard.MachinesIgnored = aggregate.MachinesIgnored;
            dashboard.MachinesTotal = machineSummaries.Count;
            dashboard.AverageOperatingProcessMinutes = aggregate.AverageOperatingProcessMinutes;
            dashboard.ErrorMinutes = aggregate.ErrorMinutes;
            dashboard.InactiveMinutes = aggregate.InactiveMinutes;

            var buildAreasWatch = Stopwatch.StartNew();
            dashboard.Areas.AddRange(BuildAreaSummaries(machineSummaries, operatorsByLocal));

            dashboard.Ranking.AddRange(
                machineSummaries
                    .Select(machine => new ProductionRankingItemDto
                    {
                        LocalId = machine.LocalId,
                        LocalNamePt = machine.LocalNamePt,
                        LocalNameJp = machine.LocalNameJp,
                        MachineCode = machine.MachineCode,
                        MachineNamePt = machine.MachineNamePt,
                        MachineNameJp = machine.MachineNameJp,
                        StopMinutes = Math.Round(machine.StoppedMinutes, 1),
                        ErrorMinutes = Math.Round(machine.ErrorMinutes, 1),
                        TotalImpactMinutes = Math.Round(machine.StoppedMinutes + machine.ErrorMinutes, 1)
                    })
                    .OrderByDescending(item => item.TotalImpactMinutes)
                    .ThenBy(item => item.MachineCode, StringComparer.OrdinalIgnoreCase)
                    .Take(12)
            );

            var cellMoments = BuildTimelineMoments(period);
            foreach (var machine in machineSummaries)
            {
                eventsByMachine.TryGetValue(machine.MachineId, out var machineEvents);
                machineEvents ??= new List<EventRow>();

                var timelineRow = new ProductionTimelineRowDto
                {
                    SectorId = machine.SectorId,
                    LocalId = machine.LocalId,
                    LocalNamePt = machine.LocalNamePt,
                    LocalNameJp = machine.LocalNameJp,
                    MachineCode = machine.MachineCode,
                    LineCode = machine.LineCode,
                    MachineNamePt = machine.MachineNamePt,
                    MachineNameJp = machine.MachineNameJp
                };

                foreach (var moment in cellMoments)
                {
                    var statusCode = ResolveStatusAt(machineEvents, moment);
                    var displayCode = NormalizeStatusCode(machine.SectorId, statusCode, statusDefinitions);
                    timelineRow.Cells.Add(new ProductionTimelineCellDto
                    {
                        TimeLabel = moment.ToString("HH:mm", CultureInfo.InvariantCulture),
                        DateTime = moment,
                        StatusCode = statusCode,
                        DisplayCode = displayCode,
                        IsIgnored = machine.IsEc2Ignored,
                        IgnoreReason = machine.Ec2IgnoreReason,
                        CssClass = GetNormalizedTimelineClass(displayCode)
                    });
                }

                dashboard.Timeline.Add(timelineRow);
            }

            foreach (var shiftItem in shifts)
            {
                var comparisonShiftName = BuildShiftPeriodHint(shiftItem.NamePt, shiftItem.NameJp);
                var comparisonDate = ResolveOperationalDate(filter.Date, comparisonShiftName);
                var comparisonPeriod = ResolveEffectivePeriod(
                    GetShiftPeriod(comparisonDate, comparisonShiftName),
                    latestKnownEventTime);
                var comparisonSummaries = BuildMachineSummaries(
                    machines,
                    statusRows,
                    eventsByMachine,
                    new Dictionary<int, List<ScheduleRow>>(),
                    ec2States,
                    statusDefinitions,
                    comparisonPeriod);

                var comparisonAggregate = Summarize(comparisonSummaries);
                dashboard.ShiftComparisons.Add(new ProductionShiftComparisonDto
                {
                    ShiftId = shiftItem.Id,
                    ShiftNamePt = shiftItem.NamePt,
                    ShiftNameJp = shiftItem.NameJp,
                    Start = comparisonPeriod.Start,
                    End = comparisonPeriod.End,
                    ProductionPercent = comparisonAggregate.ProductionPercent,
                    RunningMinutes = comparisonAggregate.RunningMinutes,
                    StoppedMinutes = comparisonAggregate.StoppedMinutes,
                    InactiveMinutes = comparisonAggregate.InactiveMinutes,
                    ErrorMinutes = comparisonAggregate.ErrorMinutes,
                    MachineCount = comparisonSummaries.Count
                });
            }

            for (var offset = HistoryDays - 1; offset >= 0; offset--)
            {
                var day = operationalDate.AddDays(-offset);
                foreach (var shiftItem in shifts)
                {
                    var trendShiftName = BuildShiftPeriodHint(shiftItem.NamePt, shiftItem.NameJp);
                    var dayPeriod = ResolveEffectivePeriod(
                        GetShiftPeriod(day, trendShiftName),
                        latestKnownEventTime);
                    var daySummaries = BuildMachineSummaries(
                        machines,
                        statusRows,
                        eventsByMachine,
                        new Dictionary<int, List<ScheduleRow>>(),
                        ec2States,
                        statusDefinitions,
                        dayPeriod);

                    var dayAggregate = Summarize(daySummaries);
                    dashboard.DailyTrend.Add(new ProductionDailyTrendDto
                    {
                        Date = day,
                        Label = day.ToString("MM/dd", CultureInfo.InvariantCulture),
                        ShiftId = shiftItem.Id,
                        ShiftNamePt = shiftItem.NamePt,
                        ShiftNameJp = shiftItem.NameJp,
                        ProductionPercent = dayAggregate.ProductionPercent,
                        RunningMinutes = dayAggregate.RunningMinutes,
                        StoppedMinutes = dayAggregate.StoppedMinutes,
                        InactiveMinutes = dayAggregate.InactiveMinutes,
                        ErrorMinutes = dayAggregate.ErrorMinutes
                    });
                }
            }

            foreach (var area in dashboard.Areas.OrderBy(item => item.LocalNamePt, StringComparer.OrdinalIgnoreCase))
            {
                var machineRows = machines
                    .Where(machine => machine.LocalId == area.LocalId)
                    .ToList();

                var areaHistory = new ProductionAreaHistoryDto
                {
                    LocalId = area.LocalId,
                    LocalNamePt = area.LocalNamePt,
                    LocalNameJp = area.LocalNameJp
                };

                for (var offset = HistoryDays - 1; offset >= 0; offset--)
                {
                    var day = operationalDate.AddDays(-offset);
                    var dayPeriod = ResolveEffectivePeriod(
                        GetShiftPeriod(day, selectedShiftName),
                        latestKnownEventTime);
                    var areaSummaries = BuildMachineSummaries(
                        machineRows,
                        statusRows,
                        eventsByMachine,
                        new Dictionary<int, List<ScheduleRow>>(),
                        ec2States,
                        statusDefinitions,
                        dayPeriod);

                    var areaAggregate = Summarize(areaSummaries);
                    areaHistory.Days.Add(new ProductionDailyTrendDto
                    {
                        Date = day,
                        Label = day.ToString("MM/dd", CultureInfo.InvariantCulture),
                        ProductionPercent = areaAggregate.ProductionPercent,
                        RunningMinutes = areaAggregate.RunningMinutes,
                        StoppedMinutes = areaAggregate.StoppedMinutes,
                        InactiveMinutes = areaAggregate.InactiveMinutes,
                        ErrorMinutes = areaAggregate.ErrorMinutes
                    });
                }

                dashboard.AreaHistory.Add(areaHistory);
            }

            var areaDayRunningMap = dashboard.AreaHistory
                .SelectMany(area => area.Days.Select(day => new
                {
                    area.LocalId,
                    day.Date,
                    day.RunningMinutes
                }))
                .GroupBy(item => new { item.LocalId, Day = item.Date.Date })
                .ToDictionary(
                    group => (group.Key.LocalId ?? 0, group.Key.Day),
                    group => group.First().RunningMinutes);

            var areaDayPercentMap = dashboard.AreaHistory
                .SelectMany(area => area.Days.Select(day => new
                {
                    area.LocalId,
                    day.Date,
                    day.ProductionPercent
                }))
                .GroupBy(item => new { item.LocalId, Day = item.Date.Date })
                .ToDictionary(
                    group => (group.Key.LocalId ?? 0, group.Key.Day),
                    group => group.First().ProductionPercent);

            var localMachineCountByLocal = machines
                .Where(machine => machine.LocalId.HasValue && machine.LocalId.Value > 0)
                .GroupBy(machine => machine.LocalId!.Value)
                .ToDictionary(group => group.Key, group => group.Count());

            var operatorScheduleGroups = scheduleHistoryRows
                .Where(row => row.LocalId > 0 && !string.IsNullOrWhiteSpace(row.OperatorCodigoFJ))
                .GroupBy(row => new { Day = row.ScheduleDate.Date, row.LocalId });

            var operatorAccumulator = new Dictionary<string, ProductionOperatorAccumulator>(StringComparer.OrdinalIgnoreCase);

            foreach (var scheduleGroup in operatorScheduleGroups)
            {
                var key = (scheduleGroup.Key.LocalId, scheduleGroup.Key.Day);
                if (!areaDayRunningMap.TryGetValue(key, out var areaRunningMinutes)
                    || !areaDayPercentMap.TryGetValue(key, out var areaProductionPercent))
                {
                    continue;
                }

                var operators = scheduleGroup
                    .GroupBy(item => item.OperatorCodigoFJ, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                    .ToList();

                if (operators.Count == 0)
                {
                    continue;
                }

                foreach (var item in operators)
                {
                    if (!operatorAccumulator.TryGetValue(item.OperatorCodigoFJ, out var accumulator))
                    {
                        accumulator = new ProductionOperatorAccumulator
                        {
                            OperatorCodigoFJ = item.OperatorCodigoFJ,
                            OperatorNamePt = item.OperatorNamePt,
                            OperatorNameJp = item.OperatorNameJp
                        };
                        operatorAccumulator[item.OperatorCodigoFJ] = accumulator;
                    }

                    var selectedShiftPeriod = GetShiftPeriod(scheduleGroup.Key.Day, selectedShiftName);
                    var movementKey = $"{item.OperatorCodigoFJ}|{scheduleGroup.Key.Day:yyyy-MM-dd}";
                    latestMovementByOperatorDay.TryGetValue(movementKey, out var movement);
                    latestReplacementMovementByOperatorDay.TryGetValue(movementKey, out var replacementMovement);
                    var coverage = DescribeCoverage(selectedShiftPeriod, movement, replacementMovement);
                    var effectivePeriod = coverage.EffectivePeriod;
                    var totalPeriodMinutes = Math.Max(0, (selectedShiftPeriod.End - selectedShiftPeriod.Start).TotalMinutes);
                    var effectiveMinutes = Math.Max(0, (effectivePeriod.End - effectivePeriod.Start).TotalMinutes);
                    if (totalPeriodMinutes <= 0 || effectiveMinutes <= 0)
                    {
                        continue;
                    }

                    var coverageRatio = Math.Min(1d, effectiveMinutes / totalPeriodMinutes);

                    // When an area is worked by a pair, both operators carry the same
                    // production history for that area/day because the final result depends
                    // on the full flow executed by the duo, not on splitting machine time.
                    accumulator.EstimatedRunningMinutes += areaRunningMinutes * coverageRatio;
                    if (localMachineCountByLocal.TryGetValue(scheduleGroup.Key.LocalId, out var localMachineCount))
                    {
                        accumulator.EligibleMinutes += effectiveMinutes * localMachineCount;
                    }
                    accumulator.EntryKadouritsuPercents.Add(Math.Round(areaProductionPercent, 1));
                    if (coverage.IsPartial)
                    {
                        accumulator.PartialCoverageDays.Add(scheduleGroup.Key.Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                        accumulator.FullCoverageDays.Remove(scheduleGroup.Key.Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                    }
                    else if (!accumulator.PartialCoverageDays.Contains(scheduleGroup.Key.Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)))
                    {
                        accumulator.FullCoverageDays.Add(scheduleGroup.Key.Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                    }

                    var localMachine = machines.FirstOrDefault(machine => machine.LocalId == item.LocalId);
                    if (localMachine != null)
                    {
                        accumulator.LocalNamesPt.Add(localMachine.LocalNamePt);
                        accumulator.LocalNamesJp.Add(localMachine.LocalNameJp);
                    }
                }
            }

            dashboard.OperatorRanking.AddRange(
                operatorAccumulator.Values
                    .Select(item => new ProductionOperatorRankingDto
                    {
                        OperatorCodigoFJ = item.OperatorCodigoFJ,
                        OperatorNamePt = item.OperatorNamePt,
                        OperatorNameJp = item.OperatorNameJp,
                        EstimatedRunningMinutes = Math.Round(item.EstimatedRunningMinutes, 1),
                        EstimatedKadouritsuPercent = item.EntryKadouritsuPercents.Count == 0
                            ? 0
                            : Math.Round(item.EntryKadouritsuPercents.Average(), 1),
                        FullCoverageDays = item.FullCoverageDays.Count,
                        PartialCoverageDays = item.PartialCoverageDays.Count,
                        LocalNamesPt = item.LocalNamesPt
                            .Where(name => !string.IsNullOrWhiteSpace(name))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList(),
                        LocalNamesJp = item.LocalNamesJp
                            .Where(name => !string.IsNullOrWhiteSpace(name))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList()
                    })
                    .OrderByDescending(item => item.EstimatedKadouritsuPercent)
                    .ThenByDescending(item => item.EstimatedRunningMinutes)
                    .ThenBy(item => item.OperatorNamePt, StringComparer.OrdinalIgnoreCase)
            );
            buildAreasWatch.Stop();
            buildAreasMs = buildAreasWatch.ElapsedMilliseconds;

            var rowsRead = machines.Count
                + statusRows.Count
                + events.Count
                + scheduleCurrentRows.Count
                + scheduleHistoryRows.Count
                + movementRows.Count
                + statusDefinitions.Count
                + ec2States.Count;

            WriteDashboardPerformanceLog(
                filter,
                totalWatch.ElapsedMilliseconds,
                queryMachinesMs,
                queryEventsMs,
                queryEc2Ms,
                buildMachinesMs,
                buildAreasMs,
                rowsRead,
                dashboard.Machines.Count,
                dashboard.Areas.Count);

            return dashboard;
        }

        public ProductionOperatorDetailDto GetOperatorDetail(ProductionDashboardFilter filter, string operatorCodigoFJ)
        {
            if (string.IsNullOrWhiteSpace(operatorCodigoFJ))
            {
                throw new InvalidOperationException("Operador invalido para o historico.");
            }

            using var conn = _factory.CreateOpenConnection();
            var haidaiService = new HaidaiModuleService(_factory);
            haidaiService.RefreshSectorTopology();

            var shifts = conn.Query<ShiftLookupRow>(
                @"
                    SELECT
                        Id,
                        COALESCE(NamePt, '') AS NamePt,
                        COALESCE(NULLIF(NameJp, ''), NamePt, '') AS NameJp
                    FROM Shifts
                    ORDER BY Id;"
            ).ToList();

            var shift = shifts.FirstOrDefault(item => item.Id == filter.ShiftId)
                ?? throw new InvalidOperationException("Turno nao encontrado para o historico do operador.");

            var selectedShiftHint = BuildShiftPeriodHint(shift.NamePt, shift.NameJp);
            var operationalDate = ResolveOperationalDate(filter.Date, selectedShiftHint);
            var historyStartDate = operationalDate.AddDays(-(HistoryDays - 1));
            var rangeStart = historyStartDate.AddDays(-1);
            var rangeEnd = operationalDate.AddDays(2);

            var detail = new ProductionOperatorDetailDto
            {
                OperatorCodigoFJ = operatorCodigoFJ.Trim(),
                ShiftNamePt = shift.NamePt,
                ShiftNameJp = shift.NameJp
            };

            var scheduleRows = conn.Query<ScheduleRow>(
                @"
                    SELECT
                        ha.ScheduleDate,
                        ha.LocalId,
                        COALESCE(ha.OperatorCodigoFJ, '') AS OperatorCodigoFJ,
                        COALESCE(op.NameRomanji, ha.OperatorCodigoFJ) AS OperatorNamePt,
                        COALESCE(NULLIF(op.NameNihongo, ''), op.NameRomanji, ha.OperatorCodigoFJ) AS OperatorNameJp
                    FROM HaidaiAssignments ha
                    LEFT JOIN Operators op ON op.CodigoFJ = ha.OperatorCodigoFJ
                    WHERE date(ha.ScheduleDate) BETWEEN date(@startDate) AND date(@endDate)
                      AND ha.ShiftId = @shiftId
                      AND ha.OperatorCodigoFJ = @codigoFJ
                      AND COALESCE(ha.IsLineupActive, 1) = 1
                      AND COALESCE(ha.LocalId, 0) > 0
                      AND (@localId <= 0 OR ha.LocalId = @localId)
                    ORDER BY date(ha.ScheduleDate) DESC, ha.LocalId;",
                new
                {
                    startDate = historyStartDate.ToString("yyyy-MM-dd"),
                    endDate = operationalDate.ToString("yyyy-MM-dd"),
                    shiftId = filter.ShiftId,
                    codigoFJ = operatorCodigoFJ.Trim(),
                    localId = filter.LocalId
                }
            ).ToList();

            if (scheduleRows.Count == 0)
            {
                return detail;
            }

            var firstRow = scheduleRows[0];
            detail.OperatorNamePt = firstRow.OperatorNamePt;
            detail.OperatorNameJp = firstRow.OperatorNameJp;

            var assignedLocalIds = scheduleRows
                .Select(row => row.LocalId)
                .Where(localId => localId > 0)
                .Distinct()
                .ToArray();

            var machines = conn.Query<MachineRow>(
                @"
                    SELECT
                        m.Id,
                        COALESCE(m.MachineCode, '') AS MachineCode,
                        COALESCE(m.LineCode, '') AS LineCode,
                        COALESCE(m.NamePt, '') AS MachineNamePt,
                        COALESCE(NULLIF(m.NameJp, ''), m.NamePt, '') AS MachineNameJp,
                        m.LocalId,
                        m.SectorId,
                        COALESCE(l.NamePt, '') AS LocalNamePt,
                        COALESCE(NULLIF(l.NameJp, ''), l.NamePt, '') AS LocalNameJp,
                        COALESCE(s.NamePt, '') AS SectorNamePt,
                        COALESCE(NULLIF(s.NameJp, ''), s.NamePt, '') AS SectorNameJp
                    FROM Machines m
                    LEFT JOIN Locals l ON l.Id = m.LocalId
                    LEFT JOIN Sectors s ON s.Id = m.SectorId
                    WHERE COALESCE(m.IsActive, 1) = 1
                      AND (@sectorId <= 0 OR m.SectorId = @sectorId)
                      AND (@localId <= 0 OR m.LocalId = @localId)
                      AND (@machineId <= 0 OR m.Id = @machineId)
                      AND m.LocalId IN @assignedLocalIds
                    ORDER BY COALESCE(m.LocalId, 0), COALESCE(m.LineCode, ''), COALESCE(m.MachineCode, ''), m.Id;",
                new
                {
                    sectorId = filter.SectorId,
                    localId = filter.LocalId,
                    machineId = filter.MachineId,
                    assignedLocalIds
                }
            ).ToList();

            if (machines.Count == 0)
            {
                return detail;
            }

            var machineIds = machines.Select(machine => machine.Id).ToArray();
            var statusRows = conn.Query<StatusRow>(
                @"
                    SELECT
                        SectorId,
                        MachineId,
                        StatusCode,
                        COALESCE(StatusText, '') AS StatusText,
                        COALESCE(RecipeName, '') AS RecipeName,
                        COALESCE(LotNo, '') AS LotNo,
                        EventDateTime
                    FROM MachineCurrentStatus
                    WHERE MachineId IN @machineIds;",
                new { machineIds }
            ).ToDictionary(row => row.MachineId);

            var events = conn.Query<EventRow>(
                @"
                    SELECT
                        SectorId,
                        MachineId,
                        StatusCode,
                        COALESCE(StatusText, '') AS StatusText,
                        COALESCE(RecipeName, '') AS RecipeName,
                        COALESCE(LotNo, '') AS LotNo,
                        EventDateTime
                    FROM MachineEvents
                    WHERE MachineId IN @machineIds
                      AND EventDateTime <= @rangeEnd
                      AND EventDateTime >= @rangeStart
                    ORDER BY MachineId, EventDateTime, Id;",
                new
                {
                    machineIds,
                    rangeStart = rangeStart.ToString("yyyy-MM-dd HH:mm:ss"),
                    rangeEnd = rangeEnd.ToString("yyyy-MM-dd HH:mm:ss")
                }
            ).ToList();

            var eventsByMachine = events
                .GroupBy(row => row.MachineId)
                .ToDictionary(group => group.Key, group => group.OrderBy(item => item.EventDateTime).ToList());

            var statusDefinitions = conn.Query<StatusDefinitionRow>(
                @"
                    SELECT
                        SectorId,
                        StatusCode,
                        DisplayCode,
                        COALESCE(Classification, '') AS Classification,
                        COALESCE(NamePt, '') AS NamePt,
                        COALESCE(NULLIF(NameJp, ''), NamePt, '') AS NameJp,
                        COALESCE(ColorHex, '') AS ColorHex,
                        COALESCE(TextColorHex, '') AS TextColorHex
                    FROM MachineStatuses
                    WHERE COALESCE(IsActive, 1) = 1;"
            ).ToDictionary(row => BuildStatusKey(row.SectorId, row.StatusCode), StringComparer.OrdinalIgnoreCase);

            var ec2States = LoadEc2States(conn, machineIds);

            var latestKnownEventCandidates = statusRows.Values
                .Select(item => item.EventDateTime)
                .Concat(events.Select(item => item.EventDateTime))
                .Concat(ec2States.Values.Select(item => item.SnapshotAt))
                .ToList();

            DateTime? latestKnownEventTime = latestKnownEventCandidates.Count == 0
                ? null
                : latestKnownEventCandidates.Max();

            var operatorMovementRows = conn.Query<MovementWindowRow>(
                @"
                    SELECT
                        date(ScheduleDate) AS Day,
                        COALESCE(OperatorCodigoFJ, '') AS OperatorCodigoFJ,
                        COALESCE(MovementType, '') AS MovementType,
                        COALESCE(EventTime, '') AS EventTime,
                        COALESCE(EventDateTime, '') AS EventDateTime,
                        COALESCE(ReplacementOperatorCodigoFJ, '') AS ReplacementOperatorCodigoFJ
                    FROM HaidaiMovements
                    WHERE (OperatorCodigoFJ = @codigoFJ
                        OR ReplacementOperatorCodigoFJ = @codigoFJ)
                      AND date(ScheduleDate) BETWEEN date(@startDate) AND date(@endDate)
                    ORDER BY date(ScheduleDate) DESC, COALESCE(EventTime, '') DESC, Id DESC;",
                new
                {
                    codigoFJ = operatorCodigoFJ.Trim(),
                    startDate = historyStartDate.ToString("yyyy-MM-dd"),
                    endDate = operationalDate.ToString("yyyy-MM-dd")
                })
                .ToList();

            var latestMovementByDay = operatorMovementRows
                .Where(item => string.Equals(item.OperatorCodigoFJ, operatorCodigoFJ.Trim(), StringComparison.OrdinalIgnoreCase))
                .GroupBy(item => item.Day, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            var latestReplacementMovementByDay = operatorMovementRows
                .Where(item => string.Equals(item.ReplacementOperatorCodigoFJ, operatorCodigoFJ.Trim(), StringComparison.OrdinalIgnoreCase))
                .GroupBy(item => item.Day, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var scheduleRow in scheduleRows
                         .GroupBy(row => new { Day = row.ScheduleDate.Date, row.LocalId })
                         .Select(group => group.First())
                         .OrderByDescending(row => row.ScheduleDate)
                         .ThenBy(row => row.LocalId))
            {
                var localMachines = machines
                    .Where(machine => machine.LocalId == scheduleRow.LocalId)
                    .ToList();

                if (localMachines.Count == 0)
                {
                    continue;
                }

                var baselinePeriod = GetShiftPeriod(scheduleRow.ScheduleDate.Date, selectedShiftHint);
                if (scheduleRow.ScheduleDate.Date == operationalDate)
                {
                    baselinePeriod = ResolveEffectivePeriod(baselinePeriod, latestKnownEventTime);
                }
                var dayKey = scheduleRow.ScheduleDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                latestMovementByDay.TryGetValue(dayKey, out var movement);
                latestReplacementMovementByDay.TryGetValue(dayKey, out var replacementMovement);
                var coverage = DescribeCoverage(baselinePeriod, movement, replacementMovement);
                var period = coverage.EffectivePeriod;
                var eventsCount = CountEventsInPeriod(localMachines, eventsByMachine, period);
                var countsInAverage = ShouldCountOperatorHistoryEntry(
                    coverage,
                    eventsCount,
                    out var averageReason);

                if (!countsInAverage && coverage.EffectiveMinutes <= 0)
                {
                    WriteDiagnostic(
                        $"[ProductionMonitor][YakinOperatorHistory] Operator={operatorCodigoFJ.Trim()} Date={scheduleRow.ScheduleDate:yyyy-MM-dd} Shift={selectedShiftHint} Area={scheduleRow.LocalId} WindowStart={period.Start:yyyy-MM-dd HH:mm:ss} WindowEnd={period.End:yyyy-MM-dd HH:mm:ss} EventsCount={eventsCount} CountsInAverage=nao Reason={averageReason}");
                    continue;
                }

                var summaries = BuildMachineSummaries(
                    localMachines,
                    statusRows,
                    eventsByMachine,
                    new Dictionary<int, List<ScheduleRow>>(),
                    ec2States,
                    statusDefinitions,
                    period);

                var aggregate = Summarize(summaries);
                var firstMachine = localMachines[0];
                    detail.Entries.Add(new ProductionOperatorHistoryEntryDto
                    {
                        Date = scheduleRow.ScheduleDate.Date,
                        Label = scheduleRow.ScheduleDate.ToString("MM/dd", CultureInfo.InvariantCulture),
                        LocalId = scheduleRow.LocalId,
                        LocalNamePt = firstMachine.LocalNamePt,
                        LocalNameJp = firstMachine.LocalNameJp,
                        RunningMinutes = aggregate.RunningMinutes,
                        StoppedMinutes = aggregate.StoppedMinutes,
                        InactiveMinutes = aggregate.InactiveMinutes,
                        ErrorMinutes = aggregate.ErrorMinutes,
                        EligibleMinutes = Math.Max(0, (period.End - period.Start).TotalMinutes),
                        KadouritsuPercent = aggregate.ProductionPercent,
                        CoverageMode = coverage.Mode,
                        IsPartialCoverage = coverage.IsPartial,
                        EffectiveMinutes = Math.Round(coverage.EffectiveMinutes, 1),
                        PlannedMinutes = Math.Round(coverage.PlannedMinutes, 1),
                        EventsCount = eventsCount,
                        CountsInAverage = countsInAverage,
                        AverageReason = averageReason,
                        WindowStart = period.Start,
                        WindowEnd = period.End
                    });
                }

            detail.LocalNamesPt = detail.Entries
                .Select(entry => entry.LocalNamePt)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            detail.LocalNamesJp = detail.Entries
                .Select(entry => entry.LocalNameJp)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            detail.AssignedAreaCount = detail.LocalNamesPt.Count > 0
                ? detail.LocalNamesPt.Count
                : detail.LocalNamesJp.Count;
            detail.TotalRunningMinutes = Math.Round(detail.Entries.Sum(entry => entry.RunningMinutes), 1);
            var averageEntries = detail.Entries.Where(entry => entry.CountsInAverage).ToList();
            detail.AverageKadouritsuPercent = averageEntries.Count == 0
                ? 0
                : Math.Round(averageEntries.Average(entry => entry.KadouritsuPercent), 1);
            detail.FullCoverageDays = averageEntries.Count(entry => !entry.IsPartialCoverage);
            detail.PartialCoverageDays = averageEntries.Count(entry => entry.IsPartialCoverage);

            return detail;
        }

        private static string BuildShiftPeriodHint(string namePt, string nameJp)
        {
            var normalizedPt = (namePt ?? string.Empty).Trim();
            var normalizedJp = (nameJp ?? string.Empty).Trim();
            var combined = $"{normalizedPt} {normalizedJp}".Trim();

            if (normalizedPt.Contains("noite", StringComparison.OrdinalIgnoreCase)
                || normalizedPt.Contains("night", StringComparison.OrdinalIgnoreCase)
                || normalizedPt.Contains("yakin", StringComparison.OrdinalIgnoreCase)
                || normalizedJp.Contains("\u591C", StringComparison.Ordinal)
                || normalizedJp.Contains("\u591C\u52E4", StringComparison.Ordinal))
            {
                return "yakin";
            }

            return string.IsNullOrWhiteSpace(combined) ? "hirukin" : combined;
        }

        private static bool IsYakinShiftHint(string shiftName)
        {
            var normalized = (shiftName ?? string.Empty).Trim().ToLowerInvariant();
            return normalized.Contains("yakin", StringComparison.Ordinal)
                || normalized.Contains("noite", StringComparison.Ordinal)
                || normalized.Contains("night", StringComparison.Ordinal)
                || normalized.Contains("\u591C", StringComparison.Ordinal)
                || normalized.Contains("\u591C\u52E4", StringComparison.Ordinal)
                || normalized.Contains("ﾃ･ﾂ､ﾅ禿･窶ｹﾂ､", StringComparison.Ordinal);
        }

        private static DateTime ResolveOperationalDate(DateTime requestedDate, string shiftName)
        {
            var date = requestedDate.Date;
            if (!IsYakinShiftHint(shiftName))
            {
                return date;
            }

            var now = DateTime.Now;
            if (date == now.Date && now.TimeOfDay <= new TimeSpan(8, 35, 0))
            {
                return date.AddDays(-1);
            }

            return date;
        }

        private static Dictionary<int, Ec2StateRow> LoadEc2States(System.Data.IDbConnection conn, IReadOnlyCollection<int> machineIds)
        {
            if (machineIds.Count == 0)
            {
                return new Dictionary<int, Ec2StateRow>();
            }

            return conn.Query<Ec2StateRow>(
                    @"
                        SELECT
                            ecs.MachineId,
                            ecs.SectorId,
                            ecs.LocalId,
                            COALESCE(ecs.AreaLabel, '') AS AreaLabel,
                            COALESCE(ecs.MachineCode, '') AS MachineCode,
                            COALESCE(ecs.MachineName, '') AS MachineName,
                            COALESCE(ecs.StatusText, '') AS StatusText,
                            COALESCE(ecs.IsRunning, 0) AS IsRunning,
                            COALESCE(ecs.IsIgnored, 0) AS IsIgnored,
                            COALESCE(ecs.IgnoreReason, '') AS IgnoreReason,
                            COALESCE(ecs.PartCode, '') AS PartCode,
                            ecs.PlannedProcessMinutes,
                            COALESCE(ecs.CapabilityType, '') AS CapabilityType,
                            ecs.OperationRate,
                            ecs.CurrentDifference,
                            COALESCE(ecs.LotNo, '') AS LotNo,
                            ecs.PlannedEndAt,
                            ecs.ProcessMinutes,
                            ecs.DailyProduction,
                            ecs.SnapshotAt,
                            COALESCE(style.ColorHex, '') AS PartColorHex,
                            COALESCE(style.TextColorHex, '') AS PartTextColorHex,
                            COALESCE(style.Description, '') AS PartDescription
                        FROM Ec2MachineCurrentState ecs
                        LEFT JOIN ProductionPartCodeStyles style
                          ON upper(trim(COALESCE(style.PartCode, ''))) = upper(trim(COALESCE(ecs.PartCode, '')))
                         AND COALESCE(style.IsActive, 1) = 1
                        WHERE ecs.MachineId IN @machineIds;",
                    new
                    {
                        machineIds
                    })
                .ToList()
                .Where(row => row.MachineId.HasValue)
                .GroupBy(row => row.MachineId!.Value)
                .ToDictionary(group => group.Key, group => group.First());
        }

        private static List<ProductionMachineSummaryDto> BuildMachineSummaries(
            IReadOnlyCollection<MachineRow> machines,
            IReadOnlyDictionary<int, StatusRow> statusRows,
            IReadOnlyDictionary<int, List<EventRow>> eventsByMachine,
            IReadOnlyDictionary<int, List<ScheduleRow>> operatorsByLocal,
            IReadOnlyDictionary<int, Ec2StateRow> ec2States,
            IReadOnlyDictionary<string, StatusDefinitionRow> statusDefinitions,
            ProductionShiftPeriod period)
        {
            var summaries = new List<ProductionMachineSummaryDto>(machines.Count);

            foreach (var machine in machines)
            {
                eventsByMachine.TryGetValue(machine.Id, out var machineEvents);
                machineEvents ??= new List<EventRow>();

                var metrics = CalculateMetrics(machineEvents, machine.SectorId, statusDefinitions, period);
                StatusRow? currentStatus = machineEvents
                    .Where(item => item.EventDateTime <= period.End)
                    .OrderByDescending(item => item.EventDateTime)
                    .FirstOrDefault();

                if (currentStatus == null && statusRows.TryGetValue(machine.Id, out var statusFallback))
                {
                    currentStatus = statusFallback;
                }

                var rawStatusCode = currentStatus?.StatusCode ?? 1;
                var statusSectorId = currentStatus?.SectorId ?? machine.SectorId;
                var displayCode = NormalizeStatusCode(statusSectorId, rawStatusCode, statusDefinitions);
                ec2States.TryGetValue(machine.Id, out var ec2State);

                var productionDenominatorMinutes = metrics.ProductionDenominatorMinutes > 0
                    ? metrics.ProductionDenominatorMinutes
                    : metrics.TotalMinutes;

                var summary = new ProductionMachineSummaryDto
                {
                    MachineId = machine.Id,
                    MachineCode = machine.MachineCode,
                    Machine = machine.MachineCode,
                    LineCode = machine.LineCode,
                    MachineNamePt = machine.MachineNamePt,
                    MachineNameJp = machine.MachineNameJp,
                    SectorId = machine.SectorId,
                    SectorNamePt = machine.SectorNamePt,
                    SectorNameJp = machine.SectorNameJp,
                    LocalId = machine.LocalId,
                    Area = machine.LocalNamePt,
                    LocalNamePt = machine.LocalNamePt,
                    LocalNameJp = machine.LocalNameJp,
                    StatusCode = rawStatusCode,
                    DisplayCode = displayCode,
                    StatusText = string.IsNullOrWhiteSpace(currentStatus?.StatusText)
                        ? ResolveStatusLabel(statusSectorId, rawStatusCode, displayCode, statusDefinitions, "pt-BR")
                        : currentStatus!.StatusText,
                    RecipeName = currentStatus?.RecipeName ?? string.Empty,
                    LotNo = currentStatus?.LotNo ?? string.Empty,
                    Ec2StatusText = ec2State?.StatusText ?? string.Empty,
                    Ec2Status = ec2State?.StatusText ?? string.Empty,
                    Ec2PartCode = ec2State?.PartCode ?? string.Empty,
                    PartCode = ec2State?.PartCode ?? string.Empty,
                    Ec2PartColorHex = ec2State?.PartColorHex ?? string.Empty,
                    PartCodeColorHex = ec2State?.PartColorHex ?? string.Empty,
                    Ec2PartTextColorHex = ec2State?.PartTextColorHex ?? string.Empty,
                    PartCodeTextColorHex = ec2State?.PartTextColorHex ?? string.Empty,
                    PartCodeDescription = ec2State?.PartDescription ?? string.Empty,
                    Ec2IgnoreReason = ec2State?.IgnoreReason ?? string.Empty,
                    IsEc2Running = ec2State?.IsRunning == 1,
                    IsEc2Ignored = ec2State?.IsIgnored == 1,
                    Ec2ProcessMinutes = ec2State?.ProcessMinutes,
                    Ec2SettingRate = ec2State?.OperationRate,
                    Ec2OperationRate = ec2State?.OperationRate,
                    Ec2SnapshotAt = ec2State?.SnapshotAt,
                    LastUpdate = currentStatus?.EventDateTime,
                    RunningMinutes = metrics.RunningMinutes,
                    StoppedMinutes = metrics.StoppedMinutes,
                    InactiveMinutes = metrics.InactiveMinutes,
                    ErrorMinutes = metrics.ErrorMinutes,
                    TotalMinutes = metrics.TotalMinutes,
                    ProductionDenominatorMinutes = productionDenominatorMinutes,
                    ProductionPercent = productionDenominatorMinutes <= 0
                        ? 0
                        : Math.Round((metrics.RunningMinutes / productionDenominatorMinutes) * 100d, 1)
                };

                if (ec2State != null)
                {
                    summary.StatusText = string.IsNullOrWhiteSpace(ec2State.StatusText)
                        ? summary.StatusText
                        : ec2State.StatusText;
                    summary.RecipeName = string.IsNullOrWhiteSpace(ec2State.PartCode)
                        ? summary.RecipeName
                        : ec2State.PartCode;
                    summary.LastUpdate = ec2State.SnapshotAt;

                    if (ec2State.IsIgnored == 1)
                    {
                        summary.DisplayCode = 1;
                        summary.RunningMinutes = 0;
                        summary.StoppedMinutes = 0;
                        summary.InactiveMinutes = 0;
                        summary.ErrorMinutes = 0;
                        summary.TotalMinutes = 0;
                        summary.ProductionDenominatorMinutes = 0;
                        summary.ProductionPercent = 0;
                    }
                    else if (ec2State.IsRunning == 0)
                    {
                        summary.DisplayCode = 3;
                    }
                }

                var includeInAreaAverage = ShouldIncludeInOperatingAverage(summary, out var areaAverageReason);
                summary.EnteredAreaAverage = includeInAreaAverage;
                summary.AreaAverageReason = includeInAreaAverage ? "ok" : areaAverageReason;

                if (machine.LocalId.HasValue && operatorsByLocal.TryGetValue(machine.LocalId.Value, out var assignedOperators))
                {
                    summary.ScheduledOperatorsPt.AddRange(
                        assignedOperators
                            .Select(item => item.OperatorNamePt)
                            .Where(name => !string.IsNullOrWhiteSpace(name))
                            .Distinct(StringComparer.OrdinalIgnoreCase));

                    summary.ScheduledOperatorsJp.AddRange(
                        assignedOperators
                            .Select(item => item.OperatorNameJp)
                            .Where(name => !string.IsNullOrWhiteSpace(name))
                            .Distinct(StringComparer.OrdinalIgnoreCase));
                }

                summaries.Add(summary);
            }

            return summaries;
        }

        private static List<ProductionAreaSummaryDto> BuildAreaSummaries(
            IReadOnlyCollection<ProductionMachineSummaryDto> machines,
            IReadOnlyDictionary<int, List<ScheduleRow>> operatorsByLocal)
        {
            return machines
                .GroupBy(machine => machine.LocalId)
                .Select(group =>
                {
                    var machineList = group.ToList();
                    var first = machineList.First();
                    var aggregate = Summarize(
                        machineList,
                        $"area:{first.LocalId?.ToString(CultureInfo.InvariantCulture) ?? "0"}:{first.LocalNamePt}");

                    var area = new ProductionAreaSummaryDto
                    {
                        LocalId = first.LocalId,
                        SectorId = first.SectorId,
                        LocalNamePt = first.LocalNamePt,
                        LocalNameJp = first.LocalNameJp,
                        SectorNamePt = first.SectorNamePt,
                        SectorNameJp = first.SectorNameJp,
                        MachineCount = machineList.Count,
                        MachinesRunning = aggregate.MachinesRunning,
                        MachinesStopped = aggregate.MachinesStopped,
                        MachinesIgnored = aggregate.MachinesIgnored,
                        AverageOperatingProcessMinutes = aggregate.AverageOperatingProcessMinutes,
                        RunningMinutes = aggregate.RunningMinutes,
                        StoppedMinutes = aggregate.StoppedMinutes,
                        InactiveMinutes = aggregate.InactiveMinutes,
                        ErrorMinutes = aggregate.ErrorMinutes,
                        TotalMinutes = aggregate.TotalMinutes,
                        ProductionPercent = aggregate.ProductionPercent,
                        LastUpdate = machineList
                            .Where(item => item.LastUpdate.HasValue)
                            .Select(item => item.LastUpdate)
                            .OrderByDescending(item => item)
                            .FirstOrDefault()
                    };

                    if (first.LocalId.HasValue && operatorsByLocal.TryGetValue(first.LocalId.Value, out var localOperators))
                    {
                        area.ScheduledOperatorCount = localOperators
                            .Select(item => item.OperatorCodigoFJ)
                            .Where(code => !string.IsNullOrWhiteSpace(code))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .Count();

                        area.ScheduledOperatorsPt.AddRange(
                            localOperators
                                .Select(item => item.OperatorNamePt)
                                .Where(name => !string.IsNullOrWhiteSpace(name))
                                .Distinct(StringComparer.OrdinalIgnoreCase));

                        area.ScheduledOperatorsJp.AddRange(
                            localOperators
                                .Select(item => item.OperatorNameJp)
                                .Where(name => !string.IsNullOrWhiteSpace(name))
                                .Distinct(StringComparer.OrdinalIgnoreCase));
                    }

                    return area;
                })
                .OrderBy(item => item.LocalNamePt, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static SummaryAggregate Summarize(
            IEnumerable<ProductionMachineSummaryDto> machines,
            string? diagnosticsContext = null)
        {
            var machineList = machines.ToList();
            var totalMinutes = machineList.Sum(machine => machine.TotalMinutes);
            var productionDenominatorMinutes = machineList.Sum(machine => machine.ProductionDenominatorMinutes > 0
                ? machine.ProductionDenominatorMinutes
                : machine.TotalMinutes);
            var runningMinutes = machineList.Sum(machine => machine.RunningMinutes);
            var operatingProcessTotal = 0d;
            var operatingProcessCount = 0;
            List<string>? diagnostics = diagnosticsContext == null
                ? null
                : new List<string>(machineList.Count + 2);

            foreach (var machine in machineList)
            {
                var includeInAverage = ShouldIncludeInOperatingAverage(machine, out var exclusionReason);
                if (includeInAverage)
                {
                    operatingProcessTotal += machine.Ec2ProcessMinutes!.Value;
                    operatingProcessCount++;
                }

                if (diagnostics != null)
                {
                    diagnostics.Add(
                        $"Area={diagnosticsContext} Machine={machine.MachineCode} Ec2Status={ResolveDiagnosticStatus(machine)} PartCode={machine.Ec2PartCode} LotNo={machine.LotNo} TimeRaw={FormatEc2ProcessMinutes(machine.Ec2ProcessMinutes)} TimeConverted={FormatEc2ProcessMinutes(machine.Ec2ProcessMinutes)} StyleMatched={(string.IsNullOrWhiteSpace(machine.Ec2PartColorHex) ? "nao" : "sim")} EnteredAreaAverage={(includeInAverage ? "sim" : "nao")} Reason={(includeInAverage ? "ok" : exclusionReason)}");
                }
            }

            var averageOperatingProcessMinutes = operatingProcessCount == 0
                ? 0
                : Math.Round(operatingProcessTotal / operatingProcessCount, 1);

            if (diagnostics != null)
            {
                diagnostics.Add(
                    $"AreaAverageSum={operatingProcessTotal.ToString("0.0", CultureInfo.InvariantCulture)} AreaAverageCount={operatingProcessCount} AreaAverage={averageOperatingProcessMinutes.ToString("0.0", CultureInfo.InvariantCulture)} Context={diagnosticsContext}");
                WriteDiagnosticLines("AverageOperatingProcessMinutes", diagnostics);
            }

            return new SummaryAggregate(
                RunningMinutes: Math.Round(runningMinutes, 1),
                StoppedMinutes: Math.Round(machineList.Sum(machine => machine.StoppedMinutes), 1),
                InactiveMinutes: Math.Round(machineList.Sum(machine => machine.InactiveMinutes), 1),
                ErrorMinutes: Math.Round(machineList.Sum(machine => machine.ErrorMinutes), 1),
                TotalMinutes: Math.Round(totalMinutes, 1),
                ProductionPercent: productionDenominatorMinutes <= 0
                    ? 0
                    : Math.Round((runningMinutes / productionDenominatorMinutes) * 100d, 1),
                MachinesRunning: machineList.Count(machine => machine.DisplayCode == 0),
                MachinesStopped: machineList.Count(machine => machine.DisplayCode == 3),
                MachinesIgnored: machineList.Count(machine => machine.IsEc2Ignored),
                AverageOperatingProcessMinutes: averageOperatingProcessMinutes);
        }

        private static bool ShouldIncludeInOperatingAverage(
            ProductionMachineSummaryDto machine,
            out string exclusionReason)
        {
            if (machine.IsEc2Ignored)
            {
                exclusionReason = $"ec2_ignored:{(string.IsNullOrWhiteSpace(machine.Ec2IgnoreReason) ? "no_reason" : machine.Ec2IgnoreReason)}";
                return false;
            }

            if (!machine.IsEc2Running)
            {
                exclusionReason = "machine_not_running";
                return false;
            }

            if (!machine.Ec2ProcessMinutes.HasValue)
            {
                exclusionReason = "process_time_null";
                return false;
            }

            var processMinutes = machine.Ec2ProcessMinutes.Value;
            if (!double.IsFinite(processMinutes))
            {
                exclusionReason = "process_time_not_finite";
                return false;
            }

            if (processMinutes <= 0)
            {
                exclusionReason = "process_time_not_positive";
                return false;
            }

            exclusionReason = "ok";
            return true;
        }

        private static string FormatEc2ProcessMinutes(double? value)
        {
            if (!value.HasValue)
            {
                return "null";
            }

            return double.IsFinite(value.Value)
                ? value.Value.ToString("0.0", CultureInfo.InvariantCulture)
                : value.Value.ToString(CultureInfo.InvariantCulture);
        }

        private static string ResolveDiagnosticStatus(ProductionMachineSummaryDto machine)
        {
            var status = string.IsNullOrWhiteSpace(machine.Ec2StatusText)
                ? machine.StatusText
                : machine.Ec2StatusText;
            return string.IsNullOrWhiteSpace(status)
                ? "-"
                : status;
        }

        private static void WriteDiagnosticLines(string scope, IEnumerable<string> lines)
        {
            foreach (var line in lines)
            {
                WriteDiagnostic($"[ProductionMonitor][{scope}] {line}");
            }
        }

        private static void WriteDashboardPerformanceLog(
            ProductionDashboardFilter filter,
            long totalMs,
            long queryMachinesMs,
            long queryEventsMs,
            long queryEc2Ms,
            long buildMachinesMs,
            long buildAreasMs,
            int rowsRead,
            int machinesCount,
            int areasCount)
        {
            WriteDiagnostic(
                "[ProductionMonitor][DashboardPerformance] " +
                $"Action=BuildDashboard " +
                $"Date={filter.Date:yyyy-MM-dd} " +
                $"Shift={filter.ShiftId} " +
                $"Sector={filter.SectorId} " +
                $"Local={filter.LocalId} " +
                $"Machine={filter.MachineId} " +
                $"QueryMachinesMs={queryMachinesMs} " +
                $"QueryEventsMs={queryEventsMs} " +
                $"QueryEc2Ms={queryEc2Ms} " +
                $"BuildMachinesMs={buildMachinesMs} " +
                $"BuildAreasMs={buildAreasMs} " +
                $"RowsRead={rowsRead} " +
                $"MachinesCount={machinesCount} " +
                $"AreasCount={areasCount} " +
                $"TotalMs={totalMs}");
        }

        private static T Measure<T>(Func<T> action, out long elapsed)
        {
            var watch = Stopwatch.StartNew();
            var result = action();
            watch.Stop();
            elapsed = watch.ElapsedMilliseconds;
            return result;
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

        private static List<DateTime> BuildTimelineMoments(ProductionShiftPeriod period)
        {
            var cells = new List<DateTime>();
            if (period.End <= period.Start)
            {
                return cells;
            }

            for (var cursor = period.Start; cursor <= period.End; cursor = cursor.AddMinutes(5))
            {
                cells.Add(cursor);
            }

            return cells;
        }

        private static ProductionMetrics CalculateMetrics(
            IReadOnlyList<EventRow> machineEvents,
            int? machineSectorId,
            IReadOnlyDictionary<string, StatusDefinitionRow> statusDefinitions,
            ProductionShiftPeriod period)
        {
            var running = 0d;
            var stopped = 0d;
            var inactive = 0d;
            var error = 0d;
            var noCount = 0d;
            var dadProductionDenominator = 0d;
            var useDadProductionRule = IsDadSector(machineSectorId)
                || machineEvents.Any(item => IsDadSector(item.SectorId));
            var total = Math.Max(0, (period.End - period.Start).TotalMinutes);

            var seed = machineEvents
                .Where(item => item.EventDateTime <= period.Start)
                .OrderByDescending(item => item.EventDateTime)
                .FirstOrDefault();

            var cursor = period.Start;
            var currentStatus = seed?.StatusCode;
            var currentSectorId = seed?.SectorId ?? machineSectorId;

            foreach (var machineEvent in machineEvents.Where(item => item.EventDateTime >= period.Start && item.EventDateTime <= period.End))
            {
                if (machineEvent.EventDateTime > cursor)
                {
                    AddMinutes(
                        ResolveStatusDefinition(currentSectorId, currentStatus ?? 1, statusDefinitions),
                        (machineEvent.EventDateTime - cursor).TotalMinutes,
                        IsDadSector(currentSectorId ?? machineSectorId),
                        ref running,
                        ref stopped,
                        ref inactive,
                        ref error,
                        ref noCount,
                        ref dadProductionDenominator);
                }

                cursor = machineEvent.EventDateTime < period.Start
                    ? period.Start
                    : machineEvent.EventDateTime;
                currentStatus = machineEvent.StatusCode;
                currentSectorId = machineEvent.SectorId ?? machineSectorId;
            }

            if (cursor < period.End)
            {
                AddMinutes(
                    ResolveStatusDefinition(currentSectorId, currentStatus ?? 1, statusDefinitions),
                    (period.End - cursor).TotalMinutes,
                    IsDadSector(currentSectorId ?? machineSectorId),
                    ref running,
                    ref stopped,
                    ref inactive,
                    ref error,
                    ref noCount,
                    ref dadProductionDenominator);
            }

            var effectiveTotal = Math.Max(0, total - noCount);
            var productionDenominator = useDadProductionRule
                ? Math.Max(0, dadProductionDenominator)
                : effectiveTotal;

            return new ProductionMetrics(running, stopped, inactive, error, effectiveTotal, productionDenominator);
        }

        private static ProductionShiftPeriod ResolveEffectivePeriod(ProductionShiftPeriod basePeriod, DateTime? latestKnownEventTime)
        {
            var now = DateTime.Now;
            if (basePeriod.End <= now)
            {
                return basePeriod;
            }

            if (basePeriod.Start > now)
            {
                return new ProductionShiftPeriod
                {
                    Start = basePeriod.Start,
                    End = basePeriod.Start
                };
            }

            var effectiveEnd = RoundDownToFiveMinutes(now);
            if (effectiveEnd < basePeriod.Start)
            {
                effectiveEnd = basePeriod.Start;
            }

            if (!latestKnownEventTime.HasValue)
            {
                effectiveEnd = basePeriod.Start;
            }
            else
            {
                var roundedLatest = RoundDownToFiveMinutes(latestKnownEventTime.Value);
                if (roundedLatest < basePeriod.Start)
                {
                    effectiveEnd = basePeriod.Start;
                }
                else if (roundedLatest < effectiveEnd)
                {
                    effectiveEnd = roundedLatest;
                }
            }

            if (effectiveEnd > basePeriod.End)
            {
                effectiveEnd = basePeriod.End;
            }

            return new ProductionShiftPeriod
            {
                Start = basePeriod.Start,
                End = effectiveEnd
            };
        }

        private static ProductionShiftPeriod ResolveEffectiveOperatorPeriod(
            ProductionShiftPeriod scheduledPeriod,
            MovementWindowRow? movement,
            MovementWindowRow? replacementMovement)
        {
            var start = scheduledPeriod.Start;
            var end = scheduledPeriod.End;

            if ((movement == null || string.IsNullOrWhiteSpace(movement.EventTime))
                && (replacementMovement == null || string.IsNullOrWhiteSpace(replacementMovement.EventTime)))
            {
                return new ProductionShiftPeriod
                {
                    Start = start,
                    End = end
                };
            }

            var eventMoment = movement == null
                ? null
                : ParseMovementMoment(movement.Day, movement.EventTime, movement.EventDateTime);
            var replacementMoment = replacementMovement == null
                ? null
                : ParseMovementMoment(replacementMovement.Day, replacementMovement.EventTime, replacementMovement.EventDateTime);

            if (!eventMoment.HasValue && !replacementMoment.HasValue)
            {
                return new ProductionShiftPeriod
                {
                    Start = start,
                    End = end
                };
            }

            if (eventMoment.HasValue && string.Equals(movement?.MovementType, "late", StringComparison.OrdinalIgnoreCase))
            {
                start = eventMoment.Value > start ? eventMoment.Value : start;
            }
            else if (eventMoment.HasValue && string.Equals(movement?.MovementType, "early_leave", StringComparison.OrdinalIgnoreCase))
            {
                end = eventMoment.Value < end ? eventMoment.Value : end;
            }

            if (replacementMoment.HasValue && string.Equals(replacementMovement?.MovementType, "late", StringComparison.OrdinalIgnoreCase))
            {
                end = replacementMoment.Value < end ? replacementMoment.Value : end;
            }
            else if (replacementMoment.HasValue && string.Equals(replacementMovement?.MovementType, "early_leave", StringComparison.OrdinalIgnoreCase))
            {
                start = replacementMoment.Value > start ? replacementMoment.Value : start;
            }

            if (end < start)
            {
                end = start;
            }

            return new ProductionShiftPeriod
            {
                Start = start,
                End = end
            };
        }

        private static ProductionCoverageDescriptor DescribeCoverage(
            ProductionShiftPeriod scheduledPeriod,
            MovementWindowRow? movement,
            MovementWindowRow? replacementMovement)
        {
            var effectivePeriod = ResolveEffectiveOperatorPeriod(scheduledPeriod, movement, replacementMovement);
            var plannedMinutes = Math.Max(0, (scheduledPeriod.End - scheduledPeriod.Start).TotalMinutes);
            var effectiveMinutes = Math.Max(0, (effectivePeriod.End - effectivePeriod.Start).TotalMinutes);
            var isPartial = plannedMinutes > 0 && effectiveMinutes + 0.5 < plannedMinutes;

            var mode = "full";
            if (replacementMovement != null)
            {
                if (string.Equals(replacementMovement.MovementType, "late", StringComparison.OrdinalIgnoreCase))
                {
                    mode = "replacement_late";
                }
                else if (string.Equals(replacementMovement.MovementType, "early_leave", StringComparison.OrdinalIgnoreCase))
                {
                    mode = "replacement_early_leave";
                }
            }
            else if (movement != null)
            {
                if (string.Equals(movement.MovementType, "late", StringComparison.OrdinalIgnoreCase))
                {
                    mode = "late";
                }
                else if (string.Equals(movement.MovementType, "early_leave", StringComparison.OrdinalIgnoreCase))
                {
                    mode = "early_leave";
                }
            }

            if (!isPartial)
            {
                mode = "full";
            }

            return new ProductionCoverageDescriptor(
                effectivePeriod,
                mode,
                isPartial,
                plannedMinutes,
                effectiveMinutes);
        }

        private static int CountEventsInPeriod(
            IReadOnlyCollection<MachineRow> machines,
            IReadOnlyDictionary<int, List<EventRow>> eventsByMachine,
            ProductionShiftPeriod period)
        {
            if (period.End <= period.Start)
            {
                return 0;
            }

            var count = 0;
            foreach (var machine in machines)
            {
                if (!eventsByMachine.TryGetValue(machine.Id, out var machineEvents))
                {
                    continue;
                }

                count += machineEvents.Count(item => item.EventDateTime >= period.Start && item.EventDateTime <= period.End);
            }

            return count;
        }

        private static bool ShouldCountOperatorHistoryEntry(
            ProductionCoverageDescriptor coverage,
            int eventsCount,
            out string reason)
        {
            if (coverage.PlannedMinutes <= 0 || coverage.EffectiveMinutes <= 0)
            {
                reason = "no_operational_window_or_events";
                return false;
            }

            if (eventsCount <= 0)
            {
                reason = "no_events_in_operational_window";
                return false;
            }

            reason = "ok";
            return true;
        }

        private static DateTime? ParseMovementMoment(string day, string eventTime, string eventDateTime)
        {
            if (!string.IsNullOrWhiteSpace(eventDateTime)
                && DateTime.TryParseExact(eventDateTime.Trim(), "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var explicitDateTime))
            {
                return explicitDateTime;
            }

            if (string.IsNullOrWhiteSpace(day) || string.IsNullOrWhiteSpace(eventTime))
            {
                return null;
            }

            if (!DateTime.TryParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var datePart))
            {
                return null;
            }

            var normalizedTime = eventTime.Trim();
            if (normalizedTime.Length == 5)
            {
                normalizedTime += ":00";
            }

            if (!TimeSpan.TryParse(normalizedTime, CultureInfo.InvariantCulture, out var timePart))
            {
                return null;
            }

            return datePart.Date.Add(timePart);
        }

        private static DateTime RoundDownToFiveMinutes(DateTime value)
        {
            var roundedMinute = value.Minute - (value.Minute % 5);
            return new DateTime(value.Year, value.Month, value.Day, value.Hour, roundedMinute, 0, value.Kind);
        }

        private static int ResolveStatusAt(
            IReadOnlyList<EventRow> machineEvents,
            DateTime moment)
        {
            for (var index = machineEvents.Count - 1; index >= 0; index--)
            {
                if (machineEvents[index].EventDateTime <= moment)
                {
                    return machineEvents[index].StatusCode;
                }
            }

            return 1;
        }

        private static bool IsDadSector(int? sectorId)
            => sectorId.GetValueOrDefault() == 2;

        private static void AddMinutes(
            StatusDefinitionRow statusDefinition,
            double minutes,
            bool useDadProductionRule,
            ref double running,
            ref double stopped,
            ref double inactive,
            ref double error,
            ref double noCount,
            ref double dadProductionDenominator)
        {
            if (minutes <= 0)
            {
                return;
            }

            var classification = NormalizeClassification(statusDefinition.Classification, statusDefinition.DisplayCode);
            if (useDadProductionRule && !string.Equals(classification, "StopCounts", StringComparison.OrdinalIgnoreCase))
            {
                // DAD follows the spreadsheet rule: running / (all motive minutes - StopCounts machine-status minutes).
                dadProductionDenominator += minutes;
            }

            switch (classification)
            {
                case "Running":
                    running += minutes;
                    break;
                case "StopNoCount":
                    noCount += minutes;
                    break;
                case "Error":
                    error += minutes;
                    break;
                case "StopCounts" when statusDefinition.DisplayCode == 3:
                    stopped += minutes;
                    break;
                default:
                    inactive += minutes;
                    break;
            }
        }

        public static string GetStatusLabel(int statusCode, string locale)
        {
            if (string.Equals(locale, "ja-JP", StringComparison.OrdinalIgnoreCase))
            {
                return statusCode switch
                {
                    0 => "\u7A3C\u50CD\u4E2D",
                    1 => "\u505C\u6B62",
                    2 => "\u975E\u7A3C\u50CD",
                    3 => "\u7570\u5E38",
                    _ => "-"
                };
            }

            return statusCode switch
            {
                0 => "Rodando",
                1 => "Parado",
                2 => "Inativo",
                3 => "Erro",
                _ => "-"
            };
        }
        public static string GetTimelineClass(int statusCode)
        {
            return statusCode switch
            {
                0 => "status-0",
                1 => "status-1",
                2 => "status-2",
                3 => "status-3",
                _ => "status-2"
            };
        }

        private static string ResolveStatusLabel(
            int? sectorId,
            int rawStatusCode,
            int displayCode,
            IReadOnlyDictionary<string, StatusDefinitionRow> statusDefinitions,
            string locale)
        {
            var statusDefinition = ResolveStatusDefinition(sectorId, rawStatusCode, statusDefinitions);
            if (statusDefinition.IsDefined)
            {
                return string.Equals(locale, "ja-JP", StringComparison.OrdinalIgnoreCase)
                    ? (string.IsNullOrWhiteSpace(statusDefinition.NameJp) ? statusDefinition.NamePt : statusDefinition.NameJp)
                    : (string.IsNullOrWhiteSpace(statusDefinition.NamePt) ? statusDefinition.NameJp : statusDefinition.NamePt);
            }

            return GetNormalizedStatusLabel(displayCode, locale);
        }

        private static string GetNormalizedStatusLabel(int statusCode, string locale)
        {
            var normalized = statusCode switch
            {
                0 => 0,
                3 => 3,
                4 => 4,
                _ => 1
            };

            if (string.Equals(locale, "ja-JP", StringComparison.OrdinalIgnoreCase))
            {
                return normalized switch
                {
                    0 => "\u7A3C\u50CD\u4E2D",
                    1 => "\u975E\u7A3C\u50CD",
                    3 => "\u505C\u6B62",
                    4 => "\u30A8\u30E9\u30FC",
                    _ => "-"
                };
            }

            return normalized switch
            {
                0 => "Rodando",
                1 => "Inativo",
                3 => "Parado",
                4 => "Erro",
                _ => "-"
            };
        }
        private static string GetNormalizedTimelineClass(int statusCode)
        {
            return (statusCode switch
            {
                0 => "status-0",
                1 => "status-1",
                3 => "status-3",
                4 => "status-4",
                _ => "status-1"
            });
        }

        private static int NormalizeStatusCode(
            int? sectorId,
            int statusCode,
            IReadOnlyDictionary<string, StatusDefinitionRow> statusDefinitions)
        {
            var statusDefinition = ResolveStatusDefinition(sectorId, statusCode, statusDefinitions);
            if (statusDefinition.IsDefined)
            {
                return statusDefinition.DisplayCode switch
                {
                    0 => 0,
                    1 => 1,
                    3 => 3,
                    4 => 4,
                    _ => 1
                };
            }

            return statusCode switch
            {
                0 => 0,
                1 => 1,
                2 => 1,
                3 => 3,
                4 => 4,
                _ => 1
            };
        }

        private static StatusDefinitionRow ResolveStatusDefinition(
            int? sectorId,
            int statusCode,
            IReadOnlyDictionary<string, StatusDefinitionRow> statusDefinitions)
        {
            if (sectorId.HasValue
                && statusDefinitions.TryGetValue(BuildStatusKey(sectorId, statusCode), out var sectorDefinition))
            {
                sectorDefinition.IsDefined = true;
                return sectorDefinition;
            }

            if (statusDefinitions.TryGetValue(BuildStatusKey(null, statusCode), out var globalDefinition))
            {
                globalDefinition.IsDefined = true;
                return globalDefinition;
            }

            return new StatusDefinitionRow
            {
                StatusCode = statusCode,
                DisplayCode = statusCode switch
                {
                    0 => 0,
                    3 => 3,
                    4 => 4,
                    _ => 1
                },
                Classification = statusCode switch
                {
                    0 => "Running",
                    4 => "Error",
                    _ => "StopCounts"
                }
            };
        }

        private static string NormalizeClassification(string classification, int displayCode)
        {
            var normalized = (classification ?? string.Empty).Trim();
            if (string.Equals(normalized, "Running", StringComparison.OrdinalIgnoreCase))
            {
                return "Running";
            }

            if (string.Equals(normalized, "StopNoCount", StringComparison.OrdinalIgnoreCase))
            {
                return "StopNoCount";
            }

            if (string.Equals(normalized, "Error", StringComparison.OrdinalIgnoreCase))
            {
                return "Error";
            }

            return displayCode == 0
                ? "Running"
                : displayCode == 4
                    ? "Error"
                    : "StopCounts";
        }

        private static string BuildStatusKey(int? sectorId, int statusCode)
        {
            return $"{sectorId.GetValueOrDefault()}:{statusCode}";
        }

        private sealed class StatusDefinitionRow
        {
            public int? SectorId { get; set; }
            public int StatusCode { get; set; }
            public int DisplayCode { get; set; }
            public string Classification { get; set; } = string.Empty;
            public string NamePt { get; set; } = string.Empty;
            public string NameJp { get; set; } = string.Empty;
            public string ColorHex { get; set; } = string.Empty;
            public string TextColorHex { get; set; } = string.Empty;
            public bool IsDefined { get; set; }
        }

        private sealed class ShiftLookupRow
        {
            public int Id { get; set; }
            public string NamePt { get; set; } = string.Empty;
            public string NameJp { get; set; } = string.Empty;
        }

        private sealed class MachineRow
        {
            public int Id { get; set; }
            public string MachineCode { get; set; } = string.Empty;
            public string LineCode { get; set; } = string.Empty;
            public string MachineNamePt { get; set; } = string.Empty;
            public string MachineNameJp { get; set; } = string.Empty;
            public int? LocalId { get; set; }
            public int? SectorId { get; set; }
            public string LocalNamePt { get; set; } = string.Empty;
            public string LocalNameJp { get; set; } = string.Empty;
            public string SectorNamePt { get; set; } = string.Empty;
            public string SectorNameJp { get; set; } = string.Empty;
        }

        private class StatusRow
        {
            public int? SectorId { get; set; }
            public int MachineId { get; set; }
            public int StatusCode { get; set; }
            public string StatusText { get; set; } = string.Empty;
            public string RecipeName { get; set; } = string.Empty;
            public string LotNo { get; set; } = string.Empty;
            public DateTime EventDateTime { get; set; }
        }

        private sealed class EventRow : StatusRow
        {
        }

        private sealed class Ec2StateRow
        {
            public int? MachineId { get; set; }
            public int? SectorId { get; set; }
            public int? LocalId { get; set; }
            public string AreaLabel { get; set; } = string.Empty;
            public string MachineCode { get; set; } = string.Empty;
            public string MachineName { get; set; } = string.Empty;
            public string StatusText { get; set; } = string.Empty;
            public int IsRunning { get; set; }
            public int IsIgnored { get; set; }
            public string IgnoreReason { get; set; } = string.Empty;
            public string PartCode { get; set; } = string.Empty;
            public double? PlannedProcessMinutes { get; set; }
            public string CapabilityType { get; set; } = string.Empty;
            public double? OperationRate { get; set; }
            public double? CurrentDifference { get; set; }
            public string LotNo { get; set; } = string.Empty;
            public DateTime? PlannedEndAt { get; set; }
            public double? ProcessMinutes { get; set; }
            public double? DailyProduction { get; set; }
            public DateTime SnapshotAt { get; set; }
            public string PartColorHex { get; set; } = string.Empty;
            public string PartTextColorHex { get; set; } = string.Empty;
            public string PartDescription { get; set; } = string.Empty;
        }

        private sealed class ScheduleRow
        {
            public DateTime ScheduleDate { get; set; }
            public int LocalId { get; set; }
            public string OperatorCodigoFJ { get; set; } = string.Empty;
            public string OperatorNamePt { get; set; } = string.Empty;
            public string OperatorNameJp { get; set; } = string.Empty;
        }

        private sealed class MovementWindowRow
        {
            public string Day { get; set; } = string.Empty;
            public string OperatorCodigoFJ { get; set; } = string.Empty;
            public string MovementType { get; set; } = string.Empty;
            public string EventTime { get; set; } = string.Empty;
            public string EventDateTime { get; set; } = string.Empty;
            public string ReplacementOperatorCodigoFJ { get; set; } = string.Empty;
        }

        private sealed class ProductionOperatorAccumulator
        {
            public string OperatorCodigoFJ { get; set; } = string.Empty;
            public string OperatorNamePt { get; set; } = string.Empty;
            public string OperatorNameJp { get; set; } = string.Empty;
            public double EstimatedRunningMinutes { get; set; }
            public double EligibleMinutes { get; set; }
            public List<double> EntryKadouritsuPercents { get; } = new();
            public HashSet<string> LocalNamesPt { get; } = new(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> LocalNamesJp { get; } = new(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> FullCoverageDays { get; } = new(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> PartialCoverageDays { get; } = new(StringComparer.OrdinalIgnoreCase);
        }

        private readonly record struct ProductionCoverageDescriptor(
            ProductionShiftPeriod EffectivePeriod,
            string Mode,
            bool IsPartial,
            double PlannedMinutes,
            double EffectiveMinutes);

        private readonly record struct SummaryAggregate(
            double RunningMinutes,
            double StoppedMinutes,
            double InactiveMinutes,
            double ErrorMinutes,
            double TotalMinutes,
            double ProductionPercent,
            int MachinesRunning,
            int MachinesStopped,
            int MachinesIgnored,
            double AverageOperatingProcessMinutes);

        private readonly record struct ProductionMetrics(
            double RunningMinutes,
            double StoppedMinutes,
            double InactiveMinutes,
            double ErrorMinutes,
            double TotalMinutes,
            double ProductionDenominatorMinutes);
    }
}
