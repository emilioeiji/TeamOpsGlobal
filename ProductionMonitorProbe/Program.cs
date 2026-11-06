using System.Globalization;
using System.Diagnostics;
using System.Configuration;
using Dapper;
using TeamOps.Core.Common;
using TeamOps.Config;
using TeamOps.Data.Db;
using TeamOps.Data.Repositories;
using TeamOps.Services;
using TeamOps.UI.Forms.Models;

var settings = new DbSettings();
var factory = new SqliteConnectionFactory(settings);
var machineRepository = new ProductionMachineRepository(factory);
var eventRepository = new ProductionEventRepository(factory);
var importer = new ProductionFileImporter(factory, machineRepository, eventRepository);
var analytics = new ProductionAnalyticsService(factory);
var sectorCatalog = new SectorCatalogService(factory);

var command = args.Length > 0
    ? args[0].Trim().ToLowerInvariant()
    : "help";

switch (command)
{
    case "help":
    case "-h":
    case "--help":
        PrintHelp();
        break;

    case "import":
        RunImport();
        break;

    case "import-profile":
        RunImportProfile(args.Skip(1).ToArray());
        break;

    case "db-index-check":
        RunDbIndexCheck();
        break;

    case "ec2-diagnostics":
        RunEc2Diagnostics();
        break;

    case "ec2-reset-latest":
        RunEc2ResetLatest();
        break;

    case "dashboard":
        ShowDashboards();
        break;

    case "status-hardening":
        RunStatusHardeningChecks();
        break;

    case "status-report":
        RunStatusReport(args.Skip(1).ToArray());
        break;

    case "schema-check":
        RunSchemaValidation(repair: false);
        break;

    case "schema-repair":
        RunSchemaValidation(repair: true);
        break;

    case "production-diagnostics":
        RunProductionDiagnostics();
        break;

    case "production-audit":
        RunProductionAudit(args.Skip(1).ToArray());
        break;

    case "validate-reports":
        RunValidateReports(args.Skip(1).ToArray());
        break;

    case "validate-yakin-production":
        RunValidateYakinProduction(args.Skip(1).ToArray());
        break;

    case "validate-overtime-rules":
        RunValidateOvertimeRules();
        break;

    case "validate-overtime-real":
        RunValidateOvertimeReal(args.Skip(1).ToArray());
        break;

    case "machine-cleanup":
        RunMachineCleanup(args.Skip(1).ToArray());
        break;

    case "machine-location-guard":
        RunMachineLocationGuard();
        break;

    case "demo":
        RunImport();
        Console.WriteLine();
        ShowDashboards();
        break;

    default:
        PrintHelp();
        Environment.ExitCode = 1;
        break;
}

void PrintHelp()
{
    Console.WriteLine("ProductionMonitorProbe - comandos");
    Console.WriteLine();
    Console.WriteLine("Uso:");
    Console.WriteLine("  ProductionMonitorProbe.exe <comando> [opcoes]");
    Console.WriteLine();
    Console.WriteLine("Comandos seguros para diagnostico:");
    Console.WriteLine("  schema-check");
    Console.WriteLine("  db-index-check");
    Console.WriteLine("  production-diagnostics");
    Console.WriteLine("  production-audit --date yyyy-MM-dd [--sector dad|gbareru]");
    Console.WriteLine("  validate-reports --date yyyy-MM-dd --shift noite --sector gbareru [--area Area1] [--machine K09] [--operator all|FJ]");
    Console.WriteLine("  validate-yakin-production --date yyyy-MM-dd [--operator CODIGOFJ]");
    Console.WriteLine("  validate-overtime-rules");
    Console.WriteLine("  validate-overtime-real --date yyyy-MM-dd --shift yakin [--operator CODIGOFJ]");
    Console.WriteLine("  ec2-diagnostics");
    Console.WriteLine("  dashboard");
    Console.WriteLine("  status-report --start yyyy-MM-dd --end yyyy-MM-dd --sector dad");
    Console.WriteLine();
    Console.WriteLine("Comandos que podem importar/alterar dados:");
    Console.WriteLine("  import");
    Console.WriteLine("  import-profile --date yyyy-MM-dd [--sector dad|gbareru] [--reimport]");
    Console.WriteLine("  schema-repair");
    Console.WriteLine("  ec2-reset-latest");
    Console.WriteLine("  machine-cleanup [--apply]");
    Console.WriteLine("  machine-location-guard");
    Console.WriteLine();
    Console.WriteLine("Observacao:");
    Console.WriteLine("  Abrir sem comando mostra esta ajuda. Use 'demo' apenas quando o BAT de importacao estiver configurado.");
}

void RunImport()
{
    TeamOps.Core.Entities.ProductionImportResult result;
    try
    {
        result = importer.ImportLatest();
    }
    catch (FileNotFoundException ex)
    {
        Console.WriteLine("=== IMPORT FAILED ===");
        Console.WriteLine(ex.Message);
        Environment.ExitCode = 2;
        return;
    }

    Console.WriteLine("=== IMPORT RESULT ===");
    Console.WriteLine($"FilesRead={result.FilesRead}");
    Console.WriteLine($"LinesRead={result.LinesRead}");
    Console.WriteLine($"Imported={result.Imported}");
    Console.WriteLine($"Ignored={result.Ignored}");
    Console.WriteLine($"MachinesCreated={result.MachinesCreated}");
    Console.WriteLine($"BatchExecuted={result.BatchExecuted}");
    Console.WriteLine($"BatchMessage={result.BatchMessage}");
    PrintDadImportResult(result);
    PrintEc2ImportResult(result);
    PrintImportPerformance(result);

    if (result.Errors.Count > 0)
    {
        Console.WriteLine("Errors:");
        foreach (var error in result.Errors)
        {
            Console.WriteLine($" - {error}");
        }
    }
}

void RunMachineLocationGuard()
{
    PrintRuntimeConfigDiagnostics();
    using var conn = factory.CreateOpenConnection();

    var before = ReadMachineLocationSnapshot(conn);
    Console.WriteLine("=== MACHINE LOCATION GUARD ===");
    Console.WriteLine($"Database={settings.DatabasePath}");
    Console.WriteLine($"MachinesBefore={before.Count}");

    TeamOps.Core.Entities.ProductionImportResult result;
    try
    {
        result = importer.ImportLatest();
    }
    catch (FileNotFoundException ex)
    {
        Console.WriteLine("=== IMPORT FAILED ===");
        Console.WriteLine(ex.Message);
        Environment.ExitCode = 2;
        return;
    }

    Console.WriteLine();
    Console.WriteLine("=== IMPORT RESULT ===");
    Console.WriteLine($"FilesRead={result.FilesRead}");
    Console.WriteLine($"LinesRead={result.LinesRead}");
    Console.WriteLine($"Imported={result.Imported}");
    Console.WriteLine($"Ignored={result.Ignored}");
    Console.WriteLine($"MachinesCreated={result.MachinesCreated}");
    Console.WriteLine($"BatchExecuted={result.BatchExecuted}");
    Console.WriteLine($"BatchMessage={result.BatchMessage}");
    PrintDadImportResult(result);
    PrintEc2ImportResult(result);
    PrintImportPerformance(result);

    var after = ReadMachineLocationSnapshot(conn);
    var changed = before.Values
        .Select(previous =>
        {
            after.TryGetValue(previous.Id, out var current);
            return new
            {
                Previous = previous,
                Current = current
            };
        })
        .Where(item => item.Current != null
            && (item.Previous.SectorId != item.Current.SectorId
                || item.Previous.LocalId != item.Current.LocalId))
        .ToList();

    Console.WriteLine();
    Console.WriteLine($"MachinesAfter={after.Count}");
    Console.WriteLine($"MachineLocationChanges={changed.Count}");

    foreach (var item in changed.Take(100))
    {
        Console.WriteLine(
            "  "
            + $"MachineId={item.Previous.Id} "
            + $"MachineCode={item.Previous.MachineCode} "
            + $"MachineKey={item.Previous.MachineKey} "
            + $"BeforeSectorId={FormatNullableInt(item.Previous.SectorId)} "
            + $"AfterSectorId={FormatNullableInt(item.Current!.SectorId)} "
            + $"BeforeLocalId={FormatNullableInt(item.Previous.LocalId)} "
            + $"AfterLocalId={FormatNullableInt(item.Current.LocalId)}");
    }

    if (changed.Count > 100)
    {
        Console.WriteLine($"  ... {changed.Count - 100} alteracoes omitidas no console.");
    }

    if (changed.Count > 0)
    {
        Console.WriteLine("FAIL: a importacao alterou SectorId/LocalId de maquinas existentes.");
        Environment.ExitCode = 3;
        return;
    }

    Console.WriteLine("OK: nenhuma maquina existente teve SectorId/LocalId alterado pela importacao.");
}

