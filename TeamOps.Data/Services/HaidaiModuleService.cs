using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Dapper;
using TeamOps.Data.Db;

namespace TeamOps.UI.Services
{
    public sealed class HaidaiModuleService
    {
        private const string GBareruSectorCode = "G-BARERU";
        private const string DadSectorCode = "DAD";
        private const string GdadSectorCode = "GDAD";
        private const int TvExportPageSize = 20;

        private readonly SqliteConnectionFactory _factory;
        private HaidaiSectorTopology _sectorTopology = HaidaiSectorTopology.Empty;

        public HaidaiModuleService(SqliteConnectionFactory factory)
        {
            _factory = factory;
        }

        public void EnsureSchema()
        {
            using var conn = _factory.CreateOpenConnection();

            conn.Execute(
                @"
                    CREATE TABLE IF NOT EXISTS HaidaiAssignments (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        ScheduleDate TEXT NOT NULL,
                        ShiftId INTEGER NOT NULL,
                        SectorId INTEGER NOT NULL,
                        OperatorCodigoFJ TEXT NOT NULL,
                        LocalId INTEGER,
                        AssignmentCode TEXT,
                        PairKey TEXT,
                        IsTrainee INTEGER NOT NULL DEFAULT 0,
                        TrainerCodigoFJ TEXT,
                        CountsTowardKousu INTEGER NOT NULL DEFAULT 1,
                        IsLineupActive INTEGER NOT NULL DEFAULT 1,
                        IsHolidayWork INTEGER NOT NULL DEFAULT 0,
                        AvailabilityStatus TEXT,
                        Notes TEXT,
                        UpdatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        FOREIGN KEY (ShiftId) REFERENCES Shifts(Id),
                        FOREIGN KEY (SectorId) REFERENCES Sectors(Id),
                        FOREIGN KEY (OperatorCodigoFJ) REFERENCES Operators(CodigoFJ),
                        FOREIGN KEY (LocalId) REFERENCES Locals(Id),
                        FOREIGN KEY (TrainerCodigoFJ) REFERENCES Operators(CodigoFJ)
                    );

                    CREATE UNIQUE INDEX IF NOT EXISTS IX_HaidaiAssignments_DateShiftSectorOperator
                    ON HaidaiAssignments(ScheduleDate, ShiftId, SectorId, OperatorCodigoFJ);

                    CREATE INDEX IF NOT EXISTS IX_HaidaiAssignments_DateShiftSector
                    ON HaidaiAssignments(ScheduleDate, ShiftId, SectorId);");

            EnsureColumn(conn, "Locals", "ShortCode", "TEXT");
            EnsureColumn(conn, "HaidaiAssignments", "IsLineupActive", "INTEGER NOT NULL DEFAULT 1");
            EnsureColumn(conn, "HaidaiAssignments", "IsHolidayWork", "INTEGER NOT NULL DEFAULT 0");
            EnsureColumn(conn, "HaidaiAssignments", "AvailabilityStatus", "TEXT");

            conn.Execute(
                @"
                    CREATE TABLE IF NOT EXISTS HaidaiMovements (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        ScheduleDate TEXT NOT NULL,
                        ShiftId INTEGER NOT NULL,
                        SectorId INTEGER NOT NULL,
                        OperatorCodigoFJ TEXT NOT NULL,
                        MovementType TEXT NOT NULL,
                        EventTime TEXT,
                        LocalId INTEGER,
                        AssignmentCode TEXT,
                        PairKey TEXT,
                        ReplacementOperatorCodigoFJ TEXT,
                        Reason TEXT,
                        CreatedByCodigoFJ TEXT NOT NULL,
                        CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        FOREIGN KEY (ShiftId) REFERENCES Shifts(Id),
                        FOREIGN KEY (SectorId) REFERENCES Sectors(Id),
                        FOREIGN KEY (OperatorCodigoFJ) REFERENCES Operators(CodigoFJ),
                        FOREIGN KEY (LocalId) REFERENCES Locals(Id),
                        FOREIGN KEY (ReplacementOperatorCodigoFJ) REFERENCES Operators(CodigoFJ),
                        FOREIGN KEY (CreatedByCodigoFJ) REFERENCES Operators(CodigoFJ)
                    );

                    CREATE INDEX IF NOT EXISTS IX_HaidaiMovements_DateShiftSectorOperator
                    ON HaidaiMovements(ScheduleDate, ShiftId, SectorId, OperatorCodigoFJ);");

            EnsureColumn(conn, "HaidaiMovements", "EventDateTime", "TEXT");
            _sectorTopology = LoadSectorTopology(conn);
        }

        public HaidaiInitPayload GetInitialPayload(int defaultShiftId, int defaultSectorId)
        {
            EnsureSchema();

            using var conn = _factory.CreateOpenConnection();

            var shifts = conn.Query<HaidaiLookupItem>(
                @"
                    SELECT
                        Id,
                        COALESCE(NULLIF(NamePt, ''), NULLIF(NameJp, ''), 'Turno ' || Id) AS Name
                    FROM Shifts
                    ORDER BY Id;")
                .ToList();

            var sectors = conn.Query<HaidaiLookupItem>(
                @"
                    SELECT
                        Id,
                        COALESCE(NULLIF(NamePt, ''), NULLIF(NameJp, ''), 'Setor ' || Id) AS Name
                    FROM Sectors
                    ORDER BY Id;")
                .ToList();

            var locals = conn.Query<HaidaiLocalLookup>(
                @"
                    SELECT
                        Id,
                        SectorId,
                        COALESCE(NULLIF(NamePt, ''), NULLIF(NameJp, ''), 'Local ' || Id) AS Name,
                        COALESCE(NULLIF(ShortCode, ''), NULLIF(NamePt, ''), NULLIF(NameJp, ''), 'L' || Id) AS ShortCode
                    FROM Locals
                    ORDER BY SectorId, Id;")
                .ToList();

            var operators = conn.Query<HaidaiOperatorLookup>(
                @"
                    SELECT
                        o.CodigoFJ,
                        COALESCE(NULLIF(o.NameRomanji, ''), o.CodigoFJ) AS Name,
                        o.SectorId,
                        o.ShiftId,
                        o.GroupId,
                        COALESCE(NULLIF(g.NamePt, ''), NULLIF(g.NameJp, ''), 'Grupo ' || o.GroupId) AS GroupName,
                        COALESCE(o.Trainer, 0) AS Trainer,
                        COALESCE(o.IsLeader, 0) AS IsLeader,
                        COALESCE(u.AccessLevel, CASE WHEN COALESCE(o.IsLeader, 0) = 1 THEN 3 ELSE 1 END) AS AccessLevel
                    FROM Operators o
                    LEFT JOIN Groups g ON g.Id = o.GroupId
                    LEFT JOIN Users u ON u.CodigoFJ = o.CodigoFJ
                    WHERE COALESCE(o.Status, 1) = 1
                    ORDER BY o.GroupId, o.NameRomanji, o.CodigoFJ;")
                .ToList();

            var resolvedShiftId = shifts.Any(item => item.Id == defaultShiftId)
                ? defaultShiftId
                : shifts.FirstOrDefault()?.Id ?? 1;

            var resolvedSectorId = sectors.Any(item => item.Id == defaultSectorId)
                ? defaultSectorId
                : sectors.FirstOrDefault()?.Id ?? 1;

            return new HaidaiInitPayload(
                DateTime.Today.ToString("yyyy-MM-dd"),
                resolvedShiftId,
                resolvedSectorId,
                shifts,
                sectors,
                locals,
                operators,
                new HaidaiSharedSectorTopology(
                    _sectorTopology.GBareru?.Id,
                    _sectorTopology.Dad?.Id,
                    _sectorTopology.Gdad?.Id));
        }

        public HaidaiMonthlyPlanPayload GetMonthlyPlan(
            int year,
            int month,
            int shiftId,
            int sectorId,
            IReadOnlyCollection<int>? authorizedSectorIds = null)
        {
            EnsureSchema();

            using var conn = _factory.CreateOpenConnection();

            var firstDay = new DateTime(year, month, 1);
            var lastDay = firstDay.AddMonths(1).AddDays(-1);
            var dayNumbers = Enumerable.Range(1, lastDay.Day).ToList();
            var includeSharedGdad = CanUseSharedGdad(authorizedSectorIds) && SupportsSharedGdad(sectorId);

            var operators = conn.Query<HaidaiOperatorRow>(
                @"
                    SELECT
                        o.CodigoFJ,
                        COALESCE(NULLIF(o.NameRomanji, ''), o.CodigoFJ) AS Name,
                        COALESCE(NULLIF(o.NameNihongo, ''), COALESCE(NULLIF(o.NameRomanji, ''), o.CodigoFJ)) AS NameJp,
                        o.SectorId AS HomeSectorId,
                        o.GroupId,
                        COALESCE(NULLIF(g.NamePt, ''), NULLIF(g.NameJp, ''), 'Grupo ' || o.GroupId) AS GroupName,
                        COALESCE(o.Trainer, 0) AS Trainer,
                        COALESCE(o.IsLeader, 0) AS IsLeader,
                        COALESCE(u.AccessLevel, CASE WHEN COALESCE(o.IsLeader, 0) = 1 THEN 3 ELSE 1 END) AS AccessLevel
                    FROM Operators o
                    LEFT JOIN Groups g ON g.Id = o.GroupId
                    LEFT JOIN Users u ON u.CodigoFJ = o.CodigoFJ
                    WHERE COALESCE(o.Status, 1) = 1
                      AND o.ShiftId = @ShiftId
                      AND (
                            o.SectorId = @SectorId
                         OR (@IncludeSharedGdad = 1 AND o.SectorId = @GdadSectorId)
                      )
                    ORDER BY o.GroupId,
                             CASE WHEN COALESCE(o.IsLeader, 0) = 1 THEN 1 ELSE 0 END,
                             o.NameRomanji,
                             o.CodigoFJ;",
                new
                {
                    SectorId = sectorId,
                    ShiftId = shiftId,
                    IncludeSharedGdad = includeSharedGdad ? 1 : 0,
                    GdadSectorId = _sectorTopology.Gdad?.Id ?? -1
                })
                .ToList();

            var operatorCodes = operators
                .Select(item => item.CodigoFJ)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var assignmentSectorIds = operators
                .Select(item => ResolveStorageSectorId(sectorId, item.HomeSectorId))
                .Distinct()
                .ToArray();

            var assignments = operatorCodes.Length == 0
                ? new Dictionary<string, Dictionary<int, HaidaiAssignmentRecord>>(StringComparer.OrdinalIgnoreCase)
                : conn.Query<HaidaiAssignmentRecord>(
                @"
                    SELECT
                        Id,
                        ScheduleDate,
                        ShiftId,
                        SectorId,
                        OperatorCodigoFJ,
                        LocalId,
                        COALESCE(AssignmentCode, '') AS AssignmentCode,
                        COALESCE(PairKey, '') AS PairKey,
                        COALESCE(IsTrainee, 0) AS IsTrainee,
                        COALESCE(TrainerCodigoFJ, '') AS TrainerCodigoFJ,
                        COALESCE(CountsTowardKousu, 1) AS CountsTowardKousu,
                        COALESCE(IsLineupActive, 1) AS IsLineupActive,
                        COALESCE(IsHolidayWork, 0) AS IsHolidayWork,
                        COALESCE(AvailabilityStatus, '') AS AvailabilityStatus,
                        COALESCE(Notes, '') AS Notes
                    FROM HaidaiAssignments
                    WHERE ShiftId = @ShiftId
                      AND SectorId IN @SectorIds
                      AND OperatorCodigoFJ IN @OperatorCodes
                      AND date(ScheduleDate) BETWEEN date(@StartDate) AND date(@EndDate);",
                new
                {
                    ShiftId = shiftId,
                    SectorIds = assignmentSectorIds,
                    OperatorCodes = operatorCodes,
                    StartDate = firstDay.ToString("yyyy-MM-dd"),
                    EndDate = lastDay.ToString("yyyy-MM-dd")
                })
                .GroupBy(item => item.OperatorCodigoFJ, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.ToDictionary(
                        item => DateTime.Parse(item.ScheduleDate, CultureInfo.InvariantCulture).Day,
                        item => item,
                        EqualityComparer<int>.Default),
                    StringComparer.OrdinalIgnoreCase);

            var exceptions = conn.Query<HaidaiMonthlyExceptionRecord>(
                @"
                    SELECT
                        a.OperatorCodigoFJ,
                        date(a.RequestDate) AS RequestDate,
                        a.TodokeMotivoId AS MotiveId
                    FROM AcompYukyu a
                    INNER JOIN (
                        SELECT
                            OperatorCodigoFJ,
                            date(RequestDate) AS RequestDay,
                            MAX(Id) AS MaxId
                        FROM AcompYukyu
                        WHERE date(RequestDate) BETWEEN date(@StartDate) AND date(@EndDate)
                          AND TodokeMotivoId IN (1, 2)
                        GROUP BY OperatorCodigoFJ, date(RequestDate)
                    ) latest ON latest.MaxId = a.Id;",
                new
                {
                    StartDate = firstDay.ToString("yyyy-MM-dd"),
                    EndDate = lastDay.ToString("yyyy-MM-dd")
                })
                .GroupBy(item => item.OperatorCodigoFJ, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.ToDictionary(
                        item => DateTime.Parse(item.RequestDate, CultureInfo.InvariantCulture).Day,
                        item => item,
                        EqualityComparer<int>.Default),
                    StringComparer.OrdinalIgnoreCase);

            var groups = operators
                .GroupBy(item => new { item.GroupId, item.GroupName })
                .OrderBy(group => group.Key.GroupId)
                .Select(group => new HaidaiMonthlyGroupPlan(
                    group.Key.GroupId,
                    group.Key.GroupName,
                    group.Select(op =>
                    {
                        assignments.TryGetValue(op.CodigoFJ, out var days);
                        var cells = dayNumbers
                            .Select(day =>
                            {
                                var record = days != null && days.TryGetValue(day, out var found)
                                    ? found
                                    : null;
                                var exception = exceptions.TryGetValue(op.CodigoFJ, out var exceptionDays)
                                    && exceptionDays.TryGetValue(day, out var foundException)
                                        ? foundException
                                        : null;
                                var plannerCode = ResolveMonthlyPlannerCode(record);
                                var status = ResolveMonthlyStatus(exception, record, plannerCode);

                                return new HaidaiMonthlyCell(
                                    day,
                                    ResolveDisplayAssignmentCode(exception?.MotiveId ?? 0, plannerCode),
                                    record?.LocalId,
                                    record?.IsTrainee ?? false,
                                    record?.IsLineupActive ?? false,
                                    record?.IsHolidayWork ?? false,
                                    status);
                            })
                            .ToList();

                        return new HaidaiMonthlyOperatorPlan(
                            op.CodigoFJ,
                            op.Name,
                            op.NameJp,
                            op.GroupId,
                            op.GroupName,
                            cells);
                    }).ToList()))
                .ToList();

            return new HaidaiMonthlyPlanPayload(
                year,
                month,
                shiftId,
                sectorId,
                dayNumbers,
                groups);
        }

        public void SaveMonthlyPlan(
            HaidaiMonthlySaveRequest request,
            IReadOnlyCollection<int>? authorizedSectorIds = null)
        {
            EnsureSchema();

            using var conn = _factory.CreateOpenConnection();
            using var transaction = conn.BeginTransaction();

            var firstDay = new DateTime(request.Year, request.Month, 1);
            var lastDay = firstDay.AddMonths(1).AddDays(-1);

            var distinctOperatorCodes = request.Cells
                .Select(item => item.OperatorCodigoFJ?.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var operatorSectorMap = distinctOperatorCodes.Length == 0
                ? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                : conn.Query<HaidaiOperatorSectorRow>(
                    @"
                        SELECT
                            CodigoFJ,
                            SectorId
                        FROM Operators
                        WHERE CodigoFJ IN @OperatorCodes;",
                    new
                    {
                        OperatorCodes = distinctOperatorCodes
                    },
                    transaction)
                    .ToDictionary(item => item.CodigoFJ, item => item.SectorId, StringComparer.OrdinalIgnoreCase);

            var canUseSharedGdad = CanUseSharedGdad(authorizedSectorIds);
            var localSectorIds = canUseSharedGdad
                && (SupportsSharedGdad(request.SectorId)
                    || (_sectorTopology.Gdad != null && request.SectorId == _sectorTopology.Gdad.Id))
                ? new[] { _sectorTopology.GBareru!.Id, _sectorTopology.Dad!.Id }
                : new[] { request.SectorId };

            var locals = conn.Query<HaidaiLocalLookup>(
                @"
                    SELECT
                        Id,
                        SectorId,
                        COALESCE(NULLIF(NamePt, ''), NULLIF(NameJp, ''), 'Local ' || Id) AS Name,
                        COALESCE(NULLIF(ShortCode, ''), NULLIF(NamePt, ''), NULLIF(NameJp, ''), 'L' || Id) AS ShortCode
                    FROM Locals
                    WHERE SectorId IN @SectorIds;",
                new
                {
                    SectorIds = localSectorIds
                },
                transaction)
                .ToList();

            var existingAssignments = distinctOperatorCodes.Length == 0
                ? new List<HaidaiAssignmentRecord>()
                : conn.Query<HaidaiAssignmentRecord>(
                @"
                    SELECT
                        Id,
                        ScheduleDate,
                        ShiftId,
                        SectorId,
                        OperatorCodigoFJ,
                        LocalId,
                        COALESCE(AssignmentCode, '') AS AssignmentCode,
                        COALESCE(PairKey, '') AS PairKey,
                        COALESCE(IsTrainee, 0) AS IsTrainee,
                        COALESCE(TrainerCodigoFJ, '') AS TrainerCodigoFJ,
                        COALESCE(CountsTowardKousu, 1) AS CountsTowardKousu,
                        COALESCE(IsLineupActive, 1) AS IsLineupActive,
                        COALESCE(IsHolidayWork, 0) AS IsHolidayWork,
                        COALESCE(AvailabilityStatus, '') AS AvailabilityStatus,
                        COALESCE(Notes, '') AS Notes
                    FROM HaidaiAssignments
                    WHERE ShiftId = @ShiftId
                      AND OperatorCodigoFJ IN @OperatorCodes
                      AND date(ScheduleDate) BETWEEN date(@StartDate) AND date(@EndDate);",
                new
                {
                    request.ShiftId,
                    OperatorCodes = distinctOperatorCodes,
                    StartDate = firstDay.ToString("yyyy-MM-dd"),
                    EndDate = lastDay.ToString("yyyy-MM-dd")
                },
                transaction)
                .ToList();

            var existingMap = existingAssignments.ToDictionary(
                item => $"{item.OperatorCodigoFJ}|{item.ScheduleDate}",
                item => item,
                StringComparer.OrdinalIgnoreCase);

            var exceptionMap = conn.Query<HaidaiMonthlyExceptionRecord>(
                @"
                    SELECT
                        a.OperatorCodigoFJ,
                        date(a.RequestDate) AS RequestDate,
                        a.TodokeMotivoId AS MotiveId
                    FROM AcompYukyu a
                    INNER JOIN (
                        SELECT
                            OperatorCodigoFJ,
                            date(RequestDate) AS RequestDay,
                            MAX(Id) AS MaxId
                        FROM AcompYukyu
                        WHERE date(RequestDate) BETWEEN date(@StartDate) AND date(@EndDate)
                          AND TodokeMotivoId IN (1, 2)
                        GROUP BY OperatorCodigoFJ, date(RequestDate)
                    ) latest ON latest.MaxId = a.Id;",
                new
                {
                    StartDate = firstDay.ToString("yyyy-MM-dd"),
                    EndDate = lastDay.ToString("yyyy-MM-dd")
                },
                transaction)
                .ToDictionary(
                    item => $"{item.OperatorCodigoFJ}|{item.RequestDate}",
                    item => item,
                    StringComparer.OrdinalIgnoreCase);

            foreach (var cell in request.Cells)
            {
                if (cell.Day < 1 || cell.Day > lastDay.Day)
                {
                    continue;
                }

                var date = new DateTime(request.Year, request.Month, cell.Day);
                var dateIso = date.ToString("yyyy-MM-dd");
                var key = $"{cell.OperatorCodigoFJ}|{dateIso}";
                existingMap.TryGetValue(key, out var existing);
                exceptionMap.TryGetValue(key, out var exception);

                var homeSectorId = operatorSectorMap.TryGetValue(cell.OperatorCodigoFJ, out var foundSectorId)
                    ? foundSectorId
                    : request.SectorId;

                if (!IsOperatorAllowedInView(request.SectorId, homeSectorId, authorizedSectorIds))
                    throw new UnauthorizedAccessException("Operador fora do escopo setorial do Haidai.");

                var allowedLocalSectorIds = GetSelectableLocalSectorIds(request.SectorId, homeSectorId);
                var localsByCode = locals
                    .Where(item => allowedLocalSectorIds.Contains(item.SectorId))
                    .GroupBy(item => item.ShortCode, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key.Trim(), group => group.First(), StringComparer.OrdinalIgnoreCase);

                var resolved = ResolveMonthlyCell(cell.AssignmentCode, cell.IsHolidayWork, localsByCode, existing, exception);
                var storageSectorId = ResolveStorageSectorId(request.SectorId, homeSectorId);

                try
                {
                    UpsertMonthlyAssignmentInternal(
                        conn,
                        transaction,
                        date,
                        request.ShiftId,
                        storageSectorId,
                        cell.OperatorCodigoFJ,
                        resolved,
                        existing);
                }
                catch (Exception ex)
                {
                    LogHaidaiSave(
                        date,
                        request.ShiftId,
                        resolved.LocalId,
                        resolved.AssignmentCode,
                        cell.OperatorCodigoFJ,
                        existing?.Id,
                        existing?.IsHolidayWork ?? false,
                        resolved.IsHolidayWork,
                        existing == null ? "Insert" : "Update",
                        saved: false,
                        error: ex.Message);
                    throw;
                }
            }

            transaction.Commit();
        }

        private static HaidaiResolvedMonthlyCell ResolveMonthlyCell(
            string? rawAssignmentCode,
            bool isHolidayWork,
            IReadOnlyDictionary<string, HaidaiLocalLookup> localsByCode,
            HaidaiAssignmentRecord? existing,
            HaidaiMonthlyExceptionRecord? exception)
        {
            var normalized = (rawAssignmentCode ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return new HaidaiResolvedMonthlyCell(
                    null,
                    string.Empty,
                    false,
                    false,
                    false,
                    exception == null ? string.Empty : ResolveExceptionStatus(exception.MotiveId));
            }

            if (IsOffDayCode(normalized))
            {
                return new HaidaiResolvedMonthlyCell(null, "\u4f11", false, false, false, "Folga");
            }

            if (IsDisplayExceptionCode(normalized) && existing != null)
            {
                return new HaidaiResolvedMonthlyCell(
                    existing.LocalId,
                    existing.AssignmentCode,
                    existing.IsTrainee,
                    exception == null ? existing.IsLineupActive : false,
                    false,
                    exception == null ? existing.AvailabilityStatus : ResolveExceptionStatus(exception.MotiveId));
            }

            var isTrainee = normalized.EndsWith("#", StringComparison.Ordinal);
            var localCode = isTrainee
                ? normalized[..^1].Trim()
                : normalized;

            int? localId = null;
            if (!string.IsNullOrWhiteSpace(localCode) &&
                localsByCode.TryGetValue(localCode, out var local))
            {
                localId = local.Id;
            }
            else
            {
                localId = existing?.LocalId;
            }

            return new HaidaiResolvedMonthlyCell(
                localId,
                normalized,
                isTrainee,
                exception == null,
                exception == null && isHolidayWork,
                exception == null ? "Escalado" : ResolveExceptionStatus(exception.MotiveId));
        }

        private static string ResolveMonthlyPlannerCode(HaidaiAssignmentRecord? record)
        {
            if (record == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(record.AssignmentCode))
            {
                return record.AssignmentCode.Trim();
            }

            return string.Equals(record.AvailabilityStatus, "Folga", StringComparison.OrdinalIgnoreCase)
                ? "\u4f11"
                : string.Empty;
        }

        private static bool IsDisplayExceptionCode(string value)
        {
            return string.Equals(value, "\u6b20", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "\u6709", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsOffDayCode(string value)
        {
            return string.Equals(value, "\u4f11", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "folga", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "off", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "-", StringComparison.OrdinalIgnoreCase);
        }

        private bool SupportsSharedGdad(int sectorId)
        {
            return _sectorTopology.SupportsSharedGdad
                && (sectorId == _sectorTopology.GBareru!.Id || sectorId == _sectorTopology.Dad!.Id);
        }

        private bool CanUseSharedGdad(IReadOnlyCollection<int>? authorizedSectorIds)
        {
            if (!_sectorTopology.SupportsSharedGdad)
                return false;

            return authorizedSectorIds == null
                || (authorizedSectorIds.Contains(_sectorTopology.GBareru!.Id)
                    && authorizedSectorIds.Contains(_sectorTopology.Dad!.Id)
                    && authorizedSectorIds.Contains(_sectorTopology.Gdad!.Id));
        }

        private bool IsOperatorAllowedInView(
            int requestedSectorId,
            int operatorHomeSectorId,
            IReadOnlyCollection<int>? authorizedSectorIds)
        {
            if (authorizedSectorIds != null && !authorizedSectorIds.Contains(operatorHomeSectorId))
                return false;

            return operatorHomeSectorId == requestedSectorId
                || (CanUseSharedGdad(authorizedSectorIds)
                    && SupportsSharedGdad(requestedSectorId)
                    && operatorHomeSectorId == _sectorTopology.Gdad!.Id);
        }

        private int ResolveStorageSectorId(int requestedSectorId, int operatorHomeSectorId)
        {
            return _sectorTopology.Gdad != null && operatorHomeSectorId == _sectorTopology.Gdad.Id
                ? _sectorTopology.Gdad.Id
                : requestedSectorId;
        }

        private int[] GetSelectableLocalSectorIds(int requestedSectorId, int operatorHomeSectorId)
        {
            if (_sectorTopology.SupportsSharedGdad
                && (operatorHomeSectorId == _sectorTopology.Gdad!.Id || requestedSectorId == _sectorTopology.Gdad.Id))
            {
                return new[] { _sectorTopology.GBareru!.Id, _sectorTopology.Dad!.Id };
            }

            return new[] { requestedSectorId };
        }

        private HaidaiLocalLookup? ResolveDisplayLocal(
            int viewingSectorId,
            int operatorHomeSectorId,
            HaidaiLocalLookup? actualLocal)
        {
            if (actualLocal == null)
            {
                return null;
            }

            if (!_sectorTopology.SupportsSharedGdad
                || operatorHomeSectorId != _sectorTopology.Gdad!.Id
                || !SupportsSharedGdad(viewingSectorId)
                || actualLocal.SectorId == viewingSectorId)
            {
                return actualLocal;
            }

            var remoteSector = actualLocal.SectorId == _sectorTopology.GBareru!.Id
                ? _sectorTopology.GBareru
                : actualLocal.SectorId == _sectorTopology.Dad!.Id
                    ? _sectorTopology.Dad
                    : null;

            return remoteSector == null
                ? actualLocal
                : new HaidaiLocalLookup
                {
                    Id = 0,
                    SectorId = viewingSectorId,
                    Name = remoteSector.Name,
                    ShortCode = remoteSector.Code
                };
        }

        private static HaidaiSectorTopology LoadSectorTopology(System.Data.IDbConnection conn)
        {
            var sectors = conn.Query<HaidaiSectorReference>(
                @"
                    SELECT
                        Id,
                        COALESCE(Code, '') AS Code,
                        COALESCE(NULLIF(NamePt, ''), NULLIF(NameJp, ''), Code, 'Setor ' || Id) AS Name
                    FROM Sectors
                    WHERE COALESCE(IsActive, 1) = 1;")
                .ToList();

            HaidaiSectorReference? Find(string code)
            {
                var normalizedCode = NormalizeSectorCode(code);
                return sectors.FirstOrDefault(item => NormalizeSectorCode(item.Code) == normalizedCode);
            }

            return new HaidaiSectorTopology(
                Find(GBareruSectorCode),
                Find(DadSectorCode),
                Find(GdadSectorCode));
        }

        private static string NormalizeSectorCode(string? value)
        {
            return new string((value ?? string.Empty)
                .Where(char.IsLetterOrDigit)
                .Select(char.ToUpperInvariant)
                .ToArray());
        }

        private static int ResolveOperatorHomeSectorId(
            System.Data.IDbConnection conn,
            string operatorCodigoFJ,
            int fallbackSectorId,
            System.Data.IDbTransaction? transaction = null)
        {
            return conn.ExecuteScalar<int?>(
                @"
                    SELECT SectorId
                    FROM Operators
                    WHERE CodigoFJ = @CodigoFJ
                    LIMIT 1;",
                new
                {
                    CodigoFJ = operatorCodigoFJ?.Trim()
                },
                transaction) ?? fallbackSectorId;
        }

        public HaidaiBoardPayload GetBoard(
            DateTime date,
            int shiftId,
            int sectorId,
            IReadOnlyCollection<int>? authorizedSectorIds = null)
        {
            EnsureSchema();

            using var conn = _factory.CreateOpenConnection();
            var includeSharedGdad = CanUseSharedGdad(authorizedSectorIds) && SupportsSharedGdad(sectorId);

            var allLocals = conn.Query<HaidaiLocalLookup>(
                @"
                    SELECT
                        Id,
                        SectorId,
                        COALESCE(NULLIF(NamePt, ''), NULLIF(NameJp, ''), 'Local ' || Id) AS Name,
                        COALESCE(NULLIF(ShortCode, ''), NULLIF(NamePt, ''), NULLIF(NameJp, ''), 'L' || Id) AS ShortCode
                    FROM Locals
                    ORDER BY Id;",
                new { })
                .ToList();

            var localsById = allLocals.ToDictionary(item => item.Id);

            var operators = conn.Query<HaidaiOperatorRow>(
                @"
                    SELECT
                        o.CodigoFJ,
                        COALESCE(NULLIF(o.NameRomanji, ''), o.CodigoFJ) AS Name,
                        COALESCE(NULLIF(o.NameNihongo, ''), COALESCE(NULLIF(o.NameRomanji, ''), o.CodigoFJ)) AS NameJp,
                        o.SectorId AS HomeSectorId,
                        o.GroupId,
                        COALESCE(NULLIF(g.NamePt, ''), NULLIF(g.NameJp, ''), 'Grupo ' || o.GroupId) AS GroupName,
                        COALESCE(o.Trainer, 0) AS Trainer,
                        COALESCE(o.IsLeader, 0) AS IsLeader,
                        COALESCE(u.AccessLevel, CASE WHEN COALESCE(o.IsLeader, 0) = 1 THEN 3 ELSE 1 END) AS AccessLevel
                    FROM Operators o
                    LEFT JOIN Groups g ON g.Id = o.GroupId
                    LEFT JOIN Users u ON u.CodigoFJ = o.CodigoFJ
                    WHERE COALESCE(o.Status, 1) = 1
                      AND o.ShiftId = @ShiftId
                      AND (
                            o.SectorId = @SectorId
                         OR (@IncludeSharedGdad = 1 AND o.SectorId = @GdadSectorId)
                      )
                    ORDER BY o.GroupId,
                             CASE WHEN COALESCE(o.IsLeader, 0) = 1 THEN 1 ELSE 0 END,
                             o.NameRomanji,
                             o.CodigoFJ;",
                new
                {
                    SectorId = sectorId,
                    ShiftId = shiftId,
                    IncludeSharedGdad = includeSharedGdad ? 1 : 0,
                    GdadSectorId = _sectorTopology.Gdad?.Id ?? -1
                })
                .ToList();

            var operatorCodes = operators
                .Select(item => item.CodigoFJ)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var assignments = operatorCodes.Length == 0
                ? new Dictionary<string, HaidaiAssignmentRecord>(StringComparer.OrdinalIgnoreCase)
                : conn.Query<HaidaiAssignmentRecord>(
                @"
                    SELECT
                        Id,
                        ScheduleDate,
                        ShiftId,
                        SectorId,
                        OperatorCodigoFJ,
                        LocalId,
                        COALESCE(AssignmentCode, '') AS AssignmentCode,
                        COALESCE(PairKey, '') AS PairKey,
                        COALESCE(IsTrainee, 0) AS IsTrainee,
                        COALESCE(TrainerCodigoFJ, '') AS TrainerCodigoFJ,
                        COALESCE(CountsTowardKousu, 1) AS CountsTowardKousu,
                        COALESCE(IsLineupActive, 1) AS IsLineupActive,
                        COALESCE(IsHolidayWork, 0) AS IsHolidayWork,
                        COALESCE(AvailabilityStatus, '') AS AvailabilityStatus,
                        COALESCE(Notes, '') AS Notes
                    FROM HaidaiAssignments
                    WHERE date(ScheduleDate) = date(@ScheduleDate)
                      AND ShiftId = @ShiftId
                      AND OperatorCodigoFJ IN @OperatorCodes;",
                new
                {
                    ScheduleDate = date.ToString("yyyy-MM-dd"),
                    ShiftId = shiftId,
                    OperatorCodes = operatorCodes
                })
                .ToDictionary(item => item.OperatorCodigoFJ, StringComparer.OrdinalIgnoreCase);

            var movementMap = operatorCodes.Length == 0
                ? new Dictionary<string, List<HaidaiMovementRecord>>(StringComparer.OrdinalIgnoreCase)
                : conn.Query<HaidaiMovementRecord>(
                @"
                    SELECT
                        Id,
                        OperatorCodigoFJ,
                        COALESCE(MovementType, '') AS MovementType,
                        COALESCE(EventTime, '') AS EventTime,
                        COALESCE(EventDateTime, '') AS EventDateTime,
                        COALESCE(AssignmentCode, '') AS AssignmentCode,
                        COALESCE(PairKey, '') AS PairKey,
                        COALESCE(ReplacementOperatorCodigoFJ, '') AS ReplacementOperatorCodigoFJ,
                        COALESCE(Reason, '') AS Reason,
                        COALESCE(CreatedAt, '') AS CreatedAt
                    FROM HaidaiMovements
                    WHERE date(ScheduleDate) = date(@ScheduleDate)
                      AND ShiftId = @ShiftId
                      AND OperatorCodigoFJ IN @OperatorCodes
                    ORDER BY COALESCE(EventTime, '') DESC, Id DESC;",
                new
                {
                    ScheduleDate = date.ToString("yyyy-MM-dd"),
                    ShiftId = shiftId,
                    OperatorCodes = operatorCodes
                })
                .GroupBy(item => item.OperatorCodigoFJ, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

            var trainerCodes = assignments.Values
                .Select(item => item.TrainerCodigoFJ?.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var trainerNames = trainerCodes.Length == 0
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : conn.Query<TrainerLookupRow>(
                    @"
                        SELECT
                            CodigoFJ,
                            COALESCE(NULLIF(NameRomanji, ''), NULLIF(NameNihongo, ''), CodigoFJ) AS Name
                        FROM Operators
                        WHERE CodigoFJ IN @TrainerCodes;",
                    new
                    {
                        TrainerCodes = trainerCodes
                    })
                    .ToDictionary(item => item.CodigoFJ, item => item.Name, StringComparer.OrdinalIgnoreCase);

            var exceptions = conn.Query<HaidaiExceptionRecord>(
                @"
                    SELECT
                        a.OperatorCodigoFJ,
                        a.TodokeMotivoId AS MotiveId,
                        COALESCE(m.NomePt, '') AS MotiveName,
                        COALESCE(a.Notes, '') AS Notes
                    FROM AcompYukyu a
                    INNER JOIN (
                        SELECT OperatorCodigoFJ, MAX(Id) AS MaxId
                        FROM AcompYukyu
                        WHERE date(RequestDate) = date(@ScheduleDate)
                          AND TodokeMotivoId IN (1, 2)
                        GROUP BY OperatorCodigoFJ
                    ) latest ON latest.MaxId = a.Id
                    LEFT JOIN TodokeMotivo m ON m.Id = a.TodokeMotivoId;",
                new
                {
                    ScheduleDate = date.ToString("yyyy-MM-dd")
                })
                .ToDictionary(item => item.OperatorCodigoFJ, StringComparer.OrdinalIgnoreCase);

            var rows = operators
                .Select(op =>
                {
                    assignments.TryGetValue(op.CodigoFJ, out var assignment);
                    exceptions.TryGetValue(op.CodigoFJ, out var exception);
                    movementMap.TryGetValue(op.CodigoFJ, out var movements);
                    movements ??= new List<HaidaiMovementRecord>();

                    var localId = assignment?.LocalId;
                    HaidaiLocalLookup? local = null;
                    if (localId.HasValue && localsById.TryGetValue(localId.Value, out var foundLocal))
                    {
                        local = foundLocal;
                    }

                    var displayLocal = ResolveDisplayLocal(sectorId, op.HomeSectorId, local);
                    var baseAssignmentCode = ResolveAssignmentCode(assignment, local);
                    var status = ResolveStatus(exception, assignment, baseAssignmentCode);
                    var trainerCode = assignment?.TrainerCodigoFJ ?? string.Empty;
                    var trainerName = !string.IsNullOrWhiteSpace(trainerCode)
                        && trainerNames.TryGetValue(trainerCode, out var foundTrainerName)
                            ? foundTrainerName
                            : string.Empty;
                    var hasSharedDisplayLocal = displayLocal != null
                        && !string.IsNullOrWhiteSpace(displayLocal.ShortCode)
                        && displayLocal.Id != local?.Id;

                    var presentedAssignmentCode = hasSharedDisplayLocal
                        ? displayLocal!.ShortCode
                        : baseAssignmentCode;
                    var displayAssignmentCode = ResolveDisplayAssignmentCode(exception?.MotiveId ?? 0, presentedAssignmentCode);
                    var latestMovement = movements.FirstOrDefault();

                    return new HaidaiBoardRow(
                        op.CodigoFJ,
                        op.Name,
                        op.NameJp,
                        op.GroupId,
                        op.GroupName,
                        op.Trainer,
                        op.IsLeader,
                        op.AccessLevel,
                        local?.Id,
                        local?.Name ?? string.Empty,
                        local?.ShortCode ?? string.Empty,
                        local?.SectorId,
                        displayLocal?.Id,
                        displayLocal?.Name ?? string.Empty,
                        displayLocal?.ShortCode ?? string.Empty,
                        displayLocal?.SectorId,
                        displayAssignmentCode,
                        baseAssignmentCode,
                        assignment?.PairKey ?? string.Empty,
                        assignment?.IsTrainee ?? false,
                        trainerCode,
                        trainerName,
                        assignment?.CountsTowardKousu ?? true,
                        assignment?.IsLineupActive ?? false,
                        assignment?.IsHolidayWork ?? false,
                        assignment?.Notes ?? string.Empty,
                        exception?.MotiveId ?? 0,
                        exception?.MotiveName ?? string.Empty,
                        exception?.Notes ?? string.Empty,
                        status,
                        latestMovement == null ? string.Empty : BuildMovementSummary(latestMovement),
                        movements.Count,
                        movements.Select(movement => new HaidaiBoardMovement(
                            movement.Id,
                            movement.MovementType,
                            movement.EventTime,
                            movement.EventDateTime,
                            movement.AssignmentCode,
                            movement.PairKey,
                            movement.ReplacementOperatorCodigoFJ,
                            movement.Reason,
                            movement.CreatedAt)).ToList());
                })
                .ToList();

            foreach (var row in rows.Take(50))
            {
                Console.WriteLine(
                    "[Haidai][LoadAssignment] "
                    + $"Date={date:yyyy-MM-dd} "
                    + $"ShiftId={shiftId} "
                    + $"OperatorId={row.CodigoFJ} "
                    + $"OperatorName={row.Name} "
                    + $"LocalId={FormatNullableInt(row.LocalId)} "
                    + $"IsHolidayWork={row.IsHolidayWork}");
            }

            var grouped = rows
                .GroupBy(item => new { item.GroupId, item.GroupName })
                .OrderBy(group => group.Key.GroupId)
                .Select(group => new HaidaiGroupBlock(
                    group.Key.GroupId,
                    group.Key.GroupName,
                    group.Count(),
                    group.ToList()))
                .ToList();

            var summary = new HaidaiSummary(
                rows.Count,
                rows.Count(item => IsAssignedInViewingSector(item, sectorId)),
                rows.Count(item => item.ExceptionMotiveId == 1),
                rows.Count(item => item.ExceptionMotiveId == 2),
                rows.Count(item => string.Equals(item.Status, "Atraso", StringComparison.OrdinalIgnoreCase)),
                rows.Count(item => string.Equals(item.Status, "Saiu cedo", StringComparison.OrdinalIgnoreCase)),
                rows.Count(item => item.IsTrainee),
                rows.Select(item => item.PairKey)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count());

            var areaTotals = rows
                .Where(item => IsAssignedInViewingSector(item, sectorId))
                .GroupBy(
                    item => ResolveAreaTotalLabel(item),
                    StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => AreaSortKey(group.Key), StringComparer.OrdinalIgnoreCase)
                .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => new HaidaiAreaTotal(group.Key, group.Count()))
                .ToList();

            return new HaidaiBoardPayload(
                date.ToString("yyyy-MM-dd"),
                shiftId,
                sectorId,
                summary,
                grouped,
                areaTotals);
        }

        public void SaveAssignment(
            HaidaiSaveAssignmentRequest request,
            IReadOnlyCollection<int>? authorizedSectorIds = null)
        {
            EnsureSchema();

            using var conn = _factory.CreateOpenConnection();
            using var transaction = conn.BeginTransaction();

            var operatorHomeSectorId = ResolveOperatorHomeSectorId(
                conn,
                request.OperatorCodigoFJ,
                request.SectorId,
                transaction);
            if (!IsOperatorAllowedInView(request.SectorId, operatorHomeSectorId, authorizedSectorIds))
                throw new UnauthorizedAccessException("Operador fora do escopo setorial do Haidai.");

            var storageSectorId = ResolveStorageSectorId(request.SectorId, operatorHomeSectorId);

            if (request.LocalId.HasValue && request.LocalId.Value > 0)
            {
                var localSectorId = conn.ExecuteScalar<int?>(
                    "SELECT SectorId FROM Locals WHERE Id = @LocalId;",
                    new { request.LocalId },
                    transaction) ?? throw new InvalidOperationException("Local do Haidai nao encontrado.");
                var selectableSectorIds = GetSelectableLocalSectorIds(request.SectorId, operatorHomeSectorId);
                if (!selectableSectorIds.Contains(localSectorId)
                    || (authorizedSectorIds != null && !authorizedSectorIds.Contains(localSectorId)))
                    throw new UnauthorizedAccessException("Local fora do escopo setorial do Haidai.");
            }

            string assignmentCode = request.AssignmentCode;
            if (request.LocalId.HasValue && request.LocalId.Value > 0 && string.IsNullOrWhiteSpace(assignmentCode))
            {
                assignmentCode = conn.ExecuteScalar<string?>(
                    @"
                        SELECT COALESCE(NULLIF(ShortCode, ''), NULLIF(NamePt, ''), NULLIF(NameJp, ''), '')
                        FROM Locals
                        WHERE Id = @LocalId;",
                    new { LocalId = request.LocalId.Value },
                    transaction) ?? string.Empty;
            }

            var normalizedRequest = request with
            {
                SectorId = storageSectorId,
                AssignmentCode = assignmentCode ?? string.Empty
            };

            UpsertAssignmentInternal(conn, transaction, normalizedRequest);

            if (normalizedRequest.ApplyPairToMonth)
            {
                ApplyPairKeyToMonth(conn, transaction, normalizedRequest);
            }

            transaction.Commit();

            Console.WriteLine(
                "[Haidai][SaveAssignment] "
                + $"Date={normalizedRequest.Date:yyyy-MM-dd} "
                + $"ShiftId={normalizedRequest.ShiftId} "
                + $"OperatorId={normalizedRequest.OperatorCodigoFJ} "
                + $"OperatorName={ResolveOperatorName(conn, normalizedRequest.OperatorCodigoFJ)} "
                + $"LocalId={FormatNullableInt(normalizedRequest.LocalId)} "
                + $"IsHolidayWork={normalizedRequest.IsHolidayWork}");
        }

        public void UpsertException(DateTime date, string operatorCodigoFJ, int motiveId, string notes, string authorizedByCodigoFJ)
        {
            EnsureSchema();

            using var conn = _factory.CreateOpenConnection();
            using var transaction = conn.BeginTransaction();

            var normalizedDate = date.ToString("yyyy-MM-dd");
            UpsertPendingTodokeRequest(
                conn,
                transaction,
                operatorCodigoFJ,
                normalizedDate,
                motiveId,
                notes,
                authorizedByCodigoFJ,
                replaceExceptionMotives: true);

            conn.Execute(
                @"
                    UPDATE HaidaiAssignments
                    SET IsLineupActive = 0,
                        AvailabilityStatus = @AvailabilityStatus,
                        UpdatedAt = CURRENT_TIMESTAMP
                    WHERE OperatorCodigoFJ = @OperatorCodigoFJ
                      AND ShiftId = @ShiftId
                      AND SectorId = @SectorId
                      AND date(ScheduleDate) = date(@ScheduleDate);",
                new
                {
                    AvailabilityStatus = motiveId == 1 ? "Yukyu" : "Falta",
                    OperatorCodigoFJ = operatorCodigoFJ,
                    ShiftId = conn.ExecuteScalar<int?>(
                        @"SELECT ShiftId
                          FROM HaidaiAssignments
                          WHERE OperatorCodigoFJ = @OperatorCodigoFJ
                            AND date(ScheduleDate) = date(@ScheduleDate)
                          ORDER BY Id DESC
                          LIMIT 1;",
                        new
                        {
                            OperatorCodigoFJ = operatorCodigoFJ,
                            ScheduleDate = normalizedDate
                        },
                        transaction) ?? 0,
                    SectorId = conn.ExecuteScalar<int?>(
                        @"SELECT SectorId
                          FROM HaidaiAssignments
                          WHERE OperatorCodigoFJ = @OperatorCodigoFJ
                            AND date(ScheduleDate) = date(@ScheduleDate)
                          ORDER BY Id DESC
                          LIMIT 1;",
                        new
                        {
                            OperatorCodigoFJ = operatorCodigoFJ,
                            ScheduleDate = normalizedDate
                        },
                        transaction) ?? 0,
                    ScheduleDate = normalizedDate
                },
                transaction);

            transaction.Commit();
        }

        public void ClearException(DateTime date, string operatorCodigoFJ)
        {
            EnsureSchema();

            using var conn = _factory.CreateOpenConnection();
            using var transaction = conn.BeginTransaction();

            var ids = conn.Query<long>(
                @"
                    SELECT Id
                    FROM AcompYukyu
                    WHERE OperatorCodigoFJ = @OperatorCodigoFJ
                      AND date(RequestDate) = date(@RequestDate)
                      AND TodokeMotivoId IN (1, 2);",
                new
                {
                    OperatorCodigoFJ = operatorCodigoFJ,
                    RequestDate = date.ToString("yyyy-MM-dd")
                },
                transaction)
                .ToList();

            if (ids.Count == 0)
            {
                transaction.Commit();
                return;
            }

            DeleteAcompYukyuCascade(conn, transaction, ids);
            conn.Execute(
                @"
                    UPDATE HaidaiAssignments
                    SET IsLineupActive = CASE
                            WHEN COALESCE(AssignmentCode, '') = '' AND COALESCE(LocalId, 0) = 0 THEN 0
                            ELSE 1
                        END,
                        AvailabilityStatus = CASE
                            WHEN COALESCE(AssignmentCode, '') = '' AND COALESCE(LocalId, 0) = 0 THEN NULL
                            ELSE 'Escalado'
                        END,
                        UpdatedAt = CURRENT_TIMESTAMP
                    WHERE OperatorCodigoFJ = @OperatorCodigoFJ
                      AND date(ScheduleDate) = date(@RequestDate);",
                new
                {
                    OperatorCodigoFJ = operatorCodigoFJ,
                    RequestDate = date.ToString("yyyy-MM-dd")
                },
                transaction);

            transaction.Commit();
        }

        public void RegisterMovement(HaidaiMovementRequest request)
        {
            EnsureSchema();

            using var conn = _factory.CreateOpenConnection();
            using var transaction = conn.BeginTransaction();

            var normalizedDate = request.Date.ToString("yyyy-MM-dd");
            var storageSectorId = ResolveStorageSectorId(
                request.SectorId,
                ResolveOperatorHomeSectorId(conn, request.OperatorCodigoFJ, request.SectorId, transaction));
            var assignment = conn.QueryFirstOrDefault<HaidaiAssignmentRecord>(
                @"
                    SELECT
                        Id,
                        ScheduleDate,
                        ShiftId,
                        SectorId,
                        OperatorCodigoFJ,
                        LocalId,
                        COALESCE(AssignmentCode, '') AS AssignmentCode,
                        COALESCE(PairKey, '') AS PairKey,
                        COALESCE(IsTrainee, 0) AS IsTrainee,
                        COALESCE(TrainerCodigoFJ, '') AS TrainerCodigoFJ,
                        COALESCE(CountsTowardKousu, 1) AS CountsTowardKousu,
                        COALESCE(IsLineupActive, 1) AS IsLineupActive,
                        COALESCE(IsHolidayWork, 0) AS IsHolidayWork,
                        COALESCE(AvailabilityStatus, '') AS AvailabilityStatus,
                        COALESCE(Notes, '') AS Notes
                    FROM HaidaiAssignments
                    WHERE OperatorCodigoFJ = @OperatorCodigoFJ
                      AND ShiftId = @ShiftId
                      AND SectorId = @SectorId
                      AND date(ScheduleDate) = date(@ScheduleDate)
                    LIMIT 1;",
                new
                {
                    request.OperatorCodigoFJ,
                    request.ShiftId,
                    SectorId = storageSectorId,
                    ScheduleDate = normalizedDate
                },
                transaction);

            var localId = assignment?.LocalId ?? request.LocalId;
            var assignmentCode = string.IsNullOrWhiteSpace(assignment?.AssignmentCode)
                ? request.AssignmentCode
                : assignment.AssignmentCode;
            var pairKey = string.IsNullOrWhiteSpace(assignment?.PairKey)
                ? request.PairKey
                : assignment.PairKey;
            var replacementOperatorCodigoFJ = request.ReplacementOperatorCodigoFJ.Trim();
            var existingMovement = request.MovementId.HasValue
                ? conn.QueryFirstOrDefault<HaidaiMovementRecord>(
                    @"
                        SELECT
                            Id,
                            OperatorCodigoFJ,
                            COALESCE(MovementType, '') AS MovementType,
                            COALESCE(EventTime, '') AS EventTime,
                            COALESCE(EventDateTime, '') AS EventDateTime,
                            COALESCE(AssignmentCode, '') AS AssignmentCode,
                            COALESCE(PairKey, '') AS PairKey,
                            COALESCE(ReplacementOperatorCodigoFJ, '') AS ReplacementOperatorCodigoFJ,
                            COALESCE(Reason, '') AS Reason,
                            COALESCE(CreatedAt, '') AS CreatedAt
                        FROM HaidaiMovements
                        WHERE Id = @Id
                          AND OperatorCodigoFJ = @OperatorCodigoFJ
                          AND ShiftId = @ShiftId
                          AND SectorId = @SectorId
                          AND date(ScheduleDate) = date(@ScheduleDate)
                        LIMIT 1;",
                    new
                    {
                        Id = request.MovementId.Value,
                        request.OperatorCodigoFJ,
                        request.ShiftId,
                        SectorId = storageSectorId,
                        ScheduleDate = normalizedDate
                    },
                    transaction)
                : null;

            var eventDateTime = ResolveMovementEventDateTime(conn, transaction, request.Date, request.ShiftId, request.EventTime);

            if (existingMovement != null)
            {
                conn.Execute(
                    @"
                        UPDATE HaidaiMovements
                        SET MovementType = @MovementType,
                            EventTime = @EventTime,
                            EventDateTime = @EventDateTime,
                            LocalId = @LocalId,
                            AssignmentCode = @AssignmentCode,
                            PairKey = @PairKey,
                            ReplacementOperatorCodigoFJ = @ReplacementOperatorCodigoFJ,
                            Reason = @Reason
                        WHERE Id = @Id;",
                    new
                    {
                        Id = existingMovement.Id,
                        request.MovementType,
                        EventTime = NullIfWhiteSpace(request.EventTime),
                        EventDateTime = eventDateTime,
                        LocalId = localId,
                        AssignmentCode = NullIfWhiteSpace(assignmentCode),
                        PairKey = NullIfWhiteSpace(pairKey),
                        ReplacementOperatorCodigoFJ = NullIfWhiteSpace(replacementOperatorCodigoFJ),
                        Reason = NullIfWhiteSpace(request.Reason)
                    },
                    transaction);
            }
            else
            {
                conn.Execute(
                    @"
                        INSERT INTO HaidaiMovements (
                            ScheduleDate,
                            ShiftId,
                            SectorId,
                            OperatorCodigoFJ,
                            MovementType,
                            EventTime,
                            EventDateTime,
                            LocalId,
                            AssignmentCode,
                            PairKey,
                            ReplacementOperatorCodigoFJ,
                            Reason,
                            CreatedByCodigoFJ
                        )
                        VALUES (
                            @ScheduleDate,
                            @ShiftId,
                            @SectorId,
                            @OperatorCodigoFJ,
                            @MovementType,
                            @EventTime,
                            @EventDateTime,
                            @LocalId,
                            @AssignmentCode,
                            @PairKey,
                            @ReplacementOperatorCodigoFJ,
                            @Reason,
                            @CreatedByCodigoFJ
                        );",
                    new
                    {
                        ScheduleDate = normalizedDate,
                        request.ShiftId,
                        SectorId = storageSectorId,
                        request.OperatorCodigoFJ,
                        request.MovementType,
                        EventTime = NullIfWhiteSpace(request.EventTime),
                        EventDateTime = eventDateTime,
                        LocalId = localId,
                        AssignmentCode = NullIfWhiteSpace(assignmentCode),
                        PairKey = NullIfWhiteSpace(pairKey),
                        ReplacementOperatorCodigoFJ = NullIfWhiteSpace(replacementOperatorCodigoFJ),
                        Reason = NullIfWhiteSpace(request.Reason),
                        request.CreatedByCodigoFJ
                    },
                    transaction);
            }

            if (existingMovement != null
                && !string.IsNullOrWhiteSpace(existingMovement.ReplacementOperatorCodigoFJ)
                && !string.Equals(existingMovement.ReplacementOperatorCodigoFJ.Trim(), replacementOperatorCodigoFJ, StringComparison.OrdinalIgnoreCase))
            {
                RemoveDerivedReplacementAssignment(
                    conn,
                    transaction,
                    request.Date,
                    request.ShiftId,
                    request.SectorId,
                    request.OperatorCodigoFJ,
                    existingMovement.ReplacementOperatorCodigoFJ.Trim());
            }

            var movementTodokeNotes = BuildMovementTodokeNotes(
                request.MovementType,
                request.EventTime,
                request.Reason,
                assignmentCode,
                replacementOperatorCodigoFJ);

            UpsertPendingTodokeRequest(
                conn,
                transaction,
                request.OperatorCodigoFJ,
                normalizedDate,
                ResolveMovementTodokeMotiveId(request.MovementType),
                movementTodokeNotes,
                request.CreatedByCodigoFJ,
                replaceMovementMotives: true);

            if (assignment != null)
            {
                conn.Execute(
                    @"
                        UPDATE HaidaiAssignments
                        SET IsLineupActive = 0,
                            AvailabilityStatus = @AvailabilityStatus,
                            UpdatedAt = CURRENT_TIMESTAMP
                        WHERE Id = @Id;",
                    new
                    {
                        Id = assignment.Id,
                        AvailabilityStatus = request.MovementType == "late" ? "Atraso" : "Saiu cedo"
                    },
                    transaction);
            }

            if (!string.IsNullOrWhiteSpace(replacementOperatorCodigoFJ))
            {
                UpsertAssignmentInternal(
                    conn,
                    transaction,
                    new HaidaiSaveAssignmentRequest(
                        request.Date,
                        request.ShiftId,
                        ResolveStorageSectorId(
                            request.SectorId,
                            ResolveOperatorHomeSectorId(conn, replacementOperatorCodigoFJ, request.SectorId, transaction)),
                        replacementOperatorCodigoFJ,
                        localId,
                        assignmentCode ?? string.Empty,
                        pairKey ?? string.Empty,
                        false,
                        string.Empty,
                        true,
                        BuildReplacementNote(request.OperatorCodigoFJ, request.MovementType, request.EventTime, request.Reason),
                        false));
            }

            transaction.Commit();
        }

        public void DeleteMovement(DateTime date, int shiftId, int sectorId, string operatorCodigoFJ, int movementId)
        {
            EnsureSchema();

            using var conn = _factory.CreateOpenConnection();
            using var transaction = conn.BeginTransaction();

            var normalizedDate = date.ToString("yyyy-MM-dd");
            var storageSectorId = ResolveStorageSectorId(
                sectorId,
                ResolveOperatorHomeSectorId(conn, operatorCodigoFJ, sectorId, transaction));
            var movement = conn.QueryFirstOrDefault<HaidaiMovementRecord>(
                @"
                    SELECT
                        Id,
                        OperatorCodigoFJ,
                        COALESCE(MovementType, '') AS MovementType,
                        COALESCE(EventTime, '') AS EventTime,
                        COALESCE(EventDateTime, '') AS EventDateTime,
                        COALESCE(AssignmentCode, '') AS AssignmentCode,
                        COALESCE(PairKey, '') AS PairKey,
                        COALESCE(ReplacementOperatorCodigoFJ, '') AS ReplacementOperatorCodigoFJ,
                        COALESCE(Reason, '') AS Reason,
                        COALESCE(CreatedAt, '') AS CreatedAt
                    FROM HaidaiMovements
                    WHERE Id = @Id
                      AND OperatorCodigoFJ = @OperatorCodigoFJ
                      AND ShiftId = @ShiftId
                      AND SectorId = @SectorId
                      AND date(ScheduleDate) = date(@ScheduleDate)
                    LIMIT 1;",
                new
                {
                    Id = movementId,
                    OperatorCodigoFJ = operatorCodigoFJ,
                    ShiftId = shiftId,
                    SectorId = storageSectorId,
                    ScheduleDate = normalizedDate
                },
                transaction);

            if (movement == null)
            {
                transaction.Commit();
                return;
            }

            conn.Execute(
                @"DELETE FROM HaidaiMovements WHERE Id = @Id;",
                new { Id = movementId },
                transaction);

            if (!string.IsNullOrWhiteSpace(movement.ReplacementOperatorCodigoFJ))
            {
                RemoveDerivedReplacementAssignment(
                    conn,
                    transaction,
                    date,
                    shiftId,
                    sectorId,
                    operatorCodigoFJ,
                    movement.ReplacementOperatorCodigoFJ.Trim());
            }

            var latestMovement = conn.QueryFirstOrDefault<HaidaiMovementRecord>(
                @"
                    SELECT
                        Id,
                        OperatorCodigoFJ,
                        COALESCE(MovementType, '') AS MovementType,
                        COALESCE(EventTime, '') AS EventTime,
                        COALESCE(EventDateTime, '') AS EventDateTime,
                        COALESCE(AssignmentCode, '') AS AssignmentCode,
                        COALESCE(PairKey, '') AS PairKey,
                        COALESCE(ReplacementOperatorCodigoFJ, '') AS ReplacementOperatorCodigoFJ,
                        COALESCE(Reason, '') AS Reason,
                        COALESCE(CreatedAt, '') AS CreatedAt
                    FROM HaidaiMovements
                    WHERE OperatorCodigoFJ = @OperatorCodigoFJ
                      AND ShiftId = @ShiftId
                      AND SectorId = @SectorId
                      AND date(ScheduleDate) = date(@ScheduleDate)
                    ORDER BY COALESCE(EventTime, '') DESC, Id DESC
                    LIMIT 1;",
                new
                {
                    OperatorCodigoFJ = operatorCodigoFJ,
                    ShiftId = shiftId,
                    SectorId = storageSectorId,
                    ScheduleDate = normalizedDate
                },
                transaction);

            if (latestMovement == null)
            {
                var movementIds = conn.Query<long>(
                    @"
                        SELECT Id
                        FROM AcompYukyu
                        WHERE OperatorCodigoFJ = @OperatorCodigoFJ
                          AND date(RequestDate) = date(@RequestDate)
                          AND TodokeMotivoId IN (3, 5);",
                    new
                    {
                        OperatorCodigoFJ = operatorCodigoFJ,
                        RequestDate = normalizedDate
                    },
                    transaction)
                    .ToList();

                if (movementIds.Count > 0)
                {
                    DeleteAcompYukyuCascade(conn, transaction, movementIds);
                }

                conn.Execute(
                    @"
                        UPDATE HaidaiAssignments
                        SET IsLineupActive = CASE
                                WHEN COALESCE(AssignmentCode, '') = '' AND COALESCE(LocalId, 0) = 0 THEN 0
                                ELSE 1
                            END,
                            AvailabilityStatus = CASE
                                WHEN COALESCE(AssignmentCode, '') = '' AND COALESCE(LocalId, 0) = 0 THEN NULL
                                ELSE 'Escalado'
                            END,
                            UpdatedAt = CURRENT_TIMESTAMP
                        WHERE OperatorCodigoFJ = @OperatorCodigoFJ
                          AND ShiftId = @ShiftId
                          AND SectorId = @SectorId
                          AND date(ScheduleDate) = date(@ScheduleDate);",
                    new
                    {
                        OperatorCodigoFJ = operatorCodigoFJ,
                        ShiftId = shiftId,
                        SectorId = storageSectorId,
                        ScheduleDate = normalizedDate
                    },
                    transaction);
            }
            else
            {
                var latestNotes = BuildMovementTodokeNotes(
                    latestMovement.MovementType,
                    latestMovement.EventTime,
                    latestMovement.Reason,
                    latestMovement.AssignmentCode,
                    latestMovement.ReplacementOperatorCodigoFJ);

                UpsertPendingTodokeRequest(
                    conn,
                    transaction,
                    operatorCodigoFJ,
                    normalizedDate,
                    ResolveMovementTodokeMotiveId(latestMovement.MovementType),
                    latestNotes,
                    string.Empty,
                    replaceMovementMotives: true);

                conn.Execute(
                    @"
                        UPDATE HaidaiAssignments
                        SET IsLineupActive = 0,
                            AvailabilityStatus = @AvailabilityStatus,
                            UpdatedAt = CURRENT_TIMESTAMP
                        WHERE OperatorCodigoFJ = @OperatorCodigoFJ
                          AND ShiftId = @ShiftId
                          AND SectorId = @SectorId
                          AND date(ScheduleDate) = date(@ScheduleDate);",
                    new
                    {
                        OperatorCodigoFJ = operatorCodigoFJ,
                        ShiftId = shiftId,
                        SectorId = storageSectorId,
                        ScheduleDate = normalizedDate,
                        AvailabilityStatus = latestMovement.MovementType == "late" ? "Atraso" : "Saiu cedo"
                    },
                    transaction);

                if (!string.IsNullOrWhiteSpace(latestMovement.ReplacementOperatorCodigoFJ))
                {
                    UpsertAssignmentInternal(
                        conn,
                        transaction,
                        new HaidaiSaveAssignmentRequest(
                            date,
                            shiftId,
                            ResolveStorageSectorId(
                                sectorId,
                                ResolveOperatorHomeSectorId(conn, latestMovement.ReplacementOperatorCodigoFJ.Trim(), sectorId, transaction)),
                            latestMovement.ReplacementOperatorCodigoFJ.Trim(),
                            null,
                            latestMovement.AssignmentCode,
                            latestMovement.PairKey,
                            false,
                            string.Empty,
                            true,
                            BuildReplacementNote(operatorCodigoFJ, latestMovement.MovementType, latestMovement.EventTime, latestMovement.Reason),
                            false));
                }
            }

            transaction.Commit();
        }

        public void RestoreLineup(DateTime date, int shiftId, int sectorId, string operatorCodigoFJ)
        {
            EnsureSchema();

            using var conn = _factory.CreateOpenConnection();
            var storageSectorId = ResolveStorageSectorId(
                sectorId,
                ResolveOperatorHomeSectorId(conn, operatorCodigoFJ, sectorId));
            conn.Execute(
                @"
                    UPDATE HaidaiAssignments
                    SET IsLineupActive = 1,
                        AvailabilityStatus = 'Escalado',
                        UpdatedAt = CURRENT_TIMESTAMP
                    WHERE OperatorCodigoFJ = @OperatorCodigoFJ
                      AND ShiftId = @ShiftId
                      AND SectorId = @SectorId
                      AND date(ScheduleDate) = date(@ScheduleDate);",
                new
                {
                    OperatorCodigoFJ = operatorCodigoFJ,
                    ShiftId = shiftId,
                    SectorId = storageSectorId,
                    ScheduleDate = date.ToString("yyyy-MM-dd")
                });
        }

        public IReadOnlyList<HaidaiPlannedAssignment> GetActiveAssignments(DateTime date, int shiftId)
        {
            EnsureSchema();

            using var conn = _factory.CreateOpenConnection();
            return conn.Query<HaidaiPlannedAssignment>(
                @"
                    SELECT
                        OperatorCodigoFJ,
                        ShiftId,
                        SectorId,
                        LocalId,
                        COALESCE(AssignmentCode, '') AS AssignmentCode,
                        COALESCE(PairKey, '') AS PairKey,
                        COALESCE(IsTrainee, 0) AS IsTrainee,
                        COALESCE(TrainerCodigoFJ, '') AS TrainerCodigoFJ,
                        COALESCE(CountsTowardKousu, 1) AS CountsTowardKousu
                    FROM HaidaiAssignments
                    WHERE date(ScheduleDate) = date(@ScheduleDate)
                      AND ShiftId = @ShiftId
                      AND COALESCE(IsLineupActive, 1) = 1
                      AND COALESCE(AssignmentCode, '') <> '';",
                new
                {
                    ScheduleDate = date.ToString("yyyy-MM-dd"),
                    ShiftId = shiftId
                })
                .ToList();
        }

        public HaidaiExportResult ExportSector(
            DateTime date,
            int sectorId,
            IReadOnlyCollection<int>? authorizedSectorIds = null)
        {
            EnsureSchema();

            var exportDirectory = ConfigurationManager.AppSettings["HaidaiExportDirectory"];
            if (string.IsNullOrWhiteSpace(exportDirectory))
            {
                throw new InvalidOperationException("HaidaiExportDirectory nao esta configurado no app.config.");
            }

            Directory.CreateDirectory(exportDirectory);
            CopyHaidaiExportAssets(exportDirectory);

            using var conn = _factory.CreateOpenConnection();

            var sector = conn.QuerySingleOrDefault<HaidaiLookupItem>(
                @"
                    SELECT
                        Id,
                        COALESCE(NULLIF(NamePt, ''), NULLIF(NameJp, ''), 'Setor ' || Id) AS Name
                    FROM Sectors
                    WHERE Id = @Id;",
                new { Id = sectorId });

            if (sector == null)
            {
                throw new InvalidOperationException("Setor nao encontrado para exportacao.");
            }

            var shifts = conn.Query<HaidaiLookupItem>(
                @"
                    SELECT
                        Id,
                        COALESCE(NULLIF(NamePt, ''), NULLIF(NameJp, ''), 'Turno ' || Id) AS Name
                    FROM Shifts
                    ORDER BY Id;")
                .ToList();

            var generatedFiles = new List<string>();

            for (var i = 0; i < shifts.Count; i++)
            {
                var shift = shifts[i];
                var board = GetBoard(date, shift.Id, sectorId, authorizedSectorIds);
                var currentFile = BuildExportFileName(sector.Name, shift.Name);
                var otherFile = shifts.Count > 1
                    ? BuildExportFileName(sector.Name, shifts[(i + 1) % shifts.Count].Name)
                    : currentFile;

                var html = BuildExportHtml(
                    sectorId,
                    sector.Name,
                    shift.Name,
                    otherFile,
                    board);

                var fullPath = Path.Combine(exportDirectory, currentFile);
                File.WriteAllText(fullPath, html, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                generatedFiles.Add(fullPath);
            }

            return new HaidaiExportResult(exportDirectory, generatedFiles);
        }

        private static string ResolveAssignmentCode(HaidaiAssignmentRecord? assignment, HaidaiLocalLookup? local)
        {
            var code = string.IsNullOrWhiteSpace(assignment?.AssignmentCode)
                ? local?.ShortCode ?? string.Empty
                : assignment!.AssignmentCode.Trim();

            if (assignment?.IsTrainee == true && !string.IsNullOrWhiteSpace(code) && !code.EndsWith("#", StringComparison.Ordinal))
            {
                code += "#";
            }

            return code;
        }

        private static string ResolveDisplayAssignmentCode(int motiveId, string assignmentCode)
        {
            return motiveId switch
            {
                1 => "\u6709",
                2 => "\u6b20",
                _ => assignmentCode
            };
        }

        private static string ResolveStatus(HaidaiExceptionRecord? exception, HaidaiAssignmentRecord? assignment, string assignmentCode)
        {
            if (exception?.MotiveId == 1)
            {
                return "Yukyu";
            }

            if (exception?.MotiveId == 2)
            {
                return "Falta";
            }

            if (!string.IsNullOrWhiteSpace(assignment?.AvailabilityStatus))
            {
                return assignment.AvailabilityStatus.Trim();
            }

            return string.IsNullOrWhiteSpace(assignmentCode) ? "Pendente" : "Escalado";
        }

        private static string ResolveMonthlyStatus(HaidaiMonthlyExceptionRecord? exception, HaidaiAssignmentRecord? assignment, string assignmentCode)
        {
            if (exception != null)
            {
                return ResolveExceptionStatus(exception.MotiveId);
            }

            if (!string.IsNullOrWhiteSpace(assignment?.AvailabilityStatus))
            {
                return assignment.AvailabilityStatus.Trim();
            }

            return string.IsNullOrWhiteSpace(assignmentCode) ? "Pendente" : "Escalado";
        }

        private static string ResolveExceptionStatus(int motiveId)
        {
            return motiveId switch
            {
                1 => "Yukyu",
                2 => "Falta",
                _ => string.Empty
            };
        }

        private static string BuildMovementSummary(HaidaiMovementRecord movement)
        {
            var typeLabel = movement.MovementType switch
            {
                "late" => "Atraso",
                "early_leave" => "Saiu cedo",
                _ => movement.MovementType
            };

            var parts = new List<string> { typeLabel };
            if (!string.IsNullOrWhiteSpace(movement.EventTime))
            {
                parts.Add(movement.EventTime);
            }

            if (!string.IsNullOrWhiteSpace(movement.AssignmentCode))
            {
                parts.Add($"Origem {movement.AssignmentCode}");
            }

            if (!string.IsNullOrWhiteSpace(movement.ReplacementOperatorCodigoFJ))
            {
                parts.Add($"Subst. {movement.ReplacementOperatorCodigoFJ}");
            }

            if (!string.IsNullOrWhiteSpace(movement.Reason))
            {
                parts.Add(movement.Reason);
            }

            return string.Join(" | ", parts);
        }

        private static string BuildReplacementNote(string originalOperatorCodigoFJ, string movementType, string? eventTime, string? reason)
        {
            var movementLabel = movementType == "late" ? "Atraso" : "Saida antecipada";
            var parts = new List<string> { $"Substituindo {originalOperatorCodigoFJ}", movementLabel };

            if (!string.IsNullOrWhiteSpace(eventTime))
            {
                parts.Add(eventTime.Trim());
            }

            if (!string.IsNullOrWhiteSpace(reason))
            {
                parts.Add(reason.Trim());
            }

            return string.Join(" | ", parts);
        }

        private static string ResolveMovementEventDateTime(
            System.Data.IDbConnection conn,
            System.Data.IDbTransaction transaction,
            DateTime scheduleDate,
            int shiftId,
            string eventTime)
        {
            var normalizedTime = NormalizeEventTime(eventTime);
            if (string.IsNullOrWhiteSpace(normalizedTime) || !TimeSpan.TryParse(normalizedTime, out var timePart))
            {
                return string.Empty;
            }

            var actualDate = scheduleDate.Date;
            if (IsOvernightShift(conn, transaction, shiftId) && timePart < TimeSpan.FromHours(12))
            {
                actualDate = actualDate.AddDays(1);
            }

            return actualDate.Add(timePart).ToString("yyyy-MM-dd HH:mm:ss");
        }

        private static string NormalizeEventTime(string eventTime)
        {
            var value = (eventTime ?? string.Empty).Trim();
            if (value.Length == 5)
            {
                value += ":00";
            }

            return value;
        }

        private static bool IsOvernightShift(System.Data.IDbConnection conn, System.Data.IDbTransaction transaction, int shiftId)
        {
            var shiftName = conn.ExecuteScalar<string?>(
                @"
                    SELECT COALESCE(NULLIF(NamePt, ''), NULLIF(NameJp, ''), '')
                    FROM Shifts
                    WHERE Id = @Id
                    LIMIT 1;",
                new { Id = shiftId },
                transaction) ?? string.Empty;

            return shiftName.IndexOf("noite", StringComparison.OrdinalIgnoreCase) >= 0
                || shiftName.IndexOf("night", StringComparison.OrdinalIgnoreCase) >= 0
                || shiftName.IndexOf("yakin", StringComparison.OrdinalIgnoreCase) >= 0
                || shiftName.Contains("夜", StringComparison.OrdinalIgnoreCase);
        }

        private void RemoveDerivedReplacementAssignment(
            System.Data.IDbConnection conn,
            System.Data.IDbTransaction transaction,
            DateTime date,
            int shiftId,
            int sectorId,
            string originalOperatorCodigoFJ,
            string replacementOperatorCodigoFJ)
        {
            var storageSectorId = ResolveStorageSectorId(
                sectorId,
                ResolveOperatorHomeSectorId(conn, replacementOperatorCodigoFJ, sectorId, transaction));

            conn.Execute(
                @"
                    DELETE FROM HaidaiAssignments
                    WHERE OperatorCodigoFJ = @OperatorCodigoFJ
                      AND ShiftId = @ShiftId
                      AND SectorId = @SectorId
                      AND date(ScheduleDate) = date(@ScheduleDate)
                      AND COALESCE(Notes, '') LIKE @NotesPattern;",
                new
                {
                    OperatorCodigoFJ = replacementOperatorCodigoFJ,
                    ShiftId = shiftId,
                    SectorId = storageSectorId,
                    ScheduleDate = date.ToString("yyyy-MM-dd"),
                    NotesPattern = $"Substituindo {originalOperatorCodigoFJ}%"
                },
                transaction);
        }

        private static void UpsertPendingTodokeRequest(
            System.Data.IDbConnection conn,
            System.Data.IDbTransaction transaction,
            string operatorCodigoFJ,
            string requestDate,
            int motiveId,
            string? notes,
            string authorizedByCodigoFJ,
            bool replaceExceptionMotives = false,
            bool replaceMovementMotives = false)
        {
            const string exactSql = @"
                SELECT Id
                FROM AcompYukyu
                WHERE OperatorCodigoFJ = @OperatorCodigoFJ
                  AND date(RequestDate) = date(@RequestDate)
                  AND TodokeMotivoId = @TodokeMotivoId
                ORDER BY Id DESC
                LIMIT 1;";

            const string exceptionSql = @"
                SELECT Id
                FROM AcompYukyu
                WHERE OperatorCodigoFJ = @OperatorCodigoFJ
                  AND date(RequestDate) = date(@RequestDate)
                  AND TodokeMotivoId IN (1, 2)
                ORDER BY Id DESC
                LIMIT 1;";

            const string movementSql = @"
                SELECT Id
                FROM AcompYukyu
                WHERE OperatorCodigoFJ = @OperatorCodigoFJ
                  AND date(RequestDate) = date(@RequestDate)
                  AND TodokeMotivoId IN (3, 5)
                ORDER BY Id DESC
                LIMIT 1;";

            var existingId = conn.ExecuteScalar<long?>(
                replaceExceptionMotives ? exceptionSql : replaceMovementMotives ? movementSql : exactSql,
                new
                {
                    OperatorCodigoFJ = operatorCodigoFJ,
                    RequestDate = requestDate,
                    TodokeMotivoId = motiveId
                },
                transaction);

            long currentId;
            if (existingId.HasValue)
            {
                conn.Execute(
                    @"
                        UPDATE AcompYukyu
                        SET TodokeMotivoId = @TodokeMotivoId,
                            Notes = @Notes,
                            AuthorizedByCodigoFJ = @AuthorizedByCodigoFJ,
                            RequestDate = @RequestDate
                        WHERE Id = @Id;",
                    new
                    {
                        Id = existingId.Value,
                        TodokeMotivoId = motiveId,
                        Notes = NullIfWhiteSpace(notes),
                        AuthorizedByCodigoFJ = authorizedByCodigoFJ,
                        RequestDate = requestDate
                    },
                    transaction);

                currentId = existingId.Value;
            }
            else
            {
                currentId = conn.ExecuteScalar<long>(
                    @"
                        INSERT INTO AcompYukyu (
                            OperatorCodigoFJ,
                            RequestDate,
                            AuthorizedByCodigoFJ,
                            Notes,
                            TodokeMotivoId
                        )
                        VALUES (
                            @OperatorCodigoFJ,
                            @RequestDate,
                            @AuthorizedByCodigoFJ,
                            @Notes,
                            @TodokeMotivoId
                        );
                        SELECT last_insert_rowid();",
                    new
                    {
                        OperatorCodigoFJ = operatorCodigoFJ,
                        RequestDate = requestDate,
                        AuthorizedByCodigoFJ = authorizedByCodigoFJ,
                        Notes = NullIfWhiteSpace(notes),
                        TodokeMotivoId = motiveId
                    },
                    transaction);
            }

            var duplicateIds = conn.Query<long>(
                @"
                    SELECT Id
                    FROM AcompYukyu
                    WHERE OperatorCodigoFJ = @OperatorCodigoFJ
                      AND date(RequestDate) = date(@RequestDate)
                      AND Id <> @CurrentId
                      AND (
                            (@ReplaceExceptionMotives = 1 AND TodokeMotivoId IN (1, 2))
                         OR (@ReplaceMovementMotives = 1 AND TodokeMotivoId IN (3, 5))
                         OR (@ReplaceExceptionMotives = 0 AND @ReplaceMovementMotives = 0 AND TodokeMotivoId = @TodokeMotivoId)
                      );",
                new
                {
                    OperatorCodigoFJ = operatorCodigoFJ,
                    RequestDate = requestDate,
                    CurrentId = currentId,
                    ReplaceExceptionMotives = replaceExceptionMotives ? 1 : 0,
                    ReplaceMovementMotives = replaceMovementMotives ? 1 : 0,
                    TodokeMotivoId = motiveId
                },
                transaction);

            DeleteAcompYukyuCascade(conn, transaction, duplicateIds);
        }

        private static void DeleteAcompYukyuCascade(
            System.Data.IDbConnection conn,
            System.Data.IDbTransaction transaction,
            IEnumerable<long> ids)
        {
            var idList = ids
                .Distinct()
                .ToList();

            if (idList.Count == 0)
            {
                return;
            }

            conn.Execute("DELETE FROM YukyuTodoke WHERE AcompYukyuId IN @Ids;", new { Ids = idList }, transaction);
            conn.Execute("DELETE FROM YukyuFolhaControle WHERE AcompYukyuId IN @Ids;", new { Ids = idList }, transaction);
            conn.Execute("DELETE FROM YukyuConferencia WHERE AcompYukyuId IN @Ids;", new { Ids = idList }, transaction);
            conn.Execute("DELETE FROM AcompYukyu WHERE Id IN @Ids;", new { Ids = idList }, transaction);
        }

        private static int ResolveMovementTodokeMotiveId(string movementType)
        {
            return string.Equals(movementType, "late", StringComparison.OrdinalIgnoreCase) ? 3 : 5;
        }

        private static string BuildMovementTodokeNotes(
            string movementType,
            string? eventTime,
            string? reason,
            string? assignmentCode,
            string? replacementOperatorCodigoFJ)
        {
            var label = string.Equals(movementType, "late", StringComparison.OrdinalIgnoreCase)
                ? "Atraso"
                : "Saida antecipada";

            var parts = new List<string> { label };
            if (!string.IsNullOrWhiteSpace(eventTime))
            {
                parts.Add($"Horario {eventTime.Trim()}");
            }

            if (!string.IsNullOrWhiteSpace(assignmentCode))
            {
                parts.Add($"Area {assignmentCode.Trim()}");
            }

            if (!string.IsNullOrWhiteSpace(replacementOperatorCodigoFJ))
            {
                parts.Add($"Substituto {replacementOperatorCodigoFJ.Trim()}");
            }

            if (!string.IsNullOrWhiteSpace(reason))
            {
                parts.Add(reason.Trim());
            }

            return string.Join(" | ", parts);
        }

        private static void UpsertMonthlyAssignmentInternal(
            System.Data.IDbConnection conn,
            System.Data.IDbTransaction transaction,
            DateTime date,
            int shiftId,
            int sectorId,
            string operatorCodigoFJ,
            HaidaiResolvedMonthlyCell resolved,
            HaidaiAssignmentRecord? existing)
        {
            var countsTowardKousu = resolved.IsTrainee
                ? false
                : existing?.CountsTowardKousu ?? true;

            var payload = new
            {
                ScheduleDate = date.ToString("yyyy-MM-dd"),
                ShiftId = shiftId,
                SectorId = sectorId,
                OperatorCodigoFJ = operatorCodigoFJ,
                resolved.LocalId,
                resolved.AssignmentCode,
                PairKey = NullIfWhiteSpace(existing?.PairKey),
                IsTrainee = resolved.IsTrainee ? 1 : 0,
                TrainerCodigoFJ = NullIfWhiteSpace(existing?.TrainerCodigoFJ),
                CountsTowardKousu = countsTowardKousu ? 1 : 0,
                IsLineupActive = resolved.IsLineupActive ? 1 : 0,
                IsHolidayWork = resolved.IsHolidayWork ? 1 : 0,
                AvailabilityStatus = NullIfWhiteSpace(resolved.AvailabilityStatus),
                Notes = NullIfWhiteSpace(existing?.Notes)
            };

            if (existing != null)
            {
                conn.Execute(
                    @"
                        UPDATE HaidaiAssignments
                        SET LocalId = @LocalId,
                            AssignmentCode = @AssignmentCode,
                            PairKey = @PairKey,
                            IsTrainee = @IsTrainee,
                            TrainerCodigoFJ = @TrainerCodigoFJ,
                            CountsTowardKousu = @CountsTowardKousu,
                            IsLineupActive = @IsLineupActive,
                            IsHolidayWork = @IsHolidayWork,
                            AvailabilityStatus = @AvailabilityStatus,
                            Notes = @Notes,
                            UpdatedAt = CURRENT_TIMESTAMP
                        WHERE Id = @Id;",
                    new
                    {
                        existing.Id,
                        payload.LocalId,
                        payload.AssignmentCode,
                        payload.PairKey,
                        payload.IsTrainee,
                        payload.TrainerCodigoFJ,
                        payload.CountsTowardKousu,
                        payload.IsLineupActive,
                        payload.IsHolidayWork,
                        payload.AvailabilityStatus,
                        payload.Notes
                    },
                    transaction);

                LogHaidaiSave(
                    date,
                    shiftId,
                    resolved.LocalId,
                    resolved.AssignmentCode,
                    operatorCodigoFJ,
                    existing.Id,
                    existing.IsHolidayWork,
                    resolved.IsHolidayWork,
                    "Update",
                    saved: true,
                    error: string.Empty);

                return;
            }

            conn.Execute(
                @"
                    INSERT INTO HaidaiAssignments (
                        ScheduleDate,
                        ShiftId,
                        SectorId,
                        OperatorCodigoFJ,
                        LocalId,
                        AssignmentCode,
                        PairKey,
                        IsTrainee,
                        TrainerCodigoFJ,
                        CountsTowardKousu,
                        IsLineupActive,
                        IsHolidayWork,
                        AvailabilityStatus,
                        Notes
                    )
                    VALUES (
                        @ScheduleDate,
                        @ShiftId,
                        @SectorId,
                        @OperatorCodigoFJ,
                        @LocalId,
                        @AssignmentCode,
                        @PairKey,
                        @IsTrainee,
                        @TrainerCodigoFJ,
                        @CountsTowardKousu,
                        @IsLineupActive,
                        @IsHolidayWork,
                        @AvailabilityStatus,
                        @Notes
                    );",
                payload,
                transaction);

            LogHaidaiSave(
                date,
                shiftId,
                resolved.LocalId,
                resolved.AssignmentCode,
                operatorCodigoFJ,
                null,
                false,
                resolved.IsHolidayWork,
                "Insert",
                saved: true,
                error: string.Empty);
        }

        private static void UpsertAssignmentInternal(System.Data.IDbConnection conn, System.Data.IDbTransaction transaction, HaidaiSaveAssignmentRequest request)
        {
            var normalizedDate = request.Date.ToString("yyyy-MM-dd");
            var assignmentCode = (request.AssignmentCode ?? string.Empty).Trim();
            var isOffDay = IsOffDayCode(assignmentCode);
            var normalizedAssignmentCode = isOffDay ? "\u4f11" : assignmentCode;

            var existing = conn.QueryFirstOrDefault<HaidaiAssignmentRecord>(
                @"
                    SELECT
                        Id,
                        ScheduleDate,
                        ShiftId,
                        SectorId,
                        OperatorCodigoFJ,
                        LocalId,
                        COALESCE(AssignmentCode, '') AS AssignmentCode,
                        COALESCE(PairKey, '') AS PairKey,
                        COALESCE(IsTrainee, 0) AS IsTrainee,
                        COALESCE(TrainerCodigoFJ, '') AS TrainerCodigoFJ,
                        COALESCE(CountsTowardKousu, 1) AS CountsTowardKousu,
                        COALESCE(IsLineupActive, 1) AS IsLineupActive,
                        COALESCE(IsHolidayWork, 0) AS IsHolidayWork,
                        COALESCE(AvailabilityStatus, '') AS AvailabilityStatus,
                        COALESCE(Notes, '') AS Notes
                    FROM HaidaiAssignments
                    WHERE date(ScheduleDate) = date(@ScheduleDate)
                      AND ShiftId = @ShiftId
                      AND SectorId = @SectorId
                      AND OperatorCodigoFJ = @OperatorCodigoFJ
                    LIMIT 1;",
                new
                {
                    ScheduleDate = normalizedDate,
                    request.ShiftId,
                    request.SectorId,
                    request.OperatorCodigoFJ
                },
                transaction);

            var action = existing == null ? "Insert" : "Update";
            var localId = isOffDay ? null : request.LocalId;
            var nextIsHolidayWork = !isOffDay && request.IsHolidayWork;

            try
            {
                if (existing != null)
                {
                    conn.Execute(
                        @"
                            UPDATE HaidaiAssignments
                            SET LocalId = @LocalId,
                                AssignmentCode = @AssignmentCode,
                                PairKey = @PairKey,
                                IsTrainee = @IsTrainee,
                                TrainerCodigoFJ = @TrainerCodigoFJ,
                                CountsTowardKousu = @CountsTowardKousu,
                                IsLineupActive = @IsLineupActive,
                                IsHolidayWork = @IsHolidayWork,
                                AvailabilityStatus = @AvailabilityStatus,
                                Notes = @Notes,
                                UpdatedAt = CURRENT_TIMESTAMP
                            WHERE Id = @Id;",
                        new
                        {
                            Id = existing.Id,
                            LocalId = localId,
                            AssignmentCode = normalizedAssignmentCode,
                            PairKey = isOffDay ? null : NullIfWhiteSpace(request.PairKey),
                            IsTrainee = isOffDay ? 0 : request.IsTrainee ? 1 : 0,
                            TrainerCodigoFJ = isOffDay ? null : NullIfWhiteSpace(request.TrainerCodigoFJ),
                            CountsTowardKousu = isOffDay ? 0 : request.CountsTowardKousu ? 1 : 0,
                            IsLineupActive = isOffDay ? 0 : 1,
                            IsHolidayWork = nextIsHolidayWork ? 1 : 0,
                            AvailabilityStatus = isOffDay ? "Folga" : "Escalado",
                            Notes = NullIfWhiteSpace(request.Notes)
                        },
                        transaction);
                }
                else
                {
                    conn.Execute(
                        @"
                            INSERT INTO HaidaiAssignments (
                                ScheduleDate,
                                ShiftId,
                                SectorId,
                                OperatorCodigoFJ,
                                LocalId,
                                AssignmentCode,
                                PairKey,
                                IsTrainee,
                                TrainerCodigoFJ,
                                CountsTowardKousu,
                                IsLineupActive,
                                IsHolidayWork,
                                AvailabilityStatus,
                                Notes
                            )
                            VALUES (
                                @ScheduleDate,
                                @ShiftId,
                                @SectorId,
                                @OperatorCodigoFJ,
                                @LocalId,
                                @AssignmentCode,
                                @PairKey,
                                @IsTrainee,
                                @TrainerCodigoFJ,
                                @CountsTowardKousu,
                                @IsLineupActive,
                                @IsHolidayWork,
                                @AvailabilityStatus,
                                @Notes
                            );",
                        new
                        {
                            ScheduleDate = normalizedDate,
                            request.ShiftId,
                            request.SectorId,
                            request.OperatorCodigoFJ,
                            LocalId = localId,
                            AssignmentCode = normalizedAssignmentCode,
                            PairKey = isOffDay ? null : NullIfWhiteSpace(request.PairKey),
                            IsTrainee = isOffDay ? 0 : request.IsTrainee ? 1 : 0,
                            TrainerCodigoFJ = isOffDay ? null : NullIfWhiteSpace(request.TrainerCodigoFJ),
                            CountsTowardKousu = isOffDay ? 0 : request.CountsTowardKousu ? 1 : 0,
                            IsLineupActive = isOffDay ? 0 : 1,
                            IsHolidayWork = nextIsHolidayWork ? 1 : 0,
                            AvailabilityStatus = isOffDay ? "Folga" : "Escalado",
                            Notes = NullIfWhiteSpace(request.Notes)
                        },
                        transaction);
                }

                LogHaidaiSave(
                    request.Date,
                    request.ShiftId,
                    localId,
                    normalizedAssignmentCode,
                    request.OperatorCodigoFJ,
                    existing?.Id,
                    existing?.IsHolidayWork ?? false,
                    nextIsHolidayWork,
                    action,
                    saved: true,
                    error: string.Empty);
            }
            catch (Exception ex)
            {
                LogHaidaiSave(
                    request.Date,
                    request.ShiftId,
                    localId,
                    normalizedAssignmentCode,
                    request.OperatorCodigoFJ,
                    existing?.Id,
                    existing?.IsHolidayWork ?? false,
                    nextIsHolidayWork,
                    action,
                    saved: false,
                    error: ex.Message);
                throw;
            }
        }

        private static void LogHaidaiSave(
            DateTime date,
            int shiftId,
            int? localId,
            string position,
            string operatorId,
            int? existingAssignmentId,
            bool isHolidayWorkBefore,
            bool isHolidayWorkAfter,
            string action,
            bool saved,
            string error)
        {
            Console.WriteLine(
                "[HaidaiSave] "
                + $"Date={date:yyyy-MM-dd} "
                + $"ShiftId={shiftId} "
                + $"LocalId={FormatNullableInt(localId)} "
                + $"Position={position} "
                + $"OperatorId={operatorId} "
                + $"ExistingAssignmentId={(existingAssignmentId?.ToString(CultureInfo.InvariantCulture) ?? "null")} "
                + $"IsHolidayWorkBefore={isHolidayWorkBefore} "
                + $"IsHolidayWorkAfter={isHolidayWorkAfter} "
                + $"Action={action} "
                + $"Saved={saved} "
                + $"Error={error}");
        }

        private static void ApplyPairKeyToMonth(System.Data.IDbConnection conn, System.Data.IDbTransaction transaction, HaidaiSaveAssignmentRequest request)
        {
            var applyStart = request.Date.Date;
            var monthStart = new DateTime(request.Date.Year, request.Date.Month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            var pairKey = NullIfWhiteSpace(request.PairKey);

            conn.Execute(
                @"
                    UPDATE HaidaiAssignments
                    SET PairKey = @PairKey,
                        UpdatedAt = CURRENT_TIMESTAMP
                    WHERE date(ScheduleDate) BETWEEN date(@ApplyStart) AND date(@MonthEnd)
                      AND ShiftId = @ShiftId
                      AND SectorId = @SectorId
                      AND OperatorCodigoFJ = @OperatorCodigoFJ;",
                new
                {
                    PairKey = pairKey,
                    ApplyStart = applyStart.ToString("yyyy-MM-dd"),
                    MonthEnd = monthEnd.ToString("yyyy-MM-dd"),
                    request.ShiftId,
                    request.SectorId,
                    request.OperatorCodigoFJ
                },
                transaction);

            if (pairKey == null)
            {
                return;
            }

            for (var date = applyStart; date <= monthEnd; date = date.AddDays(1))
            {
                conn.Execute(
                    @"
                        INSERT INTO HaidaiAssignments (
                            ScheduleDate,
                            ShiftId,
                            SectorId,
                            OperatorCodigoFJ,
                            PairKey,
                            CountsTowardKousu,
                            IsLineupActive
                        )
                        SELECT
                            @ScheduleDate,
                            @ShiftId,
                            @SectorId,
                            @OperatorCodigoFJ,
                            @PairKey,
                            1,
                            0
                        WHERE NOT EXISTS (
                            SELECT 1
                            FROM HaidaiAssignments
                            WHERE date(ScheduleDate) = date(@ScheduleDate)
                              AND ShiftId = @ShiftId
                              AND SectorId = @SectorId
                              AND OperatorCodigoFJ = @OperatorCodigoFJ
                        );",
                    new
                    {
                        ScheduleDate = date.ToString("yyyy-MM-dd"),
                        request.ShiftId,
                        request.SectorId,
                        request.OperatorCodigoFJ,
                        PairKey = pairKey
                    },
                    transaction);
            }
        }

        private static object? NullIfWhiteSpace(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string FormatNullableInt(int? value)
        {
            return value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "NULL";
        }

        private static string ResolveOperatorName(System.Data.IDbConnection conn, string codigoFJ)
        {
            return conn.ExecuteScalar<string?>(
                    @"
                        SELECT COALESCE(NULLIF(NameRomanji, ''), NULLIF(NameNihongo, ''), CodigoFJ)
                        FROM Operators
                        WHERE CodigoFJ = @CodigoFJ
                        LIMIT 1;",
                    new { CodigoFJ = codigoFJ })
                ?? codigoFJ;
        }

        private static void EnsureColumn(System.Data.IDbConnection conn, string tableName, string columnName, string definition)
        {
            var exists = conn.ExecuteScalar<int>(
                $@"
                    SELECT COUNT(1)
                    FROM pragma_table_info('{tableName}')
                    WHERE name = @ColumnName;",
                new { ColumnName = columnName }) > 0;

            if (!exists)
            {
                conn.Execute($"ALTER TABLE {tableName} ADD COLUMN {columnName} {definition};");
            }
        }

        private static string BuildExportFileName(string sectorName, string shiftName)
        {
            return $"{Slugify(sectorName)}-{SlugifyShift(shiftName)}.html";
        }

        private static string SlugifyShift(string value)
        {
            var normalized = Slugify(value);

            if (normalized.Contains("dia", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains("hiru", StringComparison.OrdinalIgnoreCase))
            {
                return "dia";
            }

            if (normalized.Contains("noite", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains("yakin", StringComparison.OrdinalIgnoreCase))
            {
                return "noite";
            }

            return normalized;
        }

        private static string Slugify(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "haidai";
            }

            var normalized = value.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder();

            foreach (var ch in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (category == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                if (char.IsLetterOrDigit(ch))
                {
                    builder.Append(char.ToLowerInvariant(ch));
                }
                else if (builder.Length == 0 || builder[^1] != '-')
                {
                    builder.Append('-');
                }
            }

            var result = builder.ToString().Trim('-');
            return string.IsNullOrWhiteSpace(result) ? "haidai" : result;
        }

        private static void CopyHaidaiExportAssets(string exportDirectory)
        {
            var sourceAssets = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "ui",
                "presence",
                "assets");

            if (!Directory.Exists(sourceAssets))
            {
                return;
            }

            var targetAssets = Path.Combine(exportDirectory, "assets");
            Directory.CreateDirectory(targetAssets);

            var defaultSource = Path.Combine(sourceAssets, "default-operator.png");
            if (File.Exists(defaultSource))
            {
                File.Copy(defaultSource, Path.Combine(targetAssets, "default-operator.png"), overwrite: true);
            }

            var sourceOperators = Path.Combine(sourceAssets, "operators");
            if (!Directory.Exists(sourceOperators))
            {
                return;
            }

            var targetOperators = Path.Combine(targetAssets, "operators");
            Directory.CreateDirectory(targetOperators);

            foreach (var file in Directory.EnumerateFiles(sourceOperators))
            {
                File.Copy(file, Path.Combine(targetOperators, Path.GetFileName(file)), overwrite: true);
            }
        }

        private static string BuildExportHtml(int sectorId, string sectorName, string shiftName, string otherFileName, HaidaiBoardPayload board)
        {
            var rows = board.Groups
                .SelectMany(group => group.Rows)
                .ToList();

            var activeRows = rows
                .Where(row => row.IsLineupActive && !string.IsNullOrWhiteSpace(row.AssignmentCode))
                .ToList();

            var tvAreaRows = activeRows
                .Where(row => IsExportableTvAreaRow(row, sectorId))
                .ToList();

            var tvAreaGroups = tvAreaRows
                .Where(row => !row.IsTrainee)
                .GroupBy(BuildTvAreaKey)
                .OrderBy(group => AreaSortKey(group.Key), StringComparer.OrdinalIgnoreCase)
                .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var tvTrainingRows = tvAreaRows
                .Where(row => row.IsTrainee || !string.IsNullOrWhiteSpace(row.TrainerCodigoFJ))
                .OrderBy(row => AreaSortKey(BuildTvAreaKey(row)), StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var tvPresentLeaders = rows
                .Where(IsPresentLeader)
                .OrderBy(row => row.AccessLevel == 2 ? 0 : 1)
                .ThenBy(row => row.GroupName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var tvSummaryMarkup = string.Join(
                Environment.NewLine,
                new List<(string Label, int Value, string Tone)>
                {
                    ("Escalados", tvAreaRows.Count, "ok"),
                    ("Areas", tvAreaGroups.Count, "area"),
                    ("Treino", tvTrainingRows.Count, "train"),
                    ("Lideres", tvPresentLeaders.Count, "leader"),
                    ("Yukyu", board.Summary.YukyuCount, "warn"),
                    ("Faltas", board.Summary.FaltaCount, "danger")
                }.Select(item => $@"
                    <div class=""metric metric-{EscapeHtml(item.Tone)}"">
                        <strong>{item.Value}</strong>
                        <span>{EscapeHtml(item.Label)}</span>
                    </div>"));

            var tvAreaMarkup = tvAreaGroups.Count == 0
                ? @"<article class=""empty-card"">Nenhuma area escalada para este turno.</article>"
                : BuildTvAreaPagesMarkup(tvAreaGroups);

            var tvTrainingMarkup = tvTrainingRows.Count == 0
                ? @"<div class=""compact-empty"">Sem treinamento hoje</div>"
                : string.Join(Environment.NewLine, tvTrainingRows.Select(BuildTrainingRowMarkup));

            var tvLeaderMarkup = tvPresentLeaders.Count == 0
                ? @"<div class=""compact-empty"">Nenhum lider presente neste turno</div>"
                : string.Join(Environment.NewLine, tvPresentLeaders.Select(BuildLeaderRowMarkup));

            var pageCount = Math.Max(1, (int)Math.Ceiling(tvAreaGroups.Count / (double)TvExportPageSize));

            return $@"
<!DOCTYPE html>
<html lang=""pt-BR"">
<head>
    <meta charset=""UTF-8"">
    <meta http-equiv=""refresh"" content=""120"">
    <title>Haidai {EscapeHtml(sectorName)} - {EscapeHtml(shiftName)}</title>
    <style>
        :root {{
            color-scheme: light;
            --bg: #eef3f7;
            --surface: #ffffff;
            --surface-2: #f6f9fc;
            --accent: #135f84;
            --accent-2: #23745c;
            --text: #14202e;
            --muted: #5f6e7c;
            --border: #d3dde6;
            --warn: #b46b00;
            --danger: #b43a43;
            --train: #7a4b00;
        }}
        * {{ box-sizing: border-box; }}
        html,
        body {{
            margin: 0;
            width: 100%;
            height: 100%;
            overflow: hidden;
            font-family: ""Segoe UI"", Tahoma, sans-serif;
            background: var(--bg);
            color: var(--text);
        }}
        .page {{
            height: 100vh;
            padding: 14px;
            display: grid;
            grid-template-columns: minmax(0, 1fr) minmax(250px, 17vw);
            grid-template-rows: auto minmax(0, 1fr) auto;
            gap: 12px;
        }}
        .topbar {{
            grid-column: 1 / -1;
            display: flex;
            justify-content: space-between;
            align-items: center;
            min-height: 58px;
            padding: 10px 16px;
            border-radius: 8px;
            background: var(--surface);
            border: 1px solid var(--border);
        }}
        .title-block {{ min-width: 0; }}
        h1 {{
            margin: 0;
            display: flex;
            align-items: baseline;
            gap: 14px;
            flex-wrap: wrap;
            font-size: clamp(24px, 2.1vw, 42px);
            line-height: 1;
            letter-spacing: 0;
        }}
        .title-date {{
            color: var(--accent);
            font-size: clamp(16px, 1.1vw, 22px);
            font-weight: 900;
        }}
        .switch-link {{
            display: inline-flex;
            align-items: center;
            justify-content: center;
            min-height: 42px;
            padding: 0 16px;
            border-radius: 8px;
            background: #e8f2f7;
            border: 1px solid #bfdae6;
            color: var(--accent);
            text-decoration: none;
            font-weight: 700;
            font-size: 14px;
            white-space: nowrap;
        }}
        .areas-panel,
        .summary-box,
        .leaders-box,
        .training-strip {{
            border-radius: 8px;
            background: var(--surface);
            border: 1px solid var(--border);
            padding: 12px;
            overflow: hidden;
        }}
        .areas-panel {{ min-height: 0; }}
        .area-pages {{
            position: relative;
            height: 100%;
            min-height: 0;
        }}
        .area-page {{
            position: absolute;
            inset: 0;
            opacity: 0;
            pointer-events: none;
            transition: opacity 420ms ease;
        }}
        .area-page.active {{
            opacity: 1;
            pointer-events: auto;
        }}
        .area-grid {{
            height: 100%;
            min-height: 0;
            display: grid;
            grid-template-columns: repeat(5, minmax(0, 1fr));
            grid-template-rows: repeat(4, minmax(0, 1fr));
            gap: 8px;
            overflow: hidden;
        }}
        .area-pages.few-areas .area-grid {{
            grid-template-rows: none;
            grid-auto-rows: minmax(150px, 180px);
            align-content: start;
        }}
        .area-card {{
            display: grid;
            grid-template-rows: auto minmax(0, 1fr);
            gap: 6px;
            min-width: 0;
            min-height: 0;
            padding: 8px;
            border-radius: 8px;
            background: var(--surface-2);
            border: 2px solid #cddbe6;
        }}
        .area-card.two-up {{
            border-color: #a8d4c4;
            background: #f2faf6;
        }}
        .area-card.paired {{
            border-color: #a8d4c4;
            background: #f2faf6;
        }}
        .area-header {{
            display: flex;
            align-items: center;
            justify-content: space-between;
            gap: 8px;
            min-width: 0;
        }}
        .area-code {{
            min-width: 0;
            color: var(--accent);
            font-size: clamp(18px, 1.3vw, 26px);
            font-weight: 900;
            line-height: 1;
            white-space: nowrap;
            overflow: hidden;
            text-overflow: ellipsis;
        }}
        .area-count {{
            display: inline-flex;
            align-items: center;
            justify-content: center;
            width: 32px;
            min-width: 32px;
            height: 32px;
            border-radius: 999px;
            background: #dfeef4;
            color: var(--accent);
            font-size: 16px;
            font-weight: 900;
        }}
        .operator-list {{
            min-height: 0;
            display: grid;
            gap: 6px;
            align-content: stretch;
        }}
        .area-card.two-up .operator-list {{ grid-template-columns: repeat(2, minmax(0, 1fr)); }}
        .area-card.paired .operator-list {{
            grid-template-columns: repeat(auto-fit, minmax(0, 1fr));
        }}
        .operator-pair {{
            min-width: 0;
            min-height: 0;
            display: grid;
            gap: 6px;
            align-content: stretch;
        }}
        .pair-label {{
            display: none;
        }}
        .area-card.two-up .operator-card {{
            display: grid;
            grid-template-rows: auto minmax(0, 1fr);
            justify-items: center;
            align-content: start;
            text-align: center;
            gap: 4px;
            padding: 5px 6px;
            overflow: hidden;
        }}
        .area-card.two-up .operator-photo {{
            width: clamp(34px, 2.45vw, 48px);
            height: clamp(34px, 2.45vw, 48px);
            min-width: clamp(34px, 2.45vw, 48px);
            border-width: 2px;
        }}
        .area-card.two-up .operator-text {{
            width: 100%;
            min-height: 0;
        }}
        .area-card.two-up .operator-name {{
            font-size: clamp(12px, 0.86vw, 17px);
            line-height: 1.08;
        }}
        .area-card.two-up .operator-meta {{
            font-size: clamp(8px, 0.58vw, 10px);
        }}
        .operator-card {{
            min-width: 0;
            min-height: 0;
            display: flex;
            align-items: center;
            justify-content: center;
            align-content: center;
            gap: 7px;
            padding: 7px;
            border-radius: 8px;
            background: #ffffff;
            border: 1px solid #d6e2eb;
            overflow: hidden;
        }}
        .operator-photo {{
            width: clamp(40px, 3.15vw, 64px);
            height: clamp(40px, 3.15vw, 64px);
            min-width: clamp(40px, 3.15vw, 64px);
            border-radius: 50%;
            object-fit: cover;
            border: 3px solid #ffffff;
            box-shadow: 0 0 0 1px #bfd0dc;
            background: #e7eef4;
        }}
        .operator-text {{
            min-width: 0;
            flex: 1;
        }}
        .operator-card.trainee {{
            background: #fff7df;
            border-color: #efca76;
        }}
        .operator-card.leader {{
            background: #eef7fb;
            border-color: #a9cedf;
        }}
        .operator-name {{
            min-width: 0;
            font-size: clamp(15px, 1.06vw, 22px);
            font-weight: 900;
            line-height: 1.08;
            overflow: visible;
            white-space: normal;
            word-break: normal;
            overflow-wrap: anywhere;
        }}
        .operator-meta {{
            color: var(--muted);
            font-size: clamp(9px, 0.68vw, 12px);
            font-weight: 800;
            white-space: nowrap;
            overflow: hidden;
            text-overflow: ellipsis;
        }}
        .page-indicator {{
            position: absolute;
            right: 12px;
            bottom: 10px;
            min-width: 114px;
            min-height: 34px;
            display: none;
            align-items: center;
            justify-content: center;
            gap: 8px;
            border-radius: 999px;
            background: rgba(20, 32, 46, 0.78);
            color: #ffffff;
            font-size: 13px;
            font-weight: 900;
        }}
        .area-pages.has-pages .page-indicator {{
            display: flex;
        }}
        .page-nav {{
            width: 28px;
            height: 28px;
            display: inline-flex;
            align-items: center;
            justify-content: center;
            border: 1px solid rgba(255, 255, 255, 0.34);
            border-radius: 999px;
            background: rgba(255, 255, 255, 0.12);
            color: #ffffff;
            cursor: pointer;
            font-size: 19px;
            line-height: 1;
            font-weight: 900;
        }}
        .page-nav:hover {{
            background: rgba(255, 255, 255, 0.24);
        }}
        .page-label {{
            min-width: 38px;
            text-align: center;
        }}
        .side-panel {{
            min-height: 0;
            display: grid;
            grid-template-rows: auto minmax(0, 1fr);
            gap: 12px;
        }}
        .summary-box {{
            display: grid;
            grid-template-columns: repeat(2, minmax(0, 1fr));
            gap: 8px;
        }}
        .metric {{
            min-width: 0;
            padding: 8px;
            border-radius: 8px;
            background: #f3f8fb;
            border: 1px solid #d5e5ed;
        }}
        .metric strong {{
            display: block;
            font-size: clamp(22px, 1.9vw, 38px);
            line-height: 1;
            color: var(--accent);
        }}
        .metric span {{
            display: block;
            margin-top: 3px;
            color: var(--muted);
            font-size: 12px;
            font-weight: 800;
        }}
        .metric-train strong {{ color: var(--train); }}
        .metric-leader strong {{ color: var(--accent-2); }}
        .metric-warn strong {{ color: var(--warn); }}
        .metric-danger strong {{ color: var(--danger); }}
        .leaders-box {{
            min-height: 0;
            display: grid;
            grid-template-rows: auto minmax(0, 1fr);
            gap: 8px;
        }}
        .panel-title {{
            margin: 0;
            font-size: clamp(16px, 1.15vw, 23px);
            line-height: 1;
            color: var(--text);
        }}
        .leader-list,
        .training-list {{
            min-height: 0;
            display: grid;
            align-content: start;
            gap: 7px;
            overflow: hidden;
        }}
        .leader-list {{
            overflow-y: auto;
            padding-right: 3px;
            scrollbar-width: thin;
        }}
        .leader-row,
        .training-row {{
            min-width: 0;
            padding: 8px;
            border-radius: 8px;
            border: 1px solid var(--border);
            background: var(--surface-2);
        }}
        .leader-row strong,
        .training-row strong {{
            display: block;
            min-width: 0;
            font-size: clamp(15px, 1vw, 20px);
            line-height: 1.05;
            white-space: nowrap;
            overflow: hidden;
            text-overflow: ellipsis;
        }}
        .leader-row span,
        .training-row span {{
            display: block;
            margin-top: 3px;
            color: var(--muted);
            font-size: clamp(11px, 0.82vw, 15px);
            font-weight: 800;
            white-space: nowrap;
            overflow: hidden;
            text-overflow: ellipsis;
        }}
        .role-badge {{
            display: inline-flex;
            align-items: center;
            justify-content: center;
            min-width: 34px;
            min-height: 24px;
            margin-right: 6px;
            border-radius: 999px;
            background: #dbeee7;
            color: #1d684e;
            font-size: 12px;
            font-weight: 900;
        }}
        .role-gl {{
            background: #deedf5;
            color: #145b7b;
        }}
        .training-strip {{
            grid-column: 1 / 2;
            min-height: 116px;
            display: grid;
            grid-template-rows: auto minmax(0, 1fr);
            gap: 8px;
        }}
        .training-list {{ grid-template-columns: repeat(auto-fit, minmax(210px, 1fr)); }}
        .compact-empty,
        .empty-card {{
            min-height: 64px;
            display: grid;
            place-items: center;
            border-radius: 8px;
            border: 1px dashed var(--border);
            color: var(--muted);
            font-size: 18px;
            font-weight: 800;
            background: var(--surface-2);
        }}
        @media (max-width: 1100px) {{
            body {{ overflow: auto; }}
            .page {{
                height: auto;
                min-height: 100vh;
                grid-template-columns: 1fr;
            }}
            .training-strip {{ grid-column: 1; }}
        }}
    </style>
</head>
<body>
    <main class=""page"">
        <section class=""topbar"">
            <div class=""title-block"">
                <h1>{EscapeHtml(sectorName)} - {EscapeHtml(shiftName)} <span class=""title-date"">{EscapeHtml(board.DateIso)}</span></h1>
            </div>
            <a class=""switch-link"" href=""{EscapeHtml(otherFileName)}"">Abrir outro turno</a>
        </section>

        <section class=""areas-panel"">
            <div class=""area-pages{BuildAreaPagesClass(tvAreaGroups.Count)}"" data-page-count=""{pageCount}"">
                {tvAreaMarkup}
                <div class=""page-indicator"" id=""pageIndicator"">
                    <button class=""page-nav"" id=""prevPage"" type=""button"" aria-label=""Pagina anterior"">&lt;</button>
                    <span class=""page-label"" id=""pageLabel"">1/{pageCount}</span>
                    <button class=""page-nav"" id=""nextPage"" type=""button"" aria-label=""Proxima pagina"">&gt;</button>
                </div>
            </div>
        </section>

        <aside class=""side-panel"">
            <section class=""summary-box"">
                {tvSummaryMarkup}
            </section>
            <section class=""leaders-box"">
                <h2 class=""panel-title"">Lideres presentes</h2>
                <div class=""leader-list"">{tvLeaderMarkup}</div>
            </section>
        </aside>

        <section class=""training-strip"">
            <h2 class=""panel-title"">Treinamentos</h2>
            <div class=""training-list"">{tvTrainingMarkup}</div>
        </section>
    </main>
    <script>
        (() => {{
            const pages = Array.from(document.querySelectorAll('.area-page'));
            const label = document.getElementById('pageLabel');
            const prev = document.getElementById('prevPage');
            const next = document.getElementById('nextPage');
            if (pages.length <= 1) {{
                return;
            }}

            let index = 0;
            const show = nextIndex => {{
                pages[index].classList.remove('active');
                index = (nextIndex + pages.length) % pages.length;
                pages[index].classList.add('active');
                if (label) {{
                    label.textContent = `${{index + 1}}/${{pages.length}}`;
                }}
            }};

            prev?.addEventListener('click', () => show(index - 1));
            next?.addEventListener('click', () => show(index + 1));
        }})();
    </script>
</body>
</html>";

        }
        private static string BuildTvAreaPagesMarkup(IReadOnlyList<IGrouping<string, HaidaiBoardRow>> groups)
        {
            var pages = groups
                .Select((group, index) => new { group, index })
                .GroupBy(item => item.index / TvExportPageSize)
                .Select((page, pageIndex) => $@"
                    <div class=""area-page{(pageIndex == 0 ? " active" : string.Empty)}"">
                        <div class=""area-grid"">
                            {string.Join(Environment.NewLine, page.Select(item => BuildTvAreaCardMarkup(item.group)))}
                        </div>
                    </div>");

            return string.Join(Environment.NewLine, pages);
        }

        private static string BuildAreaPagesClass(int areaCount)
        {
            var classes = new List<string>();
            if (areaCount > TvExportPageSize)
            {
                classes.Add("has-pages");
            }

            if (areaCount <= 8)
            {
                classes.Add("few-areas");
            }

            return classes.Count == 0 ? string.Empty : " " + string.Join(" ", classes);
        }

        private static string BuildTvAreaCardMarkup(IGrouping<string, HaidaiBoardRow> group)
        {
            var operators = group
                .OrderBy(row => string.IsNullOrWhiteSpace(row.PairKey) ? "ZZZ" + row.CodigoFJ : row.PairKey, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.IsTrainee)
                .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var hasPairGroup = operators
                .Where(row => !string.IsNullOrWhiteSpace(row.PairKey))
                .GroupBy(row => row.PairKey.Trim(), StringComparer.OrdinalIgnoreCase)
                .Any(pair => pair.Count() > 1);

            var operatorMarkup = BuildTvOperatorGroupMarkup(operators);
            var countClass = hasPairGroup
                ? " paired"
                : operators.Count == 2 ? " two-up" : string.Empty;

            return $@"
                <article class=""area-card{countClass}"">
                    <div class=""area-header"">
                        <div class=""area-code"">{EscapeHtml(group.Key)}</div>
                        <div class=""area-count"">{operators.Count}</div>
                    </div>
                    <div class=""operator-list"">{operatorMarkup}</div>
                </article>";
        }

        private static string BuildTvOperatorGroupMarkup(IReadOnlyList<HaidaiBoardRow> operators)
        {
            var groups = operators
                .GroupBy(
                    row => string.IsNullOrWhiteSpace(row.PairKey)
                        ? "__single__" + row.CodigoFJ
                        : row.PairKey.Trim(),
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => new
                {
                    Key = group.Key,
                    IsPair = !group.Key.StartsWith("__single__", StringComparison.OrdinalIgnoreCase) && group.Count() > 1,
                    Rows = group
                        .OrderBy(row => row.IsTrainee)
                        .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
                        .ToList()
                });

            return string.Join(Environment.NewLine, groups.Select(group =>
            {
                if (!group.IsPair)
                {
                    return string.Join(Environment.NewLine, group.Rows.Select(BuildTvOperatorMarkup));
                }

                return $@"
                    <div class=""operator-pair"">
                        <span class=""pair-label"">Par {EscapeHtml(group.Key)}</span>
                        {string.Join(Environment.NewLine, group.Rows.Select(BuildTvOperatorMarkup))}
                    </div>";
            }));
        }

        private static string BuildTvOperatorMarkup(HaidaiBoardRow row)
        {
            var classes = new List<string> { "operator-card" };
            if (row.IsTrainee)
            {
                classes.Add("trainee");
            }

            if (row.IsLeader)
            {
                classes.Add("leader");
            }

            var tags = new List<string>();
            if (row.IsLeader)
            {
                tags.Add(LeaderRole(row));
            }

            if (row.IsTrainee)
            {
                tags.Add("Treino");
            }

            if (!row.CountsTowardKousu)
            {
                tags.Add("Nao conta kousu");
            }

            if (!string.IsNullOrWhiteSpace(row.MovementSummary))
            {
                tags.Add(row.MovementSummary);
            }

            var meta = string.Join(" | ", tags);
            var metaMarkup = string.IsNullOrWhiteSpace(meta)
                ? string.Empty
                : $@"<div class=""operator-meta"">{EscapeHtml(meta)}</div>";

            return $@"
                <div class=""{EscapeHtml(string.Join(" ", classes))}"">
                    <img class=""operator-photo"" src=""{EscapeHtml(ResolveOperatorPhotoPath(row.CodigoFJ))}"" alt=""{EscapeHtml(row.Name)}"">
                    <div class=""operator-text"">
                        <div class=""operator-name"">{EscapeHtml(row.Name)}</div>
                        {metaMarkup}
                    </div>
                </div>";
        }

        private static string ResolveOperatorPhotoPath(string codigoFJ)
        {
            var normalizedCode = (codigoFJ ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedCode))
            {
                return "assets/default-operator.png";
            }

            var sourceOperators = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "ui",
                "presence",
                "assets",
                "operators");

            var extensions = new[] { ".png", ".jpg", ".jpeg", ".webp" };
            foreach (var extension in extensions)
            {
                var fileName = normalizedCode + extension;
                if (File.Exists(Path.Combine(sourceOperators, fileName)))
                {
                    return "assets/operators/" + fileName;
                }
            }

            return "assets/default-operator.png";
        }

        private static string BuildTrainingRowMarkup(HaidaiBoardRow row)
        {
            var trainer = string.IsNullOrWhiteSpace(row.TrainerName)
                ? "Treinador nao informado"
                : $"Treinador {row.TrainerName}";

            return $@"
                <div class=""training-row"">
                    <strong>{EscapeHtml(row.Name)}</strong>
                    <span>{EscapeHtml(BuildTvAreaKey(row))} | {EscapeHtml(trainer)}</span>
                </div>";
        }

        private static string BuildLeaderRowMarkup(HaidaiBoardRow row)
        {
            var role = LeaderRole(row);
            var roleClass = string.Equals(role, "GL", StringComparison.OrdinalIgnoreCase) ? " role-gl" : string.Empty;
            var status = string.IsNullOrWhiteSpace(row.Status) ? "Nao escalado" : row.Status;

            return $@"
                <div class=""leader-row"">
                    <strong><span class=""role-badge{roleClass}"">{EscapeHtml(role)}</span>{EscapeHtml(row.Name)}</strong>
                    <span>{EscapeHtml(row.GroupName)} | {EscapeHtml(status)}</span>
                </div>";
        }

        private static bool IsExportableTvAreaRow(HaidaiBoardRow row, int sectorId)
        {
            if (!row.LocalId.HasValue || row.LocalSectorId != sectorId)
            {
                return false;
            }

            return true;
        }

        private static bool IsAssignedInViewingSector(HaidaiBoardRow row, int viewingSectorId)
        {
            if (!row.IsLineupActive || string.IsNullOrWhiteSpace(row.AssignmentCode))
            {
                return false;
            }

            if (row.LocalSectorId.HasValue)
            {
                return row.LocalSectorId.Value == viewingSectorId;
            }

            return row.DisplayLocalSectorId == viewingSectorId;
        }

        private static string ResolveAreaTotalLabel(HaidaiBoardRow row)
        {
            var label = FirstNonEmpty(
                row.DisplayLocalShortCode,
                row.LocalShortCode,
                row.StoredAssignmentCode,
                row.AssignmentCode,
                row.DisplayLocalName,
                row.LocalName);

            return string.IsNullOrWhiteSpace(label) ? "Sem area" : label.Trim();
        }

        private static bool IsPresentLeader(HaidaiBoardRow row)
        {
            if (!row.IsLeader)
            {
                return false;
            }

            if (row.ExceptionMotiveId == 1 || row.ExceptionMotiveId == 2)
            {
                return false;
            }

            return !row.LocalId.HasValue
                && !string.Equals(row.Status, "Folga", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildTvAreaKey(HaidaiBoardRow row)
        {
            var value = FirstNonEmpty(row.DisplayLocalShortCode, row.LocalShortCode, row.AssignmentCode, row.DisplayLocalName, row.LocalName);
            return string.IsNullOrWhiteSpace(value) ? "Sem area" : value.Trim();
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }

        private static string LeaderRole(HaidaiBoardRow row)
        {
            return row.AccessLevel == 2 ? "KL" : "GL";
        }

        private static string AreaSortKey(string area)
        {
            var digits = new string((area ?? string.Empty).Where(char.IsDigit).ToArray());
            if (int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            {
                return number.ToString("0000", CultureInfo.InvariantCulture);
            }

            return area ?? string.Empty;
        }

        private static HaidaiExportLayoutDefinition? LoadExportLayout(int sectorId)
        {
            var layoutPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "ui",
                "presence-layout",
                "layouts.json");

            if (!File.Exists(layoutPath))
            {
                return null;
            }

            var json = File.ReadAllText(layoutPath);
            var layouts = JsonSerializer.Deserialize<Dictionary<string, HaidaiExportLayoutDefinition>>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            return layouts != null && layouts.TryGetValue(sectorId.ToString(CultureInfo.InvariantCulture), out var layout)
                ? layout
                : null;
        }

        private static string BuildMappedLayoutMarkup(
            HaidaiExportLayoutDefinition layout,
            IReadOnlyList<HaidaiBoardRow> activeRows,
            IReadOnlyList<(string Label, int Value)> summaryBadges)
        {
            var localsById = activeRows
                .Where(row => row.DisplayLocalId.HasValue || row.LocalId.HasValue)
                .GroupBy(row => row.DisplayLocalId ?? row.LocalId!.Value)
                .ToDictionary(group => group.Key, group => group.ToList());

            var corridors = string.Join(
                Environment.NewLine,
                layout.Corridors.Select(corridor => $@"
                    <div class=""corridor"" style=""left:{ToPercent(corridor.X, layout.Width)}%;top:{ToPercent(corridor.Y, layout.Height)}%;width:{ToPercent(corridor.W, layout.Width)}%;height:{ToPercent(corridor.H, layout.Height)}%;""></div>"));

            var slots = string.Join(
                Environment.NewLine,
                layout.Locals
                    .Where(slot => slot.ShowInExport != false)
                    .Select(slot =>
                {
                    var assigned = GetAssignedRowsForSlot(slot, localsById, activeRows);

                    var headerCode = slot.Header;
                    if (string.IsNullOrWhiteSpace(headerCode))
                    {
                        headerCode = assigned.FirstOrDefault()?.DisplayLocalShortCode;
                        if (string.IsNullOrWhiteSpace(headerCode))
                        {
                            headerCode = assigned.FirstOrDefault()?.LocalShortCode;
                        }
                        if (string.IsNullOrWhiteSpace(headerCode))
                        {
                            headerCode = assigned.FirstOrDefault()?.AssignmentCode;
                        }
                    }

                    headerCode ??= $"Area {slot.LocalId}";

                    var operators = slot.Members.Count > 0
                        ? $@"<div class=""operator-split split-{EscapeHtml(slot.SplitDirection ?? "horizontal")}"">
                                {string.Join(
                                    Environment.NewLine,
                                    slot.Members.Select(member =>
                                    {
                                        localsById.TryGetValue(member.LocalId, out var memberAssigned);
                                        var memberMarkup = BuildOperatorStackMarkup(memberAssigned ?? new List<HaidaiBoardRow>(), compact: true);
                                        var labelMarkup = string.IsNullOrWhiteSpace(member.Label)
                                            ? string.Empty
                                            : $@"<span class=""split-label"">{EscapeHtml(member.Label)}</span>";

                                        return $@"
                                            <div class=""split-cell"">
                                                {labelMarkup}
                                                {memberMarkup}
                                            </div>";
                                    }))}
                           </div>"
                        : BuildOperatorStackMarkup(assigned);

                    var helperLabel = slot.Members.Count > 0
                        ? string.Empty
                        : EscapeHtml(slot.Label ?? string.Empty);

                    return $@"
                        <article class=""local-box {(slot.Members.Count > 0 ? "local-box-compound" : string.Empty)}"" style=""left:{ToPercent(slot.X, layout.Width)}%;top:{ToPercent(slot.Y, layout.Height)}%;width:{ToPercent(slot.W, layout.Width)}%;height:{ToPercent(slot.H, layout.Height)}%;"">
                            <strong>{EscapeHtml(headerCode)}</strong>
                            <small>{helperLabel}</small>
                            <div class=""operator-stack"">
                                {operators}
                            </div>
                        </article>";
                }));

            var widgets = string.Join(
                Environment.NewLine,
                layout.Widgets.Select(widget => BuildWidgetMarkup(widget, layout, summaryBadges)));

            return $@"
                <div class=""layout-stage"" style=""aspect-ratio:{layout.Width} / {layout.Height};"">
                    <div class=""layout-canvas"">
                        {corridors}
                        {widgets}
                        {slots}
                    </div>
                </div>";
        }

        private static IReadOnlyList<HaidaiBoardRow> GetAssignedRowsForSlot(
            HaidaiExportLocalSlot slot,
            IReadOnlyDictionary<int, List<HaidaiBoardRow>> localsById,
            IReadOnlyList<HaidaiBoardRow> activeRows)
        {
            var memberSlots = slot.Members.Count > 0
                ? slot.Members
                : new List<HaidaiExportLocalMemberSlot>
                {
                    new() { LocalId = slot.LocalId }
                };

            var rows = memberSlots
                .SelectMany(member =>
                {
                    localsById.TryGetValue(member.LocalId, out var memberAssigned);
                    return memberAssigned ?? Enumerable.Empty<HaidaiBoardRow>();
                })
                .ToList();

            if (slot.MatchCodes.Count == 0)
            {
                return rows;
            }

            var matchCodes = slot.MatchCodes
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Select(code => code.Trim().ToUpperInvariant())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (matchCodes.Count == 0)
            {
                return rows;
            }

            foreach (var row in activeRows)
            {
                var assignmentCode = (row.AssignmentCode ?? string.Empty).Trim();
                if (!matchCodes.Contains(assignmentCode))
                {
                    continue;
                }

                if (rows.Any(item => string.Equals(item.CodigoFJ, row.CodigoFJ, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                rows.Add(row);
            }

            return rows;
        }

        private static string BuildWidgetMarkup(
            HaidaiExportWidgetSlot widget,
            HaidaiExportLayoutDefinition layout,
            IReadOnlyList<(string Label, int Value)> summaryBadges)
        {
            if (!string.Equals(widget.Kind, "summary", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            var summaryMarkup = string.Join(
                Environment.NewLine,
                summaryBadges.Take(4).Select(item => $@"
                    <div class=""summary-mini"">
                        <strong>{item.Value}</strong>
                        <span>{EscapeHtml(item.Label)}</span>
                    </div>"));

            return $@"
                <aside class=""map-widget summary-widget"" style=""left:{ToPercent(widget.X, layout.Width)}%;top:{ToPercent(widget.Y, layout.Height)}%;width:{ToPercent(widget.W, layout.Width)}%;height:{ToPercent(widget.H, layout.Height)}%;"">
                    {summaryMarkup}
                </aside>";
        }

        private static string BuildOperatorStackMarkup(IReadOnlyList<HaidaiBoardRow> assigned, bool compact = false)
        {
            if (assigned.Count == 0)
            {
                return compact
                    ? "<span class=\"operator-pill\"><span class=\"subcode\">Sem op.</span></span>"
                    : "<span class=\"operator-pill\"><span class=\"subcode\">Sem operador</span></span>";
            }

            if (compact)
            {
                return string.Join(
                    Environment.NewLine,
                    assigned.Select(row => $@"
                        <span class=""operator-pill {(row.IsTrainee ? "trainee" : string.Empty)}"">
                            {EscapeHtml(row.Name)}
                        </span>"));
            }

            return string.Join(
                Environment.NewLine,
                assigned.Select(row =>
                {
                    var detailCode = string.Equals(row.AssignmentCode, row.LocalShortCode, StringComparison.OrdinalIgnoreCase)
                        ? string.Empty
                        : row.AssignmentCode;

                    var tags = new List<string>();
                    if (row.IsTrainee)
                    {
                        tags.Add("Aprendiz");
                    }

                    if (!row.CountsTowardKousu)
                    {
                        tags.Add("Nao conta kousu");
                    }

                    if (!string.IsNullOrWhiteSpace(detailCode))
                    {
                        tags.Insert(0, detailCode);
                    }

                    var suffix = tags.Count == 0
                        ? string.Empty
                        : $"<span class=\"subcode\">{EscapeHtml(string.Join(" | ", tags))}</span>";

                    return $@"
                        <span class=""operator-pill {(row.IsTrainee ? "trainee" : string.Empty)}"">
                            {EscapeHtml(row.Name)}
                            {suffix}
                        </span>";
                }));
        }

        private static bool SlotMatchesLocalId(HaidaiExportLocalSlot slot, int localId)
        {
            return slot.LocalId == localId || slot.Members.Any(member => member.LocalId == localId);
        }

        private static bool SlotMatchesRow(HaidaiExportLocalSlot slot, HaidaiBoardRow row)
        {
            var mappedLocalId = row.DisplayLocalId ?? row.LocalId;
            if (mappedLocalId.HasValue && SlotMatchesLocalId(slot, mappedLocalId.Value))
            {
                return true;
            }

            if (slot.MatchCodes.Count == 0)
            {
                return false;
            }

            var assignmentCode = (row.AssignmentCode ?? string.Empty).Trim();
            return slot.MatchCodes.Any(code => string.Equals(code?.Trim(), assignmentCode, StringComparison.OrdinalIgnoreCase));
        }

        private static string EscapeHtml(string? value)
        {
            return (value ?? string.Empty)
                .Replace("&", "&amp;", StringComparison.Ordinal)
                .Replace("<", "&lt;", StringComparison.Ordinal)
                .Replace(">", "&gt;", StringComparison.Ordinal)
                .Replace("\"", "&quot;", StringComparison.Ordinal);
        }

        private static string ToPercent(int value, int max)
        {
            if (max <= 0)
            {
                return "0";
            }

            return ((value / (double)max) * 100d).ToString("0.###", CultureInfo.InvariantCulture);
        }

        private sealed class HaidaiSectorReference
        {
            public int Id { get; set; }
            public string Code { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
        }

        private sealed record HaidaiSectorTopology(
            HaidaiSectorReference? GBareru,
            HaidaiSectorReference? Dad,
            HaidaiSectorReference? Gdad)
        {
            public static HaidaiSectorTopology Empty { get; } = new(null, null, null);

            public bool SupportsSharedGdad => GBareru != null && Dad != null && Gdad != null;
        }
    }

    public sealed record HaidaiInitPayload(
        string DateIso,
        int ShiftId,
        int SectorId,
        IReadOnlyList<HaidaiLookupItem> Shifts,
        IReadOnlyList<HaidaiLookupItem> Sectors,
        IReadOnlyList<HaidaiLocalLookup> Locals,
        IReadOnlyList<HaidaiOperatorLookup> Operators,
        HaidaiSharedSectorTopology SharedSectorTopology);

    public sealed record HaidaiSharedSectorTopology(
        int? GBareruSectorId,
        int? DadSectorId,
        int? GdadSectorId)
    {
        public bool IsEnabled => GBareruSectorId.HasValue && DadSectorId.HasValue && GdadSectorId.HasValue;
    }

    public sealed class HaidaiLookupItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public sealed class HaidaiLocalLookup
    {
        public int Id { get; set; }
        public int SectorId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ShortCode { get; set; } = string.Empty;
    }

    public sealed class HaidaiOperatorLookup
    {
        public string CodigoFJ { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int SectorId { get; set; }
        public int ShiftId { get; set; }
        public int GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public bool Trainer { get; set; }
        public bool IsLeader { get; set; }
    }

    public sealed record HaidaiMonthlyPlanPayload(
        int Year,
        int Month,
        int ShiftId,
        int SectorId,
        IReadOnlyList<int> Days,
        IReadOnlyList<HaidaiMonthlyGroupPlan> Groups);

    public sealed record HaidaiMonthlyGroupPlan(
        int GroupId,
        string GroupName,
        IReadOnlyList<HaidaiMonthlyOperatorPlan> Operators);

    public sealed record HaidaiMonthlyOperatorPlan(
        string CodigoFJ,
        string Name,
        string NameJp,
        int GroupId,
        string GroupName,
        IReadOnlyList<HaidaiMonthlyCell> Cells);

    public sealed record HaidaiMonthlyCell(
        int Day,
        string AssignmentCode,
        int? LocalId,
        bool IsTrainee,
        bool IsLineupActive,
        bool IsHolidayWork,
        string Status);

    public sealed record HaidaiMonthlySaveRequest(
        int Year,
        int Month,
        int ShiftId,
        int SectorId,
        IReadOnlyList<HaidaiMonthlySaveCell> Cells);

    public sealed record HaidaiMonthlySaveCell(
        string OperatorCodigoFJ,
        int Day,
        string AssignmentCode,
        bool IsHolidayWork);

    public sealed record HaidaiBoardPayload(
        string DateIso,
        int ShiftId,
        int SectorId,
        HaidaiSummary Summary,
        IReadOnlyList<HaidaiGroupBlock> Groups,
        IReadOnlyList<HaidaiAreaTotal> AreaTotals);

    public sealed record HaidaiSummary(
        int OperatorCount,
        int AssignedCount,
        int YukyuCount,
        int FaltaCount,
        int LateCount,
        int EarlyLeaveCount,
        int TraineeCount,
        int PairCount);

    public sealed record HaidaiAreaTotal(
        string Area,
        int OperatorCount);

    public sealed record HaidaiGroupBlock(
        int GroupId,
        string GroupName,
        int OperatorCount,
        IReadOnlyList<HaidaiBoardRow> Rows);

    public sealed record HaidaiBoardRow(
        string CodigoFJ,
        string Name,
        string NameJp,
        int GroupId,
        string GroupName,
        bool Trainer,
        bool IsLeader,
        int AccessLevel,
        int? LocalId,
        string LocalName,
        string LocalShortCode,
        int? LocalSectorId,
        int? DisplayLocalId,
        string DisplayLocalName,
        string DisplayLocalShortCode,
        int? DisplayLocalSectorId,
        string AssignmentCode,
        string StoredAssignmentCode,
        string PairKey,
        bool IsTrainee,
        string TrainerCodigoFJ,
        string TrainerName,
        bool CountsTowardKousu,
        bool IsLineupActive,
        bool IsHolidayWork,
        string Notes,
        int ExceptionMotiveId,
        string ExceptionMotiveName,
        string ExceptionNotes,
        string Status,
        string MovementSummary,
        int MovementCount,
        IReadOnlyList<HaidaiBoardMovement> Movements);

    public sealed record HaidaiBoardMovement(
        int Id,
        string MovementType,
        string EventTime,
        string EventDateTime,
        string AssignmentCode,
        string PairKey,
        string ReplacementOperatorCodigoFJ,
        string Reason,
        string CreatedAt);

    public sealed record HaidaiSaveAssignmentRequest(
        DateTime Date,
        int ShiftId,
        int SectorId,
        string OperatorCodigoFJ,
        int? LocalId,
        string AssignmentCode,
        string PairKey,
        bool IsTrainee,
        string TrainerCodigoFJ,
        bool CountsTowardKousu,
        string Notes,
        bool IsHolidayWork,
        bool ApplyPairToMonth = false);

    public sealed record HaidaiMovementRequest(
        DateTime Date,
        int ShiftId,
        int SectorId,
        string OperatorCodigoFJ,
        int? MovementId,
        string MovementType,
        string EventTime,
        string ReplacementOperatorCodigoFJ,
        int? LocalId,
        string AssignmentCode,
        string PairKey,
        string Reason,
        string CreatedByCodigoFJ);

    public sealed record HaidaiExportResult(string Directory, IReadOnlyList<string> Files);
    public sealed class HaidaiPlannedAssignment
    {
        public string OperatorCodigoFJ { get; set; } = string.Empty;
        public int ShiftId { get; set; }
        public int SectorId { get; set; }
        public int? LocalId { get; set; }
        public string AssignmentCode { get; set; } = string.Empty;
        public string PairKey { get; set; } = string.Empty;
        public bool IsTrainee { get; set; }
        public string TrainerCodigoFJ { get; set; } = string.Empty;
        public bool CountsTowardKousu { get; set; }
    }

    internal sealed class HaidaiOperatorRow
    {
        public string CodigoFJ { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string NameJp { get; set; } = string.Empty;
        public int HomeSectorId { get; set; }
        public int GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public bool Trainer { get; set; }
        public bool IsLeader { get; set; }
        public int AccessLevel { get; set; }
    }

    internal sealed class HaidaiOperatorSectorRow
    {
        public string CodigoFJ { get; set; } = string.Empty;
        public int SectorId { get; set; }
    }

    internal sealed class TrainerLookupRow
    {
        public string CodigoFJ { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    internal sealed class HaidaiAssignmentRecord
    {
        public int Id { get; set; }
        public string ScheduleDate { get; set; } = string.Empty;
        public int ShiftId { get; set; }
        public int SectorId { get; set; }
        public string OperatorCodigoFJ { get; set; } = string.Empty;
        public int? LocalId { get; set; }
        public string AssignmentCode { get; set; } = string.Empty;
        public string PairKey { get; set; } = string.Empty;
        public bool IsTrainee { get; set; }
        public string TrainerCodigoFJ { get; set; } = string.Empty;
        public bool CountsTowardKousu { get; set; }
        public bool IsLineupActive { get; set; }
        public bool IsHolidayWork { get; set; }
        public string AvailabilityStatus { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }

    internal sealed class HaidaiMovementRecord
    {
        public int Id { get; set; }
        public string OperatorCodigoFJ { get; set; } = string.Empty;
        public string MovementType { get; set; } = string.Empty;
        public string EventTime { get; set; } = string.Empty;
        public string EventDateTime { get; set; } = string.Empty;
        public string AssignmentCode { get; set; } = string.Empty;
        public string PairKey { get; set; } = string.Empty;
        public string ReplacementOperatorCodigoFJ { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
    }

    internal sealed class HaidaiExceptionRecord
    {
        public string OperatorCodigoFJ { get; set; } = string.Empty;
        public int MotiveId { get; set; }
        public string MotiveName { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }

    internal sealed class HaidaiMonthlyExceptionRecord
    {
        public string OperatorCodigoFJ { get; set; } = string.Empty;
        public string RequestDate { get; set; } = string.Empty;
        public int MotiveId { get; set; }
    }

    internal sealed record HaidaiResolvedMonthlyCell(
        int? LocalId,
        string AssignmentCode,
        bool IsTrainee,
        bool IsLineupActive,
        bool IsHolidayWork,
        string AvailabilityStatus);

    internal sealed class HaidaiExportLayoutDefinition
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public List<HaidaiExportLocalSlot> Locals { get; set; } = new();
        public List<HaidaiExportWidgetSlot> Widgets { get; set; } = new();
        public List<HaidaiExportCorridorSlot> Corridors { get; set; } = new();
    }

    internal sealed class HaidaiExportLocalSlot
    {
        public int LocalId { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int W { get; set; }
        public int H { get; set; }
        public string? Header { get; set; }
        public string? Label { get; set; }
        public string? SplitDirection { get; set; }
        public bool? ShowInExport { get; set; }
        public List<string> MatchCodes { get; set; } = new();
        public List<HaidaiExportLocalMemberSlot> Members { get; set; } = new();
    }

    internal sealed class HaidaiExportWidgetSlot
    {
        public string Kind { get; set; } = string.Empty;
        public int X { get; set; }
        public int Y { get; set; }
        public int W { get; set; }
        public int H { get; set; }
    }

    internal sealed class HaidaiExportLocalMemberSlot
    {
        public int LocalId { get; set; }
        public string? Label { get; set; }
    }

    internal sealed class HaidaiExportCorridorSlot
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int W { get; set; }
        public int H { get; set; }
        public bool Rotate { get; set; }
    }
}
