using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Dapper;
using TeamOps.Core.Common;
using TeamOps.Data.Db;
using TeamOps.UI.Forms.Models;
using TeamOps.Services;

namespace TeamOps.UI.Services
{
    public sealed class OperatorManagerReportService
    {
        private const int HolidayWorkFixedMinutes = 11 * 60;

        private readonly SqliteConnectionFactory _factory;
        private readonly int[] _authorizedSectorIds;

        public OperatorManagerReportService(
            SqliteConnectionFactory factory,
            IEnumerable<int> authorizedSectorIds)
        {
            _factory = factory;
            _authorizedSectorIds = authorizedSectorIds
                .Where(id => id > 0)
                .Distinct()
                .ToArray();

            if (_authorizedSectorIds.Length == 0)
                throw new UnauthorizedAccessException("Usuario sem setores autorizados para relatorios.");
        }

        public OperatorManagerInitPayload GetInitialPayload(int defaultShiftId, int defaultSectorId)
        {
            using var conn = _factory.CreateOpenConnection();
            MasterCardModuleService.EnsureSchema(conn);

            var shifts = conn.Query<OperatorManagerLookupItem>(
                @"
                    SELECT
                        Id,
                        COALESCE(NULLIF(NamePt, ''), NULLIF(NameJp, ''), 'Turno ' || Id) AS Name
                    FROM Shifts
                    ORDER BY Id;")
                .ToList();

            var sectors = conn.Query<OperatorManagerLookupItem>(
                @"
                    SELECT
                        Id,
                        COALESCE(NULLIF(NamePt, ''), NULLIF(NameJp, ''), 'Setor ' || Id) AS Name
                    FROM Sectors
                    WHERE Id IN @AuthorizedSectorIds
                    ORDER BY Id;",
                new { AuthorizedSectorIds = _authorizedSectorIds })
                .ToList();

            var groups = conn.Query<OperatorManagerLookupItem>(
                @"
                    SELECT
                        Id,
                        COALESCE(NULLIF(NamePt, ''), NULLIF(NameJp, ''), 'Grupo ' || Id) AS Name
                    FROM Groups
                    ORDER BY Id;")
                .ToList();

            return new OperatorManagerInitPayload(
                shifts.Any(item => item.Id == defaultShiftId) ? defaultShiftId : 0,
                sectors.Any(item => item.Id == defaultSectorId) ? defaultSectorId : 0,
                90,
                shifts,
                sectors,
                groups);
        }

        public OperatorManagerDirectoryPayload GetDirectory(OperatorManagerDirectoryFilter filter)
        {
            using var conn = _factory.CreateOpenConnection();
            MasterCardModuleService.EnsureSchema(conn);

            var startDate = DateTime.Today.AddDays(-(Math.Max(1, filter.PeriodDays) - 1));
            var endDate = DateTime.Today;
            var search = (filter.Search ?? string.Empty).Trim();
            DemandSector(filter.SectorId);

            var rows = conn.Query<OperatorManagerDirectoryRow>(
                @"
                    SELECT
                        o.CodigoFJ,
                        COALESCE(NULLIF(o.NameRomanji, ''), o.CodigoFJ) AS Name,
                        COALESCE(NULLIF(o.NameNihongo, ''), COALESCE(NULLIF(o.NameRomanji, ''), o.CodigoFJ)) AS NameJp,
                        o.ShiftId,
                        COALESCE(NULLIF(s.NamePt, ''), NULLIF(s.NameJp, ''), 'Turno ' || o.ShiftId) AS ShiftName,
                        o.SectorId,
                        COALESCE(NULLIF(sec.NamePt, ''), NULLIF(sec.NameJp, ''), 'Setor ' || o.SectorId) AS SectorName,
                        o.GroupId,
                        COALESCE(NULLIF(g.NamePt, ''), NULLIF(g.NameJp, ''), 'Grupo ' || o.GroupId) AS GroupName,
                        COALESCE(o.Trainer, 0) AS Trainer,
                        COALESCE(o.IsLeader, 0) AS IsLeader
                    FROM Operators o
                    LEFT JOIN Shifts s ON s.Id = o.ShiftId
                    LEFT JOIN Sectors sec ON sec.Id = o.SectorId
                    LEFT JOIN Groups g ON g.Id = o.GroupId
                    WHERE COALESCE(o.Status, 1) = 1
                      AND o.SectorId IN @AuthorizedSectorIds
                      AND (@ShiftId <= 0 OR o.ShiftId = @ShiftId)
                      AND (@SectorId <= 0 OR o.SectorId = @SectorId)
                      AND (@GroupId <= 0 OR o.GroupId = @GroupId)
                      AND (
                            @Search = ''
                         OR o.CodigoFJ LIKE '%' || @Search || '%'
                         OR COALESCE(o.NameRomanji, '') LIKE '%' || @Search || '%'
                         OR COALESCE(o.NameNihongo, '') LIKE '%' || @Search || '%'
                      )
                    ORDER BY o.NameRomanji, o.CodigoFJ;",
                new
                {
                    StartDate = startDate.ToString("yyyy-MM-dd"),
                    EndDate = endDate.ToString("yyyy-MM-dd"),
                    filter.ShiftId,
                    filter.SectorId,
                    filter.GroupId,
                    Search = search,
                    AuthorizedSectorIds = _authorizedSectorIds
                })
                .ToList();

            if (rows.Count == 0)
            {
                return new OperatorManagerDirectoryPayload(
                    startDate.ToString("yyyy-MM-dd"),
                    endDate.ToString("yyyy-MM-dd"),
                    Array.Empty<OperatorManagerDirectoryItem>());
            }

            var operatorCodes = rows
                .Select(item => item.CodigoFJ)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var scheduleRows = conn.Query<OperatorManagerScheduleRow>(
                @"
                    SELECT
                        ha.OperatorCodigoFJ,
                        date(ha.ScheduleDate) AS Day,
                        ha.ShiftId,
                        ha.LocalId,
                        COALESCE(NULLIF(l.NamePt, ''), NULLIF(l.NameJp, ''), '') AS LocalName,
                        COALESCE(ha.AssignmentCode, '') AS AssignmentCode,
                        COALESCE(ha.AvailabilityStatus, '') AS AvailabilityStatus,
                        COALESCE(ha.IsLineupActive, 1) AS IsLineupActive,
                        COALESCE(ha.IsTrainee, 0) AS IsTrainee
                    FROM HaidaiAssignments ha
                    LEFT JOIN Locals l ON l.Id = ha.LocalId
                    WHERE ha.OperatorCodigoFJ IN @OperatorCodes
                      AND date(ha.ScheduleDate) BETWEEN date(@StartDate) AND date(@EndDate)
                    ORDER BY date(ha.ScheduleDate) DESC, ha.Id DESC;",
                new
                {
                    OperatorCodes = operatorCodes,
                    StartDate = startDate.ToString("yyyy-MM-dd"),
                    EndDate = endDate.ToString("yyyy-MM-dd")
                })
                .ToList();

            var latestSchedulesByOperator = scheduleRows
                .GroupBy(item => item.OperatorCodigoFJ, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyDictionary<string, OperatorManagerScheduleRow>)group
                        .GroupBy(item => item.Day, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(dayGroup => dayGroup.Key, dayGroup => dayGroup.First(), StringComparer.OrdinalIgnoreCase),
                    StringComparer.OrdinalIgnoreCase);

            var todokeRows = conn.Query<OperatorManagerTodokeRow>(
                @"
                    SELECT
                        a.OperatorCodigoFJ,
                        date(a.RequestDate) AS Day,
                        a.Id,
                        a.TodokeMotivoId AS MotiveId,
                        COALESCE(m.NomePt, '') AS MotiveName,
                        COALESCE(a.Notes, '') AS Notes,
                        CASE
                            WHEN EXISTS (
                                SELECT 1
                                FROM YukyuTodoke y
                                WHERE y.AcompYukyuId = a.Id
                            ) THEN 1
                            ELSE 0
                        END AS Validated
                    FROM AcompYukyu a
                    LEFT JOIN TodokeMotivo m ON m.Id = a.TodokeMotivoId
                    WHERE a.OperatorCodigoFJ IN @OperatorCodes
                      AND date(a.RequestDate) BETWEEN date(@StartDate) AND date(@EndDate)
                    ORDER BY date(a.RequestDate) DESC, a.Id DESC;",
                new
                {
                    OperatorCodes = operatorCodes,
                    StartDate = startDate.ToString("yyyy-MM-dd"),
                    EndDate = endDate.ToString("yyyy-MM-dd")
                })
                .ToList();

            var latestTodokesByOperator = todokeRows
                .GroupBy(item => item.OperatorCodigoFJ, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyDictionary<string, OperatorManagerTodokeRow>)group
                        .GroupBy(item => item.Day, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(dayGroup => dayGroup.Key, dayGroup => dayGroup.First(), StringComparer.OrdinalIgnoreCase),
                    StringComparer.OrdinalIgnoreCase);

            var movementRows = conn.Query<OperatorManagerMovementRow>(
                @"
                    SELECT
                        OperatorCodigoFJ,
                        date(ScheduleDate) AS Day,
                        COALESCE(MovementType, '') AS MovementType,
                        COALESCE(EventTime, '') AS EventTime,
                        COALESCE(EventDateTime, '') AS EventDateTime,
                        COALESCE(AssignmentCode, '') AS AssignmentCode,
                        COALESCE(Reason, '') AS Reason
                    FROM HaidaiMovements
                    WHERE OperatorCodigoFJ IN @OperatorCodes
                      AND date(ScheduleDate) BETWEEN date(@StartDate) AND date(@EndDate)
                    ORDER BY date(ScheduleDate) DESC, COALESCE(EventTime, '') DESC, Id DESC;",
                new
                {
                    OperatorCodes = operatorCodes,
                    StartDate = startDate.ToString("yyyy-MM-dd"),
                    EndDate = endDate.ToString("yyyy-MM-dd")
                })
                .ToList();

            var latestMovementsByOperator = movementRows
                .GroupBy(item => item.OperatorCodigoFJ, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyDictionary<string, OperatorManagerMovementRow>)group
                        .GroupBy(item => item.Day, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(dayGroup => dayGroup.Key, dayGroup => dayGroup.First(), StringComparer.OrdinalIgnoreCase),
                    StringComparer.OrdinalIgnoreCase);

            var followUpCountByOperator = conn.Query<OperatorManagerFollowUpCountRow>(
                @"
                    SELECT
                        OperatorCodigoFJ,
                        COUNT(1) AS Count
                    FROM FollowUps
                    WHERE OperatorCodigoFJ IN @OperatorCodes
                      AND date(Date) BETWEEN date(@StartDate) AND date(@EndDate)
                    GROUP BY OperatorCodigoFJ;",
                new
                {
                    OperatorCodes = operatorCodes,
                    StartDate = startDate.ToString("yyyy-MM-dd"),
                    EndDate = endDate.ToString("yyyy-MM-dd")
                })
                .ToDictionary(item => item.OperatorCodigoFJ, item => item.Count, StringComparer.OrdinalIgnoreCase);

            var emptyPresenceMap = new Dictionary<string, OperatorManagerPresenceRow>(StringComparer.OrdinalIgnoreCase);
            var emptyScheduleMap = new Dictionary<string, OperatorManagerScheduleRow>(StringComparer.OrdinalIgnoreCase);
            var emptyTodokeMap = new Dictionary<string, OperatorManagerTodokeRow>(StringComparer.OrdinalIgnoreCase);
            var emptyMovementMap = new Dictionary<string, OperatorManagerMovementRow>(StringComparer.OrdinalIgnoreCase);
            var items = rows
                .Select(row =>
                {
                    latestSchedulesByOperator.TryGetValue(row.CodigoFJ, out var schedulesByDay);
                    latestTodokesByOperator.TryGetValue(row.CodigoFJ, out var todokesByDay);
                    latestMovementsByOperator.TryGetValue(row.CodigoFJ, out var movementsByDay);
                    followUpCountByOperator.TryGetValue(row.CodigoFJ, out var followUpCount);

                    schedulesByDay ??= emptyScheduleMap;
                    todokesByDay ??= emptyTodokeMap;
                    movementsByDay ??= emptyMovementMap;

                    var attendanceByDay = BuildAttendanceDaySummaries(
                        schedulesByDay,
                        emptyPresenceMap,
                        todokesByDay,
                        movementsByDay);

                    var metrics = BuildPresenceMetrics(
                        attendanceByDay.Values,
                        emptyPresenceMap,
                        todokesByDay.Values,
                        followUpCount);

                    return new OperatorManagerDirectoryItem(
                        row.CodigoFJ,
                        row.Name,
                        row.NameJp,
                        row.ShiftId,
                        row.ShiftName,
                        row.SectorId,
                        row.SectorName,
                        row.GroupId,
                        row.GroupName,
                        row.Trainer,
                        row.IsLeader,
                        metrics.ScheduledDays,
                        metrics.PresentDays,
                        metrics.YukyuDays,
                        metrics.FaltaDays,
                        metrics.LateDays,
                        metrics.EarlyLeaveDays,
                        metrics.FollowUpCount,
                        metrics.PendingTodokeCount,
                        metrics.PresencePercent,
                        metrics.CoveragePercent);
                })
                .ToList();

            return new OperatorManagerDirectoryPayload(
                startDate.ToString("yyyy-MM-dd"),
                endDate.ToString("yyyy-MM-dd"),
                items);
        }

        public OperatorPresenceReportPayload GetPresenceReport(OperatorPresenceReportFilter filter)
        {
            new HaidaiModuleService(_factory).EnsureSchema();
            using var conn = _factory.CreateOpenConnection();

            var startDate = ParseDateOrDefault(filter.StartDateIso, DateTime.Today.AddDays(-29));
            var endDate = ParseDateOrDefault(filter.EndDateIso, DateTime.Today);
            if (endDate < startDate)
            {
                (startDate, endDate) = (endDate, startDate);
            }

            var search = (filter.Search ?? string.Empty).Trim();
            DemandSector(filter.SectorId);
            var statusFilter = (filter.Status ?? string.Empty).Trim().ToLowerInvariant();
            var loadPresenceWatch = new Stopwatch();
            var loadHaidaiWatch = new Stopwatch();
            var buildOvertimeWatch = new Stopwatch();
            var buildProjectionWatch = new Stopwatch();
            var buildRankingWatch = new Stopwatch();
            var currentMonthStart = new DateTime(endDate.Year, endDate.Month, 1);
            var currentMonthEnd = currentMonthStart.AddMonths(1).AddDays(-1);
            var previousMonthStart = currentMonthStart.AddMonths(-1);
            var previousMonthEnd = currentMonthStart.AddDays(-1);
            var overtimeStartDate = previousMonthStart < startDate ? previousMonthStart : startDate;
            var overtimeEndDate = currentMonthEnd > endDate ? currentMonthEnd : endDate;

            var operators = conn.Query<OperatorManagerDirectoryRow>(
                @"
                    SELECT
                        o.CodigoFJ,
                        COALESCE(NULLIF(o.NameRomanji, ''), o.CodigoFJ) AS Name,
                        COALESCE(NULLIF(o.NameNihongo, ''), COALESCE(NULLIF(o.NameRomanji, ''), o.CodigoFJ)) AS NameJp,
                        o.ShiftId,
                        COALESCE(NULLIF(s.NamePt, ''), NULLIF(s.NameJp, ''), 'Turno ' || o.ShiftId) AS ShiftName,
                        o.SectorId,
                        COALESCE(NULLIF(sec.NamePt, ''), NULLIF(sec.NameJp, ''), 'Setor ' || o.SectorId) AS SectorName,
                        o.GroupId,
                        COALESCE(NULLIF(g.NamePt, ''), NULLIF(g.NameJp, ''), 'Grupo ' || o.GroupId) AS GroupName,
                        COALESCE(o.Trainer, 0) AS Trainer,
                        COALESCE(o.IsLeader, 0) AS IsLeader
                    FROM Operators o
                    LEFT JOIN Shifts s ON s.Id = o.ShiftId
                    LEFT JOIN Sectors sec ON sec.Id = o.SectorId
                    LEFT JOIN Groups g ON g.Id = o.GroupId
                    WHERE COALESCE(o.Status, 1) = 1
                      AND o.SectorId IN @AuthorizedSectorIds
                      AND (@ShiftId <= 0 OR o.ShiftId = @ShiftId)
                      AND (@SectorId <= 0 OR o.SectorId = @SectorId)
                      AND (@GroupId <= 0 OR o.GroupId = @GroupId)
                      AND (
                            @Search = ''
                         OR o.CodigoFJ LIKE '%' || @Search || '%'
                         OR COALESCE(o.NameRomanji, '') LIKE '%' || @Search || '%'
                         OR COALESCE(o.NameNihongo, '') LIKE '%' || @Search || '%'
                      )
                    ORDER BY o.NameRomanji, o.CodigoFJ;",
                new
                {
                    filter.ShiftId,
                    filter.SectorId,
                    filter.GroupId,
                    Search = search,
                    AuthorizedSectorIds = _authorizedSectorIds
                })
                .ToList();

            if (operators.Count == 0)
            {
                return new OperatorPresenceReportPayload(
                    startDate.ToString("yyyy-MM-dd"),
                    endDate.ToString("yyyy-MM-dd"),
                    new OperatorPresenceReportSummary(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
                    Array.Empty<OperatorPresenceOvertimeRankingItem>(),
                    Array.Empty<OperatorPresenceAbsenceRankingItem>(),
                    new OperatorPresenceReportPerformance(0, 0, 0, 0, 0),
                    Array.Empty<OperatorPresenceReportRow>());
            }

            var operatorCodes = operators
                .Select(item => item.CodigoFJ)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            loadHaidaiWatch.Start();
            var scheduleRows = conn.Query<OperatorManagerScheduleRow>(
                @"
                    SELECT
                        ha.OperatorCodigoFJ,
                        date(ha.ScheduleDate) AS Day,
                        ha.ShiftId,
                        ha.LocalId,
                        COALESCE(NULLIF(l.NamePt, ''), NULLIF(l.NameJp, ''), '') AS LocalName,
                        COALESCE(ha.AssignmentCode, '') AS AssignmentCode,
                        COALESCE(ha.AvailabilityStatus, '') AS AvailabilityStatus,
                        COALESCE(ha.IsLineupActive, 1) AS IsLineupActive,
                        COALESCE(ha.IsHolidayWork, 0) AS IsHolidayWork,
                        COALESCE(ha.IsTrainee, 0) AS IsTrainee
                    FROM HaidaiAssignments ha
                    LEFT JOIN Locals l ON l.Id = ha.LocalId
                    WHERE ha.OperatorCodigoFJ IN @OperatorCodes
                      AND date(ha.ScheduleDate) BETWEEN date(@StartDate) AND date(@EndDate)
                    ORDER BY date(ha.ScheduleDate) DESC, ha.Id DESC;",
                new
                {
                    OperatorCodes = operatorCodes,
                    StartDate = startDate.ToString("yyyy-MM-dd"),
                    EndDate = endDate.ToString("yyyy-MM-dd")
                })
                .ToList();
            loadHaidaiWatch.Stop();

            loadHaidaiWatch.Start();
            var overtimeScheduleRows = conn.Query<OperatorManagerScheduleRow>(
                @"
                    SELECT
                        ha.OperatorCodigoFJ,
                        date(ha.ScheduleDate) AS Day,
                        ha.ShiftId,
                        ha.LocalId,
                        COALESCE(NULLIF(l.NamePt, ''), NULLIF(l.NameJp, ''), '') AS LocalName,
                        COALESCE(ha.AssignmentCode, '') AS AssignmentCode,
                        COALESCE(ha.AvailabilityStatus, '') AS AvailabilityStatus,
                        COALESCE(ha.IsLineupActive, 1) AS IsLineupActive,
                        COALESCE(ha.IsHolidayWork, 0) AS IsHolidayWork,
                        COALESCE(ha.IsTrainee, 0) AS IsTrainee
                    FROM HaidaiAssignments ha
                    LEFT JOIN Locals l ON l.Id = ha.LocalId
                    WHERE ha.OperatorCodigoFJ IN @OperatorCodes
                      AND date(ha.ScheduleDate) BETWEEN date(@StartDate) AND date(@EndDate)
                    ORDER BY date(ha.ScheduleDate) DESC, ha.Id DESC;",
                new
                {
                    OperatorCodes = operatorCodes,
                    StartDate = overtimeStartDate.ToString("yyyy-MM-dd"),
                    EndDate = overtimeEndDate.ToString("yyyy-MM-dd")
                })
                .ToList();
            loadHaidaiWatch.Stop();

            loadPresenceWatch.Start();
            var overtimePresenceRows = conn.Query<OperatorManagerPresenceEventRow>(
                @"
                    SELECT
                        CodigoFJ AS OperatorCodigoFJ,
                        Date AS PresenceDateTime
                    FROM OperatorPresence
                    WHERE CodigoFJ IN @OperatorCodes
                      AND Date >= @StartDateTime
                      AND Date < @EndExclusive
                    ORDER BY CodigoFJ, Date;",
                new
                {
                    OperatorCodes = operatorCodes,
                    StartDateTime = overtimeStartDate.Date,
                    EndExclusive = overtimeEndDate.Date.AddDays(2)
                })
                .ToList();
            loadPresenceWatch.Stop();

            var overtimeTodokeRows = conn.Query<OperatorManagerTodokeRow>(
                @"
                    SELECT
                        a.OperatorCodigoFJ,
                        date(a.RequestDate) AS Day,
                        a.Id,
                        a.TodokeMotivoId AS MotiveId,
                        COALESCE(m.NomePt, '') AS MotiveName,
                        COALESCE(a.Notes, '') AS Notes,
                        CASE
                            WHEN EXISTS (
                                SELECT 1
                                FROM YukyuTodoke y
                                WHERE y.AcompYukyuId = a.Id
                            ) THEN 1
                            ELSE 0
                        END AS Validated
                    FROM AcompYukyu a
                    LEFT JOIN TodokeMotivo m ON m.Id = a.TodokeMotivoId
                    WHERE a.OperatorCodigoFJ IN @OperatorCodes
                      AND date(a.RequestDate) BETWEEN date(@StartDate) AND date(@EndDate)
                    ORDER BY date(a.RequestDate) DESC, a.Id DESC;",
                new
                {
                    OperatorCodes = operatorCodes,
                    StartDate = overtimeStartDate.ToString("yyyy-MM-dd"),
                    EndDate = overtimeEndDate.ToString("yyyy-MM-dd")
                })
                .ToList();

            var overtimeMovementRows = conn.Query<OperatorManagerMovementRow>(
                @"
                    SELECT
                        OperatorCodigoFJ,
                        date(ScheduleDate) AS Day,
                        COALESCE(MovementType, '') AS MovementType,
                        COALESCE(EventTime, '') AS EventTime,
                        COALESCE(EventDateTime, '') AS EventDateTime,
                        COALESCE(AssignmentCode, '') AS AssignmentCode,
                        COALESCE(Reason, '') AS Reason
                    FROM HaidaiMovements
                    WHERE OperatorCodigoFJ IN @OperatorCodes
                      AND date(ScheduleDate) BETWEEN date(@StartDate) AND date(@EndDate)
                    ORDER BY date(ScheduleDate) DESC, COALESCE(EventTime, '') DESC, Id DESC;",
                new
                {
                    OperatorCodes = operatorCodes,
                    StartDate = overtimeStartDate.ToString("yyyy-MM-dd"),
                    EndDate = overtimeEndDate.ToString("yyyy-MM-dd")
                })
                .ToList();

            loadPresenceWatch.Start();
            var presenceRows = conn.Query<OperatorPresenceReportPresenceRow>(
                @"
                    SELECT
                        CodigoFJ AS OperatorCodigoFJ,
                        date(Date) AS Day,
                        MAX(Date) AS LastPresence
                    FROM OperatorPresence
                    WHERE CodigoFJ IN @OperatorCodes
                      AND date(Date) BETWEEN date(@StartDate) AND date(@EndDate)
                    GROUP BY CodigoFJ, date(Date);",
                new
                {
                    OperatorCodes = operatorCodes,
                    StartDate = startDate.ToString("yyyy-MM-dd"),
                    EndDate = endDate.ToString("yyyy-MM-dd")
                })
                .ToList();
            loadPresenceWatch.Stop();

            var todokeRows = conn.Query<OperatorManagerTodokeRow>(
                @"
                    SELECT
                        a.OperatorCodigoFJ,
                        date(a.RequestDate) AS Day,
                        a.Id,
                        a.TodokeMotivoId AS MotiveId,
                        COALESCE(m.NomePt, '') AS MotiveName,
                        COALESCE(a.Notes, '') AS Notes,
                        CASE
                            WHEN EXISTS (
                                SELECT 1
                                FROM YukyuTodoke y
                                WHERE y.AcompYukyuId = a.Id
                            ) THEN 1
                            ELSE 0
                        END AS Validated
                    FROM AcompYukyu a
                    LEFT JOIN TodokeMotivo m ON m.Id = a.TodokeMotivoId
                    WHERE a.OperatorCodigoFJ IN @OperatorCodes
                      AND date(a.RequestDate) BETWEEN date(@StartDate) AND date(@EndDate)
                    ORDER BY date(a.RequestDate) DESC, a.Id DESC;",
                new
                {
                    OperatorCodes = operatorCodes,
                    StartDate = startDate.ToString("yyyy-MM-dd"),
                    EndDate = endDate.ToString("yyyy-MM-dd")
                })
                .ToList();

            var movementRows = conn.Query<OperatorManagerMovementRow>(
                @"
                    SELECT
                        OperatorCodigoFJ,
                        date(ScheduleDate) AS Day,
                        COALESCE(MovementType, '') AS MovementType,
                        COALESCE(EventTime, '') AS EventTime,
                        COALESCE(EventDateTime, '') AS EventDateTime,
                        COALESCE(AssignmentCode, '') AS AssignmentCode,
                        COALESCE(Reason, '') AS Reason
                    FROM HaidaiMovements
                    WHERE OperatorCodigoFJ IN @OperatorCodes
                      AND date(ScheduleDate) BETWEEN date(@StartDate) AND date(@EndDate)
                    ORDER BY date(ScheduleDate) DESC, COALESCE(EventTime, '') DESC, Id DESC;",
                new
                {
                    OperatorCodes = operatorCodes,
                    StartDate = startDate.ToString("yyyy-MM-dd"),
                    EndDate = endDate.ToString("yyyy-MM-dd")
                })
                .ToList();

            var schedulesByOperator = scheduleRows
                .GroupBy(item => item.OperatorCodigoFJ, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => (IReadOnlyDictionary<string, OperatorManagerScheduleRow>)group
                    .GroupBy(item => item.Day, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(dayGroup => dayGroup.Key, dayGroup => dayGroup.First(), StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);

            var presencesByOperator = presenceRows
                .GroupBy(item => item.OperatorCodigoFJ, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => (IReadOnlyDictionary<string, OperatorManagerPresenceRow>)group
                    .ToDictionary(item => item.Day, item => new OperatorManagerPresenceRow { Day = item.Day, LastPresence = item.LastPresence }, StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);

            var todokesByOperator = todokeRows
                .GroupBy(item => item.OperatorCodigoFJ, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => (IReadOnlyDictionary<string, OperatorManagerTodokeRow>)group
                    .GroupBy(item => item.Day, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(dayGroup => dayGroup.Key, dayGroup => dayGroup.First(), StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);

            var movementsByOperator = movementRows
                .GroupBy(item => item.OperatorCodigoFJ, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => (IReadOnlyDictionary<string, OperatorManagerMovementRow>)group
                    .GroupBy(item => item.Day, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(dayGroup => dayGroup.Key, dayGroup => dayGroup.First(), StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);

            var overtimeSchedulesByOperator = overtimeScheduleRows
                .GroupBy(item => item.OperatorCodigoFJ, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => (IReadOnlyDictionary<string, OperatorManagerScheduleRow>)group
                    .GroupBy(item => item.Day, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(dayGroup => dayGroup.Key, dayGroup => dayGroup.First(), StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);

            var overtimePresencesByOperator = overtimePresenceRows
                .GroupBy(item => item.OperatorCodigoFJ, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => (IReadOnlyList<OperatorManagerPresenceEventRow>)group
                    .OrderBy(item => item.PresenceDateTime)
                    .ToList(), StringComparer.OrdinalIgnoreCase);

            var overtimeTodokesByOperator = overtimeTodokeRows
                .GroupBy(item => item.OperatorCodigoFJ, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => (IReadOnlyDictionary<string, OperatorManagerTodokeRow>)group
                    .GroupBy(item => item.Day, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(dayGroup => dayGroup.Key, dayGroup => dayGroup.First(), StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);

            var overtimeMovementsByOperator = overtimeMovementRows
                .GroupBy(item => item.OperatorCodigoFJ, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => (IReadOnlyDictionary<string, OperatorManagerMovementRow>)group
                    .GroupBy(item => item.Day, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(dayGroup => dayGroup.Key, dayGroup => dayGroup.First(), StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);

            var emptyScheduleMap = new Dictionary<string, OperatorManagerScheduleRow>(StringComparer.OrdinalIgnoreCase);
            var emptyPresenceMap = new Dictionary<string, OperatorManagerPresenceRow>(StringComparer.OrdinalIgnoreCase);
            var emptyTodokeMap = new Dictionary<string, OperatorManagerTodokeRow>(StringComparer.OrdinalIgnoreCase);
            var emptyMovementMap = new Dictionary<string, OperatorManagerMovementRow>(StringComparer.OrdinalIgnoreCase);

            var rows = new List<OperatorPresenceReportRow>();
            buildOvertimeWatch.Start();
            buildProjectionWatch.Start();
            foreach (var op in operators)
            {
                schedulesByOperator.TryGetValue(op.CodigoFJ, out var scheduleMap);
                presencesByOperator.TryGetValue(op.CodigoFJ, out var presenceMap);
                todokesByOperator.TryGetValue(op.CodigoFJ, out var todokeMap);
                movementsByOperator.TryGetValue(op.CodigoFJ, out var movementMap);

                scheduleMap ??= emptyScheduleMap;
                presenceMap ??= emptyPresenceMap;
                todokeMap ??= emptyTodokeMap;
                movementMap ??= emptyMovementMap;
                overtimeSchedulesByOperator.TryGetValue(op.CodigoFJ, out var overtimeScheduleMap);
                overtimePresencesByOperator.TryGetValue(op.CodigoFJ, out var overtimePresenceEvents);
                overtimeTodokesByOperator.TryGetValue(op.CodigoFJ, out var overtimeTodokeMap);
                overtimeMovementsByOperator.TryGetValue(op.CodigoFJ, out var overtimeMovementMap);
                overtimeScheduleMap ??= emptyScheduleMap;
                overtimePresenceEvents ??= Array.Empty<OperatorManagerPresenceEventRow>();
                overtimeTodokeMap ??= emptyTodokeMap;
                overtimeMovementMap ??= emptyMovementMap;

                var attendanceDays = BuildAttendanceDaySummaries(scheduleMap, presenceMap, todokeMap, movementMap);
                var metrics = BuildPresenceMetrics(attendanceDays.Values, presenceMap, todokeMap.Values, 0);
                var overtime = BuildOvertimeMetrics(
                    op,
                    overtimeScheduleMap,
                    overtimePresenceEvents,
                    overtimeTodokeMap,
                    overtimeMovementMap,
                    previousMonthStart,
                    previousMonthEnd,
                    currentMonthStart,
                    currentMonthEnd,
                    DateTime.Today);
                var latest = attendanceDays.Values
                    .OrderByDescending(item => item.Day, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();

                var row = new OperatorPresenceReportRow(
                    op.CodigoFJ,
                    op.Name,
                    op.NameJp,
                    op.ShiftName,
                    op.SectorName,
                    op.GroupName,
                    metrics.ScheduledDays,
                    metrics.PresentDays,
                    metrics.YukyuDays,
                    metrics.FaltaDays,
                    metrics.LateDays,
                    metrics.EarlyLeaveDays,
                    metrics.PendingTodokeCount,
                    metrics.PresencePercent,
                    metrics.ScheduledDaysWithoutSunday,
                    metrics.ScheduledDaysWithSunday,
                    metrics.AbsencesWithoutSunday,
                    metrics.AbsencesWithSunday,
                    metrics.PresencePercentWithoutSunday,
                    metrics.PresencePercentWithSunday,
                    metrics.ScheduledDaysWithoutSunday > 0 && metrics.PresencePercentWithoutSunday < 82d,
                    metrics.ScheduledDaysWithSunday > 0 && metrics.PresencePercentWithSunday < 82d,
                    overtime.CurrentMonthOvertimeHours,
                    overtime.PreviousMonthOvertimeHours,
                    overtime.RealizedOvertimeHours,
                    overtime.ProjectedRemainingOvertimeHours,
                    overtime.ProjectedFinalOvertimeHours,
                    overtime.WorkedSundays,
                    overtime.HolidayWorkDays,
                    overtime.HolidayWorkHours,
                    overtime.TotalOvertimeHours,
                    overtime.DomingoShukkinTotalHours,
                    overtime.OvertimeLimitDifferenceHours,
                    overtime.OvertimeRiskLevel,
                    overtime.TotalOvertimeRiskLevel,
                    overtime.OvertimePlusSundaysLabel,
                    latest?.DisplayStatus ?? "Sem registro",
                    latest?.Day ?? string.Empty,
                    latest?.Area ?? string.Empty);

                LogPresenceReportDiagnostic(
                    row,
                    op.CodigoFJ,
                    op.Name,
                    op.ShiftId,
                    op.ShiftName,
                    startDate,
                    endDate);

                if (MatchesPresenceStatusFilter(row, statusFilter))
                {
                    rows.Add(row);
                }
            }
            buildProjectionWatch.Stop();
            buildOvertimeWatch.Stop();

            var summary = new OperatorPresenceReportSummary(
                rows.Count,
                rows.Sum(item => item.ScheduledDays),
                rows.Sum(item => item.PresentDays),
                rows.Sum(item => item.YukyuDays),
                rows.Sum(item => item.FaltaDays),
                rows.Sum(item => item.LateDays),
                rows.Sum(item => item.EarlyLeaveDays),
                rows.Count == 0 ? 0 : Math.Round(rows.Average(item => item.PresencePercent), 1),
                rows.Sum(item => item.ScheduledDaysWithoutSunday),
                rows.Sum(item => item.ScheduledDaysWithSunday),
                rows.Sum(item => item.AbsencesWithoutSunday),
                rows.Sum(item => item.AbsencesWithSunday),
                rows.Count == 0 ? 0 : Math.Round(rows.Average(item => item.PresencePercentWithoutSunday), 1),
                rows.Count == 0 ? 0 : Math.Round(rows.Average(item => item.PresencePercentWithSunday), 1),
                rows.Count(item => item.ProjectedFinalOvertimeHours > 45d),
                rows.Count(item => item.ProjectedFinalOvertimeHours >= 35d && item.ProjectedFinalOvertimeHours <= 45d),
                rows.Count(item => item.ProjectedFinalOvertimeHours < 35d),
                Math.Round(rows.Sum(item => item.CurrentMonthOvertimeHours), 1),
                rows.Sum(item => item.WorkedSundays),
                rows.Sum(item => item.HolidayWorkDays));
            LogSundayWorkedSummary(startDate, endDate, summary.TotalWorkedSundays);

            buildRankingWatch.Start();
            var topOvertime = rows
                .OrderByDescending(item => item.ProjectedFinalOvertimeHours)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .Take(10)
                .Select((item, index) => new OperatorPresenceOvertimeRankingItem(
                    index + 1,
                    item.CodigoFJ,
                    item.Name,
                    item.NameJp,
                    item.ShiftName,
                    item.ProjectedFinalOvertimeHours,
                    item.HolidayWorkDays))
                .ToList();
            var topAbsences = rows
                .Select(item => new
                {
                    Row = item,
                    TotalOccurrences = item.FaltaDays + item.LateDays + item.EarlyLeaveDays + item.YukyuDays
                })
                .Where(item => item.TotalOccurrences > 0)
                .OrderByDescending(item => item.TotalOccurrences)
                .ThenBy(item => item.Row.Name, StringComparer.OrdinalIgnoreCase)
                .Take(10)
                .Select((item, index) => new OperatorPresenceAbsenceRankingItem(
                    index + 1,
                    item.Row.CodigoFJ,
                    item.Row.Name,
                    item.Row.NameJp,
                    item.Row.ShiftName,
                    item.Row.FaltaDays,
                    item.Row.LateDays,
                    item.Row.EarlyLeaveDays,
                    item.Row.YukyuDays,
                    item.TotalOccurrences))
                .ToList();
            foreach (var item in topAbsences)
            {
                LogAbsenceRankingAudit(item);
            }
            buildRankingWatch.Stop();

            var performance = new OperatorPresenceReportPerformance(
                LoadPresenceMs: loadPresenceWatch.ElapsedMilliseconds,
                LoadHaidaiMs: loadHaidaiWatch.ElapsedMilliseconds,
                BuildOvertimeMs: buildOvertimeWatch.ElapsedMilliseconds,
                BuildProjectionMs: buildProjectionWatch.ElapsedMilliseconds,
                BuildRankingMs: buildRankingWatch.ElapsedMilliseconds);

            return new OperatorPresenceReportPayload(
                startDate.ToString("yyyy-MM-dd"),
                endDate.ToString("yyyy-MM-dd"),
                summary,
                topOvertime,
                topAbsences,
                performance,
                rows
                    .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList());
        }

        public OperatorManagerReportPayload GetReport(string codigoFJ, int periodDays)
        {
            if (string.IsNullOrWhiteSpace(codigoFJ))
            {
                throw new InvalidOperationException("Operador invalido para o relatorio gerencial.");
            }

            using var conn = _factory.CreateOpenConnection();
            MasterCardModuleService.EnsureSchema(conn);

            var operatorInfo = conn.QuerySingleOrDefault<OperatorManagerOperatorInfo>(
                @"
                    SELECT
                        o.CodigoFJ,
                        COALESCE(NULLIF(o.NameRomanji, ''), o.CodigoFJ) AS Name,
                        COALESCE(NULLIF(o.NameNihongo, ''), COALESCE(NULLIF(o.NameRomanji, ''), o.CodigoFJ)) AS NameJp,
                        o.ShiftId,
                        COALESCE(NULLIF(s.NamePt, ''), NULLIF(s.NameJp, ''), 'Turno ' || o.ShiftId) AS ShiftName,
                        o.SectorId,
                        COALESCE(NULLIF(sec.NamePt, ''), NULLIF(sec.NameJp, ''), 'Setor ' || o.SectorId) AS SectorName,
                        o.GroupId,
                        COALESCE(NULLIF(g.NamePt, ''), NULLIF(g.NameJp, ''), 'Grupo ' || o.GroupId) AS GroupName,
                        o.StartDate,
                        COALESCE(o.Trainer, 0) AS Trainer,
                        COALESCE(o.IsLeader, 0) AS IsLeader
                    FROM Operators o
                    LEFT JOIN Shifts s ON s.Id = o.ShiftId
                    LEFT JOIN Sectors sec ON sec.Id = o.SectorId
                    LEFT JOIN Groups g ON g.Id = o.GroupId
                    WHERE o.CodigoFJ = @CodigoFJ
                      AND o.SectorId IN @AuthorizedSectorIds;",
                new
                {
                    CodigoFJ = codigoFJ.Trim(),
                    AuthorizedSectorIds = _authorizedSectorIds
                });

            if (operatorInfo == null)
            {
                throw new UnauthorizedAccessException("Operador inexistente ou fora do escopo autorizado.");
            }

            var startDate = DateTime.Today.AddDays(-(Math.Max(1, periodDays) - 1));
            var endDate = DateTime.Today;

            var scheduledRows = conn.Query<OperatorManagerScheduleRow>(
                @"
                    SELECT
                        date(ha.ScheduleDate) AS Day,
                        ha.ShiftId,
                        ha.LocalId,
                        COALESCE(NULLIF(l.NamePt, ''), NULLIF(l.NameJp, ''), '') AS LocalName,
                        COALESCE(ha.AssignmentCode, '') AS AssignmentCode,
                        COALESCE(ha.AvailabilityStatus, '') AS AvailabilityStatus,
                        COALESCE(ha.IsLineupActive, 1) AS IsLineupActive,
                        COALESCE(ha.IsTrainee, 0) AS IsTrainee
                    FROM HaidaiAssignments ha
                    LEFT JOIN Locals l ON l.Id = ha.LocalId
                    WHERE ha.OperatorCodigoFJ = @CodigoFJ
                      AND date(ha.ScheduleDate) BETWEEN date(@StartDate) AND date(@EndDate)
                    ORDER BY date(ha.ScheduleDate) DESC, ha.Id DESC;",
                new
                {
                    CodigoFJ = codigoFJ.Trim(),
                    StartDate = startDate.ToString("yyyy-MM-dd"),
                    EndDate = endDate.ToString("yyyy-MM-dd")
                })
                .ToList();

            var latestScheduleByDay = scheduledRows
                .GroupBy(item => item.Day)
                .ToDictionary(group => group.Key, group => group.First());

            var presentRows = conn.Query<OperatorManagerPresenceRow>(
                @"
                    SELECT
                        date(Date) AS Day,
                        MAX(Date) AS LastPresence
                    FROM OperatorPresence
                    WHERE CodigoFJ = @CodigoFJ
                      AND date(Date) BETWEEN date(@StartDate) AND date(@EndDate)
                    GROUP BY date(Date);",
                new
                {
                    CodigoFJ = codigoFJ.Trim(),
                    StartDate = startDate.ToString("yyyy-MM-dd"),
                    EndDate = endDate.ToString("yyyy-MM-dd")
                })
                .ToDictionary(item => item.Day, item => item);

            var todokeRows = conn.Query<OperatorManagerTodokeRow>(
                @"
                    SELECT
                        date(a.RequestDate) AS Day,
                        a.Id,
                        a.TodokeMotivoId AS MotiveId,
                        COALESCE(m.NomePt, '') AS MotiveName,
                        COALESCE(a.Notes, '') AS Notes,
                        CASE
                            WHEN EXISTS (
                                SELECT 1
                                FROM YukyuTodoke y
                                WHERE y.AcompYukyuId = a.Id
                            ) THEN 1
                            ELSE 0
                        END AS Validated
                    FROM AcompYukyu a
                    LEFT JOIN TodokeMotivo m ON m.Id = a.TodokeMotivoId
                    WHERE a.OperatorCodigoFJ = @CodigoFJ
                      AND date(a.RequestDate) BETWEEN date(@StartDate) AND date(@EndDate)
                    ORDER BY date(a.RequestDate) DESC, a.Id DESC;",
                new
                {
                    CodigoFJ = codigoFJ.Trim(),
                    StartDate = startDate.ToString("yyyy-MM-dd"),
                    EndDate = endDate.ToString("yyyy-MM-dd")
                })
                .ToList();

            var latestTodokeByDay = todokeRows
                .GroupBy(item => item.Day)
                .ToDictionary(group => group.Key, group => group.First());

            var movementRows = conn.Query<OperatorManagerMovementRow>(
                @"
                    SELECT
                        date(ScheduleDate) AS Day,
                        COALESCE(MovementType, '') AS MovementType,
                        COALESCE(EventTime, '') AS EventTime,
                        COALESCE(EventDateTime, '') AS EventDateTime,
                        COALESCE(AssignmentCode, '') AS AssignmentCode,
                        COALESCE(Reason, '') AS Reason,
                        COALESCE(ReplacementOperatorCodigoFJ, '') AS ReplacementOperatorCodigoFJ
                    FROM HaidaiMovements
                    WHERE (OperatorCodigoFJ = @CodigoFJ
                       OR ReplacementOperatorCodigoFJ = @CodigoFJ)
                       AND date(ScheduleDate) BETWEEN date(@StartDate) AND date(@EndDate)
                    ORDER BY date(ScheduleDate) DESC, COALESCE(EventTime, '') DESC, Id DESC;",
                new
                {
                    CodigoFJ = codigoFJ.Trim(),
                    StartDate = startDate.ToString("yyyy-MM-dd"),
                    EndDate = endDate.ToString("yyyy-MM-dd")
                })
                .ToList();

            var primaryMovementByDay = movementRows
                .Where(item => string.IsNullOrWhiteSpace(item.ReplacementOperatorCodigoFJ))
                .GroupBy(item => item.Day)
                .ToDictionary(group => group.Key, group => group.First());

            var replacementMovementByDay = movementRows
                .Where(item => string.Equals(item.ReplacementOperatorCodigoFJ, codigoFJ.Trim(), StringComparison.OrdinalIgnoreCase))
                .GroupBy(item => item.Day)
                .ToDictionary(group => group.Key, group => group.First());

            var followUpCount = conn.ExecuteScalar<int>(
                @"
                    SELECT COUNT(1)
                    FROM FollowUps
                    WHERE OperatorCodigoFJ = @CodigoFJ
                      AND date(Date) BETWEEN date(@StartDate) AND date(@EndDate);",
                new
                {
                    CodigoFJ = codigoFJ.Trim(),
                    StartDate = startDate.ToString("yyyy-MM-dd"),
                    EndDate = endDate.ToString("yyyy-MM-dd")
                });

            var recentFollowUps = conn.Query<OperatorManagerFollowUpRow>(
                @"
                    SELECT
                        f.Id,
                        f.Date,
                        COALESCE(NULLIF(s.NamePt, ''), NULLIF(s.NameJp, ''), '') AS ShiftName,
                        COALESCE(NULLIF(r.NamePt, ''), '') AS ReasonName,
                        COALESCE(NULLIF(t.NamePt, ''), '') AS TypeName,
                        COALESCE(NULLIF(l.NamePt, ''), NULLIF(l.NameJp, ''), '') AS LocalName,
                        COALESCE(NULLIF(e.NamePt, ''), NULLIF(e.NameJp, ''), '') AS EquipmentName,
                        COALESCE(f.Description, '') AS Description,
                        COALESCE(f.Guidance, '') AS Guidance
                    FROM FollowUps f
                    LEFT JOIN Shifts s ON s.Id = f.ShiftId
                    LEFT JOIN FollowUpReasons r ON r.Id = f.ReasonId
                    LEFT JOIN FollowUpTypes t ON t.Id = f.TypeId
                    LEFT JOIN Locals l ON l.Id = f.LocalId
                    LEFT JOIN Equipments e ON e.Id = f.EquipmentId
                    WHERE f.OperatorCodigoFJ = @CodigoFJ
                      AND date(f.Date) BETWEEN date(@StartDate) AND date(@EndDate)
                    ORDER BY datetime(f.Date) DESC
                    LIMIT 12;",
                new
                {
                    CodigoFJ = codigoFJ.Trim(),
                    StartDate = startDate.ToString("yyyy-MM-dd"),
                    EndDate = endDate.ToString("yyyy-MM-dd")
                })
                .ToList();

            var masterCardRows = conn.Query<OperatorManagerMasterCardRow>(
                @"
                    SELECT
                        m.Id,
                        COALESCE(NULLIF(eq.NamePt, ''), NULLIF(eq.NameJp, ''), '') AS EquipmentName,
                        COALESCE(NULLIF(sec.NamePt, ''), NULLIF(sec.NameJp, ''), '') AS SectorName,
                        m.Status,
                        substr(m.StartDate, 1, 10) AS StartDate,
                        CASE WHEN m.ConcludedAt IS NULL THEN '' ELSE substr(m.ConcludedAt, 1, 16) END AS ConcludedAt,
                        CASE WHEN m.FollowDate IS NULL THEN '' ELSE substr(m.FollowDate, 1, 10) END AS FollowDate,
                        CASE WHEN m.FinalizedAt IS NULL THEN '' ELSE substr(m.FinalizedAt, 1, 16) END AS FinalizedAt,
                        COALESCE(m.Notes, '') AS Notes
                    FROM MasterCards m
                    LEFT JOIN Equipments eq ON eq.Id = m.EquipmentId
                    LEFT JOIN Sectors sec ON sec.Id = m.SectorId
                    WHERE m.OperatorCodigoFJ = @CodigoFJ
                      AND date(m.StartDate) BETWEEN date(@StartDate) AND date(@EndDate)
                    ORDER BY
                        CASE m.Status
                            WHEN 'follow' THEN 0
                            WHEN 'in_progress' THEN 1
                            ELSE 2
                        END,
                        date(COALESCE(m.FollowDate, m.StartDate)) ASC,
                        m.Id DESC;",
                new
                {
                    CodigoFJ = codigoFJ.Trim(),
                    StartDate = startDate.ToString("yyyy-MM-dd"),
                    EndDate = endDate.ToString("yyyy-MM-dd")
                })
                .ToList();

            var attendanceByDay = BuildAttendanceDaySummaries(latestScheduleByDay, presentRows, latestTodokeByDay, primaryMovementByDay);
            var metrics = BuildPresenceMetrics(attendanceByDay.Values, presentRows, latestTodokeByDay.Values, followUpCount);
            var production = BuildProductionSummary(
                operatorInfo,
                scheduledRows,
                presentRows.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase),
                primaryMovementByDay,
                replacementMovementByDay,
                startDate,
                endDate);
            var masterCards = BuildMasterCardSummary(masterCardRows);

            var dailyHistory = BuildDailyHistory(attendanceByDay.Values, presentRows, latestTodokeByDay, primaryMovementByDay);

            return new OperatorManagerReportPayload(
                operatorInfo.CodigoFJ,
                operatorInfo.Name,
                operatorInfo.NameJp,
                operatorInfo.ShiftName,
                operatorInfo.SectorName,
                operatorInfo.GroupName,
                operatorInfo.StartDate.ToString("yyyy-MM-dd"),
                operatorInfo.Trainer,
                operatorInfo.IsLeader,
                metrics,
                masterCards,
                production,
                recentFollowUps
                    .Select(item => new OperatorManagerFollowUpItem(
                        item.Id,
                        item.Date.ToString("yyyy-MM-dd HH:mm"),
                        item.ShiftName,
                        item.ReasonName,
                        item.TypeName,
                        item.LocalName,
                        item.EquipmentName,
                        item.Description,
                        item.Guidance))
                    .ToList(),
                dailyHistory);
        }

        private void DemandSector(int sectorId)
        {
            if (sectorId > 0 && !_authorizedSectorIds.Contains(sectorId))
                throw new UnauthorizedAccessException("Setor fora do escopo autorizado.");
        }

        private OperatorManagerMasterCardSummary BuildMasterCardSummary(IReadOnlyList<OperatorManagerMasterCardRow> rows)
        {
            var today = DateTime.Today;
            var inProgressCount = rows.Count(item => item.Status == "in_progress");
            var followCount = rows.Count(item => item.Status == "follow");
            var completedCount = rows.Count(item => item.Status == "completed");
            var overdueFollowCount = rows.Count(item =>
                item.Status == "follow"
                && DateTime.TryParse(item.FollowDate, out var followDate)
                && followDate.Date < today);
            var dueSoonFollowCount = rows.Count(item =>
                item.Status == "follow"
                && DateTime.TryParse(item.FollowDate, out var followDate)
                && followDate.Date >= today
                && followDate.Date <= today.AddDays(7));

            return new OperatorManagerMasterCardSummary(
                rows.Count,
                inProgressCount,
                followCount,
                completedCount,
                overdueFollowCount,
                dueSoonFollowCount,
                rows
                    .Take(12)
                    .Select(item => new OperatorManagerMasterCardItem(
                        item.Id,
                        item.EquipmentName,
                        item.SectorName,
                        item.Status,
                        item.StartDate,
                        item.ConcludedAt,
                        item.FollowDate,
                        item.FinalizedAt,
                        item.Notes,
                        ResolveMasterCardFollowState(item)))
                    .ToList());
        }

        private static string ResolveMasterCardFollowState(OperatorManagerMasterCardRow row)
        {
            if (row.Status != "follow")
            {
                return row.Status switch
                {
                    "in_progress" => "in_progress",
                    "completed" => "completed",
                    _ => "none"
                };
            }

            if (!DateTime.TryParse(row.FollowDate, out var followDate))
            {
                return "follow";
            }

            var today = DateTime.Today;
            if (followDate.Date < today)
            {
                return "overdue";
            }

            if (followDate.Date <= today.AddDays(7))
            {
                return "due_soon";
            }

            return "scheduled";
        }

        private OperatorManagerPresenceMetrics BuildPresenceMetrics(
            IEnumerable<OperatorManagerAttendanceDaySummary> attendanceDays,
            IReadOnlyDictionary<string, OperatorManagerPresenceRow> presences,
            IEnumerable<OperatorManagerTodokeRow> todokes,
            int followUpCount)
        {
            var dayList = attendanceDays
                .Where(item => item.CountsAsScheduled)
                .ToList();
            var dayListWithoutSunday = dayList
                .Where(item => !IsSunday(item.Day))
                .ToList();

            var scheduledDays = dayListWithoutSunday.Count;
            var scheduledDaysWithSunday = dayList.Count;
            var effectivePresenceDays = dayListWithoutSunday.Count(item => item.CountsTowardPresence);
            var effectivePresenceDaysWithSunday = dayList.Count(item => item.CountsTowardPresence);
            var coveredDays = dayListWithoutSunday.Count(item => item.CountsTowardCoverage);
            var yukyuDays = dayList.Count(item => item.StatusKey == AttendanceStatusKeys.Yukyu);
            var faltaDays = dayList.Count(item => item.StatusKey == AttendanceStatusKeys.Falta);
            var lateDays = dayList.Count(item => item.StatusKey == AttendanceStatusKeys.Late);
            var earlyLeaveDays = dayList.Count(item => item.StatusKey == AttendanceStatusKeys.EarlyLeave);
            var pendingTodokeCount = todokes.Count(item => !item.Validated);
            var absencesWithoutSunday = Math.Max(0, scheduledDays - effectivePresenceDays);
            var absencesWithSunday = Math.Max(0, scheduledDaysWithSunday - effectivePresenceDaysWithSunday);

            var presencePercent = scheduledDays <= 0
                ? 0
                : Math.Round((effectivePresenceDays / (double)scheduledDays) * 100d, 1);
            var presencePercentWithSunday = scheduledDaysWithSunday <= 0
                ? 0
                : Math.Round((effectivePresenceDaysWithSunday / (double)scheduledDaysWithSunday) * 100d, 1);

            var coveragePercent = scheduledDays <= 0
                ? 0
                : Math.Round((coveredDays / (double)scheduledDays) * 100d, 1);

            return new OperatorManagerPresenceMetrics(
                scheduledDays,
                effectivePresenceDays,
                yukyuDays,
                faltaDays,
                lateDays,
                earlyLeaveDays,
                pendingTodokeCount,
                followUpCount,
                presencePercent,
                coveragePercent,
                scheduledDays,
                scheduledDaysWithSunday,
                absencesWithoutSunday,
                absencesWithSunday,
                presencePercent,
                presencePercentWithSunday);
        }

        private static OperatorPresenceOvertimeMetrics BuildOvertimeMetrics(
            OperatorManagerDirectoryRow op,
            IReadOnlyDictionary<string, OperatorManagerScheduleRow> schedules,
            IReadOnlyList<OperatorManagerPresenceEventRow> presenceEvents,
            IReadOnlyDictionary<string, OperatorManagerTodokeRow> todokes,
            IReadOnlyDictionary<string, OperatorManagerMovementRow> movements,
            DateTime previousMonthStart,
            DateTime previousMonthEnd,
            DateTime currentMonthStart,
            DateTime currentMonthEnd,
            DateTime today)
        {
            var previousMonthMinutes = 0d;
            var currentMonthNormalMinutes = 0d;
            var realizedCurrentNormalMinutes = 0d;
            var projectedRemainingNormalMinutes = 0d;
            var workedSundays = 0;
            var holidayWorkDays = 0;
            var holidayWorkMinutes = 0d;
            var sundayWorkedDays = 0;
            var sundayWorkedMinutes = 0d;

            for (var date = previousMonthStart.Date; date <= currentMonthEnd.Date; date = date.AddDays(1))
            {
                var day = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                schedules.TryGetValue(day, out var schedule);
                todokes.TryGetValue(day, out var todoke);
                movements.TryGetValue(day, out var movement);
                var presenceWindow = ResolvePresenceWindowForOvertime(date, op.ShiftName, presenceEvents);
                var presence = presenceWindow.Presence;
                var hasHaidaiAssignment = IsScheduledDay(schedule);

                var statusKey = ResolveAttendanceStatusKey(schedule, presence, todoke, movement);
                var daily = CalculateDailyOvertime(op, date, schedule, presenceWindow, todoke, movement, statusKey, isProjection: date.Date > today.Date);
                var countedAsSundayWorked = ResolveSundayWorked(
                    date,
                    daily.IsHolidayWork,
                    hasHaidaiAssignment,
                    presence != null,
                    statusKey,
                    out var sundayWorkedReason);

                if (date >= previousMonthStart && date <= previousMonthEnd)
                {
                    previousMonthMinutes += daily.NormalZangyouMinutes;
                }

                if (date >= currentMonthStart && date <= currentMonthEnd)
                {
                    currentMonthNormalMinutes += daily.NormalZangyouMinutes;
                    if (date.Date <= today.Date)
                    {
                        realizedCurrentNormalMinutes += daily.NormalZangyouMinutes;
                    }
                    else
                    {
                        projectedRemainingNormalMinutes += daily.NormalZangyouMinutes;
                    }

                    LogDailyOvertimeAudit(op, date, daily, countedAsSundayWorked ? ToHours(HolidayWorkFixedMinutes) : 0d);
                    if (daily.IsSunday)
                    {
                        LogSundayWorkedAudit(
                            op,
                            date,
                            daily.IsSunday,
                            daily.IsHolidayWork,
                            hasHaidaiAssignment,
                            statusKey,
                            countedAsSundayWorked,
                            sundayWorkedReason);
                    }
                }

                if (date >= currentMonthStart && date <= currentMonthEnd && countedAsSundayWorked)
                {
                    workedSundays++;
                    sundayWorkedDays++;
                    sundayWorkedMinutes += HolidayWorkFixedMinutes;
                }

                if (date >= currentMonthStart && date <= currentMonthEnd && daily.IsHolidayWork)
                {
                    holidayWorkDays++;
                    holidayWorkMinutes += daily.ShukkinMinutes;
                }

                if (daily.HasDiagnostic)
                {
                    var isYakin = OvertimeRuleCalculator.TryResolveShiftWindow(op.ShiftName, date, out var window)
                        && window.ShiftKind == OvertimeShiftKind.Yakin;
                    Console.WriteLine(
                        "[PresenceOvertime][Diagnostic] "
                        + $"OperatorId={op.CodigoFJ} "
                        + $"OperatorName={op.Name} "
                        + $"BaseDate={day} "
                        + $"ShiftId={op.ShiftId} "
                        + $"ShiftName={op.ShiftName} "
                        + $"IsYakin={isYakin} "
                        + $"PresenceRowsFound={presenceWindow.RowsFound} "
                        + $"FirstPresenceTime={FormatDiagnosticDateTime(presenceWindow.FirstPresenceTime)} "
                        + $"LastPresenceTime={FormatDiagnosticDateTime(presenceWindow.LastPresenceTime)} "
                        + $"DayOfWeek={daily.DayOfWeek} "
                        + $"AttendanceStatus={statusKey} "
                        + $"IsSunday={daily.IsSunday} "
                        + $"IsHolidayWork={daily.IsHolidayWork} "
                        + $"ScheduledStart={FormatDiagnosticDateTime(daily.ScheduledStart)} "
                        + $"TeijiEnd={FormatDiagnosticDateTime(daily.TeijiEnd)} "
                        + $"ZangyouStart={FormatDiagnosticDateTime(daily.ZangyouStart)} "
                        + $"ZangyouEnd={FormatDiagnosticDateTime(daily.ZangyouEnd)} "
                        + $"ActualEnd={FormatDiagnosticDateTime(daily.ActualEnd)} "
                        + $"HasLate={daily.IsLate} "
                        + $"HasEarlyLeave={daily.IsEarlyLeave} "
                        + $"NormalZangyouMinutes={daily.NormalZangyouMinutes.ToString("0.#", CultureInfo.InvariantCulture)} "
                        + $"HolidayWorkMinutes={daily.ShukkinMinutes.ToString("0.#", CultureInfo.InvariantCulture)} "
                        + $"TotalOvertimeMinutes={daily.OvertimeMinutes.ToString("0.#", CultureInfo.InvariantCulture)} "
                        + $"Reason={daily.Reason}");
                }
            }

            var currentHours = ToHours(currentMonthNormalMinutes);
            var holidayWorkHours = ToHours(holidayWorkMinutes);
            var sundayWorkedHours = ToHours(sundayWorkedMinutes);
            var zangyouJikanHours = Math.Round(currentHours + holidayWorkHours, 1);
            var domingoShukkinTotalHours = Math.Round(zangyouJikanHours + sundayWorkedHours, 1);
            var projectedFinalHours = Math.Round(ToHours(realizedCurrentNormalMinutes + projectedRemainingNormalMinutes) + holidayWorkHours, 1);
            var diff = Math.Round(projectedFinalHours - 45d, 1);
            var risk = projectedFinalHours > 45d
                ? "danger"
                : projectedFinalHours >= 35d
                    ? "warn"
                    : "normal";
            var totalRisk = ResolveTotalOvertimeRiskLevel(domingoShukkinTotalHours);

            LogOperatorOvertimeAudit(
                op,
                currentMonthStart,
                currentHours,
                holidayWorkDays,
                holidayWorkHours,
                sundayWorkedDays,
                sundayWorkedHours,
                zangyouJikanHours,
                domingoShukkinTotalHours,
                projectedFinalHours > 45d,
                domingoShukkinTotalHours > 80d && domingoShukkinTotalHours <= 90d,
                domingoShukkinTotalHours > 90d);

            return new OperatorPresenceOvertimeMetrics(
                CurrentMonthOvertimeHours: zangyouJikanHours,
                PreviousMonthOvertimeHours: ToHours(previousMonthMinutes),
                RealizedOvertimeHours: Math.Round(ToHours(realizedCurrentNormalMinutes) + holidayWorkHours, 1),
                ProjectedRemainingOvertimeHours: ToHours(projectedRemainingNormalMinutes),
                ProjectedFinalOvertimeHours: projectedFinalHours,
                WorkedSundays: workedSundays,
                HolidayWorkDays: holidayWorkDays,
                HolidayWorkHours: holidayWorkHours,
                TotalOvertimeHours: Math.Round(zangyouJikanHours + ToHours(previousMonthMinutes), 1),
                DomingoShukkinTotalHours: domingoShukkinTotalHours,
                OvertimeLimitDifferenceHours: diff,
                OvertimeRiskLevel: risk,
                TotalOvertimeRiskLevel: totalRisk,
                OvertimePlusSundaysLabel: $"{domingoShukkinTotalHours.ToString("0.0", CultureInfo.InvariantCulture)}h");
        }

        private static void LogOperatorOvertimeAudit(
            OperatorManagerDirectoryRow op,
            DateTime month,
            double normalZangyouHours,
            int holidayWorkDays,
            double holidayWorkHours,
            int sundayWorkedDays,
            double sundayWorkedHours,
            double zangyouJikanHours,
            double domingoShukkinTotalHours,
            bool zangyouLimit45Exceeded,
            bool domingoShukkin80To90,
            bool domingoShukkinOver90)
        {
            Console.WriteLine(
                "[PresenceOvertime][OperatorAudit] "
                + $"OperatorId={op.CodigoFJ} "
                + $"OperatorName={op.Name} "
                + $"Month={month.ToString("yyyy-MM", CultureInfo.InvariantCulture)} "
                + $"NormalZangyouHours={normalZangyouHours.ToString("0.0", CultureInfo.InvariantCulture)} "
                + $"ShukkinDays={holidayWorkDays.ToString(CultureInfo.InvariantCulture)} "
                + $"ShukkinHours={holidayWorkHours.ToString("0.0", CultureInfo.InvariantCulture)} "
                + $"SundayWorkedDays={sundayWorkedDays.ToString(CultureInfo.InvariantCulture)} "
                + $"SundayWorkedHours={sundayWorkedHours.ToString("0.0", CultureInfo.InvariantCulture)} "
                + $"ZangyouJikan={zangyouJikanHours.ToString("0.0", CultureInfo.InvariantCulture)} "
                + $"ZangyouPlusDomShukkin={domingoShukkinTotalHours.ToString("0.0", CultureInfo.InvariantCulture)} "
                + $"ZangyouLimit45Exceeded={zangyouLimit45Exceeded} "
                + $"DomingoShukkin80To90={domingoShukkin80To90} "
                + $"DomingoShukkinOver90={domingoShukkinOver90}");
        }

        private static void LogDailyOvertimeAudit(
            OperatorManagerDirectoryRow op,
            DateTime date,
            OperatorDailyOvertimeAudit daily,
            double sundayWorkedHours)
        {
            var normalHours = ToHours(daily.NormalZangyouMinutes);
            var holidayHours = ToHours(daily.ShukkinMinutes);
            Console.WriteLine(
                "[PresenceOvertime][DailyAudit] "
                + $"OperatorId={op.CodigoFJ} "
                + $"Date={date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} "
                + $"DayOfWeek={daily.DayOfWeek} "
                + $"IsSunday={daily.IsSunday} "
                + $"IsHolidayWork={daily.IsHolidayWork} "
                + $"AttendanceStatus={daily.AttendanceStatus} "
                + $"NormalZangyouHours={normalHours.ToString("0.0", CultureInfo.InvariantCulture)} "
                + $"ShukkinHours={holidayHours.ToString("0.0", CultureInfo.InvariantCulture)} "
                + $"SundayWorkedHours={sundayWorkedHours.ToString("0.0", CultureInfo.InvariantCulture)} "
                + $"IncludedInZangyouJikan={(normalHours + holidayHours).ToString("0.0", CultureInfo.InvariantCulture)} "
                + $"IncludedInZangyouPlusDomShukkin={(normalHours + holidayHours + sundayWorkedHours).ToString("0.0", CultureInfo.InvariantCulture)} "
                + $"Reason={daily.Reason}");
        }

        private static bool ResolveSundayWorked(
            DateTime date,
            bool isHolidayWork,
            bool hasHaidaiAssignment,
            bool hasPresence,
            string attendanceStatus,
            out string reason)
        {
            if (date.DayOfWeek != DayOfWeek.Sunday)
            {
                reason = "not_sunday";
                return false;
            }

            if (attendanceStatus is AttendanceStatusKeys.Falta or AttendanceStatusKeys.Yukyu)
            {
                reason = attendanceStatus;
                return false;
            }

            if (isHolidayWork)
            {
                reason = "holiday_work";
                return true;
            }

            if (hasHaidaiAssignment)
            {
                reason = "haidai_assignment";
                return true;
            }

            if (hasPresence)
            {
                reason = "presence_on_sunday";
                return true;
            }

            reason = "no_sunday_work_evidence";
            return false;
        }

        private static void LogSundayWorkedAudit(
            OperatorManagerDirectoryRow op,
            DateTime date,
            bool isSunday,
            bool isHolidayWork,
            bool hasHaidaiAssignment,
            string attendanceStatus,
            bool countedAsSundayWorked,
            string reason)
        {
            Console.WriteLine(
                "[PresenceOvertime][SundayWorkedAudit] "
                + $"OperatorId={op.CodigoFJ} "
                + $"OperatorName={op.Name} "
                + $"Date={date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} "
                + $"DayOfWeek={date.DayOfWeek} "
                + $"IsSunday={isSunday} "
                + $"IsHolidayWork={isHolidayWork} "
                + $"HasHaidaiAssignment={hasHaidaiAssignment} "
                + $"AttendanceStatus={attendanceStatus} "
                + $"CountedAsSundayWorked={countedAsSundayWorked} "
                + $"Reason={reason}");
        }

        private static void LogSundayWorkedSummary(DateTime periodStart, DateTime periodEnd, int sundayWorkedCount)
        {
            Console.WriteLine(
                "[PresenceOvertime][SundayWorkedSummary] "
                + $"PeriodStart={periodStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} "
                + $"PeriodEnd={periodEnd.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} "
                + $"SundayWorkedCount={sundayWorkedCount.ToString(CultureInfo.InvariantCulture)} "
                + $"SundayWorkedHours={ToHours(sundayWorkedCount * HolidayWorkFixedMinutes).ToString("0.0", CultureInfo.InvariantCulture)}");
        }

        private static void LogAbsenceRankingAudit(OperatorPresenceAbsenceRankingItem item)
        {
            Console.WriteLine(
                "[PresenceReport][AbsenceRankingAudit] "
                + $"OperatorId={item.CodigoFJ} "
                + $"OperatorName={item.Name} "
                + $"Faltas={item.FaltaDays.ToString(CultureInfo.InvariantCulture)} "
                + $"Atrasos={item.LateDays.ToString(CultureInfo.InvariantCulture)} "
                + $"SaidasAntecipadas={item.EarlyLeaveDays.ToString(CultureInfo.InvariantCulture)} "
                + $"Yuukyu={item.YukyuDays.ToString(CultureInfo.InvariantCulture)} "
                + $"TotalOcorrencias={item.TotalOccurrences.ToString(CultureInfo.InvariantCulture)} "
                + $"RankingPosition={item.Rank.ToString(CultureInfo.InvariantCulture)}");
        }

        private static OperatorDailyOvertimeAudit CalculateDailyOvertime(
            OperatorManagerDirectoryRow op,
            DateTime date,
            OperatorManagerScheduleRow? schedule,
            OperatorOvertimePresenceWindowSummary presenceWindow,
            OperatorManagerTodokeRow? todoke,
            OperatorManagerMovementRow? movement,
            string statusKey,
            bool isProjection)
        {
            var presence = presenceWindow.Presence;
            var hasSchedule = IsScheduledDay(schedule);
            var isLate = statusKey == AttendanceStatusKeys.Late;
            var isEarlyLeave = statusKey == AttendanceStatusKeys.EarlyLeave;
            var actualEnd = ResolveActualEndForOvertime(date, op.ShiftName, presenceWindow, movement);
            var allowFallback = ShouldAllowFullShiftFallback(date, op.ShiftName, presenceWindow, hasSchedule, isLate, isEarlyLeave);
            var audit = OvertimeRuleCalculator.Calculate(new OvertimeCalculationInput(
                op.CodigoFJ,
                op.Name,
                date.Date,
                op.ShiftName,
                hasSchedule,
                schedule?.IsHolidayWork == true,
                statusKey,
                actualEnd,
                isLate,
                isEarlyLeave,
                isProjection,
                AllowFullShiftFallbackWithoutPresenceEnd: !isProjection && allowFallback));

            return new OperatorDailyOvertimeAudit(
                DayOfWeek: audit.DayOfWeek,
                ScheduledStart: audit.ScheduledStart,
                TeijiEnd: audit.TeijiEnd,
                ZangyouStart: audit.ZangyouStart,
                ZangyouEnd: audit.ZangyouEnd,
                ActualEnd: audit.ActualEnd,
                IsSunday: audit.IsSunday,
                IsHolidayWork: audit.IsHolidayWork,
                AttendanceStatus: audit.AttendanceStatus,
                WorkedSunday: audit.WorkedSunday,
                IsFullShift: audit.IsFullShift,
                IsLate: audit.HasLate,
                IsEarlyLeave: audit.HasEarlyLeave,
                ExpectedWorkMinutes: audit.ExpectedWorkMinutes,
                ActualWorkMinutes: audit.ActualWorkMinutes,
                NormalZangyouMinutes: audit.NormalZangyouMinutes,
                ShukkinMinutes: audit.HolidayWorkMinutes,
                OvertimeMinutes: audit.TotalOvertimeMinutes,
                Reason: audit.Reason,
                HasDiagnostic: audit.HasDiagnostic);
        }

        private static string ResolveTotalOvertimeRiskLevel(double totalHours)
        {
            if (totalHours > 90d)
            {
                return "danger";
            }

            if (totalHours > 80d)
            {
                return "warn";
            }

            return "normal";
        }

        private static OperatorOvertimePresenceWindowSummary ResolvePresenceWindowForOvertime(
            DateTime scheduleDate,
            string shiftName,
            IReadOnlyList<OperatorManagerPresenceEventRow> presenceEvents)
        {
            if (presenceEvents.Count == 0
                || !OvertimeRuleCalculator.TryResolveShiftWindow(shiftName, scheduleDate, out var window))
            {
                return new OperatorOvertimePresenceWindowSummary(0, null, null, null);
            }

            var matches = presenceEvents
                .Where(item => item.PresenceDateTime >= window.ScheduledStart && item.PresenceDateTime <= window.ZangyouEnd)
                .Select(item => item.PresenceDateTime)
                .OrderBy(item => item)
                .ToList();

            if (matches.Count == 0)
            {
                return new OperatorOvertimePresenceWindowSummary(0, null, null, null);
            }

            var firstPresence = matches[0];
            var lastPresence = matches[^1];
            var presence = new OperatorManagerPresenceRow
            {
                Day = scheduleDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                LastPresence = lastPresence
            };

            return new OperatorOvertimePresenceWindowSummary(
                matches.Count,
                firstPresence,
                lastPresence,
                presence);
        }

        private static DateTime? ResolveActualEndForOvertime(
            DateTime scheduleDate,
            string shiftName,
            OperatorOvertimePresenceWindowSummary presenceWindow,
            OperatorManagerMovementRow? movement)
        {
            var movementMoment = ResolveMovementActualEnd(scheduleDate, shiftName, movement);
            if (movementMoment.HasValue)
            {
                return movementMoment;
            }

            return presenceWindow.LastPresenceTime;
        }

        private static DateTime? ResolveMovementActualEnd(
            DateTime scheduleDate,
            string shiftName,
            OperatorManagerMovementRow? movement)
        {
            if (movement == null
                || !string.Equals(movement.MovementType, "early_leave", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return OvertimeRuleCalculator.NormalizeMovementMoment(
                scheduleDate,
                shiftName,
                movement.EventTime,
                movement.EventDateTime);
        }

        private static bool ShouldAllowFullShiftFallback(
            DateTime scheduleDate,
            string shiftName,
            OperatorOvertimePresenceWindowSummary presenceWindow,
            bool hasSchedule,
            bool isLate,
            bool isEarlyLeave)
        {
            if (!hasSchedule
                || isLate
                || isEarlyLeave
                || !OvertimeRuleCalculator.TryResolveShiftWindow(shiftName, scheduleDate, out var window))
            {
                return false;
            }

            if (presenceWindow.RowsFound == 0)
            {
                return true;
            }

            if (presenceWindow.RowsFound == 1
                && presenceWindow.LastPresenceTime.HasValue)
            {
                return presenceWindow.LastPresenceTime.Value <= window.ScheduledStart.AddHours(3);
            }

            return false;
        }

        private static string FormatDiagnosticDateTime(DateTime? value)
        {
            return value?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "-";
        }

        private static double ToHours(double minutes)
        {
            return Math.Round(minutes / 60d, 1);
        }

        private static bool IsSunday(string day)
        {
            return DateTime.TryParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                && parsed.DayOfWeek == DayOfWeek.Sunday;
        }

        private static void LogPresenceReportDiagnostic(
            OperatorPresenceReportRow row,
            string operatorId,
            string operatorName,
            int shiftId,
            string shiftName,
            DateTime periodStart,
            DateTime periodEnd)
        {
            Console.WriteLine(
                "[PresenceReport][Diagnostic] "
                + $"OperatorId={operatorId} "
                + $"OperatorName={operatorName} "
                + $"ShiftId={shiftId} "
                + $"ShiftName={shiftName} "
                + $"PeriodStart={periodStart:yyyy-MM-dd} "
                + $"PeriodEnd={periodEnd:yyyy-MM-dd} "
                + $"ExpectedDaysWithoutSunday={row.ScheduledDaysWithoutSunday} "
                + $"ExpectedDaysWithSunday={row.ScheduledDaysWithSunday} "
                + $"PresentDays={row.PresentDays} "
                + $"AbsencesWithoutSunday={row.AbsencesWithoutSunday} "
                + $"AbsencesWithSunday={row.AbsencesWithSunday} "
                + $"AttendancePercentWithoutSunday={row.PresencePercentWithoutSunday.ToString("0.0", CultureInfo.InvariantCulture)} "
                + $"AttendancePercentWithSunday={row.PresencePercentWithSunday.ToString("0.0", CultureInfo.InvariantCulture)} "
                + $"Below82WithoutSunday={row.Below82WithoutSunday} "
                + $"Below82WithSunday={row.Below82WithSunday}");
        }

        private static bool MatchesPresenceStatusFilter(OperatorPresenceReportRow row, string statusFilter)
        {
            return statusFilter switch
            {
                "" or "all" => true,
                "present" => row.PresentDays > 0,
                "yukyu" => row.YukyuDays > 0,
                "falta" => row.FaltaDays > 0,
                "late" => row.LateDays > 0,
                "early_leave" => row.EarlyLeaveDays > 0,
                "pending" => row.PendingTodokeCount > 0,
                "issue" => row.FaltaDays > 0 || row.LateDays > 0 || row.EarlyLeaveDays > 0 || row.PendingTodokeCount > 0,
                _ => true
            };
        }

        private static DateTime ParseDateOrDefault(string value, DateTime fallback)
        {
            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed)
                ? parsed.Date
                : fallback.Date;
        }

        private OperatorManagerProductionSummary BuildProductionSummary(
            OperatorManagerOperatorInfo operatorInfo,
            IReadOnlyList<OperatorManagerScheduleRow> scheduleRows,
            ISet<string> presentDays,
            IReadOnlyDictionary<string, OperatorManagerMovementRow> movementRows,
            IReadOnlyDictionary<string, OperatorManagerMovementRow> replacementMovementRows,
            DateTime startDate,
            DateTime endDate)
        {
            var analytics = new ProductionAnalyticsService(_factory);
            var detail = analytics.GetOperatorDetail(
                new ProductionDashboardFilter
                {
                    Date = endDate.Date,
                    ShiftId = operatorInfo.ShiftId,
                    SectorId = operatorInfo.SectorId
                },
                operatorInfo.CodigoFJ);

            var productionDays = detail.Entries
                .OrderByDescending(item => item.Date)
                .Select(item => new OperatorManagerProductionDay(
                    item.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    item.RunningMinutes,
                    item.KadouritsuPercent,
                    new[] { item.LocalNamePt }
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                    item.CoverageMode,
                    item.IsPartialCoverage,
                    item.EffectiveMinutes,
                    item.PlannedMinutes))
                .ToList();

            return new OperatorManagerProductionSummary(
                detail.TotalRunningMinutes,
                detail.AverageKadouritsuPercent,
                detail.LocalNamesPt.Count > 0 ? detail.LocalNamesPt : detail.LocalNamesJp,
                detail.FullCoverageDays,
                detail.PartialCoverageDays,
                productionDays.Take(12).ToList());
        }

        private static ProductionCoverageDescriptor DescribeProductionCoverage(
            ProductionShiftPeriod scheduledPeriod,
            OperatorManagerMovementRow? movement,
            OperatorManagerMovementRow? replacementMovement)
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
                mode,
                isPartial,
                plannedMinutes,
                effectiveMinutes);
        }

        private static ProductionShiftPeriod ResolveEffectiveOperatorPeriod(
            ProductionShiftPeriod scheduledPeriod,
            OperatorManagerMovementRow? movement,
            OperatorManagerMovementRow? replacementMovement)
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

        private List<OperatorManagerDailyHistoryItem> BuildDailyHistory(
            IEnumerable<OperatorManagerAttendanceDaySummary> attendanceDays,
            IReadOnlyDictionary<string, OperatorManagerPresenceRow> presences,
            IReadOnlyDictionary<string, OperatorManagerTodokeRow> todokes,
            IReadOnlyDictionary<string, OperatorManagerMovementRow> movements)
        {
            var orderedDays = attendanceDays
                .OrderByDescending(item => item.Day, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var list = new List<OperatorManagerDailyHistoryItem>(orderedDays.Count);
            foreach (var daySummary in orderedDays)
            {
                presences.TryGetValue(daySummary.Day, out var presence);
                todokes.TryGetValue(daySummary.Day, out var todoke);
                movements.TryGetValue(daySummary.Day, out var movement);

                var notes = new List<string>();
                if (!string.IsNullOrWhiteSpace(todoke?.Notes))
                {
                    notes.Add(todoke.Notes);
                }

                if (!string.IsNullOrWhiteSpace(movement?.Reason))
                {
                    notes.Add(movement.Reason);
                }

                if (presence?.LastPresence != null)
                {
                    notes.Add($"Presenca {presence.LastPresence.Value.ToString("HH:mm", CultureInfo.InvariantCulture)}");
                }

                list.Add(new OperatorManagerDailyHistoryItem(
                    daySummary.Day,
                    daySummary.DisplayStatus,
                    daySummary.Area,
                    string.Join(" | ", notes.Where(item => !string.IsNullOrWhiteSpace(item))),
                    todoke != null && !todoke.Validated));
            }

            return list.Take(20).ToList();
        }

        private static IReadOnlyDictionary<string, OperatorManagerAttendanceDaySummary> BuildAttendanceDaySummaries(
            IReadOnlyDictionary<string, OperatorManagerScheduleRow> latestScheduleByDay,
            IReadOnlyDictionary<string, OperatorManagerPresenceRow> presences,
            IReadOnlyDictionary<string, OperatorManagerTodokeRow> todokes,
            IReadOnlyDictionary<string, OperatorManagerMovementRow> movements)
        {
            var allDays = latestScheduleByDay.Keys
                .Concat(presences.Keys)
                .Concat(todokes.Keys)
                .Concat(movements.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var map = new Dictionary<string, OperatorManagerAttendanceDaySummary>(StringComparer.OrdinalIgnoreCase);
            foreach (var day in allDays)
            {
                latestScheduleByDay.TryGetValue(day, out var schedule);
                presences.TryGetValue(day, out var presence);
                todokes.TryGetValue(day, out var todoke);
                movements.TryGetValue(day, out var movement);

                var statusKey = ResolveAttendanceStatusKey(schedule, presence, todoke, movement);
                var area = !string.IsNullOrWhiteSpace(schedule?.AssignmentCode)
                    ? schedule.AssignmentCode
                    : movement?.AssignmentCode ?? string.Empty;
                var countsAsScheduled = IsScheduledDay(schedule);
                var countsTowardPresence = countsAsScheduled && statusKey != AttendanceStatusKeys.Falta
                    && statusKey != AttendanceStatusKeys.Late
                    && statusKey != AttendanceStatusKeys.EarlyLeave;
                var countsTowardCoverage = countsAsScheduled && statusKey != AttendanceStatusKeys.Falta;

                map[day] = new OperatorManagerAttendanceDaySummary(
                    day,
                    statusKey,
                    ResolveDisplayStatus(statusKey),
                    area,
                    countsAsScheduled,
                    countsTowardPresence,
                    countsTowardCoverage);
            }

            return map;
        }

        private static string ResolveAttendanceStatusKey(
            OperatorManagerScheduleRow? schedule,
            OperatorManagerPresenceRow? presence,
            OperatorManagerTodokeRow? todoke,
            OperatorManagerMovementRow? movement)
        {
            if (todoke?.MotiveId == 2)
            {
                return AttendanceStatusKeys.Falta;
            }

            if (todoke?.MotiveId == 3
                || string.Equals(movement?.MovementType, "late", StringComparison.OrdinalIgnoreCase)
                || string.Equals(schedule?.AvailabilityStatus, "Atraso", StringComparison.OrdinalIgnoreCase))
            {
                return AttendanceStatusKeys.Late;
            }

            if (todoke?.MotiveId == 5
                || string.Equals(movement?.MovementType, "early_leave", StringComparison.OrdinalIgnoreCase)
                || string.Equals(schedule?.AvailabilityStatus, "Saiu cedo", StringComparison.OrdinalIgnoreCase)
                || string.Equals(schedule?.AvailabilityStatus, "Saida antecipada", StringComparison.OrdinalIgnoreCase))
            {
                return AttendanceStatusKeys.EarlyLeave;
            }

            if (todoke?.MotiveId == 1
                || string.Equals(schedule?.AvailabilityStatus, "Yukyu", StringComparison.OrdinalIgnoreCase))
            {
                return AttendanceStatusKeys.Yukyu;
            }

            if (string.Equals(schedule?.AvailabilityStatus, "Folga", StringComparison.OrdinalIgnoreCase))
            {
                return AttendanceStatusKeys.Folga;
            }

            if (presence != null)
            {
                return AttendanceStatusKeys.Present;
            }

            if (IsScheduledDay(schedule))
            {
                return AttendanceStatusKeys.Scheduled;
            }

            return AttendanceStatusKeys.NoRecord;
        }

        private static bool IsScheduledDay(OperatorManagerScheduleRow? schedule)
        {
            return schedule != null
                && !string.Equals(schedule.AvailabilityStatus, "Folga", StringComparison.OrdinalIgnoreCase)
                && (!string.IsNullOrWhiteSpace(schedule.AssignmentCode)
                    || schedule.LocalId > 0
                    || !string.IsNullOrWhiteSpace(schedule.AvailabilityStatus));
        }

        private static string ResolveDisplayStatus(string statusKey)
        {
            return statusKey switch
            {
                AttendanceStatusKeys.Falta => "Falta",
                AttendanceStatusKeys.Late => "Atraso",
                AttendanceStatusKeys.EarlyLeave => "Sair cedo",
                AttendanceStatusKeys.Yukyu => "Yukyu",
                AttendanceStatusKeys.Folga => "Folga",
                AttendanceStatusKeys.Present => "Presente",
                AttendanceStatusKeys.Scheduled => "Escalado",
                _ => "Sem registro"
            };
        }

        private static double CalculateRunningMinutes(IReadOnlyList<ProductionEventPoint> events, ProductionShiftPeriod period)
        {
            var running = 0d;
            var seed = events
                .Where(item => item.EventDateTime <= period.Start)
                .OrderByDescending(item => item.EventDateTime)
                .FirstOrDefault();

            var cursor = period.Start;
            var currentStatus = seed?.StatusCode ?? 2;

            foreach (var machineEvent in events.Where(item => item.EventDateTime >= period.Start && item.EventDateTime <= period.End))
            {
                if (machineEvent.EventDateTime > cursor && currentStatus == 0)
                {
                    running += (machineEvent.EventDateTime - cursor).TotalMinutes;
                }

                cursor = machineEvent.EventDateTime < period.Start ? period.Start : machineEvent.EventDateTime;
                currentStatus = machineEvent.StatusCode;
            }

            if (cursor < period.End && currentStatus == 0)
            {
                running += (period.End - cursor).TotalMinutes;
            }

            return running;
        }

        private static ProductionShiftPeriod ResolveObservedProductionPeriod(
            IReadOnlyList<ProductionEventPoint> events,
            ProductionShiftPeriod scheduledPeriod)
        {
            var end = scheduledPeriod.End;
            var now = DateTime.Now;
            if (end > now)
            {
                end = now;
            }

            var latestEvent = events
                .Where(item => item.EventDateTime >= scheduledPeriod.Start && item.EventDateTime <= scheduledPeriod.End)
                .OrderByDescending(item => item.EventDateTime)
                .FirstOrDefault();

            if (latestEvent == null)
            {
                end = scheduledPeriod.Start;
            }
            else if (latestEvent.EventDateTime < end)
            {
                end = latestEvent.EventDateTime;
            }

            if (end < scheduledPeriod.Start)
            {
                end = scheduledPeriod.Start;
            }

            return new ProductionShiftPeriod
            {
                Start = scheduledPeriod.Start,
                End = end
            };
        }

        private sealed class OperatorManagerDirectoryRow
        {
            public string CodigoFJ { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string NameJp { get; set; } = string.Empty;
            public int ShiftId { get; set; }
            public string ShiftName { get; set; } = string.Empty;
            public int SectorId { get; set; }
            public string SectorName { get; set; } = string.Empty;
            public int GroupId { get; set; }
            public string GroupName { get; set; } = string.Empty;
            public bool Trainer { get; set; }
            public bool IsLeader { get; set; }
            public int ScheduledDays { get; set; }
            public int PresentDays { get; set; }
            public int FollowUpCount { get; set; }
            public int YukyuDays { get; set; }
            public int FaltaDays { get; set; }
            public int LateDays { get; set; }
            public int EarlyLeaveDays { get; set; }
            public int PresenceImpactDays { get; set; }
            public int PendingTodokeCount { get; set; }
        }

        private sealed class OperatorManagerOperatorInfo
        {
            public string CodigoFJ { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string NameJp { get; set; } = string.Empty;
            public int ShiftId { get; set; }
            public string ShiftName { get; set; } = string.Empty;
            public int SectorId { get; set; }
            public string SectorName { get; set; } = string.Empty;
            public int GroupId { get; set; }
            public string GroupName { get; set; } = string.Empty;
            public DateTime StartDate { get; set; }
            public bool Trainer { get; set; }
            public bool IsLeader { get; set; }
        }

        private sealed class OperatorManagerScheduleRow
        {
            public string OperatorCodigoFJ { get; set; } = string.Empty;
            public string Day { get; set; } = string.Empty;
            public int ShiftId { get; set; }
            public int LocalId { get; set; }
            public string LocalName { get; set; } = string.Empty;
            public string AssignmentCode { get; set; } = string.Empty;
            public string AvailabilityStatus { get; set; } = string.Empty;
        public bool IsLineupActive { get; set; }
        public bool IsHolidayWork { get; set; }
        public bool IsTrainee { get; set; }
        }

        private sealed class OperatorManagerPresenceRow
        {
            public string Day { get; set; } = string.Empty;
            public DateTime? LastPresence { get; set; }
        }

        private sealed class OperatorPresenceReportPresenceRow
        {
            public string OperatorCodigoFJ { get; set; } = string.Empty;
            public string Day { get; set; } = string.Empty;
            public DateTime? LastPresence { get; set; }
        }

        private sealed class OperatorManagerPresenceEventRow
        {
            public string OperatorCodigoFJ { get; set; } = string.Empty;
            public DateTime PresenceDateTime { get; set; }
        }

        private sealed record OperatorOvertimePresenceWindowSummary(
            int RowsFound,
            DateTime? FirstPresenceTime,
            DateTime? LastPresenceTime,
            OperatorManagerPresenceRow? Presence);

        private sealed class OperatorManagerTodokeRow
        {
            public string OperatorCodigoFJ { get; set; } = string.Empty;
            public string Day { get; set; } = string.Empty;
            public long Id { get; set; }
            public int MotiveId { get; set; }
            public string MotiveName { get; set; } = string.Empty;
            public string Notes { get; set; } = string.Empty;
            public bool Validated { get; set; }
        }

        private sealed class OperatorManagerMovementRow
        {
            public string OperatorCodigoFJ { get; set; } = string.Empty;
            public string Day { get; set; } = string.Empty;
            public string MovementType { get; set; } = string.Empty;
            public string EventTime { get; set; } = string.Empty;
            public string EventDateTime { get; set; } = string.Empty;
            public string AssignmentCode { get; set; } = string.Empty;
            public string Reason { get; set; } = string.Empty;
            public string ReplacementOperatorCodigoFJ { get; set; } = string.Empty;
        }

        private sealed class OperatorManagerFollowUpCountRow
        {
            public string OperatorCodigoFJ { get; set; } = string.Empty;
            public int Count { get; set; }
        }

        private sealed record OperatorManagerAttendanceDaySummary(
            string Day,
            string StatusKey,
            string DisplayStatus,
            string Area,
            bool CountsAsScheduled,
            bool CountsTowardPresence,
            bool CountsTowardCoverage);

        private sealed record OperatorPresenceOvertimeMetrics(
            double CurrentMonthOvertimeHours,
            double PreviousMonthOvertimeHours,
            double RealizedOvertimeHours,
            double ProjectedRemainingOvertimeHours,
            double ProjectedFinalOvertimeHours,
            int WorkedSundays,
            int HolidayWorkDays,
            double HolidayWorkHours,
            double TotalOvertimeHours,
            double DomingoShukkinTotalHours,
            double OvertimeLimitDifferenceHours,
            string OvertimeRiskLevel,
            string TotalOvertimeRiskLevel,
            string OvertimePlusSundaysLabel);

        private sealed record OperatorDailyOvertimeAudit(
            string DayOfWeek,
            DateTime? ScheduledStart,
            DateTime? TeijiEnd,
            DateTime? ZangyouStart,
            DateTime? ZangyouEnd,
            DateTime? ActualEnd,
            bool IsSunday,
            bool IsHolidayWork,
            string AttendanceStatus,
            bool WorkedSunday,
            bool IsFullShift,
            bool IsLate,
            bool IsEarlyLeave,
            double ExpectedWorkMinutes,
            double ActualWorkMinutes,
            double NormalZangyouMinutes,
            double ShukkinMinutes,
            double OvertimeMinutes,
            string Reason,
            bool HasDiagnostic);

        private readonly record struct ProductionCoverageDescriptor(
            string Mode,
            bool IsPartial,
            double PlannedMinutes,
            double EffectiveMinutes);

        private sealed class OperatorManagerFollowUpRow
        {
            public int Id { get; set; }
            public DateTime Date { get; set; }
            public string ShiftName { get; set; } = string.Empty;
            public string ReasonName { get; set; } = string.Empty;
            public string TypeName { get; set; } = string.Empty;
            public string LocalName { get; set; } = string.Empty;
            public string EquipmentName { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string Guidance { get; set; } = string.Empty;
        }

        private sealed class OperatorManagerMasterCardRow
        {
            public int Id { get; set; }
            public string EquipmentName { get; set; } = string.Empty;
            public string SectorName { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public string StartDate { get; set; } = string.Empty;
            public string ConcludedAt { get; set; } = string.Empty;
            public string FollowDate { get; set; } = string.Empty;
            public string FinalizedAt { get; set; } = string.Empty;
            public string Notes { get; set; } = string.Empty;
        }

        private sealed class OperatorManagerMachineRow
        {
            public int Id { get; set; }
            public int LocalId { get; set; }
        }

        private sealed class OperatorManagerEventRow
        {
            public int MachineId { get; set; }
            public int StatusCode { get; set; }
            public DateTime EventDateTime { get; set; }
        }

        private sealed record ProductionEventPoint(int MachineId, int StatusCode, DateTime EventDateTime);

        private static class AttendanceStatusKeys
        {
            public const string Scheduled = "scheduled";
            public const string Present = "present";
            public const string Yukyu = "yukyu";
            public const string Falta = "falta";
            public const string Late = "late";
            public const string EarlyLeave = "early_leave";
            public const string Folga = "folga";
            public const string NoRecord = "no_record";
        }
    }

    public sealed record OperatorManagerInitPayload(
        int DefaultShiftId,
        int DefaultSectorId,
        int DefaultPeriodDays,
        IReadOnlyList<OperatorManagerLookupItem> Shifts,
        IReadOnlyList<OperatorManagerLookupItem> Sectors,
        IReadOnlyList<OperatorManagerLookupItem> Groups);

    public sealed class OperatorManagerLookupItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public sealed record OperatorManagerDirectoryFilter(
        int ShiftId,
        int SectorId,
        int GroupId,
        int PeriodDays,
        string Search);

    public sealed record OperatorPresenceReportFilter(
        string StartDateIso,
        string EndDateIso,
        int ShiftId,
        int SectorId,
        int GroupId,
        string Status,
        string Search);

    public sealed record OperatorPresenceReportPayload(
        string StartDateIso,
        string EndDateIso,
        OperatorPresenceReportSummary Summary,
        IReadOnlyList<OperatorPresenceOvertimeRankingItem> TopOvertime,
        IReadOnlyList<OperatorPresenceAbsenceRankingItem> TopAbsences,
        OperatorPresenceReportPerformance Performance,
        IReadOnlyList<OperatorPresenceReportRow> Rows);

    public sealed record OperatorPresenceReportSummary(
        int OperatorCount,
        int ScheduledDays,
        int PresentDays,
        int YukyuDays,
        int FaltaDays,
        int LateDays,
        int EarlyLeaveDays,
        double PresencePercent,
        int ScheduledDaysWithoutSunday,
        int ScheduledDaysWithSunday,
        int AbsencesWithoutSunday,
        int AbsencesWithSunday,
        double PresencePercentWithoutSunday,
        double PresencePercentWithSunday,
        int OvertimeOver45Count,
        int OvertimeBetween35And45Count,
        int OvertimeBelow35Count,
        double TotalCurrentMonthOvertimeHours,
        int TotalWorkedSundays,
        int TotalHolidayWorkDays);

    public sealed record OperatorPresenceReportRow(
        string CodigoFJ,
        string Name,
        string NameJp,
        string ShiftName,
        string SectorName,
        string GroupName,
        int ScheduledDays,
        int PresentDays,
        int YukyuDays,
        int FaltaDays,
        int LateDays,
        int EarlyLeaveDays,
        int PendingTodokeCount,
        double PresencePercent,
        int ScheduledDaysWithoutSunday,
        int ScheduledDaysWithSunday,
        int AbsencesWithoutSunday,
        int AbsencesWithSunday,
        double PresencePercentWithoutSunday,
        double PresencePercentWithSunday,
        bool Below82WithoutSunday,
        bool Below82WithSunday,
        double CurrentMonthOvertimeHours,
        double PreviousMonthOvertimeHours,
        double RealizedOvertimeHours,
        double ProjectedRemainingOvertimeHours,
        double ProjectedFinalOvertimeHours,
        int WorkedSundays,
        int HolidayWorkDays,
        double HolidayWorkHours,
        double TotalOvertimeHours,
        double DomingoShukkinTotalHours,
        double OvertimeLimitDifferenceHours,
        string OvertimeRiskLevel,
        string TotalOvertimeRiskLevel,
        string OvertimePlusSundaysLabel,
        string LastStatus,
        string LastDateIso,
        string LastArea);

    public sealed record OperatorPresenceOvertimeRankingItem(
        int Rank,
        string CodigoFJ,
        string Name,
        string NameJp,
        string ShiftName,
        double OvertimeHours,
        int HolidayWorkDays);

    public sealed record OperatorPresenceAbsenceRankingItem(
        int Rank,
        string CodigoFJ,
        string Name,
        string NameJp,
        string ShiftName,
        int FaltaDays,
        int LateDays,
        int EarlyLeaveDays,
        int YukyuDays,
        int TotalOccurrences);

    public sealed record OperatorPresenceReportPerformance(
        long LoadPresenceMs,
        long LoadHaidaiMs,
        long BuildOvertimeMs,
        long BuildProjectionMs,
        long BuildRankingMs);

    public sealed record OperatorManagerDirectoryPayload(
        string StartDateIso,
        string EndDateIso,
        IReadOnlyList<OperatorManagerDirectoryItem> Items);

    public sealed record OperatorManagerDirectoryItem(
        string CodigoFJ,
        string Name,
        string NameJp,
        int ShiftId,
        string ShiftName,
        int SectorId,
        string SectorName,
        int GroupId,
        string GroupName,
        bool Trainer,
        bool IsLeader,
        int ScheduledDays,
        int PresentDays,
        int YukyuDays,
        int FaltaDays,
        int LateDays,
        int EarlyLeaveDays,
        int FollowUpCount,
        int PendingTodokeCount,
        double PresencePercent,
        double CoveragePercent);

    public sealed record OperatorManagerReportPayload(
        string CodigoFJ,
        string Name,
        string NameJp,
        string ShiftName,
        string SectorName,
        string GroupName,
        string StartDateIso,
        bool Trainer,
        bool IsLeader,
        OperatorManagerPresenceMetrics Presence,
        OperatorManagerMasterCardSummary MasterCards,
        OperatorManagerProductionSummary Production,
        IReadOnlyList<OperatorManagerFollowUpItem> FollowUps,
        IReadOnlyList<OperatorManagerDailyHistoryItem> DailyHistory);

    public sealed record OperatorManagerPresenceMetrics(
        int ScheduledDays,
        int PresentDays,
        int YukyuDays,
        int FaltaDays,
        int LateDays,
        int EarlyLeaveDays,
        int PendingTodokeCount,
        int FollowUpCount,
        double PresencePercent,
        double CoveragePercent,
        int ScheduledDaysWithoutSunday,
        int ScheduledDaysWithSunday,
        int AbsencesWithoutSunday,
        int AbsencesWithSunday,
        double PresencePercentWithoutSunday,
        double PresencePercentWithSunday);

    public sealed record OperatorManagerMasterCardSummary(
        int TotalCount,
        int InProgressCount,
        int FollowCount,
        int CompletedCount,
        int OverdueFollowCount,
        int DueSoonFollowCount,
        IReadOnlyList<OperatorManagerMasterCardItem> Items);

    public sealed record OperatorManagerMasterCardItem(
        int Id,
        string EquipmentName,
        string SectorName,
        string Status,
        string StartDateIso,
        string ConcludedAt,
        string FollowDateIso,
        string FinalizedAt,
        string Notes,
        string FollowState);

    public sealed record OperatorManagerProductionSummary(
        double EstimatedRunningMinutes,
        double EstimatedKadouritsuPercent,
        IReadOnlyList<string> LocalNames,
        int FullCoverageDays,
        int PartialCoverageDays,
        IReadOnlyList<OperatorManagerProductionDay> Days);

    public sealed record OperatorManagerProductionDay(
        string DateIso,
        double EstimatedRunningMinutes,
        double EstimatedKadouritsuPercent,
        IReadOnlyList<string> LocalNames,
        string CoverageMode,
        bool IsPartialCoverage,
        double EffectiveMinutes,
        double PlannedMinutes);

    public sealed record OperatorManagerFollowUpItem(
        int Id,
        string DateLabel,
        string ShiftName,
        string ReasonName,
        string TypeName,
        string LocalName,
        string EquipmentName,
        string Description,
        string Guidance);

    public sealed record OperatorManagerDailyHistoryItem(
        string DateIso,
        string Status,
        string Area,
        string Notes,
        bool HasPendingTodoke);
}