void RunValidateOvertimeRules()
{
    var monday = new DateTime(2026, 6, 8);
    var sunday = new DateTime(2026, 6, 7);
    var cases = new[]
    {
        (Code: "H1", Name: "Hirukin teiji", Input: new OvertimeCalculationInput("OP-H1", "Case H1", monday, "Dia", true, false, "present", monday.AddHours(18).AddMinutes(35), false, false, false, false), ExpectedMinutes: 0d),
        (Code: "H2", Name: "Hirukin 2h", Input: new OvertimeCalculationInput("OP-H2", "Case H2", monday, "Dia", true, false, "present", monday.AddHours(20).AddMinutes(35), false, false, false, false), ExpectedMinutes: 120d),
        (Code: "H3", Name: "Hirukin 1h", Input: new OvertimeCalculationInput("OP-H3", "Case H3", monday, "Dia", true, false, "present", monday.AddHours(19).AddMinutes(35), false, false, false, false), ExpectedMinutes: 60d),
        (Code: "H4", Name: "Hirukin antes do teiji", Input: new OvertimeCalculationInput("OP-H4", "Case H4", monday, "Dia", true, false, "present", monday.AddHours(18), false, false, false, false), ExpectedMinutes: 0d),
        (Code: "Y1", Name: "Yakin teiji", Input: new OvertimeCalculationInput("OP-Y1", "Case Y1", monday, "Noite", true, false, "present", monday.AddDays(1).AddHours(6).AddMinutes(35), false, false, false, false), ExpectedMinutes: 0d),
        (Code: "Y2", Name: "Yakin 2h", Input: new OvertimeCalculationInput("OP-Y2", "Case Y2", monday, "Noite", true, false, "present", monday.AddDays(1).AddHours(8).AddMinutes(35), false, false, false, false), ExpectedMinutes: 120d),
        (Code: "Y3", Name: "Yakin 1h", Input: new OvertimeCalculationInput("OP-Y3", "Case Y3", monday, "Noite", true, false, "present", monday.AddDays(1).AddHours(7).AddMinutes(35), false, false, false, false), ExpectedMinutes: 60d),
        (Code: "Y4", Name: "Yakin antes do teiji", Input: new OvertimeCalculationInput("OP-Y4", "Case Y4", monday, "Noite", true, false, "present", monday.AddDays(1).AddHours(6), false, false, false, false), ExpectedMinutes: 0d),
        (Code: "S1", Name: "Domingo normal", Input: new OvertimeCalculationInput("OP-S1", "Case S1", sunday, "Dia", true, false, "present", sunday.AddHours(20).AddMinutes(35), false, false, false, false), ExpectedMinutes: 0d),
        (Code: "S2", Name: "Domingo holiday work", Input: new OvertimeCalculationInput("OP-S2", "Case S2", sunday, "Dia", true, true, "present", sunday.AddHours(20).AddMinutes(35), false, false, false, false), ExpectedMinutes: 660d),
        (Code: "A1", Name: "Falta", Input: new OvertimeCalculationInput("OP-A1", "Case A1", monday, "Dia", true, false, "falta", monday.AddHours(20).AddMinutes(35), false, false, false, false), ExpectedMinutes: 0d),
        (Code: "A2", Name: "Yukyu", Input: new OvertimeCalculationInput("OP-A2", "Case A2", monday, "Dia", true, false, "yukyu", monday.AddHours(20).AddMinutes(35), false, false, false, false), ExpectedMinutes: 0d)
    };

    Console.WriteLine("=== OVERTIME RULE VALIDATION ===");
    Console.WriteLine("Case\tExpectedMinutes\tActualMinutes\tPass\tReason");

    var audits = new List<OvertimeCalculationAudit>();
    var failures = 0;
    foreach (var item in cases)
    {
        var audit = OvertimeRuleCalculator.Calculate(item.Input);
        audits.Add(audit);
        var pass = Math.Abs(audit.TotalOvertimeMinutes - item.ExpectedMinutes) < 0.1;
        if (!pass)
        {
            failures++;
        }

        Console.WriteLine($"{item.Code}\t{item.ExpectedMinutes}\t{audit.TotalOvertimeMinutes.ToString("0.#", CultureInfo.InvariantCulture)}\t{(pass ? "OK" : "FAIL")}\t{audit.Reason}");
    }

    Console.WriteLine();
    Console.WriteLine("OperatorId\tOperatorName\tDate\tDayOfWeek\tShift\tIsSunday\tIsHolidayWork\tAttendanceStatus\tScheduledStart\tTeijiEnd\tZangyouStart\tZangyouEnd\tActualEnd\tHasLate\tHasEarlyLeave\tNormalZangyouMinutes\tHolidayWorkMinutes\tTotalOvertimeMinutes\tReason");
    foreach (var audit in audits)
    {
        Console.WriteLine(string.Join("\t", new[]
        {
            audit.OperatorId,
            audit.OperatorName,
            audit.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            audit.DayOfWeek,
            audit.Shift,
            audit.IsSunday ? "1" : "0",
            audit.IsHolidayWork ? "1" : "0",
            audit.AttendanceStatus,
            FormatAuditDateTime(audit.ScheduledStart),
            FormatAuditDateTime(audit.TeijiEnd),
            FormatAuditDateTime(audit.ZangyouStart),
            FormatAuditDateTime(audit.ZangyouEnd),
            FormatAuditDateTime(audit.ActualEnd),
            audit.HasLate ? "1" : "0",
            audit.HasEarlyLeave ? "1" : "0",
            audit.NormalZangyouMinutes.ToString("0.#", CultureInfo.InvariantCulture),
            audit.HolidayWorkMinutes.ToString("0.#", CultureInfo.InvariantCulture),
            audit.TotalOvertimeMinutes.ToString("0.#", CultureInfo.InvariantCulture),
            audit.Reason
        }));
    }

    Console.WriteLine();
    var aggregateCases = new[]
    {
        new OvertimeAggregateCase("C1", 40d, 0, 0, 40d, 40d, false, false, false),
        new OvertimeAggregateCase("C2", 40d, 1, 0, 51d, 51d, true, false, false),
        new OvertimeAggregateCase("C3", 40d, 0, 2, 40d, 62d, false, false, false),
        new OvertimeAggregateCase("C4", 40d, 1, 2, 51d, 73d, true, false, false),
        new OvertimeAggregateCase("C5", 0d, 0, 0, 0d, 0d, false, false, false),
        new OvertimeAggregateCase("C6", 40d, 1, 0, 51d, 51d, true, false, false),
        new OvertimeAggregateCase("C7", 45d, 0, 0, 45d, 45d, false, false, false),
        new OvertimeAggregateCase("C8", 70d, 1, 0, 81d, 81d, true, true, false),
        new OvertimeAggregateCase("C9", 80d, 1, 0, 91d, 91d, true, false, true)
    };

    Console.WriteLine("=== OVERTIME AGGREGATE VALIDATION ===");
    Console.WriteLine("Case\tNormalZangyouHours\tShukkinDays\tShukkinHours\tSundayWorkedDays\tSundayWorkedHours\tExpectedZangyouJikan\tActualZangyouJikan\tExpectedDomingoShukkin\tActualDomingoShukkin\tPass\tZangyouOver45\tDomingo80To90\tDomingoOver90");
    foreach (var item in aggregateCases)
    {
        var holidayWorkHours = item.ShukkinDays * 11d;
        var sundayWorkedHours = item.SundayWorkedDays * 11d;
        var actualZangyou = Math.Round(item.NormalZangyouHours + holidayWorkHours, 1);
        var actualDomingoShukkin = Math.Round(actualZangyou + sundayWorkedHours, 1);
        var zangyouOver45 = actualZangyou > 45d;
        var domingo80To90 = actualDomingoShukkin > 80d && actualDomingoShukkin <= 90d;
        var domingoOver90 = actualDomingoShukkin > 90d;
        var pass = Math.Abs(actualZangyou - item.ExpectedZangyouJikanHours) < 0.1
            && Math.Abs(actualDomingoShukkin - item.ExpectedDomingoShukkinHours) < 0.1
            && zangyouOver45 == item.ExpectedZangyouOver45
            && domingo80To90 == item.ExpectedDomingo80To90
            && domingoOver90 == item.ExpectedDomingoOver90;
        if (!pass)
        {
            failures++;
        }

        Console.WriteLine(string.Join("\t", new[]
        {
            item.Code,
            item.NormalZangyouHours.ToString("0.0", CultureInfo.InvariantCulture),
            item.ShukkinDays.ToString(CultureInfo.InvariantCulture),
            holidayWorkHours.ToString("0.0", CultureInfo.InvariantCulture),
            item.SundayWorkedDays.ToString(CultureInfo.InvariantCulture),
            sundayWorkedHours.ToString("0.0", CultureInfo.InvariantCulture),
            item.ExpectedZangyouJikanHours.ToString("0.0", CultureInfo.InvariantCulture),
            actualZangyou.ToString("0.0", CultureInfo.InvariantCulture),
            item.ExpectedDomingoShukkinHours.ToString("0.0", CultureInfo.InvariantCulture),
            actualDomingoShukkin.ToString("0.0", CultureInfo.InvariantCulture),
            pass ? "OK" : "FAIL",
            zangyouOver45 ? "1" : "0",
            domingo80To90 ? "1" : "0",
            domingoOver90 ? "1" : "0"
        }));
    }

    Console.WriteLine();
    Console.WriteLine($"Summary: Cases={cases.Length + aggregateCases.Length} Failures={failures}");
    if (failures > 0)
    {
        Environment.ExitCode = 2;
    }
}

void RunValidateOvertimeReal(string[] overtimeArgs)
{
    var options = ValidateOvertimeRealOptions.Parse(overtimeArgs);
    using var conn = factory.CreateOpenConnection();
    new TeamOps.UI.Services.HaidaiModuleService(factory).RefreshSectorTopology();

    var shift = ResolveValidateShift(conn, options.Shift);
    var rows = conn.Query<OvertimeRealScheduleRow>(
        @"
            SELECT
                o.CodigoFJ AS OperatorCodigoFJ,
                COALESCE(NULLIF(o.NameRomanji, ''), o.CodigoFJ) AS OperatorName,
                o.ShiftId,
                COALESCE(NULLIF(s.NamePt, ''), NULLIF(s.NameJp, ''), 'Turno ' || o.ShiftId) AS ShiftName,
                o.SectorId,
                COALESCE(NULLIF(sec.NamePt, ''), NULLIF(sec.NameJp, ''), 'Setor ' || o.SectorId) AS SectorName,
                date(ha.ScheduleDate) AS BaseDate,
                ha.LocalId,
                COALESCE(NULLIF(l.NamePt, ''), NULLIF(l.NameJp, ''), '') AS LocalName,
                COALESCE(ha.AssignmentCode, '') AS AssignmentCode,
                COALESCE(ha.AvailabilityStatus, '') AS AvailabilityStatus,
                COALESCE(ha.IsHolidayWork, 0) AS IsHolidayWork
            FROM HaidaiAssignments ha
            INNER JOIN Operators o ON o.CodigoFJ = ha.OperatorCodigoFJ
            LEFT JOIN Shifts s ON s.Id = o.ShiftId
            LEFT JOIN Sectors sec ON sec.Id = o.SectorId
            LEFT JOIN Locals l ON l.Id = ha.LocalId
            WHERE date(ha.ScheduleDate) = date(@BaseDate)
              AND ha.ShiftId = @ShiftId
              AND (@Operator = '' OR upper(trim(ha.OperatorCodigoFJ)) = upper(trim(@Operator)))
            ORDER BY o.SectorId, o.CodigoFJ, ha.Id DESC;",
        new
        {
            BaseDate = options.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ShiftId = shift.Id,
            Operator = options.Operator.Equals("all", StringComparison.OrdinalIgnoreCase) ? string.Empty : options.Operator
        })
        .GroupBy(item => item.OperatorCodigoFJ, StringComparer.OrdinalIgnoreCase)
        .Select(group => group.First())
        .ToList();

    Console.WriteLine("=== VALIDATE OVERTIME REAL ===");
    Console.WriteLine($"Database={settings.DatabasePath}");
    Console.WriteLine($"BaseDate={options.Date:yyyy-MM-dd}");
    Console.WriteLine($"Shift={shift.Id} {shift.NamePt} {shift.NameJp}".Trim());
    Console.WriteLine($"OperatorFilter={options.Operator}");
    Console.WriteLine($"Rows={rows.Count}");
    Console.WriteLine();

    if (rows.Count == 0)
    {
        Console.WriteLine("No scheduled operators found for this base date/shift.");
        return;
    }

    var operatorCodes = rows.Select(item => item.OperatorCodigoFJ).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    var presenceEvents = conn.Query<OvertimeRealPresenceEventRow>(
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
            StartDateTime = options.Date.Date,
            EndExclusive = options.Date.Date.AddDays(2)
        })
        .GroupBy(item => item.OperatorCodigoFJ, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => (IReadOnlyList<OvertimeRealPresenceEventRow>)group.OrderBy(item => item.PresenceDateTime).ToList(), StringComparer.OrdinalIgnoreCase);

    var todokes = conn.Query<OvertimeRealTodokeRow>(
        @"
            SELECT
                a.OperatorCodigoFJ,
                date(a.RequestDate) AS BaseDate,
                a.TodokeMotivoId AS MotiveId,
                COALESCE(m.NomePt, '') AS MotiveName
            FROM AcompYukyu a
            LEFT JOIN TodokeMotivo m ON m.Id = a.TodokeMotivoId
            WHERE a.OperatorCodigoFJ IN @OperatorCodes
              AND date(a.RequestDate) = date(@BaseDate)
            ORDER BY a.Id DESC;",
        new
        {
            OperatorCodes = operatorCodes,
            BaseDate = options.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        })
        .GroupBy(item => item.OperatorCodigoFJ, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

    var movements = conn.Query<OvertimeRealMovementRow>(
        @"
            SELECT
                OperatorCodigoFJ,
                date(ScheduleDate) AS BaseDate,
                COALESCE(MovementType, '') AS MovementType,
                COALESCE(EventTime, '') AS EventTime,
                COALESCE(EventDateTime, '') AS EventDateTime,
                COALESCE(Reason, '') AS Reason
            FROM HaidaiMovements
            WHERE OperatorCodigoFJ IN @OperatorCodes
              AND date(ScheduleDate) = date(@BaseDate)
            ORDER BY COALESCE(EventTime, '') DESC, Id DESC;",
        new
        {
            OperatorCodes = operatorCodes,
            BaseDate = options.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        })
        .GroupBy(item => item.OperatorCodigoFJ, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

    Console.WriteLine("Operator\tSector\tBaseDate\tShift\tIsYakin\tPresenceRowsFound\tFirstPresenceTime\tLastPresenceTime\tScheduledStart\tTeijiEnd\tZangyouStart\tZangyouEnd\tActualEnd\tAttendanceStatus\tIsHolidayWork\tIsSunday\tHasLate\tHasEarlyLeave\tNormalZangyouMinutes\tHolidayWorkMinutes\tTotalOvertimeMinutes\tReason");
    foreach (var row in rows)
    {
        presenceEvents.TryGetValue(row.OperatorCodigoFJ, out var operatorPresenceEvents);
        todokes.TryGetValue(row.OperatorCodigoFJ, out var todoke);
        movements.TryGetValue(row.OperatorCodigoFJ, out var movement);
        operatorPresenceEvents ??= Array.Empty<OvertimeRealPresenceEventRow>();

        var presenceWindow = ResolveProbePresenceWindow(options.Date, row.ShiftName, operatorPresenceEvents);
        var statusKey = ResolveProbeAttendanceStatus(row, presenceWindow.RowsFound > 0, todoke, movement);
        var hasLate = statusKey == "late";
        var hasEarlyLeave = statusKey == "early_leave";
        var actualEnd = ResolveProbeActualEnd(options.Date, row.ShiftName, presenceWindow, movement);
        var allowFallback = ShouldAllowProbeFullShiftFallback(options.Date, row.ShiftName, presenceWindow, ProbeIsScheduledDay(row), hasLate, hasEarlyLeave);
        var audit = OvertimeRuleCalculator.Calculate(new OvertimeCalculationInput(
            row.OperatorCodigoFJ,
            row.OperatorName,
            options.Date.Date,
            row.ShiftName,
            HasSchedule: ProbeIsScheduledDay(row),
            IsHolidayWork: row.IsHolidayWork,
            AttendanceStatus: statusKey,
            ActualEnd: actualEnd,
            HasLate: hasLate,
            HasEarlyLeave: hasEarlyLeave,
            IsProjection: false,
            AllowFullShiftFallbackWithoutPresenceEnd: allowFallback));

        var isYakin = OvertimeRuleCalculator.TryResolveShiftWindow(row.ShiftName, options.Date.Date, out var window)
            && window.ShiftKind == OvertimeShiftKind.Yakin;

        Console.WriteLine(string.Join("\t", new[]
        {
            row.OperatorCodigoFJ,
            row.SectorName,
            options.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            row.ShiftName,
            isYakin ? "1" : "0",
            presenceWindow.RowsFound.ToString(CultureInfo.InvariantCulture),
            FormatAuditDateTime(presenceWindow.FirstPresenceTime),
            FormatAuditDateTime(presenceWindow.LastPresenceTime),
            FormatAuditDateTime(audit.ScheduledStart),
            FormatAuditDateTime(audit.TeijiEnd),
            FormatAuditDateTime(audit.ZangyouStart),
            FormatAuditDateTime(audit.ZangyouEnd),
            FormatAuditDateTime(audit.ActualEnd),
            statusKey,
            audit.IsHolidayWork ? "1" : "0",
            audit.IsSunday ? "1" : "0",
            audit.HasLate ? "1" : "0",
            audit.HasEarlyLeave ? "1" : "0",
            audit.NormalZangyouMinutes.ToString("0.#", CultureInfo.InvariantCulture),
            audit.HolidayWorkMinutes.ToString("0.#", CultureInfo.InvariantCulture),
            audit.TotalOvertimeMinutes.ToString("0.#", CultureInfo.InvariantCulture),
            audit.Reason
        }));
    }
}

string FormatAuditDateTime(DateTime? value)
{
    return value?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "-";
}

ProbePresenceWindowSummary ResolveProbePresenceWindow(
    DateTime baseDate,
    string shiftName,
    IReadOnlyList<OvertimeRealPresenceEventRow> presenceEvents)
{
    if (presenceEvents.Count == 0
        || !OvertimeRuleCalculator.TryResolveShiftWindow(shiftName, baseDate, out var window))
    {
        return new ProbePresenceWindowSummary(0, null, null);
    }

    var matches = presenceEvents
        .Where(item => item.PresenceDateTime >= window.ScheduledStart && item.PresenceDateTime <= window.ZangyouEnd)
        .Select(item => item.PresenceDateTime)
        .OrderBy(item => item)
        .ToList();

    return matches.Count == 0
        ? new ProbePresenceWindowSummary(0, null, null)
        : new ProbePresenceWindowSummary(matches.Count, matches[0], matches[^1]);
}

DateTime? ResolveProbeActualEnd(
    DateTime baseDate,
    string shiftName,
    ProbePresenceWindowSummary presenceWindow,
    OvertimeRealMovementRow? movement)
{
    if (movement != null && string.Equals(movement.MovementType, "early_leave", StringComparison.OrdinalIgnoreCase))
    {
        var movementMoment = OvertimeRuleCalculator.NormalizeMovementMoment(baseDate, shiftName, movement.EventTime, movement.EventDateTime);
        if (movementMoment.HasValue)
        {
            return movementMoment;
        }
    }

    return presenceWindow.LastPresenceTime;
}

string ResolveProbeAttendanceStatus(
    OvertimeRealScheduleRow schedule,
    bool hasPresence,
    OvertimeRealTodokeRow? todoke,
    OvertimeRealMovementRow? movement)
{
    if (todoke?.MotiveId == 2)
    {
        return "falta";
    }

    if (todoke?.MotiveId == 3
        || string.Equals(movement?.MovementType, "late", StringComparison.OrdinalIgnoreCase)
        || string.Equals(schedule.AvailabilityStatus, "Atraso", StringComparison.OrdinalIgnoreCase))
    {
        return "late";
    }

    if (todoke?.MotiveId == 5
        || string.Equals(movement?.MovementType, "early_leave", StringComparison.OrdinalIgnoreCase)
        || string.Equals(schedule.AvailabilityStatus, "Saiu cedo", StringComparison.OrdinalIgnoreCase)
        || string.Equals(schedule.AvailabilityStatus, "Saida antecipada", StringComparison.OrdinalIgnoreCase))
    {
        return "early_leave";
    }

    if (todoke?.MotiveId == 1
        || string.Equals(schedule.AvailabilityStatus, "Yukyu", StringComparison.OrdinalIgnoreCase))
    {
        return "yukyu";
    }

    if (string.Equals(schedule.AvailabilityStatus, "Folga", StringComparison.OrdinalIgnoreCase))
    {
        return "folga";
    }

    if (hasPresence)
    {
        return "present";
    }

    return ProbeIsScheduledDay(schedule) ? "scheduled" : "no_record";
}

bool ProbeIsScheduledDay(OvertimeRealScheduleRow? schedule)
{
    return schedule != null
        && !string.Equals(schedule.AvailabilityStatus, "Folga", StringComparison.OrdinalIgnoreCase)
        && (!string.IsNullOrWhiteSpace(schedule.AssignmentCode)
            || schedule.LocalId > 0
            || !string.IsNullOrWhiteSpace(schedule.AvailabilityStatus));
}

bool ShouldAllowProbeFullShiftFallback(
    DateTime baseDate,
    string shiftName,
    ProbePresenceWindowSummary presenceWindow,
    bool hasSchedule,
    bool hasLate,
    bool hasEarlyLeave)
{
    if (!hasSchedule
        || hasLate
        || hasEarlyLeave
        || !OvertimeRuleCalculator.TryResolveShiftWindow(shiftName, baseDate, out var window))
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

Dictionary<int, MachineLocationSnapshotProbeRow> ReadMachineLocationSnapshot(System.Data.IDbConnection conn)
{
    return conn.Query<MachineLocationSnapshotProbeRow>(
            @"
                SELECT
                    Id,
                    COALESCE(MachineCode, '') AS MachineCode,
                    COALESCE(MachineKey, '') AS MachineKey,
                    SectorId,
                    LocalId
                FROM Machines;")
        .ToDictionary(item => item.Id);
}

static string FormatNullableInt(int? value)
{
    return value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "NULL";
}

void ShowDashboards()
{
    PrintRuntimeConfigDiagnostics();
    using var conn = factory.CreateOpenConnection();

    var shifts = conn.Query<ShiftRow>(
        @"
            SELECT
                Id,
                COALESCE(NamePt, '') AS NamePt,
                COALESCE(NULLIF(NameJp, ''), NamePt, '') AS NameJp
            FROM Shifts
            ORDER BY Id;"
    ).ToList();

    var dayShift = shifts.FirstOrDefault(shift => MatchesShift(shift, "hiru", "day", "dia"));
    var nightShift = shifts.FirstOrDefault(shift => MatchesShift(shift, "yakin", "night", "noite", "夜"));

    if (dayShift != null)
    {
        ShowDashboard("HIRUKIN", new DateTime(2026, 4, 29), dayShift.Id);
    }

    if (nightShift != null)
    {
        Console.WriteLine();
        ShowDashboard("YAKIN", new DateTime(2026, 4, 28), nightShift.Id);
    }

    if (dayShift == null && nightShift == null)
    {
        var fallbackShift = shifts.FirstOrDefault();
        if (fallbackShift == null)
        {
            Console.WriteLine("Nenhum turno encontrado para o probe.");
            return;
        }

        ShowDashboard("DEFAULT", new DateTime(2026, 4, 29), fallbackShift.Id);
    }
}

void RunImportProfile(string[] profileArgs)
{
    var options = ImportProfileOptions.Parse(profileArgs, sectorCatalog.ResolveFilter, sectorCatalog.GetLabel);
    var totalWatch = Stopwatch.StartNew();
    PrintRuntimeConfigDiagnostics();
    var importOptions = options.Reimport && options.Date.HasValue
        ? new ProductionFileImporter.ProductionImportOptions
        {
            CleanExistingEvents = true,
            CleanupFilter = new ProductionDashboardFilter
            {
                Date = options.Date.Value.Date,
                SectorId = options.SectorId
            }
        }
        : null;
    TeamOps.Core.Entities.ProductionImportResult result;
    try
    {
        result = importer.ImportLatest(importOptions);
    }
    catch (FileNotFoundException ex)
    {
        totalWatch.Stop();
        Console.WriteLine("=== IMPORT PROFILE FAILED ===");
        Console.WriteLine($"Database={settings.DatabasePath}");
        Console.WriteLine($"ReimportDate={(options.Date.HasValue ? options.Date.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "(none)")}");
        Console.WriteLine($"ReimportSector={options.SectorLabel}");
        Console.WriteLine(ex.Message);
        Console.WriteLine($"ProbeTotal={totalWatch.ElapsedMilliseconds}ms");
        Environment.ExitCode = 2;
        return;
    }
    totalWatch.Stop();

    Console.WriteLine("=== IMPORT PROFILE ===");
    Console.WriteLine($"Database={settings.DatabasePath}");
    Console.WriteLine($"ReimportDate={(options.Date.HasValue ? options.Date.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "(none)")}");
    Console.WriteLine($"ReimportSector={options.SectorLabel}");
    Console.WriteLine($"ReimportMode={(options.Reimport ? "enabled" : "disabled")}");
    Console.WriteLine($"FilesRead={result.FilesRead}");
    Console.WriteLine($"LinesRead={result.LinesRead}");
    Console.WriteLine($"Imported={result.Imported}");
    Console.WriteLine($"Ignored={result.Ignored}");
    Console.WriteLine($"MachinesCreated={result.MachinesCreated}");
    PrintDadImportResult(result);
    PrintEc2ImportResult(result);
    Console.WriteLine($"ProbeTotal={totalWatch.ElapsedMilliseconds}ms");
    PrintImportPerformance(result);

    if (options.Date.HasValue)
    {
        var analyticsWatch = Stopwatch.StartNew();
        using var conn = factory.CreateOpenConnection();

        var shifts = conn.Query<ShiftRow>(
            @"
                SELECT
                    Id,
                    COALESCE(NamePt, '') AS NamePt,
                    COALESCE(NULLIF(NameJp, ''), NamePt, '') AS NameJp
                FROM Shifts
                ORDER BY Id;")
            .ToList();

        var dayShift = shifts.FirstOrDefault(shift => MatchesShift(shift, "hiru", "day", "dia"));
        var nightShift = shifts.FirstOrDefault(shift => MatchesShift(shift, "yakin", "night", "noite", "夜"));
        var selectedShift = dayShift ?? nightShift ?? shifts.FirstOrDefault();
        if (selectedShift == null)
        {
            Console.WriteLine("=== POST IMPORT ANALYTICS PROFILE ===");
            Console.WriteLine("Shift=none");
            Console.WriteLine("Reason=no-shift-found");
            return;
        }

        var dashboard = analytics.BuildDashboard(new ProductionDashboardFilter
        {
            Date = options.Date.Value.Date,
            SectorId = options.SectorId,
            ShiftId = selectedShift.Id
        });
        analyticsWatch.Stop();

        Console.WriteLine();
        Console.WriteLine("=== POST IMPORT ANALYTICS PROFILE ===");
        Console.WriteLine($"Date={options.Date.Value:yyyy-MM-dd}");
        Console.WriteLine($"Sector={options.SectorLabel}");
        Console.WriteLine($"Shift={selectedShift.Id} {selectedShift.NamePt}");
        Console.WriteLine($"Analytics={analyticsWatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"Machines={dashboard.Machines.Count}");
        Console.WriteLine($"Areas={dashboard.Areas.Count}");
        Console.WriteLine($"Kadouritsu={dashboard.ProductionPercent:F1}");
        Console.WriteLine($"Running={dashboard.MachinesRunning}");
        Console.WriteLine($"Stopped={dashboard.MachinesStopped}");
        Console.WriteLine($"Ignored={dashboard.MachinesIgnored}");
        Console.WriteLine($"AverageOperatingProcessMinutes={dashboard.AverageOperatingProcessMinutes:F1}");
    }

    if (result.Errors.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine("Errors:");
        foreach (var error in result.Errors)
        {
            Console.WriteLine($" - {error}");
        }
    }
}

void PrintDadImportResult(TeamOps.Core.Entities.ProductionImportResult result)
{
    if (result.DadLinesRead == 0
        && result.DadRowsParsed == 0
        && result.DadRowsImported == 0
        && result.DadRowsIgnored == 0)
    {
        return;
    }

    Console.WriteLine("=== DAD IMPORT DIAGNOSTICS ===");
    Console.WriteLine($"DadLinesRead={result.DadLinesRead}");
    Console.WriteLine($"DadRowsParsed={result.DadRowsParsed}");
    Console.WriteLine($"DadRowsImported={result.DadRowsImported}");
    Console.WriteLine($"DadRowsIgnored={result.DadRowsIgnored}");
    Console.WriteLine($"DadMachinesFound={result.DadMachinesFound}");
    Console.WriteLine($"DadMachinesImported={result.DadMachinesImported}");
    Console.WriteLine($"DadMachinesWithRunningEvents={result.DadMachinesWithRunningEvents}");
    Console.WriteLine($"DadMachinesWithZeroRunningEvents={result.DadMachinesWithZeroRunningEvents}");
    Console.WriteLine($"DadLinkErrors={result.DadLinkErrors}");

    if (result.DadEventsByMachine.Count > 0)
    {
        Console.WriteLine("DadTotalProductionByMachine=eventos importados / eventos rodando");
        foreach (var machine in result.DadEventsByMachine.Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase))
        {
            result.DadRunningEventsByMachine.TryGetValue(machine, out var running);
            Console.WriteLine($"  Machine={machine} ImportedEvents={result.DadEventsByMachine[machine]} RunningEvents={running}");
        }
    }

    if (result.DadIgnoredByMachine.Count > 0)
    {
        Console.WriteLine("DadIgnoredByMachine:");
        foreach (var machine in result.DadIgnoredByMachine.Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine($"  Machine={machine} IgnoredRows={result.DadIgnoredByMachine[machine]}");
        }
    }

    if (result.DadDiagnostics.Count > 0)
    {
        Console.WriteLine("DadDiagnostics:");
        foreach (var item in result.DadDiagnostics)
        {
            Console.WriteLine($"  {item}");
        }
    }
}

void PrintEc2ImportResult(TeamOps.Core.Entities.ProductionImportResult result)
{
    Console.WriteLine($"Ec2Attempted={result.Ec2ImportAttempted}");
    Console.WriteLine($"Ec2Skipped={result.Ec2ImportSkipped}");
    Console.WriteLine($"Ec2Message={result.Ec2ImportMessage}");
    Console.WriteLine($"Ec2AdministratorFilePath={result.Ec2FilePath}");
    Console.WriteLine($"Ec2ResolvedFullPath={result.Ec2ResolvedFullPath}");
    Console.WriteLine($"Ec2FileExists={result.Ec2FileExists}");
    Console.WriteLine($"Ec2FileSize={result.Ec2FileSize}");
    Console.WriteLine($"Ec2LastWriteTime={result.Ec2FileLastWriteTime}");
    Console.WriteLine($"Ec2EncodingDetected={result.Ec2EncodingDetected}");
    Console.WriteLine($"Ec2DelimiterDetected={result.Ec2DelimiterDetected}");
    Console.WriteLine($"Ec2RawLinePreview={result.Ec2RawLinePreview}");
    Console.WriteLine($"Ec2DecodedLinePreview={result.Ec2DecodedLinePreview}");
    Console.WriteLine($"Ec2ContainsReplacementChar={result.Ec2ContainsReplacementChar}");
    Console.WriteLine($"Ec2FirstLinePreview={result.Ec2FirstLinePreview}");
    Console.WriteLine($"Ec2HeaderLinePreview={result.Ec2HeaderLinePreview}");
    Console.WriteLine($"Ec2FirstDataLinePreview={result.Ec2FirstDataLinePreview}");
    Console.WriteLine($"Ec2RowsRead={result.Ec2RowsRead}");
    Console.WriteLine($"Ec2RowsCandidate={result.Ec2RowsCandidate}");
    Console.WriteLine($"Ec2RowsImported={result.Ec2RowsImported}");
    Console.WriteLine($"Ec2RowsIgnored={result.Ec2RowsIgnored}");
    Console.WriteLine($"Ec2IgnoredByEmptyLine={result.Ec2IgnoredByEmptyLine}");
    Console.WriteLine($"Ec2IgnoredByNotAreaBlock={result.Ec2IgnoredByNotAreaBlock}");
    Console.WriteLine($"Ec2IgnoredByTooFewColumns={result.Ec2IgnoredByTooFewColumns}");
    Console.WriteLine($"Ec2IgnoredByMissingMachine={result.Ec2IgnoredByMissingMachine}");
    Console.WriteLine($"Ec2IgnoredByInvalidStatus={result.Ec2IgnoredByInvalidStatus}");
    Console.WriteLine($"Ec2IgnoredByInvalidTime={result.Ec2IgnoredByInvalidTime}");
    Console.WriteLine($"Ec2IgnoredByUnknownFormat={result.Ec2IgnoredByUnknownFormat}");
    Console.WriteLine($"Ec2Areas={result.Ec2AreaCount}");
    Console.WriteLine($"Ec2Running={result.Ec2RunningCount}");
    Console.WriteLine($"Ec2Stopped={result.Ec2StoppedCount}");
    Console.WriteLine($"Ec2Ignored={result.Ec2IgnoredCount}");

    if (result.Ec2DiscardSamples.Count > 0)
    {
        Console.WriteLine("Ec2DiscardSamples:");
        foreach (var sample in result.Ec2DiscardSamples)
        {
            Console.WriteLine($" - {sample}");
        }
    }

    if (result.Ec2ImportedSamples.Count > 0)
    {
        Console.WriteLine("Ec2ImportedSamples:");
        foreach (var sample in result.Ec2ImportedSamples)
        {
            Console.WriteLine($" - {sample}");
        }
    }
}

void RunEc2Diagnostics()
{
    PrintRuntimeConfigDiagnostics();
    using var conn = factory.CreateOpenConnection();

    var latestImport = conn.QueryFirstOrDefault<Ec2ImportProbeRow>(
        @"
            SELECT
                Id,
                COALESCE(SourceType, 'Administrator') AS SourceType,
                COALESCE(SourceFile, '') AS SourceFile,
                COALESCE(FileHash, '') AS FileHash,
                COALESCE(FileLastWriteTime, '') AS FileLastWriteTime,
                COALESCE(ImportedAt, '') AS ImportedAt,
                RowsRead,
                RowsImported,
                RowsIgnored
            FROM Ec2AdministratorImports
            ORDER BY datetime(ImportedAt) DESC, Id DESC
            LIMIT 1;");

    var current = conn.Query<Ec2CurrentProbeRow>(
            @"
                SELECT
                    COALESCE(AreaLabel, '') AS AreaLabel,
                    COALESCE(MachineCode, '') AS MachineCode,
                    COALESCE(ImportId, 0) AS ImportId,
                    COALESCE(SourceType, 'Administrator') AS SourceType,
                    COALESCE(IsStale, 0) AS IsStale,
                    COALESCE(StatusText, '') AS StatusText,
                    COALESCE(IsRunning, 0) AS IsRunning,
                    COALESCE(IsIgnored, 0) AS IsIgnored,
                    COALESCE(IgnoreReason, '') AS IgnoreReason,
                    COALESCE(PartCode, '') AS PartCode,
                    ProcessMinutes,
                    COALESCE(LotNo, '') AS LotNo,
                    COALESCE(SnapshotAt, '') AS SnapshotAt,
                    COALESCE(LastSeenAt, '') AS LastSeenAt,
                    COALESCE(StoppedSinceAt, '') AS StoppedSinceAt
                FROM Ec2MachineCurrentState
                ORDER BY AreaLabel, MachineCode;")
        .ToList();
    var latestImportId = latestImport?.Id ?? 0;
    var latestMachineCodes = latestImportId <= 0
        ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        : conn.Query<string>(
                "SELECT upper(trim(MachineCode)) FROM Ec2MachineSnapshots WHERE ImportId = @importId;",
                new { importId = latestImportId })
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    var lastImportMachines = latestMachineCodes.Count;
    var staleRows = current
        .Where(row => row.SourceType.Equals("Administrator", StringComparison.OrdinalIgnoreCase))
        .Where(row => latestImportId > 0 && !latestMachineCodes.Contains(NormalizeProbeCode(row.MachineCode)))
        .ToList();

    var areaCount = current
        .Select(row => row.AreaLabel)
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();
    var running = current.Count(row => row.IsRunning == 1);
    var ignored = current.Count(row => row.IsIgnored == 1);
    var stopped = Math.Max(0, current.Count - running);
    var auditRows = current
        .Select(row => BuildEc2AverageAuditRow(row, latestMachineCodes, latestImportId))
        .ToList();
    var operatingProcessRows = auditRows
        .Where(row => row.EnteredAverage)
        .ToList();
    var operatingProcessSum = operatingProcessRows.Sum(row => row.TimeRead!.Value);
    var averageProcess = operatingProcessRows.Count == 0
        ? 0d
        : Math.Round(operatingProcessSum / operatingProcessRows.Count, 1);
    var currentStateStrictRows = current
        .Where(row => row.IsRunning == 1
            && row.IsIgnored == 0
            && row.ProcessMinutes.HasValue
            && double.IsFinite(row.ProcessMinutes.Value)
            && row.ProcessMinutes.Value > 0)
        .ToList();
    var currentStateStrictSum = currentStateStrictRows.Sum(row => row.ProcessMinutes!.Value);
    var currentStateStrictAverage = currentStateStrictRows.Count == 0
        ? 0d
        : Math.Round(currentStateStrictSum / currentStateStrictRows.Count, 1);

    Console.WriteLine("=== EC2 ADMINISTRATOR DIAGNOSTICS ===");
    Console.WriteLine($"Database={settings.DatabasePath}");
    Console.WriteLine($"LastImportId={latestImport?.Id.ToString(CultureInfo.InvariantCulture) ?? "(none)"}");
    Console.WriteLine($"LastImportSourceType={latestImport?.SourceType ?? "(none)"}");
    Console.WriteLine($"LastFile={latestImport?.SourceFile ?? "(none)"}");
    Console.WriteLine($"LastHash={latestImport?.FileHash ?? "(none)"}");
    Console.WriteLine($"LastFileWriteTime={latestImport?.FileLastWriteTime ?? "(none)"}");
    Console.WriteLine($"LastImportedAt={latestImport?.ImportedAt ?? "(none)"}");
    Console.WriteLine($"RowsRead={latestImport?.RowsRead ?? 0}");
    Console.WriteLine($"RowsImported={latestImport?.RowsImported ?? 0}");
    Console.WriteLine($"RowsIgnored={latestImport?.RowsIgnored ?? 0}");
    Console.WriteLine($"CurrentStateTotalMachines={current.Count}");
    Console.WriteLine($"LastImportMachines={lastImportMachines}");
    Console.WriteLine($"StaleMachinesFromOlderImports={staleRows.Count}");
    Console.WriteLine($"Areas={areaCount}");
    Console.WriteLine($"Machines={current.Count}");
    Console.WriteLine($"Running={running}");
    Console.WriteLine($"Stopped={stopped}");
    Console.WriteLine($"Ignored={ignored}");
    Console.WriteLine($"AverageOperatingProcessMinutes={averageProcess:F1}");
    Console.WriteLine("AverageRule=IsRunning=1 AND IsIgnored=0 AND ProcessMinutes>0 AND finite AND PresentInLastAdministratorImport");
    Console.WriteLine($"Sum={operatingProcessSum:F1}");
    Console.WriteLine($"Count={operatingProcessRows.Count}");
    Console.WriteLine($"Excluded={auditRows.Count - operatingProcessRows.Count}");
    Console.WriteLine($"AverageCurrentStateStrict={currentStateStrictAverage:F1}");
    Console.WriteLine($"CurrentStateStrictSum={currentStateStrictSum:F1}");
    Console.WriteLine($"CurrentStateStrictCount={currentStateStrictRows.Count}");

    Console.WriteLine();
    Console.WriteLine("AverageAuditRows:");
    foreach (var row in auditRows)
    {
        Console.WriteLine($"  Machine={row.MachineCode} Status={row.StatusText} TimeRead={row.TimeRead?.ToString("0.0", CultureInfo.InvariantCulture) ?? "null"} EnteredAverage={(row.EnteredAverage ? "sim" : "nao")} Reason={row.Reason}");
    }

    Console.WriteLine();
    Console.WriteLine("StaleMachines:");
    if (staleRows.Count == 0)
    {
        Console.WriteLine("  (none)");
    }
    else
    {
        foreach (var row in staleRows)
        {
            var sourceFile = conn.ExecuteScalar<string>(
                    "SELECT COALESCE(SourceFile, '') FROM Ec2AdministratorImports WHERE Id = @importId;",
                    new { importId = row.ImportId }) ?? string.Empty;
            Console.WriteLine($"  Machine={row.MachineCode} ImportId={(row.ImportId <= 0 ? "(unknown)" : row.ImportId.ToString(CultureInfo.InvariantCulture))} SourceFile={(string.IsNullOrWhiteSpace(sourceFile) ? "(unknown)" : sourceFile)} LastSeenAt={row.LastSeenAt}");
        }
    }

    Console.WriteLine();
    Console.WriteLine("CurrentStateSample:");
    foreach (var row in current.Take(20))
    {
        Console.WriteLine($"  Area={row.AreaLabel} Machine={row.MachineCode} ImportId={(row.ImportId <= 0 ? "(unknown)" : row.ImportId.ToString(CultureInfo.InvariantCulture))} Stale={row.IsStale} Status={row.StatusText} Running={row.IsRunning} Ignored={row.IsIgnored} Part={row.PartCode} Lot={row.LotNo} Process={row.ProcessMinutes?.ToString("0.0", CultureInfo.InvariantCulture) ?? ""} Snapshot={row.SnapshotAt} Reason={row.IgnoreReason}");
    }

    Console.WriteLine();
    Console.WriteLine("PartCodeStyles:");
    foreach (var style in conn.Query<PartCodeStyleProbeRow>(
        @"
            SELECT
                PartCode,
                ColorHex,
                TextColorHex,
                COALESCE(Description, '') AS Description,
                COALESCE(IsActive, 1) AS IsActive
            FROM ProductionPartCodeStyles
            ORDER BY PartCode;"))
    {
        Console.WriteLine($"  {style.PartCode}: {style.ColorHex}/{style.TextColorHex} Active={style.IsActive} {style.Description}");
    }
}

static Ec2AverageAuditProbeRow BuildEc2AverageAuditRow(
    Ec2CurrentProbeRow row,
    IReadOnlySet<string> latestMachineCodes,
    long latestImportId)
{
    var normalizedMachine = NormalizeProbeCode(row.MachineCode);
    var presentInLatestImport = latestImportId <= 0 || latestMachineCodes.Contains(normalizedMachine);

    if (!presentInLatestImport)
    {
        return new Ec2AverageAuditProbeRow(row.MachineCode, row.StatusText, row.ProcessMinutes, false, "stale_from_older_import");
    }

    if (row.IsStale == 1)
    {
        return new Ec2AverageAuditProbeRow(row.MachineCode, row.StatusText, row.ProcessMinutes, false, "stale_current_state");
    }

    if (row.IsIgnored == 1)
    {
        var reason = string.IsNullOrWhiteSpace(row.IgnoreReason) ? "no_reason" : row.IgnoreReason;
        return new Ec2AverageAuditProbeRow(row.MachineCode, row.StatusText, row.ProcessMinutes, false, $"ec2_ignored:{reason}");
    }

    if (row.IsRunning != 1)
    {
        return new Ec2AverageAuditProbeRow(row.MachineCode, row.StatusText, row.ProcessMinutes, false, "machine_not_running");
    }

    if (!row.ProcessMinutes.HasValue)
    {
        return new Ec2AverageAuditProbeRow(row.MachineCode, row.StatusText, row.ProcessMinutes, false, "process_time_null");
    }

    var processMinutes = row.ProcessMinutes.Value;
    if (!double.IsFinite(processMinutes))
    {
        return new Ec2AverageAuditProbeRow(row.MachineCode, row.StatusText, row.ProcessMinutes, false, "process_time_not_finite");
    }

    if (processMinutes <= 0)
    {
        return new Ec2AverageAuditProbeRow(row.MachineCode, row.StatusText, row.ProcessMinutes, false, "process_time_not_positive");
    }

    return new Ec2AverageAuditProbeRow(row.MachineCode, row.StatusText, row.ProcessMinutes, true, "ok");
}

static string NormalizeProbeCode(string value)
{
    return string.IsNullOrWhiteSpace(value)
        ? string.Empty
        : value.Trim().ToUpperInvariant();
}

void RunEc2ResetLatest()
{
    PrintRuntimeConfigDiagnostics();

    var ec2FilePath = ConfigurationManager.AppSettings["Ec2AdministratorFilePath"] ?? string.Empty;
    if (string.IsNullOrWhiteSpace(ec2FilePath))
    {
        Console.WriteLine("Ec2AdministratorFilePath vazio; nada para limpar.");
        return;
    }

    using var conn = factory.CreateOpenConnection();

    var latest = conn.QueryFirstOrDefault<Ec2ImportProbeRow>(
        @"
            SELECT
                Id,
                COALESCE(SourceType, 'Administrator') AS SourceType,
                COALESCE(SourceFile, '') AS SourceFile,
                COALESCE(FileHash, '') AS FileHash,
                COALESCE(FileLastWriteTime, '') AS FileLastWriteTime,
                COALESCE(ImportedAt, '') AS ImportedAt,
                RowsRead,
                RowsImported,
                RowsIgnored
            FROM Ec2AdministratorImports
            WHERE SourceFile = @sourceFile
            ORDER BY datetime(ImportedAt) DESC, Id DESC
            LIMIT 1;",
        new
        {
            sourceFile = ec2FilePath
        });

    if (latest == null)
    {
        Console.WriteLine($"Nenhum import EC2 encontrado para {ec2FilePath}.");
        return;
    }

    using var tx = conn.BeginTransaction();
    var snapshotsDeleted = conn.Execute("DELETE FROM Ec2MachineSnapshots WHERE ImportId = @importId;", new { importId = latest.Id }, tx);
    var importsDeleted = conn.Execute("DELETE FROM Ec2AdministratorImports WHERE Id = @importId;", new { importId = latest.Id }, tx);
    tx.Commit();

    Console.WriteLine("=== EC2 RESET LATEST ===");
    Console.WriteLine($"SourceFile={latest.SourceFile}");
    Console.WriteLine($"ImportId={latest.Id}");
    Console.WriteLine($"FileHash={latest.FileHash}");
    Console.WriteLine($"SnapshotsDeleted={snapshotsDeleted}");
    Console.WriteLine($"ImportsDeleted={importsDeleted}");
}

void PrintImportPerformance(TeamOps.Core.Entities.ProductionImportResult result)
{
    if (result.PerformanceMs.Count == 0)
    {
        return;
    }

    Console.WriteLine("Import performance:");
    foreach (var item in result.PerformanceMs.OrderBy(item => item.Key.Equals("Total", StringComparison.OrdinalIgnoreCase) ? 1 : 0).ThenBy(item => item.Key))
    {
        Console.WriteLine($"{item.Key}={item.Value}ms");
    }
}

void RunDbIndexCheck()
{
    using var conn = factory.CreateOpenConnection();

    var requiredIndexes = new[]
    {
        "IX_MachineStatuses_Sector_StatusCode",
        "IX_MachineStatuses_SectorId",
        "IX_MachineStatuses_StatusCode",
        "IX_MachineEvents_UniqueEvent",
        "IX_MachineEvents_UniqueRawEvent",
        "IX_MachineEvents_EventDateTime",
        "IX_MachineEvents_Machine_EventTime",
        "IX_Machines_MachineKey_Unique",
        "IX_Machines_MachineCode_LineCode",
        "IX_Hikitsugui_OperatorRead",
        "IX_HikitsuguiReads_Reader_Hikitsugui",
        "IX_HikitsuguiAttachments_Hikitsugui",
        "IX_Ec2AdministratorImports_FileHash",
        "IX_Ec2MachineSnapshots_Machine",
        "IX_Ec2MachineSnapshots_Area",
        "IX_Ec2MachineSnapshots_Status",
        "IX_Ec2MachineSnapshots_PartCode",
        "IX_Ec2MachineCurrentState_MachineId",
        "IX_Ec2MachineCurrentState_PartCode"
    };

    var indexes = conn.Query<IndexProbeRow>(
            @"
                SELECT
                    name AS Name,
                    tbl_name AS TableName,
                    COALESCE(sql, '') AS Sql
                FROM sqlite_master
                WHERE type = 'index'
                  AND name NOT LIKE 'sqlite_autoindex%'
                ORDER BY tbl_name, name;")
        .ToList();

    Console.WriteLine("=== DB INDEX CHECK ===");
    Console.WriteLine($"Database={settings.DatabasePath}");
    foreach (var indexName in requiredIndexes)
    {
        Console.WriteLine($"{indexName}={(indexes.Any(item => item.Name.Equals(indexName, StringComparison.OrdinalIgnoreCase)) ? "OK" : "MISSING")}");
    }

    Console.WriteLine();
    Console.WriteLine("Indexes:");
    foreach (var index in indexes)
    {
        Console.WriteLine($"{index.TableName}.{index.Name}: {index.Sql}");
    }
}

void RunSchemaValidation(bool repair)
{
    using var conn = factory.CreateOpenConnection();

    var before = InspectProductionSchema(conn);
    if (repair)
    {
        RepairProductionStatusSeeds(conn);
    }

    var after = repair
        ? InspectProductionSchema(conn)
        : before;

    Console.WriteLine(repair ? "=== PRODUCTION SCHEMA REPAIR ===" : "=== PRODUCTION SCHEMA CHECK ===");
    Console.WriteLine($"Database={settings.DatabasePath}");
    Console.WriteLine($"JournalMode={after.JournalMode}");
    Console.WriteLine($"QuickCheck={after.QuickCheck}");

    if (repair)
    {
        Console.WriteLine($"IssuesBefore={before.Issues.Count}");
        foreach (var issue in before.Issues)
        {
            Console.WriteLine($"BEFORE: {issue}");
        }
    }

    Console.WriteLine($"IssuesAfter={after.Issues.Count}");
    foreach (var issue in after.Issues)
    {
        Console.WriteLine($"AFTER: {issue}");
    }

    if (after.Issues.Count == 0)
    {
        Console.WriteLine("OK: schema de producao validado.");
        return;
    }

    Environment.ExitCode = 2;
}

void RunStatusHardeningChecks()
{
    using var conn = factory.CreateOpenConnection();
    var dadSectorId = sectorCatalog.ResolveRequired("DAD");
    var gBareruSectorId = sectorCatalog.ResolveRequired("G-BARERU");

    var indexSql = conn.ExecuteScalar<string>(
        @"
            SELECT COALESCE(sql, '')
            FROM sqlite_master
            WHERE type = 'index'
              AND name = 'IX_MachineStatuses_Sector_StatusCode';"
    ) ?? string.Empty;

    Require(
        indexSql.Contains("COALESCE(SectorId, 0)", StringComparison.OrdinalIgnoreCase)
        && indexSql.Contains("StatusCode", StringComparison.OrdinalIgnoreCase),
        "IX_MachineStatuses_Sector_StatusCode deve usar COALESCE(SectorId, 0), StatusCode.");

    var duplicateKeys = conn.ExecuteScalar<int>(
        @"
            SELECT COUNT(1)
            FROM (
                SELECT COALESCE(SectorId, 0), StatusCode, COUNT(1) AS Total
                FROM MachineStatuses
                GROUP BY COALESCE(SectorId, 0), StatusCode
                HAVING COUNT(1) > 1
            );"
    );
    Require(duplicateKeys == 0, "MachineStatuses possui chaves duplicadas por setor/codigo.");

    var statuses = conn.Query<StatusDefinitionProbeRow>(
        @"
            SELECT
                SectorId,
                StatusCode,
                DisplayCode,
                COALESCE(Classification, '') AS Classification,
                COALESCE(NamePt, '') AS NamePt,
                COALESCE(ColorHex, '') AS ColorHex
            FROM MachineStatuses
            WHERE COALESCE(SectorId, 0) IN (0, @dadSectorId)
              AND StatusCode IN (0, 1, 3, 4, 5, 10, 17, 18, 19, 99);"
        , new { dadSectorId }).ToList();

    ExpectStatus(statuses, null, 0, 0, "Running");
    ExpectStatus(statuses, null, 3, 3, "StopCounts");
    ExpectStatus(statuses, dadSectorId, 0, 0, "Running");
    ExpectStatus(statuses, dadSectorId, 3, 3, "StopNoCount");
    ExpectStatus(statuses, dadSectorId, 17, 1, "StopNoCount");
    ExpectStatus(statuses, dadSectorId, 18, 1, "StopNoCount");
    ExpectStatus(statuses, dadSectorId, 19, 1, "StopNoCount");

    var gBareruFive = ResolveStatusForProbe(statuses, gBareruSectorId, 5);
    Require(gBareruFive == null || gBareruFive.Classification == "StopCounts", "GBareru status 5 deve ficar no fallback antigo ou StopCounts.");

    var gBareruTen = ResolveStatusForProbe(statuses, gBareruSectorId, 10);
    Require(gBareruTen == null || gBareruTen.Classification == "StopCounts", "GBareru status 10 deve ficar no fallback antigo ou StopCounts.");

    var dadUnknown = ResolveStatusForProbe(statuses, dadSectorId, 99);
    Require(dadUnknown == null || dadUnknown.Classification == "StopCounts", "DAD status 99 deve ficar desconhecido ou conservador StopCounts.");

    Require(CalculateProbeEfficiency(6, 0, 4) == 100d, "StopNoCount deve sair do denominador: 6h running + 4h no-count = 100%.");
    Require(CalculateProbeEfficiency(6, 2, 2) == 75d, "StopCounts deve permanecer no denominador: 6h running + 2h stop + 2h no-count = 75%.");

    Console.WriteLine("=== STATUS HARDENING CHECKS ===");
    Console.WriteLine("OK: indice setorial, seeds DAD, fallback conservador e formula StopNoCount validados.");
}

ProductionSchemaInspection InspectProductionSchema(System.Data.IDbConnection conn)
{
    var issues = new List<string>();
    var dadSectorId = sectorCatalog.ResolveRequired("DAD");
    var journalMode = conn.ExecuteScalar<string>("PRAGMA journal_mode;") ?? string.Empty;
    var quickCheck = conn.ExecuteScalar<string>("PRAGMA quick_check;") ?? string.Empty;

    if (!quickCheck.Equals("ok", StringComparison.OrdinalIgnoreCase))
    {
        issues.Add($"DB_QUICK_CHECK_FAILED: {quickCheck}");
    }

    RequireTable(conn, "Machines", issues);
    RequireTable(conn, "MachineEvents", issues);
    RequireTable(conn, "MachineCurrentStatus", issues);
    RequireTable(conn, "MachineStatuses", issues);
    RequireTable(conn, "Ec2AdministratorImports", issues);
    RequireTable(conn, "Ec2MachineSnapshots", issues);
    RequireTable(conn, "Ec2MachineCurrentState", issues);
    RequireTable(conn, "ProductionPartCodeStyles", issues);

    RequireColumn(conn, "Machines", "MachineCode", issues);
    RequireColumn(conn, "Machines", "MachineKey", issues);
    RequireColumn(conn, "Machines", "LineCode", issues);
    RequireColumn(conn, "Machines", "LocalId", issues);
    RequireColumn(conn, "Machines", "SectorId", issues);
    RequireColumn(conn, "Machines", "IsActive", issues);

    RequireColumn(conn, "MachineEvents", "MachineId", issues);
    RequireColumn(conn, "MachineEvents", "SectorId", issues);
    RequireColumn(conn, "MachineEvents", "StatusCode", issues);
    RequireColumn(conn, "MachineEvents", "EventDateTime", issues);
    RequireColumn(conn, "MachineEvents", "InternalState", issues);

    RequireColumn(conn, "MachineCurrentStatus", "MachineId", issues);
    RequireColumn(conn, "MachineCurrentStatus", "SectorId", issues);
    RequireColumn(conn, "MachineCurrentStatus", "StatusCode", issues);
    RequireColumn(conn, "MachineCurrentStatus", "EventDateTime", issues);

    RequireColumn(conn, "MachineStatuses", "SectorId", issues);
    RequireColumn(conn, "MachineStatuses", "StatusCode", issues);
    RequireColumn(conn, "MachineStatuses", "DisplayCode", issues);
    RequireColumn(conn, "MachineStatuses", "Classification", issues);
    RequireColumn(conn, "Ec2MachineCurrentState", "MachineCode", issues);
    RequireColumn(conn, "Ec2AdministratorImports", "SourceType", issues);
    RequireColumn(conn, "Ec2MachineCurrentState", "ImportId", issues);
    RequireColumn(conn, "Ec2MachineCurrentState", "SourceType", issues);
    RequireColumn(conn, "Ec2MachineCurrentState", "IsStale", issues);
    RequireColumn(conn, "Ec2MachineCurrentState", "MachineId", issues);
    RequireColumn(conn, "Ec2MachineCurrentState", "StatusText", issues);
    RequireColumn(conn, "Ec2MachineCurrentState", "IsRunning", issues);
    RequireColumn(conn, "Ec2MachineCurrentState", "IsIgnored", issues);
    RequireColumn(conn, "Ec2MachineCurrentState", "StoppedSinceAt", issues);
    RequireColumn(conn, "ProductionPartCodeStyles", "PartCode", issues);
    RequireColumn(conn, "ProductionPartCodeStyles", "ColorHex", issues);

    RequireIndex(conn, "IX_MachineStatuses_Sector_StatusCode", issues);
    RequireIndex(conn, "IX_MachineEvents_UniqueEvent", issues);
    RequireIndex(conn, "IX_MachineEvents_UniqueRawEvent", issues);
    RequireIndex(conn, "IX_MachineEvents_EventDateTime", issues);
    RequireIndex(conn, "IX_MachineEvents_Machine_EventTime", issues);
    RequireIndex(conn, "IX_Machines_MachineKey_Unique", issues);
    RequireIndex(conn, "IX_Machines_MachineCode_LineCode", issues);
    RequireIndex(conn, "IX_Ec2AdministratorImports_FileHash", issues);
    RequireIndex(conn, "IX_Ec2MachineSnapshots_Machine", issues);
    RequireIndex(conn, "IX_Ec2MachineCurrentState_MachineId", issues);

    WarnIfIndexExists(conn, "IX_MachineEvents_Sector_EventTime", issues);
    WarnIfIndexExists(conn, "IX_MachineEvents_StatusCode_EventTime", issues);
    WarnIfIndexExists(conn, "IX_MachineEvents_Sector_Status_EventTime", issues);

    var statusIndexSql = conn.ExecuteScalar<string>(
        @"
            SELECT COALESCE(sql, '')
            FROM sqlite_master
            WHERE type = 'index'
              AND name = 'IX_MachineStatuses_Sector_StatusCode';"
    ) ?? string.Empty;

    if (!statusIndexSql.Contains("COALESCE(SectorId, 0)", StringComparison.OrdinalIgnoreCase)
        || !statusIndexSql.Contains("StatusCode", StringComparison.OrdinalIgnoreCase))
    {
        issues.Add("BAD_INDEX_SQL: IX_MachineStatuses_Sector_StatusCode deve usar COALESCE(SectorId, 0), StatusCode.");
    }

    var duplicateStatusKeys = conn.ExecuteScalar<int>(
        @"
            SELECT COUNT(1)
            FROM (
                SELECT COALESCE(SectorId, 0), StatusCode, COUNT(1) AS Total
                FROM MachineStatuses
                GROUP BY COALESCE(SectorId, 0), StatusCode
                HAVING COUNT(1) > 1
            );"
    );
    if (duplicateStatusKeys > 0)
    {
        issues.Add($"DUPLICATE_MACHINE_STATUSES: {duplicateStatusKeys}");
    }

    var globalSeedCount = conn.ExecuteScalar<int>(
        @"
            SELECT COUNT(1)
            FROM MachineStatuses
            WHERE SectorId IS NULL
              AND StatusCode IN (0, 1, 3, 4);"
    );
    if (globalSeedCount < 4)
    {
        issues.Add($"MISSING_GLOBAL_STATUS_SEEDS: found {globalSeedCount}/4");
    }

    var dadSeedCount = conn.ExecuteScalar<int>(
        @"
            SELECT COUNT(1)
            FROM MachineStatuses
            WHERE SectorId = @DadSectorId
              AND StatusCode IN (0, 1, 3, 4, 17, 18, 19);",
        new
        {
            DadSectorId = dadSectorId
        }
    );
    if (dadSeedCount < 7)
    {
        issues.Add($"MISSING_DAD_STATUS_SEEDS: found {dadSeedCount}/7");
    }

    var dadNoCountCount = conn.ExecuteScalar<int>(
        @"
            SELECT COUNT(1)
            FROM MachineStatuses
            WHERE SectorId = @DadSectorId
              AND StatusCode IN (3, 17, 18, 19)
              AND Classification = 'StopNoCount';",
        new
        {
            DadSectorId = dadSectorId
        }
    );
    if (dadNoCountCount < 4)
    {
        var dadNoCountRows = conn.Query<StatusDefinitionProbeRow>(
            @"
                SELECT
                    SectorId,
                    StatusCode,
                    DisplayCode,
                    COALESCE(Classification, '') AS Classification,
                    COALESCE(NamePt, '') AS NamePt,
                    COALESCE(NameJp, '') AS NameJp,
                    COALESCE(ColorHex, '') AS ColorHex
                FROM MachineStatuses
                WHERE SectorId = @DadSectorId
                  AND StatusCode IN (3, 17, 18, 19)
                ORDER BY StatusCode;",
            new
            {
                DadSectorId = dadSectorId
            })
            .Select(row => $"{row.StatusCode}:{row.Classification}")
            .DefaultIfEmpty("(none)");

        issues.Add($"BAD_DAD_STOP_NO_COUNT_SEEDS: found {dadNoCountCount}/4 [{string.Join(", ", dadNoCountRows)}]");
    }

    return new ProductionSchemaInspection(journalMode, quickCheck, issues);
}

void RepairProductionStatusSeeds(System.Data.IDbConnection conn)
{
    var dadSectorId = sectorCatalog.ResolveRequired("DAD");
    conn.Execute(
        @"
            INSERT OR IGNORE INTO MachineStatuses
            (SectorId, StatusCode, DisplayCode, Classification, NamePt, NameJp, ColorHex, TextColorHex, SortOrder, IsActive)
            VALUES
            (@DadSectorId, 0, 0, 'Running', 'Rodando', 'Rodando', '#5B88E8', '#FFFFFF', 0, 1),
            (@DadSectorId, 1, 3, 'StopCounts', 'Parado DAD', 'Parado DAD', '#F2CB58', '#4A3200', 1, 1),
            (@DadSectorId, 3, 3, 'StopNoCount', 'Limpeza programada', 'Limpeza programada', '#8EC5A8', '#123524', 3, 1),
            (@DadSectorId, 4, 4, 'Error', 'Erro', 'Erro', '#FFFFFF', '#516174', 4, 1),
            (@DadSectorId, 17, 1, 'StopNoCount', 'Intervalo', 'Intervalo', '#8EC5A8', '#123524', 17, 1),
            (@DadSectorId, 18, 1, 'StopNoCount', 'Limpeza programada', 'Limpeza programada', '#8EC5A8', '#123524', 18, 1),
            (@DadSectorId, 19, 1, 'StopNoCount', 'Amostra', 'Amostra', '#8EC5A8', '#123524', 19, 1);

            UPDATE MachineStatuses
            SET DisplayCode = 0, Classification = 'Running', IsActive = 1
            WHERE SectorId = @DadSectorId AND StatusCode = 0;

            UPDATE MachineStatuses
            SET DisplayCode = 3, Classification = 'StopCounts', IsActive = 1
            WHERE SectorId = @DadSectorId AND StatusCode = 1;

            UPDATE MachineStatuses
            SET DisplayCode = 3, Classification = 'StopNoCount', IsActive = 1
            WHERE SectorId = @DadSectorId AND StatusCode = 3;

            UPDATE MachineStatuses
            SET DisplayCode = 4, Classification = 'Error', IsActive = 1
            WHERE SectorId = @DadSectorId AND StatusCode = 4;

            UPDATE MachineStatuses
            SET DisplayCode = 1, Classification = 'StopNoCount', IsActive = 1
            WHERE SectorId = @DadSectorId AND StatusCode IN (17, 18, 19);",
        new
        {
            DadSectorId = dadSectorId
        });
}

void RunProductionDiagnostics()
{
    using var conn = factory.CreateOpenConnection();
    var dadSectorId = sectorCatalog.ResolveRequired("DAD");
    var gBareruSectorId = sectorCatalog.ResolveRequired("G-BARERU");

    var eventCount = conn.ExecuteScalar<long>("SELECT COUNT(1) FROM MachineEvents;");
    var currentCount = conn.ExecuteScalar<long>("SELECT COUNT(1) FROM MachineCurrentStatus;");
    var statusCount = conn.ExecuteScalar<long>("SELECT COUNT(1) FROM MachineStatuses;");
    var machineCount = conn.ExecuteScalar<long>("SELECT COUNT(1) FROM Machines WHERE COALESCE(IsActive, 1) = 1;");
    var dadMachineCount = conn.ExecuteScalar<long>(
        "SELECT COUNT(1) FROM Machines WHERE COALESCE(IsActive, 1) = 1 AND SectorId = @DadSectorId;",
        new { DadSectorId = dadSectorId });
    var dadEventCount = conn.ExecuteScalar<long>(
        @"
            SELECT COUNT(1)
            FROM MachineEvents me
            JOIN Machines m ON m.Id = me.MachineId
            WHERE m.SectorId = @DadSectorId;",
        new { DadSectorId = dadSectorId });
    var gBareruProcedureTimeCount = conn.ExecuteScalar<long>(
        @"
            SELECT COUNT(DISTINCT upper(trim(ProcedureCode)))
            FROM ProductionProcedureTimes
            WHERE SectorId = @gBareruSectorId
              AND COALESCE(IsActive, 1) = 1
              AND upper(trim(ProcedureCode)) IN ('ECII', 'BUNKATSU', 'DCS');",
        new { gBareruSectorId });
    var minEvent = conn.ExecuteScalar<string>("SELECT MIN(EventDateTime) FROM MachineEvents;") ?? string.Empty;
    var maxEvent = conn.ExecuteScalar<string>("SELECT MAX(EventDateTime) FROM MachineEvents;") ?? string.Empty;

    Console.WriteLine("=== PRODUCTION DIAGNOSTICS ===");
    Console.WriteLine($"Database={settings.DatabasePath}");
    Console.WriteLine($"MachineEvents={eventCount}");
    Console.WriteLine($"MachineCurrentStatus={currentCount}");
    Console.WriteLine($"MachineStatuses={statusCount}");
    Console.WriteLine($"ActiveMachines={machineCount}");
    Console.WriteLine($"DadActiveMachines={dadMachineCount}");
    Console.WriteLine($"DadEvents={dadEventCount}");
    Console.WriteLine($"GBareruActiveProcedureTimes={gBareruProcedureTimeCount}/3");
    Console.WriteLine($"FirstEvent={minEvent}");
    Console.WriteLine($"LastEvent={maxEvent}");

    Console.WriteLine();
    Console.WriteLine("EventsByDate:");
    foreach (var row in conn.Query<EventDateCountRow>(
        @"
            SELECT substr(EventDateTime, 1, 10) AS Day, COUNT(1) AS Total
            FROM MachineEvents
            GROUP BY substr(EventDateTime, 1, 10)
            ORDER BY Day DESC
            LIMIT 10;"))
    {
        Console.WriteLine($"  {row.Day}: {row.Total}");
    }

    Console.WriteLine();
    Console.WriteLine("DadStatuses:");
    foreach (var row in conn.Query<StatusDefinitionProbeRow>(
        @"
            SELECT
                SectorId,
                StatusCode,
                DisplayCode,
                COALESCE(Classification, '') AS Classification,
                COALESCE(NamePt, '') AS NamePt,
                COALESCE(NameJp, '') AS NameJp,
                COALESCE(ColorHex, '') AS ColorHex
            FROM MachineStatuses
            WHERE SectorId = @DadSectorId
            ORDER BY StatusCode;",
        new
        {
            DadSectorId = dadSectorId
        }))
    {
        Console.WriteLine($"  Sector={row.SectorId} Code={row.StatusCode} Display={row.DisplayCode} Classification={row.Classification} Name={row.NamePt}");
    }

    var latestDate = conn.ExecuteScalar<string>("SELECT substr(MAX(EventDateTime), 1, 10) FROM MachineEvents;") ?? string.Empty;
    if (!DateTime.TryParse(latestDate, out var dashboardDate))
    {
        Console.WriteLine();
        Console.WriteLine("DashboardLatestDate=none");
        return;
    }

    var shift = conn.QueryFirstOrDefault<ShiftRow>(
        @"
            SELECT Id, COALESCE(NamePt, '') AS NamePt, COALESCE(NULLIF(NameJp, ''), NamePt, '') AS NameJp
            FROM Shifts
            ORDER BY Id;");
    if (shift == null)
    {
        Console.WriteLine("DashboardLatestDate=no-shift");
        return;
    }

    var dashboard = analytics.BuildDashboard(new ProductionDashboardFilter
    {
        Date = dashboardDate.Date,
        ShiftId = shift.Id
    });

    Console.WriteLine();
    Console.WriteLine($"DashboardLatestDate={dashboardDate:yyyy-MM-dd}");
    Console.WriteLine($"DashboardShift={shift.Id} {shift.NamePt}");
    Console.WriteLine($"DashboardMachines={dashboard.Machines.Count}");
    Console.WriteLine($"DashboardAreas={dashboard.Areas.Count}");
    Console.WriteLine($"DashboardKadouritsu={dashboard.ProductionPercent:F1}");
    Console.WriteLine($"DashboardRunning={dashboard.MachinesRunning}");
    Console.WriteLine($"DashboardStopped={dashboard.MachinesStopped}");
}

void RunProductionAudit(string[] auditArgs)
{
    var date = DateTime.Today;
    var sectorLabel = "all";
    int? sectorId = null;
    var machineFilter = string.Empty;

    for (var i = 0; i < auditArgs.Length; i++)
    {
        var arg = auditArgs[i];
        if (arg.Equals("--date", StringComparison.OrdinalIgnoreCase) && i + 1 < auditArgs.Length)
        {
            if (DateTime.TryParse(auditArgs[++i], CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsedDate))
            {
                date = parsedDate.Date;
            }
        }
        else if (arg.Equals("--sector", StringComparison.OrdinalIgnoreCase) && i + 1 < auditArgs.Length)
        {
            sectorLabel = auditArgs[++i].Trim().ToLowerInvariant();
            sectorId = sectorLabel is "all" or "*"
                ? null
                : sectorCatalog.ResolveFilter(sectorLabel);
        }
        else if (arg.Equals("--machine", StringComparison.OrdinalIgnoreCase) && i + 1 < auditArgs.Length)
        {
            machineFilter = auditArgs[++i].Trim().ToUpperInvariant();
        }
    }

    using var conn = factory.CreateOpenConnection();

    Console.WriteLine("=== PRODUCTION AUDIT ===");
    Console.WriteLine($"Database={settings.DatabasePath}");
    Console.WriteLine($"Date={date:yyyy-MM-dd}");
    Console.WriteLine($"Sector={sectorLabel}");
    Console.WriteLine($"Machine={(string.IsNullOrWhiteSpace(machineFilter) ? "all" : machineFilter)}");
    Console.WriteLine("Ec2CurrentStateMatchKey=MachineId");

    var statusRows = conn.Query(
        @"
            SELECT
                COALESCE(SectorId, 0) AS SectorKey,
                StatusCode,
                DisplayCode,
                COALESCE(Classification, '') AS Classification,
                COALESCE(NamePt, '') AS NamePt
            FROM MachineStatuses
            WHERE COALESCE(IsActive, 1) = 1
              AND (@sectorId IS NULL OR SectorId IS NULL OR SectorId = @sectorId)
            ORDER BY COALESCE(SectorId, 0), StatusCode;",
        new { sectorId }).ToList();

    Console.WriteLine();
    Console.WriteLine("StatusDefinitions:");
    foreach (var row in statusRows)
    {
        Console.WriteLine($"  Sector={row.SectorKey} Code={row.StatusCode} Display={row.DisplayCode} Classification={row.Classification} Name={row.NamePt}");
    }

    var eventAudit = conn.Query(
        @"
            SELECT
                me.StatusCode,
                COALESCE(ms.DisplayCode, me.StatusCode) AS DisplayCode,
                COALESCE(ms.Classification, '') AS Classification,
                COUNT(1) AS Quantity
            FROM MachineEvents me
            JOIN Machines m ON m.Id = me.MachineId
            LEFT JOIN MachineStatuses ms
                ON ms.StatusCode = me.StatusCode
               AND COALESCE(ms.SectorId, 0) = COALESCE(m.SectorId, 0)
            WHERE date(me.EventDateTime) = date(@date)
              AND (@sectorId IS NULL OR m.SectorId = @sectorId)
            GROUP BY me.StatusCode, COALESCE(ms.DisplayCode, me.StatusCode), COALESCE(ms.Classification, '')
            ORDER BY me.StatusCode;",
        new
        {
            date = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            sectorId
        }).ToList();

    Console.WriteLine();
    Console.WriteLine("EventsByStatus:");
    if (eventAudit.Count == 0)
    {
        Console.WriteLine("  (none)");
    }
    foreach (var row in eventAudit)
    {
        Console.WriteLine($"  Status={row.StatusCode} Display={row.DisplayCode} Classification={row.Classification} Quantity={row.Quantity}");
    }

    var ec2Rows = conn.Query(
        @"
            SELECT
                c.MachineCode,
                c.MachineId,
                c.SectorId,
                c.LocalId,
                c.AreaLabel,
                c.IsRunning,
                c.IsIgnored,
                COALESCE(c.IgnoreReason, '') AS IgnoreReason,
                COALESCE(m.MachineKey, '') AS MachineKey,
                m.SectorId AS MachineSectorId,
                m.LocalId AS MachineLocalId
            FROM Ec2MachineCurrentState c
            LEFT JOIN Machines m ON m.Id = c.MachineId
            WHERE (@sectorId IS NULL OR c.SectorId = @sectorId)
              AND (@machine = '' OR upper(trim(c.MachineCode)) = @machine)
            ORDER BY c.MachineCode;",
        new { sectorId, machine = machineFilter }).ToList();

    Console.WriteLine();
    Console.WriteLine("Ec2CurrentCross:");
    if (ec2Rows.Count == 0)
    {
        Console.WriteLine("  (none)");
    }
    foreach (var row in ec2Rows)
    {
        Console.WriteLine($"  Machine={row.MachineCode} MachineId={row.MachineId} Sector={row.SectorId} Local={row.LocalId} Area={row.AreaLabel} MachineKey={row.MachineKey} MachineSector={row.MachineSectorId} MachineLocal={row.MachineLocalId} Running={row.IsRunning} Ignored={row.IsIgnored} Reason={row.IgnoreReason}");
    }

    var duplicateMachines = conn.Query(
        @"
            SELECT
                MachineCode,
                COUNT(1) AS Total,
                GROUP_CONCAT(Id || ':' || COALESCE(SectorId, 'NULL') || ':' || COALESCE(LocalId, 'NULL') || ':' || COALESCE(MachineKey, ''), ' | ') AS Detail
            FROM Machines
            WHERE trim(COALESCE(MachineCode, '')) <> ''
            GROUP BY MachineCode
            HAVING COUNT(1) > 1
            ORDER BY MachineCode;").ToList();

    Console.WriteLine();
    Console.WriteLine("DuplicateMachinesByCode:");
    if (duplicateMachines.Count == 0)
    {
        Console.WriteLine("  (none)");
    }
    foreach (var row in duplicateMachines)
    {
        Console.WriteLine($"  Machine={row.MachineCode} Total={row.Total} Detail={row.Detail}");
    }

    var k09Rows = conn.Query(
        @"
            SELECT 'current' AS Source, MachineCode, MachineId, SectorId, LocalId, AreaLabel, IsIgnored, COALESCE(IgnoreReason, '') AS IgnoreReason
            FROM Ec2MachineCurrentState
            WHERE upper(trim(MachineCode)) = 'K09'
              AND (@machine = '' OR upper(trim(MachineCode)) = @machine)
            UNION ALL
            SELECT 'snapshot' AS Source, MachineCode, MachineId, SectorId, LocalId, AreaLabel, IsIgnored, COALESCE(IgnoreReason, '') AS IgnoreReason
            FROM Ec2MachineSnapshots
            WHERE upper(trim(MachineCode)) = 'K09'
              AND (@machine = '' OR upper(trim(MachineCode)) = @machine);",
        new { machine = machineFilter }).ToList();

    Console.WriteLine();
    Console.WriteLine("K09:");
    if (k09Rows.Count == 0)
    {
        Console.WriteLine("  (not found in Ec2MachineSnapshots or Ec2MachineCurrentState)");
    }
    foreach (var row in k09Rows)
    {
        Console.WriteLine($"  Source={row.Source} Machine={row.MachineCode} MachineId={row.MachineId} Sector={row.SectorId} Local={row.LocalId} Area={row.AreaLabel} Ignored={row.IsIgnored} Reason={row.IgnoreReason}");
    }

    var sobra = conn.QueryFirst(
        @"
            SELECT
                COUNT(1) AS TotalRows,
                COALESCE(SUM(Quantidade), 0) AS TotalQuantidade,
                COALESCE(SUM(PesoGramas), 0) AS TotalPesoGramas,
                COALESCE(MIN(Data), '') AS FirstDate,
                COALESCE(MAX(Data), '') AS LastDate
            FROM SobraDePeca;");

    Console.WriteLine();
    Console.WriteLine("SobraDePeca:");
    Console.WriteLine($"  Rows={sobra.TotalRows} Quantidade={sobra.TotalQuantidade} PesoGramas={sobra.TotalPesoGramas} FirstDate={sobra.FirstDate} LastDate={sobra.LastDate}");

    var hikitsuguiIndexes = conn.Query<string>(
        @"
            SELECT name
            FROM sqlite_master
            WHERE type = 'index'
              AND tbl_name IN ('Hikitsugui', 'HikitsuguiReads', 'HikitsuguiAttachments')
              AND name NOT LIKE 'sqlite_autoindex%'
            ORDER BY name;").ToList();

    Console.WriteLine();
    Console.WriteLine("OperatorAppIndexes:");
    if (hikitsuguiIndexes.Count == 0)
    {
        Console.WriteLine("  (none)");
    }
    foreach (var indexName in hikitsuguiIndexes)
    {
        Console.WriteLine($"  {indexName}=OK");
    }
}

void RunValidateReports(string[] validateArgs)
{
    var options = ValidateReportsOptions.Parse(validateArgs);
    using var conn = factory.CreateOpenConnection();
    new TeamOps.UI.Services.HaidaiModuleService(factory).RefreshSectorTopology();

    var shift = ResolveValidateShift(conn, options.Shift);
    var sectorId = ResolveValidateSector(options.Sector);
    var localId = ResolveValidateLocal(conn, options.Area);
    var machine = ResolveValidateMachine(conn, options.Machine, sectorId, localId);
    var filter = new ProductionDashboardFilter
    {
        Date = options.Date.Date,
        ShiftId = shift.Id,
        SectorId = sectorId,
        LocalId = localId,
        MachineId = machine?.Id ?? 0,
        MachineCode = machine?.MachineCode ?? string.Empty
    };

    var totalWatch = Stopwatch.StartNew();
    var dashboardWatch = Stopwatch.StartNew();
    var dashboard = analytics.BuildDashboard(filter);
    if (sectorId <= 0 || sectorId == sectorCatalog.ResolveRequired("G-BARERU"))
    {
        dashboard.GBareruCapacityForecast = new TeamOps.UI.Services.GBareruCapacityForecastService(factory)
            .BuildForecast(filter, dashboard);
    }
    dashboardWatch.Stop();

    Console.WriteLine("=== VALIDATE REPORTS ===");
    Console.WriteLine($"Database={settings.DatabasePath}");
    Console.WriteLine($"Date={filter.Date:yyyy-MM-dd}");
    Console.WriteLine($"Shift={shift.Id} {shift.NamePt} {shift.NameJp}".Trim());
    Console.WriteLine($"Sector={ResolveSectorLabel(sectorId, string.Empty, string.Empty)} ({sectorId})");
    Console.WriteLine($"Area={(localId <= 0 ? "all" : localId.ToString(CultureInfo.InvariantCulture))}");
    Console.WriteLine($"Machine={(machine == null ? "all" : $"{machine.Id}:{machine.MachineCode}:{machine.MachineKey}")}");
    Console.WriteLine($"Operator={options.Operator}");
    Console.WriteLine("ReferenceProductionScreen=ProductionAnalyticsService.BuildDashboard");
    Console.WriteLine("ReferenceOperatorDetail=ProductionAnalyticsService.GetOperatorDetail");
    Console.WriteLine("ReferencePresenceReport=OperatorManagerReportService logic: Operators + HaidaiAssignments + OperatorPresence + AcompYukyu/YukyuTodoke");
    Console.WriteLine("Ec2CurrentStateMatchKey=MachineId");

    Console.WriteLine();
    Console.WriteLine("ProductionTotals:");
    Console.WriteLine($"  Machines={dashboard.Machines.Count} Areas={dashboard.Areas.Count} TimelineRows={dashboard.Timeline.Count} OperatorRanking={dashboard.OperatorRanking.Count}");
    Console.WriteLine($"  Kadouritsu={dashboard.ProductionPercent:F1} Running={dashboard.MachinesRunning} Stopped={dashboard.MachinesStopped} Ignored={dashboard.MachinesIgnored} Total={dashboard.MachinesTotal}");
    Console.WriteLine($"  Minutes Running={dashboard.Machines.Sum(item => item.RunningMinutes):F1} Stop={dashboard.Machines.Sum(item => item.StoppedMinutes):F1} Error={dashboard.ErrorMinutes:F1} Inactive={dashboard.InactiveMinutes:F1}");
    Console.WriteLine($"  GBareruForecastAvailable={dashboard.GBareruCapacityForecast.IsAvailable} Message={dashboard.GBareruCapacityForecast.Message}");

    Console.WriteLine();
    Console.WriteLine("MachineMetrics:");
    if (dashboard.Machines.Count == 0)
    {
        Console.WriteLine("  (none) Reason=no active machine matched filters or production schema has no machines for this filter");
    }
    foreach (var item in dashboard.Machines)
    {
        Console.WriteLine($"  MachineId={item.MachineId} Code={item.MachineCode} Line={item.LineCode} Sector={item.SectorId} Local={item.LocalId} Area={item.Area} Status={item.StatusCode}/{item.DisplayCode} Text={item.StatusText} EC2={item.Ec2StatusText} Running={item.RunningMinutes:F1} Stop={item.StoppedMinutes:F1} Error={item.ErrorMinutes:F1} Inactive={item.InactiveMinutes:F1} Total={item.TotalMinutes:F1} Kadouritsu={item.ProductionPercent:F1} Ec2Ignored={item.IsEc2Ignored} IgnoreReason={item.Ec2IgnoreReason}");
    }

    var operatorWatch = Stopwatch.StartNew();
    var rankingItems = dashboard.OperatorRanking
        .Where(item => options.Operator.Equals("all", StringComparison.OrdinalIgnoreCase)
            || item.OperatorCodigoFJ.Equals(options.Operator, StringComparison.OrdinalIgnoreCase))
        .OrderBy(item => item.OperatorCodigoFJ, StringComparer.OrdinalIgnoreCase)
        .ToList();

    Console.WriteLine();
    Console.WriteLine("OperatorRankingVsDetail:");
    if (rankingItems.Count == 0)
    {
        Console.WriteLine("  (none) Reason=no operator ranking entries matched filters. Check HaidaiAssignments, MachineEvents and selected date/shift/sector.");
    }
    var rankingOperatorCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var item in rankingItems)
    {
        rankingOperatorCodes.Add(item.OperatorCodigoFJ);
        var detail = analytics.GetOperatorDetail(filter, item.OperatorCodigoFJ);
        var kadouritsuDiff = Math.Round(item.EstimatedKadouritsuPercent - detail.AverageKadouritsuPercent, 3);
        var runningDiff = Math.Round(item.EstimatedRunningMinutes - detail.TotalRunningMinutes, 3);
        var status = Math.Abs(kadouritsuDiff) <= 0.1d && Math.Abs(runningDiff) <= 0.1d
            ? "OK"
            : "DIVERGENCE";
        var reason = status == "OK"
            ? "same calculation as detail"
            : "ranking aggregate differs from detail average/sum";

        Console.WriteLine($"  Operator={item.OperatorCodigoFJ} RankingKadouritsu={item.EstimatedKadouritsuPercent:F1} DetailKadouritsu={detail.AverageKadouritsuPercent:F1} DiffKadouritsu={kadouritsuDiff:F3} RankingRunning={item.EstimatedRunningMinutes:F1} DetailRunning={detail.TotalRunningMinutes:F1} DiffRunning={runningDiff:F3} Entries={detail.Entries.Count} Status={status} Reason={reason}");
    }
    operatorWatch.Stop();

    var presenceWatch = Stopwatch.StartNew();
    var presenceAudit = BuildPresenceAudit(conn, filter, rankingOperatorCodes);
    presenceWatch.Stop();

    Console.WriteLine();
    Console.WriteLine("PresenceHaidaiProduction:");
    Console.WriteLine($"  HaidaiOperators={presenceAudit.HaidaiOperators.Count} PresenceOperators={presenceAudit.PresenceOperators.Count} RankingOperators={rankingOperatorCodes.Count}");
    Console.WriteLine($"  HaidaiSemPresenca={presenceAudit.HaidaiWithoutPresence.Count} [{string.Join(",", presenceAudit.HaidaiWithoutPresence.Take(20))}]");
    Console.WriteLine($"  PresencaSemHaidai={presenceAudit.PresenceWithoutHaidai.Count} [{string.Join(",", presenceAudit.PresenceWithoutHaidai.Take(20))}]");
    Console.WriteLine($"  RankingSemPresenca={presenceAudit.RankingWithoutPresence.Count} [{string.Join(",", presenceAudit.RankingWithoutPresence.Take(20))}]");
    Console.WriteLine($"  RankingSemHaidai={presenceAudit.RankingWithoutHaidai.Count} [{string.Join(",", presenceAudit.RankingWithoutHaidai.Take(20))}]");

    var duplicateMachines = conn.Query<DuplicateMachineProbeRow>(
        @"
            SELECT
                MachineCode,
                COUNT(1) AS Total,
                GROUP_CONCAT(Id || ':' || COALESCE(SectorId, 'NULL') || ':' || COALESCE(LocalId, 'NULL') || ':' || COALESCE(MachineKey, ''), ' | ') AS Detail
            FROM Machines
            WHERE trim(COALESCE(MachineCode, '')) <> ''
            GROUP BY MachineCode
            HAVING COUNT(1) > 1
            ORDER BY MachineCode;").ToList();

    Console.WriteLine();
    Console.WriteLine("DuplicateMachinesByCode:");
    if (duplicateMachines.Count == 0)
    {
        Console.WriteLine("  (none)");
    }
    foreach (var row in duplicateMachines)
    {
        Console.WriteLine($"  Machine={row.MachineCode} Total={row.Total} Detail={row.Detail}");
    }

    var machineCodeOnlyRisk = conn.QueryFirstOrDefault<int>(
        @"
            SELECT COUNT(1)
            FROM Ec2MachineCurrentState c
            JOIN Machines m ON upper(trim(m.MachineCode)) = upper(trim(c.MachineCode))
            WHERE m.Id <> c.MachineId;") > 0;

    Console.WriteLine();
    Console.WriteLine("MachineJoinAudit:");
    Console.WriteLine($"  Ec2CurrentStatePrimaryKey={ResolveEc2CurrentPrimaryKey(conn)}");
    Console.WriteLine($"  Ec2MachineCodeMismatchRows={(machineCodeOnlyRisk ? "yes" : "no")}");
    Console.WriteLine("  MachineCodeOnlyLegacyHelper=ProductionMachineRepository.GetByMachineCode still exists; EC2 current-state import/audit path uses MachineId.");

    totalWatch.Stop();
    Console.WriteLine();
    Console.WriteLine("Performance:");
    Console.WriteLine($"  BuildProductionMs={dashboardWatch.ElapsedMilliseconds}");
    Console.WriteLine($"  BuildOperatorDetailsMs={operatorWatch.ElapsedMilliseconds}");
    Console.WriteLine($"  BuildPresenceAuditMs={presenceWatch.ElapsedMilliseconds}");
    Console.WriteLine($"  TotalMs={totalWatch.ElapsedMilliseconds}");
}

void RunValidateYakinProduction(string[] validateArgs)
{
    var options = ValidateYakinProductionOptions.Parse(validateArgs);
    using var conn = factory.CreateOpenConnection();
    new TeamOps.UI.Services.HaidaiModuleService(factory).RefreshSectorTopology();

    var shift = ResolveValidateShift(conn, "yakin");
    var operatorCodes = options.Operator.Equals("all", StringComparison.OrdinalIgnoreCase)
        ? conn.Query<string>(
                @"
                    SELECT DISTINCT COALESCE(OperatorCodigoFJ, '')
                    FROM HaidaiAssignments
                    WHERE date(ScheduleDate) = date(@date)
                      AND ShiftId = @shiftId
                      AND COALESCE(IsLineupActive, 1) = 1
                      AND COALESCE(LocalId, 0) > 0
                      AND trim(COALESCE(OperatorCodigoFJ, '')) <> ''
                    ORDER BY OperatorCodigoFJ;",
                new
                {
                    date = options.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    shiftId = shift.Id
                })
            .ToList()
        : new List<string> { options.Operator };

    var filter = new ProductionDashboardFilter
    {
        Date = options.Date.Date,
        ShiftId = shift.Id,
        SectorId = 0,
        LocalId = 0,
        MachineId = 0
    };

    Console.WriteLine("=== VALIDATE YAKIN PRODUCTION ===");
    Console.WriteLine($"Database={settings.DatabasePath}");
    Console.WriteLine($"BaseDate={options.Date:yyyy-MM-dd}");
    Console.WriteLine($"Shift={shift.Id} {shift.NamePt} {shift.NameJp}".Trim());
    Console.WriteLine("Columns=OperatorId|OperatorName|Date|Shift|Area|Kadouritsu|RunningMinutes|TotalWindowMinutes|EventsCount|Coverage|CountsInAverage|Reason|WindowStart|WindowEnd|Suspicious");

    var suspiciousCount = 0;
    foreach (var operatorCode in operatorCodes)
    {
        var detail = analytics.GetOperatorDetail(filter, operatorCode);
        foreach (var entry in detail.Entries.OrderBy(item => item.Date).ThenBy(item => item.LocalNamePt, StringComparer.OrdinalIgnoreCase))
        {
            var suspicious = entry.KadouritsuPercent == 0
                && entry.RunningMinutes == 0
                && entry.EffectiveMinutes == 0
                && string.Equals(entry.CoverageMode, "full", StringComparison.OrdinalIgnoreCase);

            if (suspicious)
            {
                suspiciousCount++;
            }

            Console.WriteLine(
                $"{detail.OperatorCodigoFJ}|{detail.OperatorNamePt}|{entry.Date:yyyy-MM-dd}|{shift.NamePt}|{entry.LocalNamePt}|{entry.KadouritsuPercent:F1}|{entry.RunningMinutes:F1}|{entry.EffectiveMinutes:F1}/{entry.PlannedMinutes:F1}|{entry.EventsCount}|{entry.CoverageMode}|{(entry.CountsInAverage ? "sim" : "nao")}|{entry.AverageReason}|{entry.WindowStart:yyyy-MM-dd HH:mm:ss}|{entry.WindowEnd:yyyy-MM-dd HH:mm:ss}|{(suspicious ? "yes" : "no")}");
        }
    }

    Console.WriteLine($"OperatorsChecked={operatorCodes.Count}");
    Console.WriteLine($"SuspiciousRows={suspiciousCount}");
}

static void RequireTable(System.Data.IDbConnection conn, string tableName, List<string> issues)
{
    var exists = conn.ExecuteScalar<int>(
        @"
            SELECT COUNT(1)
            FROM sqlite_master
            WHERE type = 'table'
              AND name = @tableName;",
        new
        {
            tableName
        }) > 0;

    if (!exists)
    {
        issues.Add($"MISSING_TABLE: {tableName}");
    }
}

void RunMachineCleanup(string[] cleanupArgs)
{
    var apply = cleanupArgs.Any(arg => arg.Equals("--apply", StringComparison.OrdinalIgnoreCase));
    using var conn = factory.CreateOpenConnection();

    var rows = conn.Query<MachineCleanupProbeRow>(
            @"
                SELECT
                    m.Id,
                    COALESCE(m.MachineCode, '') AS MachineCode,
                    COALESCE(m.MachineKey, '') AS MachineKey,
                    COALESCE(m.LineCode, '') AS LineCode,
                    COALESCE(m.NamePt, '') AS NamePt,
                    COALESCE(m.NameJp, '') AS NameJp,
                    COALESCE(m.IsActive, 1) AS IsActive,
                    (
                        SELECT COUNT(1)
                        FROM MachineEvents e
                        WHERE e.MachineId = m.Id
                    ) AS EventCount,
                    (
                        SELECT COUNT(1)
                        FROM MachineCurrentStatus cs
                        WHERE cs.MachineId = m.Id
                    ) AS CurrentStatusCount
                FROM Machines m
                WHERE COALESCE(m.IsActive, 1) = 1
                  AND (
                        trim(COALESCE(NULLIF(m.MachineCode, ''), m.NamePt, '')) = ''
                        OR trim(COALESCE(NULLIF(m.MachineCode, ''), m.NamePt, '')) NOT GLOB '*[A-Za-z]*'
                        OR trim(COALESCE(NULLIF(m.MachineCode, ''), m.NamePt, '')) GLOB '*.*'
                        OR trim(COALESCE(NULLIF(m.MachineCode, ''), m.NamePt, '')) GLOB '*/*'
                        OR trim(COALESCE(NULLIF(m.MachineCode, ''), m.NamePt, '')) GLOB '*,*'
                  )
                ORDER BY m.Id;")
        .ToList();

    Console.WriteLine("=== MACHINE CLEANUP ===");
    Console.WriteLine($"Database={settings.DatabasePath}");
    Console.WriteLine($"Mode={(apply ? "APPLY" : "DRY-RUN")}");
    Console.WriteLine($"InvalidActiveMachines={rows.Count}");

    foreach (var row in rows.Take(100))
    {
        Console.WriteLine($"  Id={row.Id} Code={row.MachineCode} Key={row.MachineKey} Line={row.LineCode} NamePt={row.NamePt} Active={row.IsActive} Events={row.EventCount} Current={row.CurrentStatusCount}");
    }

    if (rows.Count > 100)
    {
        Console.WriteLine($"  ... {rows.Count - 100} registros omitidos no console.");
    }

    if (!apply || rows.Count == 0)
    {
        Console.WriteLine(apply ? "OK: nada para inativar." : "DRY-RUN: rode com --apply para inativar as maquinas invalidas.");
        return;
    }

    using var tx = conn.BeginTransaction();
    var affected = conn.Execute(
        @"
            UPDATE Machines
            SET IsActive = 0
            WHERE COALESCE(IsActive, 1) = 1
              AND (
                    trim(COALESCE(NULLIF(MachineCode, ''), NamePt, '')) = ''
                    OR trim(COALESCE(NULLIF(MachineCode, ''), NamePt, '')) NOT GLOB '*[A-Za-z]*'
                    OR trim(COALESCE(NULLIF(MachineCode, ''), NamePt, '')) GLOB '*.*'
                    OR trim(COALESCE(NULLIF(MachineCode, ''), NamePt, '')) GLOB '*/*'
                    OR trim(COALESCE(NULLIF(MachineCode, ''), NamePt, '')) GLOB '*,*'
              );",
        transaction: tx);
    tx.Commit();

    Console.WriteLine($"InactiveMachines={affected}");
    Console.WriteLine("OK: maquinas invalidas foram inativadas; eventos historicos foram preservados.");
}

static void RequireColumn(System.Data.IDbConnection conn, string tableName, string columnName, List<string> issues)
{
    var exists = conn.ExecuteScalar<int>(
        $@"
            SELECT COUNT(1)
            FROM pragma_table_info('{tableName}')
            WHERE name = @columnName;",
        new
        {
            columnName
        }) > 0;

    if (!exists)
    {
        issues.Add($"MISSING_COLUMN: {tableName}.{columnName}");
    }
}

static void RequireIndex(System.Data.IDbConnection conn, string indexName, List<string> issues)
{
    if (!IndexExists(conn, indexName))
    {
        issues.Add($"MISSING_INDEX: {indexName}");
    }
}

static void WarnIfIndexExists(System.Data.IDbConnection conn, string indexName, List<string> issues)
{
    if (IndexExists(conn, indexName))
    {
        issues.Add($"EXTRA_INDEX_PERFORMANCE_RISK: {indexName}");
    }
}

static bool IndexExists(System.Data.IDbConnection conn, string indexName)
{
    return conn.ExecuteScalar<int>(
        @"
            SELECT COUNT(1)
            FROM sqlite_master
            WHERE type = 'index'
              AND name = @indexName;",
        new
        {
            indexName
        }) > 0;
}

void RunStatusReport(string[] reportArgs)
{
    var options = StatusReportOptions.Parse(reportArgs, sectorCatalog.ResolveFilter, sectorCatalog.GetLabel);
    var dadSectorId = sectorCatalog.ResolveRequired("DAD");

    using var conn = factory.CreateOpenConnection();

    var statuses = conn.Query<StatusDefinitionProbeRow>(
        @"
            SELECT
                SectorId,
                StatusCode,
                DisplayCode,
                COALESCE(Classification, '') AS Classification,
                COALESCE(NamePt, '') AS NamePt,
                COALESCE(NameJp, '') AS NameJp,
                COALESCE(ColorHex, '') AS ColorHex
            FROM MachineStatuses
            WHERE COALESCE(IsActive, 1) = 1;"
    ).ToList();

    var events = conn.Query<StatusEventProbeRow>(
        @"
            SELECT
                e.MachineId,
                e.MachineCode,
                e.LineCode,
                e.SectorId,
                COALESCE(s.NamePt, '') AS SectorName,
                e.StatusCode,
                COALESCE(e.StatusText, '') AS StatusText,
                e.EventDateTime
            FROM MachineEvents e
            LEFT JOIN Sectors s ON s.Id = e.SectorId
            WHERE (@startDate = '' OR datetime(e.EventDateTime) >= datetime(@startDate))
              AND (@endDate = '' OR datetime(e.EventDateTime) < datetime(@endDate))
              AND (@sectorId <= 0 OR COALESCE(e.SectorId, 0) = @sectorId)
            ORDER BY e.MachineId, datetime(e.EventDateTime), e.Id;",
        new
        {
            startDate = options.StartDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty,
            endDate = options.EndDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty,
            sectorId = options.SectorId
        }
    ).ToList();

    var rows = BuildStatusReportRows(events, statuses);

    Console.WriteLine("=== PRODUCTION STATUS REPORT ===");
    Console.WriteLine($"Rows={rows.Count} Events={events.Count} Start={options.StartDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? "(all)"} End={options.EndDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? "(all)"} Sector={options.SectorIdLabel}");
    Console.WriteLine("Sector,StatusCode,Description,Classification,Source,Occurrences,TotalMinutes,CountsInEfficiencyDenominator,UsedFallback,EstimatedEfficiencyImpact,Warning");

    foreach (var row in rows)
    {
        Console.WriteLine(row.ToCsvLine());
    }

    var dadWarnings = rows
        .Where(row => row.SectorId == dadSectorId && !string.IsNullOrWhiteSpace(row.Warning))
        .ToList();

    Console.WriteLine();
    Console.WriteLine("=== DAD FOCUS ===");
    Console.WriteLine($"KnownCodesFound={string.Join(",", rows.Where(row => row.SectorId == dadSectorId && IsDadSeedStatusCode(row.StatusCode)).Select(row => row.StatusCode).Distinct().OrderBy(code => code))}");
    Console.WriteLine($"UnexpectedCodesFound={string.Join(",", rows.Where(row => row.SectorId == dadSectorId && !IsDadSeedStatusCode(row.StatusCode)).Select(row => row.StatusCode).Distinct().OrderBy(code => code))}");
    Console.WriteLine($"WarningRows={dadWarnings.Count}");

    if (options.CsvPath.Length > 0)
    {
        var csvLines = new[]
            {
                "Sector,StatusCode,Description,Classification,Source,Occurrences,TotalMinutes,CountsInEfficiencyDenominator,UsedFallback,EstimatedEfficiencyImpact,Warning"
            }
            .Concat(rows.Select(row => row.ToCsvLine()));

        File.WriteAllLines(options.CsvPath, csvLines);
        Console.WriteLine($"CSV={Path.GetFullPath(options.CsvPath)}");
    }
}

void ShowDashboard(string label, DateTime date, int shiftId)
{
    var dashboard = analytics.BuildDashboard(new ProductionDashboardFilter
    {
        Date = date,
        ShiftId = shiftId,
        MachineCode = "E01"
    });

    Console.WriteLine($"=== DASHBOARD {label} ===");
    Console.WriteLine($"Date={date:yyyy-MM-dd}");
    Console.WriteLine($"Period={dashboard.Period.Start:yyyy-MM-dd HH:mm:ss} -> {dashboard.Period.End:yyyy-MM-dd HH:mm:ss}");
    Console.WriteLine($"Kadouritsu={dashboard.ProductionPercent:F1}");
    Console.WriteLine($"MachinesRunning={dashboard.MachinesRunning}");
    Console.WriteLine($"MachinesStopped={dashboard.MachinesStopped}");
    Console.WriteLine($"ErrorMinutes={dashboard.ErrorMinutes:F1}");
    Console.WriteLine($"InactiveMinutes={dashboard.InactiveMinutes:F1}");
    Console.WriteLine($"Areas={dashboard.Areas.Count}");
    Console.WriteLine($"OperatorRanking={dashboard.OperatorRanking.Count}");

    foreach (var machine in dashboard.Machines)
    {
        Console.WriteLine($"Machine={machine.MachineCode} Status={machine.StatusText} Kadouritsu={machine.ProductionPercent:F1}% Running={machine.RunningMinutes:F1} Stop={machine.StoppedMinutes:F1} Error={machine.ErrorMinutes:F1} Inactive={machine.InactiveMinutes:F1} Updated={machine.LastUpdate:yyyy-MM-dd HH:mm:ss}");
    }
}

void PrintRuntimeConfigDiagnostics()
{
    var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None).FilePath;
    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
    var ec2FilePath = ConfigurationManager.AppSettings["Ec2AdministratorFilePath"] ?? string.Empty;
    var ignoreAfter = ConfigurationManager.AppSettings["MachineStoppedIgnoreAfterMinutes"] ?? string.Empty;
    var runningKeywords = ConfigurationManager.AppSettings["Ec2AdministratorRunningStatusKeywords"] ?? string.Empty;

    Console.WriteLine("=== RUNTIME CONFIG DIAGNOSTICS ===");
    Console.WriteLine($"BaseDirectory={baseDir}");
    Console.WriteLine($"ConfigurationFile={configFile}");
    Console.WriteLine($"Ec2AdministratorFilePath={ec2FilePath}");
    var ec2ResolvedPath = string.IsNullOrWhiteSpace(ec2FilePath)
        ? string.Empty
        : Path.GetFullPath(ec2FilePath);
    var ec2Exists = !string.IsNullOrWhiteSpace(ec2FilePath) && File.Exists(ec2FilePath);
    Console.WriteLine($"Ec2AdministratorResolvedFullPath={ec2ResolvedPath}");
    Console.WriteLine($"Ec2AdministratorFileExists={(ec2Exists ? "yes" : "no")}");
    if (ec2Exists)
    {
        var fileInfo = new FileInfo(ec2ResolvedPath);
        Console.WriteLine($"Ec2AdministratorFileSize={fileInfo.Length}");
        Console.WriteLine($"Ec2AdministratorLastWriteTime={fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}");
    }
    Console.WriteLine($"MachineStoppedIgnoreAfterMinutes={ignoreAfter}");
    Console.WriteLine($"Ec2AdministratorRunningStatusKeywords={runningKeywords}");
    Console.WriteLine();
}

static ShiftRow ResolveValidateShift(System.Data.IDbConnection conn, string shiftFilter)
{
    var shifts = conn.Query<ShiftRow>(
        @"
            SELECT
                Id,
                COALESCE(NamePt, '') AS NamePt,
                COALESCE(NULLIF(NameJp, ''), NamePt, '') AS NameJp
            FROM Shifts
            ORDER BY Id;").ToList();

    if (shifts.Count == 0)
    {
        throw new InvalidOperationException("Nenhum turno cadastrado.");
    }

    var normalized = (shiftFilter ?? string.Empty).Trim();
    if (string.IsNullOrWhiteSpace(normalized))
    {
        return shifts[0];
    }

    if (int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var shiftId))
    {
        return shifts.FirstOrDefault(item => item.Id == shiftId)
            ?? throw new InvalidOperationException($"Turno nao encontrado: {shiftFilter}");
    }

    var keywords = normalized.Equals("noite", StringComparison.OrdinalIgnoreCase)
        || normalized.Equals("night", StringComparison.OrdinalIgnoreCase)
        || normalized.Equals("yakin", StringComparison.OrdinalIgnoreCase)
            ? new[] { "noite", "night", "yakin" }
            : normalized.Equals("dia", StringComparison.OrdinalIgnoreCase)
              || normalized.Equals("day", StringComparison.OrdinalIgnoreCase)
              || normalized.Equals("hirukin", StringComparison.OrdinalIgnoreCase)
              || normalized.Equals("hiru", StringComparison.OrdinalIgnoreCase)
                ? new[] { "dia", "day", "hiru", "hirukin" }
                : new[] { normalized };

    return shifts.FirstOrDefault(item => MatchesShift(item, keywords))
        ?? throw new InvalidOperationException($"Turno nao encontrado: {shiftFilter}");
}

int ResolveValidateSector(string sectorFilter)
{
    var normalized = (sectorFilter ?? string.Empty).Trim();
    if (string.IsNullOrWhiteSpace(normalized) || normalized.Equals("all", StringComparison.OrdinalIgnoreCase))
    {
        return 0;
    }

    return sectorCatalog.ResolveFilter(normalized);
}

static int ResolveValidateLocal(System.Data.IDbConnection conn, string areaFilter)
{
    var normalized = (areaFilter ?? string.Empty).Trim();
    if (string.IsNullOrWhiteSpace(normalized) || normalized.Equals("all", StringComparison.OrdinalIgnoreCase))
    {
        return 0;
    }

    if (int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var localId))
    {
        return localId;
    }

    var row = conn.QueryFirstOrDefault<LocalLookupProbeRow>(
        @"
            SELECT Id, COALESCE(NamePt, '') AS NamePt, COALESCE(NameJp, '') AS NameJp, COALESCE(ShortCode, '') AS ShortCode
            FROM Locals
            WHERE lower(trim(COALESCE(NamePt, ''))) = lower(trim(@value))
               OR lower(trim(COALESCE(NameJp, ''))) = lower(trim(@value))
               OR lower(trim(COALESCE(ShortCode, ''))) = lower(trim(@value))
            ORDER BY Id
            LIMIT 1;",
        new { value = normalized });

    if (row != null)
    {
        return row.Id;
    }

    var digits = new string(normalized.Where(char.IsDigit).ToArray());
    if (int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var areaNumber))
    {
        return areaNumber == 1 ? 1 : areaNumber + 1;
    }

    throw new InvalidOperationException($"Area/local nao encontrado: {areaFilter}");
}

static MachineLookupProbeRow? ResolveValidateMachine(System.Data.IDbConnection conn, string machineFilter, int sectorId, int localId)
{
    var normalized = (machineFilter ?? string.Empty).Trim();
    if (string.IsNullOrWhiteSpace(normalized) || normalized.Equals("all", StringComparison.OrdinalIgnoreCase))
    {
        return null;
    }

    List<MachineLookupProbeRow> rows;
    if (int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var machineId))
    {
        rows = conn.Query<MachineLookupProbeRow>(
            @"
                SELECT Id, COALESCE(MachineCode, '') AS MachineCode, COALESCE(MachineKey, '') AS MachineKey, SectorId, LocalId
                FROM Machines
                WHERE Id = @machineId;",
            new { machineId }).ToList();
    }
    else
    {
        rows = conn.Query<MachineLookupProbeRow>(
            @"
                SELECT Id, COALESCE(MachineCode, '') AS MachineCode, COALESCE(MachineKey, '') AS MachineKey, SectorId, LocalId
                FROM Machines
                WHERE upper(trim(COALESCE(MachineCode, ''))) = upper(trim(@machineCode))
                  AND (@sectorId <= 0 OR SectorId = @sectorId)
                  AND (@localId <= 0 OR LocalId = @localId)
                ORDER BY Id;",
            new
            {
                machineCode = normalized,
                sectorId,
                localId
            }).ToList();

        if (rows.Count == 0)
        {
            rows = conn.Query<MachineLookupProbeRow>(
                @"
                    SELECT Id, COALESCE(MachineCode, '') AS MachineCode, COALESCE(MachineKey, '') AS MachineKey, SectorId, LocalId
                    FROM Machines
                    WHERE upper(trim(COALESCE(MachineCode, ''))) = upper(trim(@machineCode))
                    ORDER BY Id;",
                new { machineCode = normalized }).ToList();
        }
    }

    if (rows.Count == 0)
    {
        throw new InvalidOperationException($"Maquina nao encontrada: {machineFilter}");
    }

    if (rows.Count > 1)
    {
        Console.WriteLine($"MachineResolutionWarning=duplicate-code Matches={string.Join(" | ", rows.Select(item => $"{item.Id}:{item.MachineCode}:{item.MachineKey}:{item.SectorId}:{item.LocalId}"))}");
    }

    return rows[0];
}

static PresenceAuditProbeResult BuildPresenceAudit(
    System.Data.IDbConnection conn,
    ProductionDashboardFilter filter,
    IReadOnlySet<string> rankingOperatorCodes)
{
    var parameters = new
    {
        date = filter.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        shiftId = filter.ShiftId,
        sectorId = filter.SectorId,
        localId = filter.LocalId
    };

    var haidaiOperators = conn.Query<string>(
        @"
            SELECT DISTINCT COALESCE(ha.OperatorCodigoFJ, '') AS OperatorCodigoFJ
            FROM HaidaiAssignments ha
            WHERE date(ha.ScheduleDate) = date(@date)
              AND ha.ShiftId = @shiftId
              AND COALESCE(ha.IsLineupActive, 1) = 1
              AND trim(COALESCE(ha.OperatorCodigoFJ, '')) <> ''
              AND (@sectorId <= 0 OR ha.SectorId = @sectorId)
              AND (@localId <= 0 OR ha.LocalId = @localId);",
        parameters).ToHashSet(StringComparer.OrdinalIgnoreCase);

    var presenceOperators = conn.Query<string>(
        @"
            SELECT DISTINCT COALESCE(CodigoFJ, '') AS OperatorCodigoFJ
            FROM OperatorPresence
            WHERE date(Date) = date(@date)
              AND ShiftId = @shiftId
              AND trim(COALESCE(CodigoFJ, '')) <> ''
              AND (@sectorId <= 0 OR SectorId = @sectorId);",
        parameters).ToHashSet(StringComparer.OrdinalIgnoreCase);

    return new PresenceAuditProbeResult(
        Sorted(haidaiOperators),
        Sorted(presenceOperators),
        Sorted(haidaiOperators.Except(presenceOperators, StringComparer.OrdinalIgnoreCase)),
        Sorted(presenceOperators.Except(haidaiOperators, StringComparer.OrdinalIgnoreCase)),
        Sorted(rankingOperatorCodes.Except(presenceOperators, StringComparer.OrdinalIgnoreCase)),
        Sorted(rankingOperatorCodes.Except(haidaiOperators, StringComparer.OrdinalIgnoreCase)));
}

static string ResolveEc2CurrentPrimaryKey(System.Data.IDbConnection conn)
{
    var primaryKeys = conn.Query<string>(
        @"
            SELECT name
            FROM pragma_table_info('Ec2MachineCurrentState')
            WHERE pk > 0
            ORDER BY pk;").ToList();

    return primaryKeys.Count == 0
        ? "(none)"
        : string.Join(",", primaryKeys);
}

static List<string> Sorted(IEnumerable<string> values)
{
    return values
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
        .ToList();
}

static bool MatchesShift(ShiftRow shift, params string[] keywords)
{
    var values = new[] { shift.NamePt, shift.NameJp }
        .Where(value => !string.IsNullOrWhiteSpace(value));

    foreach (var value in values)
    {
        foreach (var keyword in keywords)
        {
            if (value.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
    }

    return false;
}

static void ExpectStatus(
    IReadOnlyList<StatusDefinitionProbeRow> statuses,
    int? sectorId,
    int statusCode,
    int displayCode,
    string classification)
{
    var status = statuses.FirstOrDefault(item =>
        item.SectorId == sectorId
        && item.StatusCode == statusCode);

    Require(status != null, $"Status esperado nao encontrado: setor={sectorId?.ToString() ?? "global"} codigo={statusCode}.");
    Require(status!.DisplayCode == displayCode, $"DisplayCode invalido para setor={sectorId?.ToString() ?? "global"} codigo={statusCode}.");
    Require(string.Equals(status.Classification, classification, StringComparison.OrdinalIgnoreCase), $"Classification invalida para setor={sectorId?.ToString() ?? "global"} codigo={statusCode}.");
}

static StatusDefinitionProbeRow? ResolveStatusForProbe(
    IReadOnlyList<StatusDefinitionProbeRow> statuses,
    int sectorId,
    int statusCode)
{
    return statuses.FirstOrDefault(item => item.SectorId == sectorId && item.StatusCode == statusCode)
        ?? statuses.FirstOrDefault(item => !item.SectorId.HasValue && item.StatusCode == statusCode);
}

List<StatusReportRow> BuildStatusReportRows(
    IReadOnlyList<StatusEventProbeRow> events,
    IReadOnlyList<StatusDefinitionProbeRow> statuses)
{
    var accumulators = new Dictionary<string, StatusReportAccumulator>(StringComparer.OrdinalIgnoreCase);

    foreach (var machineEvents in events.GroupBy(item => item.MachineId))
    {
        var orderedEvents = machineEvents
            .OrderBy(item => item.EventDateTime)
            .ToList();

        for (var index = 0; index < orderedEvents.Count; index++)
        {
            var current = orderedEvents[index];
            var next = index + 1 < orderedEvents.Count
                ? orderedEvents[index + 1]
                : null;
            var minutes = next == null
                ? 0d
                : Math.Max(0d, (next.EventDateTime - current.EventDateTime).TotalMinutes);
            var description = string.IsNullOrWhiteSpace(current.StatusText)
                ? "-"
                : current.StatusText.Trim();
            var key = $"{current.SectorId.GetValueOrDefault()}|{current.StatusCode}|{description}";

            if (!accumulators.TryGetValue(key, out var accumulator))
            {
                var resolved = ResolveStatusForReport(statuses, current.SectorId, current.StatusCode);
                accumulator = new StatusReportAccumulator
                {
                    SectorId = current.SectorId,
                    SectorName = ResolveSectorLabel(current.SectorId, current.SectorName, current.LineCode),
                    StatusCode = current.StatusCode,
                    Description = description,
                    Classification = NormalizeReportClassification(resolved.Definition?.Classification, resolved.Definition?.DisplayCode),
                    Source = resolved.Source,
                    UsedFallback = resolved.UsedFallback
                };
                accumulators[key] = accumulator;
            }

            accumulator.Occurrences++;
            accumulator.TotalMinutes += minutes;
        }
    }

    return accumulators.Values
        .Select(CreateStatusReportRow)
        .OrderBy(row => row.SectorId.GetValueOrDefault())
        .ThenBy(row => row.StatusCode)
        .ThenBy(row => row.Description, StringComparer.OrdinalIgnoreCase)
        .ToList();
}

StatusReportRow CreateStatusReportRow(StatusReportAccumulator accumulator)
{
    var countsInDenominator = CountsInEfficiencyDenominator(accumulator.Classification);
    var estimatedImpact = countsInDenominator && !accumulator.Classification.Equals("Running", StringComparison.OrdinalIgnoreCase)
        ? Math.Round(accumulator.TotalMinutes, 1)
        : 0d;

    return new StatusReportRow
    {
        SectorId = accumulator.SectorId,
        Sector = accumulator.SectorName,
        StatusCode = accumulator.StatusCode,
        Description = accumulator.Description,
        Classification = accumulator.Classification,
        Source = accumulator.Source,
        Occurrences = accumulator.Occurrences,
        TotalMinutes = Math.Round(accumulator.TotalMinutes, 1),
        CountsInEfficiencyDenominator = countsInDenominator,
        UsedFallback = accumulator.UsedFallback,
        EstimatedEfficiencyImpact = estimatedImpact,
        Warning = BuildWarning(accumulator)
    };
}

StatusDefinitionResolution ResolveStatusForReport(
    IReadOnlyList<StatusDefinitionProbeRow> statuses,
    int? sectorId,
    int statusCode)
{
    var exact = statuses.FirstOrDefault(item => item.SectorId == sectorId && item.StatusCode == statusCode);
    if (exact != null)
    {
        return new StatusDefinitionResolution(exact, ResolveStatusSource(sectorId, statusCode, false), false);
    }

    var global = statuses.FirstOrDefault(item => !item.SectorId.HasValue && item.StatusCode == statusCode);
    if (global != null)
    {
        return new StatusDefinitionResolution(global, ResolveStatusSource(null, statusCode, sectorId.HasValue), sectorId.HasValue);
    }

    return new StatusDefinitionResolution(null, "unknown-conservative", false);
}

string ResolveStatusSource(int? sectorId, int statusCode, bool fallback)
{
    if (fallback)
    {
        return "fallback-global";
    }

    if (!sectorId.HasValue)
    {
        return IsGlobalSeedStatusCode(statusCode)
            ? "global-seed"
            : "auto-created-or-manual-global";
    }

    if (sectorId == sectorCatalog.ResolveRequired("DAD"))
    {
        return IsDadSeedStatusCode(statusCode)
            ? "setorial-seed"
            : "auto-created-or-manual-setorial";
    }

    return "setorial";
}

static string NormalizeReportClassification(string? classification, int? displayCode)
{
    var normalized = (classification ?? string.Empty).Trim();
    if (normalized.Equals("Running", StringComparison.OrdinalIgnoreCase))
    {
        return "Running";
    }

    if (normalized.Equals("StopNoCount", StringComparison.OrdinalIgnoreCase))
    {
        return "StopNoCount";
    }

    if (normalized.Equals("Error", StringComparison.OrdinalIgnoreCase))
    {
        return "Error";
    }

    return displayCode == 0
        ? "Running"
        : displayCode == 4
            ? "Error"
            : "StopCounts";
}

string ResolveSectorLabel(int? sectorId, string sectorName, string lineCode)
{
    if (!string.IsNullOrWhiteSpace(sectorName))
    {
        return sectorName.Trim();
    }

    if (sectorId.HasValue && sectorId.Value > 0)
    {
        return sectorCatalog.GetLabel(sectorId.Value);
    }

    return string.IsNullOrWhiteSpace(lineCode) ? "Sector 0" : lineCode.Trim();
}

static bool CountsInEfficiencyDenominator(string classification)
{
    return !classification.Equals("StopNoCount", StringComparison.OrdinalIgnoreCase);
}

string BuildWarning(StatusReportAccumulator accumulator)
{
    var warnings = new List<string>();
    var dadSectorId = sectorCatalog.ResolveRequired("DAD");

    if (accumulator.SectorId == dadSectorId && !IsDadSeedStatusCode(accumulator.StatusCode))
    {
        warnings.Add("UNKNOWN_DAD_STATUS");
    }

    if (accumulator.SectorId == dadSectorId && accumulator.UsedFallback)
    {
        warnings.Add("FALLBACK_USED_FOR_DAD");
    }

    if (accumulator.Source.Contains("auto-created", StringComparison.OrdinalIgnoreCase))
    {
        warnings.Add("AUTO_CREATED_STATUS");
    }

    if (accumulator.SectorId == dadSectorId
        && !accumulator.Classification.Equals("Running", StringComparison.OrdinalIgnoreCase)
        && CountsInEfficiencyDenominator(accumulator.Classification)
        && accumulator.TotalMinutes > 0)
    {
        warnings.Add("POSSIBLE_EFFICIENCY_IMPACT");
    }

    return string.Join("|", warnings.Distinct(StringComparer.OrdinalIgnoreCase));
}

static double CalculateProbeEfficiency(double runningHours, double stopCountsHours, double stopNoCountHours)
{
    var denominator = runningHours + stopCountsHours;
    return denominator <= 0
        ? 0
        : Math.Round((runningHours / denominator) * 100d, 1);
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static bool IsGlobalSeedStatusCode(int statusCode)
{
    return statusCode is 0 or 1 or 3 or 4;
}

static bool IsDadSeedStatusCode(int statusCode)
{
    return statusCode is 0 or 1 or 3 or 4 or 17 or 18 or 19;
}

sealed class ShiftRow
{
    public int Id { get; set; }
    public string NamePt { get; set; } = string.Empty;
    public string NameJp { get; set; } = string.Empty;
}

sealed class StatusReportOptions
{
    public DateTime? StartDate { get; private init; }
    public DateTime? EndDate { get; private init; }
    public int SectorId { get; private init; }
    public string CsvPath { get; private init; } = string.Empty;
    public string SectorIdLabel { get; private init; } = "(all)";

    public static StatusReportOptions Parse(string[] args, Func<string, int> resolveSector,
        Func<int, string> getSectorLabel)
    {
        DateTime? startDate = null;
        DateTime? endDate = null;
        var sectorId = 0;
        var csvPath = string.Empty;

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index].Trim();
            if (arg.Equals("--start", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                startDate = DateTime.Parse(args[++index], CultureInfo.InvariantCulture);
            }
            else if (arg.Equals("--end", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                endDate = DateTime.Parse(args[++index], CultureInfo.InvariantCulture);
            }
            else if (arg.Equals("--sector", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                sectorId = resolveSector(args[++index]);
            }
            else if (arg.Equals("--csv", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                csvPath = args[++index].Trim();
            }
        }

        return new StatusReportOptions
        {
            StartDate = startDate,
            EndDate = endDate,
            SectorId = sectorId,
            SectorIdLabel = sectorId <= 0 ? "(all)" : getSectorLabel(sectorId),
            CsvPath = csvPath
        };
    }
}

sealed class ValidateReportsOptions
{
    public DateTime Date { get; private init; } = DateTime.Today;
    public string Shift { get; private init; } = "noite";
    public string Sector { get; private init; } = "gbareru";
    public string Area { get; private init; } = "all";
    public string Machine { get; private init; } = "all";
    public string Operator { get; private init; } = "all";

    public static ValidateReportsOptions Parse(string[] args)
    {
        var date = DateTime.Today;
        var shift = "noite";
        var sector = "gbareru";
        var area = "all";
        var machine = "all";
        var operatorCode = "all";

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index].Trim();
            if (arg.Equals("--date", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                date = DateTime.Parse(args[++index], CultureInfo.InvariantCulture).Date;
            }
            else if (arg.Equals("--shift", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                shift = args[++index].Trim();
            }
            else if (arg.Equals("--sector", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                sector = args[++index].Trim();
            }
            else if (arg.Equals("--area", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                area = args[++index].Trim();
            }
            else if (arg.Equals("--machine", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                machine = args[++index].Trim();
            }
            else if (arg.Equals("--operator", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                operatorCode = args[++index].Trim();
            }
        }

        return new ValidateReportsOptions
        {
            Date = date,
            Shift = shift,
            Sector = sector,
            Area = area,
            Machine = machine,
            Operator = string.IsNullOrWhiteSpace(operatorCode) ? "all" : operatorCode
        };
    }
}

sealed class ValidateYakinProductionOptions
{
    public DateTime Date { get; private init; } = DateTime.Today;
    public string Operator { get; private init; } = "all";

    public static ValidateYakinProductionOptions Parse(string[] args)
    {
        var date = DateTime.Today;
        var operatorCode = "all";

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index].Trim();
            if (arg.Equals("--date", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                date = DateTime.Parse(args[++index], CultureInfo.InvariantCulture).Date;
            }
            else if (arg.Equals("--operator", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                operatorCode = args[++index].Trim();
            }
        }

        return new ValidateYakinProductionOptions
        {
            Date = date,
            Operator = string.IsNullOrWhiteSpace(operatorCode) ? "all" : operatorCode
        };
    }
}

sealed class ValidateOvertimeRealOptions
{
    public DateTime Date { get; private init; } = DateTime.Today;
    public string Shift { get; private init; } = "yakin";
    public string Operator { get; private init; } = "all";

    public static ValidateOvertimeRealOptions Parse(string[] args)
    {
        var date = DateTime.Today;
        var shift = "yakin";
        var operatorCode = "all";

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index].Trim();
            if (arg.Equals("--date", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                date = DateTime.Parse(args[++index], CultureInfo.InvariantCulture).Date;
            }
            else if (arg.Equals("--shift", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                shift = args[++index].Trim();
            }
            else if (arg.Equals("--operator", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                operatorCode = args[++index].Trim();
            }
        }

        return new ValidateOvertimeRealOptions
        {
            Date = date,
            Shift = shift,
            Operator = string.IsNullOrWhiteSpace(operatorCode) ? "all" : operatorCode
        };
    }
}

sealed record OvertimeAggregateCase(
    string Code,
    double NormalZangyouHours,
    int ShukkinDays,
    int SundayWorkedDays,
    double ExpectedZangyouJikanHours,
    double ExpectedDomingoShukkinHours,
    bool ExpectedZangyouOver45,
    bool ExpectedDomingo80To90,
    bool ExpectedDomingoOver90);

sealed class ImportProfileOptions
{
    public DateTime? Date { get; private init; }
    public int SectorId { get; private init; }
    public bool Reimport { get; private init; }
    public string SectorLabel { get; private init; } = "(all)";

    public static ImportProfileOptions Parse(string[] args, Func<string, int> resolveSector,
        Func<int, string> getSectorLabel)
    {
        DateTime? date = null;
        var sectorId = 0;
        var reimport = false;

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index].Trim();
            if (arg.Equals("--date", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                date = DateTime.Parse(args[++index], CultureInfo.InvariantCulture);
            }
            else if (arg.Equals("--sector", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                sectorId = resolveSector(args[++index]);
            }
            else if (arg.Equals("--reimport", StringComparison.OrdinalIgnoreCase))
            {
                reimport = true;
            }
        }

        return new ImportProfileOptions
        {
            Date = date,
            SectorId = sectorId,
            SectorLabel = sectorId <= 0 ? "(all)" : getSectorLabel(sectorId),
            Reimport = reimport
        };
    }
}

sealed record ProbePresenceWindowSummary(
    int RowsFound,
    DateTime? FirstPresenceTime,
    DateTime? LastPresenceTime);

sealed class OvertimeRealScheduleRow
{
    public string OperatorCodigoFJ { get; set; } = string.Empty;
    public string OperatorName { get; set; } = string.Empty;
    public int ShiftId { get; set; }
    public string ShiftName { get; set; } = string.Empty;
    public int SectorId { get; set; }
    public string SectorName { get; set; } = string.Empty;
    public string BaseDate { get; set; } = string.Empty;
    public int LocalId { get; set; }
    public string LocalName { get; set; } = string.Empty;
    public string AssignmentCode { get; set; } = string.Empty;
    public string AvailabilityStatus { get; set; } = string.Empty;
    public bool IsHolidayWork { get; set; }
}

sealed class OvertimeRealPresenceEventRow
{
    public string OperatorCodigoFJ { get; set; } = string.Empty;
    public DateTime PresenceDateTime { get; set; }
}

sealed class OvertimeRealTodokeRow
{
    public string OperatorCodigoFJ { get; set; } = string.Empty;
    public string BaseDate { get; set; } = string.Empty;
    public int MotiveId { get; set; }
    public string MotiveName { get; set; } = string.Empty;
}

sealed class OvertimeRealMovementRow
{
    public string OperatorCodigoFJ { get; set; } = string.Empty;
    public string BaseDate { get; set; } = string.Empty;
    public string MovementType { get; set; } = string.Empty;
    public string EventTime { get; set; } = string.Empty;
    public string EventDateTime { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

sealed class IndexProbeRow
{
    public string Name { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string Sql { get; set; } = string.Empty;
}

sealed class Ec2ImportProbeRow
{
    public long Id { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string SourceFile { get; set; } = string.Empty;
    public string FileHash { get; set; } = string.Empty;
    public string FileLastWriteTime { get; set; } = string.Empty;
    public string ImportedAt { get; set; } = string.Empty;
    public int RowsRead { get; set; }
    public int RowsImported { get; set; }
    public int RowsIgnored { get; set; }
}

sealed class Ec2CurrentProbeRow
{
    public string AreaLabel { get; set; } = string.Empty;
    public string MachineCode { get; set; } = string.Empty;
    public long ImportId { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public int IsStale { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public int IsRunning { get; set; }
    public int IsIgnored { get; set; }
    public string IgnoreReason { get; set; } = string.Empty;
    public string PartCode { get; set; } = string.Empty;
    public double? ProcessMinutes { get; set; }
    public string LotNo { get; set; } = string.Empty;
    public string SnapshotAt { get; set; } = string.Empty;
    public string LastSeenAt { get; set; } = string.Empty;
    public string StoppedSinceAt { get; set; } = string.Empty;
}

sealed record Ec2AverageAuditProbeRow(
    string MachineCode,
    string StatusText,
    double? TimeRead,
    bool EnteredAverage,
    string Reason);

sealed class PartCodeStyleProbeRow
{
    public string PartCode { get; set; } = string.Empty;
    public string ColorHex { get; set; } = string.Empty;
    public string TextColorHex { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int IsActive { get; set; }
}

sealed class StatusDefinitionProbeRow
{
    public int? SectorId { get; set; }
    public int StatusCode { get; set; }
    public int DisplayCode { get; set; }
    public string Classification { get; set; } = string.Empty;
    public string NamePt { get; set; } = string.Empty;
    public string NameJp { get; set; } = string.Empty;
    public string ColorHex { get; set; } = string.Empty;
}

sealed class EventDateCountRow
{
    public string Day { get; set; } = string.Empty;
    public long Total { get; set; }
}

sealed class MachineCleanupProbeRow
{
    public int Id { get; set; }
    public string MachineCode { get; set; } = string.Empty;
    public string MachineKey { get; set; } = string.Empty;
    public string LineCode { get; set; } = string.Empty;
    public string NamePt { get; set; } = string.Empty;
    public string NameJp { get; set; } = string.Empty;
    public int IsActive { get; set; }
    public long EventCount { get; set; }
    public long CurrentStatusCount { get; set; }
}

sealed class MachineLocationSnapshotProbeRow
{
    public int Id { get; set; }
    public string MachineCode { get; set; } = string.Empty;
    public string MachineKey { get; set; } = string.Empty;
    public int? SectorId { get; set; }
    public int? LocalId { get; set; }
}

sealed class MachineLookupProbeRow
{
    public int Id { get; set; }
    public string MachineCode { get; set; } = string.Empty;
    public string MachineKey { get; set; } = string.Empty;
    public int? SectorId { get; set; }
    public int? LocalId { get; set; }
}

sealed class LocalLookupProbeRow
{
    public int Id { get; set; }
    public string NamePt { get; set; } = string.Empty;
    public string NameJp { get; set; } = string.Empty;
    public string ShortCode { get; set; } = string.Empty;
}

sealed class DuplicateMachineProbeRow
{
    public string MachineCode { get; set; } = string.Empty;
    public long Total { get; set; }
    public string Detail { get; set; } = string.Empty;
}

sealed record PresenceAuditProbeResult(
    IReadOnlyList<string> HaidaiOperators,
    IReadOnlyList<string> PresenceOperators,
    IReadOnlyList<string> HaidaiWithoutPresence,
    IReadOnlyList<string> PresenceWithoutHaidai,
    IReadOnlyList<string> RankingWithoutPresence,
    IReadOnlyList<string> RankingWithoutHaidai);

sealed class StatusEventProbeRow
{
    public int MachineId { get; set; }
    public string MachineCode { get; set; } = string.Empty;
    public string LineCode { get; set; } = string.Empty;
    public int? SectorId { get; set; }
    public string SectorName { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public DateTime EventDateTime { get; set; }
}

sealed class StatusReportAccumulator
{
    public int? SectorId { get; set; }
    public string SectorName { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Classification { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public bool UsedFallback { get; set; }
    public int Occurrences { get; set; }
    public double TotalMinutes { get; set; }
}

sealed class StatusReportRow
{
    public int? SectorId { get; set; }
    public string Sector { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Classification { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public int Occurrences { get; set; }
    public double TotalMinutes { get; set; }
    public bool CountsInEfficiencyDenominator { get; set; }
    public bool UsedFallback { get; set; }
    public double EstimatedEfficiencyImpact { get; set; }
    public string Warning { get; set; } = string.Empty;

    public string ToCsvLine()
    {
        return string.Join(
            ",",
            EscapeCsv(Sector),
            StatusCode.ToString(CultureInfo.InvariantCulture),
            EscapeCsv(Description),
            EscapeCsv(Classification),
            EscapeCsv(Source),
            Occurrences.ToString(CultureInfo.InvariantCulture),
            TotalMinutes.ToString("0.0", CultureInfo.InvariantCulture),
            CountsInEfficiencyDenominator ? "true" : "false",
            UsedFallback ? "true" : "false",
            EstimatedEfficiencyImpact.ToString("0.0", CultureInfo.InvariantCulture),
            EscapeCsv(Warning));
    }

    private static string EscapeCsv(string value)
    {
        var safe = value ?? string.Empty;
        return safe.Contains(',') || safe.Contains('"') || safe.Contains('\n') || safe.Contains('\r')
            ? $"\"{safe.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : safe;
    }
}

readonly record struct StatusDefinitionResolution(
    StatusDefinitionProbeRow? Definition,
    string Source,
    bool UsedFallback);

readonly record struct ProductionSchemaInspection(
    string JournalMode,
    string QuickCheck,
    List<string> Issues);
