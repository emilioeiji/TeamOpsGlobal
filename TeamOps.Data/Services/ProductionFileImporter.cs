using System;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Dapper;
using TeamOps.Core.Entities;
using TeamOps.Config;
using TeamOps.Data.Db;
using TeamOps.Data.Repositories;
using TeamOps.UI.Forms.Models;

namespace TeamOps.Services
{
    public sealed class ProductionFileImporter
    {
        private static readonly string[] FileSuffixes = { "211D", "2400" };
        private static readonly StringComparer TextComparer = StringComparer.OrdinalIgnoreCase;

        private readonly SqliteConnectionFactory _factory;
        private readonly ProductionMachineRepository _machineRepository;
        private readonly ProductionEventRepository _eventRepository;
        private readonly ProductionPlanDatImporter _planImporter;
        private readonly Ec2AdministratorImporter _ec2Importer;

        public ProductionFileImporter(
            SqliteConnectionFactory factory,
            ProductionMachineRepository machineRepository,
            ProductionEventRepository eventRepository)
        {
            _factory = factory;
            _machineRepository = machineRepository;
            _eventRepository = eventRepository;
            _planImporter = new ProductionPlanDatImporter();
            _ec2Importer = new Ec2AdministratorImporter(factory, machineRepository);
        }

        public ProductionImportResult ImportLatest(ProductionImportOptions? options = null)
        {
            var totalWatch = Stopwatch.StartNew();
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var settings = LoadSettings();
            var result = new ProductionImportResult();

            var batchWatch = Stopwatch.StartNew();
            ExecuteBatchIfConfigured(settings, result);
            Record(result, "Batch", batchWatch);

            var discoverWatch = Stopwatch.StartNew();
            var importDates = ResolveImportDates(options);
            var files = importDates
                .SelectMany(date => FileSuffixes.Select(suffix => Path.Combine(settings.EventsDirectory, $"{date:yyMMdd}_{suffix}_E.txt")))
                .Where(File.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
            Record(result, "DiscoverFiles", discoverWatch);

            var parsedRows = new List<ParsedLine>();
            var readElapsed = 0L;
            var parseElapsed = 0L;
            foreach (var filePath in files)
            {
                result.FilesRead++;

                var readWatch = Stopwatch.StartNew();
                var lines = ReadAllLines(filePath);
                readWatch.Stop();
                readElapsed += readWatch.ElapsedMilliseconds;

                var parseWatch = Stopwatch.StartNew();
                foreach (var line in lines)
                {
                    result.LinesRead++;
                    var isDadFile = IsDadEventFile(filePath);
                    if (isDadFile)
                    {
                        result.DadLinesRead++;
                    }

                    if (!TryParseLine(filePath, line, out var parsed, out var ignoreReason))
                    {
                        result.Ignored++;
                        if (isDadFile)
                        {
                            result.DadRowsIgnored++;
                            TrackDadIgnored(result, filePath, line, ignoreReason);
                        }
                        PushError(result, ignoreReason);
                        continue;
                    }

                    parsed.SourceFile = Path.GetFileName(filePath);
                    parsedRows.Add(parsed);
                }
                parseWatch.Stop();
                parseElapsed += parseWatch.ElapsedMilliseconds;
            }
            result.PerformanceMs["ReadFiles"] = readElapsed;
            result.PerformanceMs["Parse"] = parseElapsed;
            var dadParsedRows = parsedRows
                .Where(row => row.SectorId == 2)
                .ToList();
            result.DadRowsParsed = dadParsedRows.Count;
            result.DadMachinesFound = dadParsedRows
                .Select(row => row.MachineCode)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            if (options?.CleanExistingEvents == true)
            {
                if (options.CleanupFilter == null)
                {
                    throw new InvalidOperationException("Reimportacao com limpeza requer um filtro de recorte.");
                }

                if (parsedRows.Count == 0)
                {
                    throw new InvalidOperationException("Reimportacao com limpeza cancelada: nenhum registro valido foi encontrado para importar.");
                }

                var cleanupDate = options.CleanupFilter.Date.Date;
                var hasFileForCleanupDate = files.Any(path =>
                    Path.GetFileName(path).StartsWith(cleanupDate.ToString("yyMMdd", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase));
                if (!hasFileForCleanupDate)
                {
                    throw new InvalidOperationException($"Reimportacao cancelada: nenhum arquivo de eventos do dia {cleanupDate:yyyy-MM-dd} foi encontrado apos executar o BAT.");
                }
            }

            var openWatch = Stopwatch.StartNew();
            using var conn = _factory.CreateOpenConnection();
            Record(result, "OpenConnection", openWatch);

            var schemaWatch = Stopwatch.StartNew();
            ProductionSchemaMigrator.Ensure(conn);
            Record(result, "EnsureSchema", schemaWatch);

            var txWatch = Stopwatch.StartNew();
            using var tx = conn.BeginTransaction();
            Record(result, "BeginTransaction", txWatch);

            if (options?.CleanExistingEvents == true && options.CleanupFilter != null)
            {
                var cleanupWatch = Stopwatch.StartNew();
                CleanupExistingEvents(conn, tx, options.CleanupFilter, result);
                Record(result, "Cleanup", cleanupWatch);
            }

            var loadMachinesWatch = Stopwatch.StartNew();
            var machineCache = conn.Query<Machine>(
                    @"
                        SELECT
                            Id,
                            NamePt,
                            NameJp,
                            MachineCode,
                            MachineKey,
                            LineCode,
                            LocalId,
                            SectorId,
                            COALESCE(IsActive, 1) AS IsActive
                        FROM Machines;",
                    transaction: tx)
                .ToList()
                .Select(machine => new
                {
                    Key = string.IsNullOrWhiteSpace(machine.MachineKey)
                        ? ProductionMachineRepository.BuildMachineKey(machine.MachineCode ?? string.Empty, machine.LineCode ?? string.Empty)
                        : machine.MachineKey!,
                    Machine = machine
                })
                .Where(item => !string.IsNullOrWhiteSpace(item.Key) && item.Key != ":")
                .GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().Machine, StringComparer.OrdinalIgnoreCase);
            Record(result, "LoadMachines", loadMachinesWatch);

            var loadStatusesWatch = Stopwatch.StartNew();
            var knownStatusCodes = conn.Query<StatusKeyRow>(
                    "SELECT SectorId, StatusCode FROM MachineStatuses;",
                    transaction: tx)
                .ToList()
                .Select(row => BuildStatusKey(row.SectorId, row.StatusCode))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            Record(result, "LoadStatuses", loadStatusesWatch);

            var planWatch = Stopwatch.StartNew();
            _planImporter.ImportLatestPlanFiles(
                conn,
                tx,
                settings.EventsDirectory,
                settings.SourceDatDirectory,
                result);
            Record(result, "ImportPlans", planWatch);

            var resolveWatch = Stopwatch.StartNew();
            var machineEvents = new List<MachineEvent>(parsedRows.Count);
            var touchedMachineIds = new HashSet<int>();
            var importNow = DateTime.Now;
            foreach (var parsed in parsedRows)
            {
                try
                {
                    var machineKey = ProductionMachineRepository.BuildMachineKey(parsed.MachineCode, parsed.LineCode);
                    if (!machineCache.TryGetValue(machineKey, out var machine))
                    {
                        machine = _machineRepository.EnsureMachine(conn, parsed.MachineCode, parsed.LineCode, parsed.SectorId);
                        machineCache[machineKey] = machine;
                        result.MachinesCreated++;
                    }
                    else if (parsed.SectorId.HasValue && machine.SectorId != parsed.SectorId.Value)
                    {
                        machine = _machineRepository.EnsureMachine(conn, parsed.MachineCode, parsed.LineCode, parsed.SectorId);
                        machineCache[machineKey] = machine;
                    }
                    else if (!string.Equals(machine.MachineKey, machineKey, StringComparison.OrdinalIgnoreCase))
                    {
                        machine = _machineRepository.EnsureMachine(conn, parsed.MachineCode, parsed.LineCode, parsed.SectorId);
                        machineCache[machineKey] = machine;
                    }

                    var statusKey = BuildStatusKey(parsed.SectorId, parsed.StatusCode);
                    if (!knownStatusCodes.Contains(statusKey))
                    {
                        var createStatusWatch = Stopwatch.StartNew();
                        EnsureMachineStatus(conn, tx, parsed.SectorId, parsed.StatusCode, parsed.StatusText);
                        AddElapsed(result, "CreateStatuses", createStatusWatch);
                        knownStatusCodes.Add(statusKey);
                    }

                    machineEvents.Add(new MachineEvent
                    {
                        MachineId = machine.Id,
                        MachineCode = parsed.MachineCode,
                        LineCode = parsed.LineCode,
                        LocalId = machine.LocalId,
                        SectorId = parsed.SectorId ?? machine.SectorId,
                        RecipeName = parsed.RecipeName,
                        LotNo = parsed.LotNo,
                        StatusCode = parsed.StatusCode,
                        StatusText = parsed.StatusText,
                        InternalState = parsed.InternalState,
                        EventDateTime = parsed.EventDateTime,
                        SourceFile = parsed.SourceFile,
                        ImportedAt = importNow
                    });
                }
                catch (Exception ex)
                {
                    result.Ignored++;
                    if (parsed.SectorId == 2)
                    {
                        result.DadLinkErrors++;
                        AddDadDiagnostic(
                            result,
                            $"DAD_LINK_ERROR Machine={parsed.MachineCode} Line={parsed.LineCode} Status={parsed.StatusCode} Event={parsed.EventDateTime:yyyy-MM-dd HH:mm:ss} Reason={ex.Message}");
                    }
                    PushError(result, $"{parsed.SourceFile}: {ex.Message}");
                }
            }
            Record(result, "ResolveMachinesStatuses", resolveWatch);

            var duplicateWatch = Stopwatch.StartNew();
            var candidates = FilterDuplicateEvents(conn, tx, machineEvents, result);
            Record(result, "DuplicateCheck", duplicateWatch);

            var insertWatch = Stopwatch.StartNew();
            var affected = candidates.Count == 0
                ? 0
                : _eventRepository.InsertOrIgnoreMany(conn, tx, candidates);
            result.Imported += affected;
            if (affected < candidates.Count)
            {
                result.Ignored += candidates.Count - affected;
            }
            TrackDadImportedCandidates(result, candidates);
            foreach (var machineId in candidates.Select(item => item.MachineId).Distinct())
            {
                touchedMachineIds.Add(machineId);
            }
            Record(result, "InsertEvents", insertWatch);

            var refreshWatch = Stopwatch.StartNew();
            _eventRepository.RefreshCurrentStatuses(conn, tx, touchedMachineIds);
            Record(result, "RefreshCurrentStatus", refreshWatch);

            var commitWatch = Stopwatch.StartNew();
            tx.Commit();
            Record(result, "Commit", commitWatch);

            var ec2Watch = Stopwatch.StartNew();
            _ec2Importer.ImportIfConfigured(result);
            AddElapsed(result, "Ec2Import", ec2Watch);

            Record(result, "Total", totalWatch);
            return result;
        }

        private static IReadOnlyList<DateTime> ResolveImportDates(ProductionImportOptions? options)
        {
            if (options?.CleanupFilter != null)
            {
                return new[] { options.CleanupFilter.Date.Date };
            }

            return new[]
            {
                DateTime.Today.AddDays(-1).Date,
                DateTime.Today.Date
            };
        }

        private static List<MachineEvent> FilterDuplicateEvents(
            System.Data.IDbConnection conn,
            System.Data.IDbTransaction tx,
            IReadOnlyList<MachineEvent> machineEvents,
            ProductionImportResult result)
        {
            if (machineEvents.Count == 0)
            {
                return new List<MachineEvent>();
            }

            var machineIds = machineEvents.Select(item => item.MachineId).Distinct().ToArray();
            var rangeStart = machineEvents.Min(item => item.EventDateTime).ToString("yyyy-MM-dd HH:mm:ss");
            var rangeEnd = machineEvents.Max(item => item.EventDateTime).ToString("yyyy-MM-dd HH:mm:ss");

            var existingRows = conn.Query<ExistingEventKeyRow>(
                    @"
                        SELECT
                            MachineId,
                            StatusCode,
                            InternalState,
                            EventDateTime
                        FROM MachineEvents
                        WHERE MachineId IN @machineIds
                          AND EventDateTime >= @rangeStart
                          AND EventDateTime <= @rangeEnd;",
                    new
                    {
                        machineIds,
                        rangeStart,
                        rangeEnd
                    },
                    tx)
                .ToList();

            var eventKeys = existingRows
                .Select(row => BuildEventKey(row.MachineId, row.EventDateTime, row.StatusCode))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var rawKeys = existingRows
                .Select(row => BuildRawEventKey(row.MachineId, row.EventDateTime, row.InternalState))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var candidates = new List<MachineEvent>(machineEvents.Count);
            foreach (var machineEvent in machineEvents.OrderBy(item => item.MachineId).ThenBy(item => item.EventDateTime))
            {
                var eventDateTime = machineEvent.EventDateTime.ToString("yyyy-MM-dd HH:mm:ss");
                var eventKey = BuildEventKey(machineEvent.MachineId, eventDateTime, machineEvent.StatusCode);
                var rawKey = BuildRawEventKey(machineEvent.MachineId, eventDateTime, machineEvent.InternalState);

                if (!eventKeys.Add(eventKey) || !rawKeys.Add(rawKey))
                {
                    result.Ignored++;
                    continue;
                }

                candidates.Add(machineEvent);
            }

            return candidates;
        }

        private static string[] ReadAllLines(string filePath)
        {
            var raw = File.ReadAllBytes(filePath);

            foreach (var encoding in new[]
            {
                new UTF8Encoding(false, true),
                Encoding.GetEncoding(932, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback)
            })
            {
                try
                {
                    var text = encoding.GetString(raw);
                    if (text.Contains('|') && !ContainsBrokenEncoding(text))
                    {
                        return text
                            .Replace("\r\n", "\n", StringComparison.Ordinal)
                            .Replace('\r', '\n')
                            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    }
                }
                catch (DecoderFallbackException)
                {
                    // Try the next expected production file encoding.
                }
            }

            return Array.Empty<string>();
        }

        private static bool ContainsBrokenEncoding(string text)
        {
            return text.Contains('\uFFFD', StringComparison.Ordinal)
                || text.Contains("ã", StringComparison.Ordinal)
                || text.Contains("å", StringComparison.Ordinal)
                || text.Contains("ç", StringComparison.Ordinal)
                || text.Contains("æ", StringComparison.Ordinal);
        }

        private static bool TryParseLine(
            string filePath,
            string line,
            out ParsedLine parsed,
            out string ignoreReason)
        {
            parsed = default;
            ignoreReason = string.Empty;

            if (string.IsNullOrWhiteSpace(line))
            {
                ignoreReason = $"{Path.GetFileName(filePath)}: linha vazia.";
                return false;
            }

            var parts = line.Split('|');
            if (parts.Length < 10)
            {
                ignoreReason = $"{Path.GetFileName(filePath)}: linha invalida.";
                return false;
            }

            var lineCode = NormalizeCodeForImport(Safe(parts, 3));
            var machineCode = NormalizeCodeForImport(Safe(parts, 4));
            var internalState = Safe(parts, 5);
            var eventDate = Safe(parts, 7);
            var eventTime = Safe(parts, 8);
            var statusText = Safe(parts, 9);
            var recipeName = Safe(parts, 10);
            var numericStatusCode = Safe(parts, 11);
            var lotNo = Safe(parts, 12);

            if (string.IsNullOrWhiteSpace(lineCode) || string.IsNullOrWhiteSpace(machineCode))
            {
                ignoreReason = $"{Path.GetFileName(filePath)}: codigo de linha ou maquina vazio.";
                return false;
            }

            if (!ProductionMachineRepository.IsValidProductionMachineCode(machineCode))
            {
                ignoreReason = $"{Path.GetFileName(filePath)}: codigo de maquina invalido ({machineCode}).";
                return false;
            }

            if (!DateTime.TryParseExact(
                    $"{eventDate} {eventTime}",
                    "yyyy/MM/dd HH:mm:ss",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var eventDateTime))
            {
                ignoreReason = $"{Path.GetFileName(filePath)}: data/hora invalida para {machineCode}.";
                return false;
            }

            parsed = new ParsedLine
            {
                LineCode = lineCode,
                MachineCode = machineCode,
                SectorId = ResolveSectorId(lineCode),
                InternalState = internalState,
                StatusCode = ParseRawStatusCode(numericStatusCode, internalState, statusText),
                StatusText = statusText,
                RecipeName = recipeName,
                LotNo = lotNo,
                EventDateTime = eventDateTime
            };

            return true;
        }

        private static int ParseRawStatusCode(string numericStatusCode, string internalState, string statusText)
        {
            if (int.TryParse(internalState, out var parsedInternal))
            {
                return parsedInternal;
            }

            if (int.TryParse(numericStatusCode, out var parsedNumeric))
            {
                return parsedNumeric;
            }

            var normalized = (statusText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return 1;
            }

            if (normalized.Contains("\u7A3C\u50CD", StringComparison.Ordinal)
                || normalized.Contains("\u904B\u8EE2", StringComparison.Ordinal)
                || normalized.Contains("RUN", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (normalized.Contains("\u505C\u6B62", StringComparison.Ordinal)
                || normalized.Contains("STOP", StringComparison.OrdinalIgnoreCase))
            {
                return 3;
            }

            if (normalized.Contains("\u7570\u5E38", StringComparison.Ordinal)
                || normalized.Contains("\u30A8\u30E9\u30FC", StringComparison.Ordinal)
                || normalized.Contains("ERROR", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("ALARM", StringComparison.OrdinalIgnoreCase))
            {
                return 4;
            }

            if (normalized.Contains("\u30B5\u30F3\u30D7\u30EB", StringComparison.Ordinal)
                || normalized.Contains("\u30EC\u30B9\u51E6\u7406", StringComparison.Ordinal)
                || normalized.Contains("\u5438\u5F15\u6642\u9593", StringComparison.Ordinal))
            {
                return 1;
            }

            return 1;
        }
        private static int? ResolveSectorId(string lineCode)
        {
            if (TextComparer.Equals(lineCode, "211D"))
            {
                return 2;
            }

            if (TextComparer.Equals(lineCode, "2400"))
            {
                return 1;
            }

            return null;
        }

        private static string Safe(string[] parts, int index)
        {
            return index >= 0 && index < parts.Length
                ? (parts[index] ?? string.Empty).Trim()
                : string.Empty;
        }

        private static string NormalizeCodeForImport(string value)
        {
            return new string((value ?? string.Empty)
                    .Where(character => !char.IsWhiteSpace(character)
                        && character != '\u200B'
                        && character != '\u200C'
                        && character != '\u200D'
                        && character != '\uFEFF')
                    .ToArray())
                .ToUpperInvariant();
        }

        private static bool IsDadEventFile(string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            return fileName.Contains("_211D_", StringComparison.OrdinalIgnoreCase);
        }

        private static void TrackDadIgnored(ProductionImportResult result, string filePath, string line, string reason)
        {
            var parts = line.Split('|');
            var machineCode = parts.Length > 4
                ? NormalizeCodeForImport(parts[4])
                : string.Empty;

            if (string.IsNullOrWhiteSpace(machineCode))
            {
                machineCode = "(unknown)";
            }

            Increment(result.DadIgnoredByMachine, machineCode);
            AddDadDiagnostic(result, $"DAD_IGNORED File={Path.GetFileName(filePath)} Machine={machineCode} Reason={reason}");
        }

        private static void TrackDadImportedCandidates(ProductionImportResult result, IReadOnlyList<MachineEvent> candidates)
        {
            var dadCandidates = candidates
                .Where(item => item.SectorId == 2)
                .ToList();

            result.DadRowsImported = dadCandidates.Count;
            result.DadMachinesImported = dadCandidates
                .Select(item => item.MachineCode)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            foreach (var machineEvent in dadCandidates)
            {
                Increment(result.DadEventsByMachine, machineEvent.MachineCode);
                var displayCode = ResolveDisplayCode(machineEvent.StatusCode, machineEvent.StatusText);
                if (displayCode == 0)
                {
                    Increment(result.DadRunningEventsByMachine, machineEvent.MachineCode);
                }

                AddDadDiagnostic(
                    result,
                    $"DAD_IMPORTED Machine={machineEvent.MachineCode} Line={machineEvent.LineCode} Status={machineEvent.StatusCode} Display={displayCode} Event={machineEvent.EventDateTime:yyyy-MM-dd HH:mm:ss} Recipe={machineEvent.RecipeName} Lot={machineEvent.LotNo}");
            }

            result.DadMachinesWithRunningEvents = result.DadRunningEventsByMachine.Count;
            result.DadMachinesWithZeroRunningEvents = result.DadEventsByMachine.Keys
                .Count(machineCode => !result.DadRunningEventsByMachine.ContainsKey(machineCode));
        }

        private static void Increment(IDictionary<string, int> values, string key)
        {
            if (values.TryGetValue(key, out var current))
            {
                values[key] = current + 1;
                return;
            }

            values[key] = 1;
        }

        private static void AddDadDiagnostic(ProductionImportResult result, string message)
        {
            if (result.DadDiagnostics.Count < 80)
            {
                result.DadDiagnostics.Add(message);
            }
        }

        private static void PushError(ProductionImportResult result, string message)
        {
            if (!string.IsNullOrWhiteSpace(message) && result.Errors.Count < 30)
            {
                result.Errors.Add(message);
            }
        }

        private static string BuildStatusKey(int? sectorId, int statusCode)
        {
            return $"{sectorId.GetValueOrDefault()}:{statusCode}";
        }

        private static void EnsureMachineStatus(System.Data.IDbConnection conn, System.Data.IDbTransaction tx, int? sectorId, int statusCode, string statusText)
        {
            var existing = conn.ExecuteScalar<int>(
                @"SELECT COUNT(1)
                  FROM MachineStatuses
                  WHERE COALESCE(SectorId, 0) = @sectorKey
                    AND StatusCode = @statusCode;",
                new
                {
                    sectorKey = sectorId.GetValueOrDefault(),
                    statusCode
                },
                tx);

            if (existing > 0)
            {
                return;
            }

            var displayCode = ResolveDisplayCode(statusCode, statusText);

            var (namePt, colorHex, textColorHex) = displayCode switch
            {
                0 => ("Rodando", "#5B88E8", "#FFFFFF"),
                3 => ("Parado", "#F2CB58", "#4A3200"),
                4 => ("Erro", "#FFFFFF", "#516174"),
                _ => ("Inativo", "#EF6F63", "#FFFFFF")
            };

            conn.Execute(
                @"
                    INSERT OR IGNORE INTO MachineStatuses
                    (
                        StatusCode,
                        DisplayCode,
                        SectorId,
                        Classification,
                        NamePt,
                        NameJp,
                        ColorHex,
                        TextColorHex,
                        SortOrder,
                        IsActive
                    )
                    VALUES
                    (
                        @statusCode,
                        @displayCode,
                        @sectorId,
                        @classification,
                        @namePt,
                        @nameJp,
                        @colorHex,
                        @textColorHex,
                        @sortOrder,
                        1
                    );",
                new
                {
                    statusCode,
                    displayCode,
                    sectorId,
                    classification = displayCode switch
                    {
                        0 => "Running",
                        4 => "Error",
                        _ => "StopCounts"
                    },
                    namePt,
                    nameJp = string.IsNullOrWhiteSpace(statusText) ? namePt : statusText.Trim(),
                    colorHex,
                    textColorHex,
                    sortOrder = statusCode
                },
                tx);
        }

        private static void Record(ProductionImportResult result, string name, Stopwatch stopwatch)
        {
            stopwatch.Stop();
            result.PerformanceMs[name] = stopwatch.ElapsedMilliseconds;
        }

        private static void AddElapsed(ProductionImportResult result, string name, Stopwatch stopwatch)
        {
            stopwatch.Stop();
            result.PerformanceMs[name] = result.PerformanceMs.TryGetValue(name, out var current)
                ? current + stopwatch.ElapsedMilliseconds
                : stopwatch.ElapsedMilliseconds;
        }

        private static string BuildEventKey(int machineId, string eventDateTime, int statusCode)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{machineId}|{eventDateTime}|{statusCode}");
        }

        private static string BuildRawEventKey(int machineId, string eventDateTime, string internalState)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{machineId}|{eventDateTime}|{internalState ?? string.Empty}");
        }

        private static int ResolveDisplayCode(int statusCode, string statusText)
        {
            if (statusCode == 0)
            {
                return 0;
            }

            if (statusCode == 3)
            {
                return 3;
            }

            if (statusCode == 4)
            {
                return 4;
            }

            var normalized = (statusText ?? string.Empty).Trim();
            if (normalized.Contains("稼働", StringComparison.Ordinal)
                || normalized.Contains("運転", StringComparison.Ordinal))
            {
                return 0;
            }

            if (normalized.Contains("停止", StringComparison.Ordinal))
            {
                return 3;
            }

            if (normalized.Contains("異常", StringComparison.Ordinal)
                || normalized.Contains("エラー", StringComparison.Ordinal))
            {
                return 4;
            }

            if (normalized.Contains("遞ｼ蜒堺ｸｭ", StringComparison.Ordinal)
                || normalized.Contains("驕玖ｻ｢", StringComparison.Ordinal)
                || normalized.Contains("RUN", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (normalized.Contains("蛛懈ｭ｢", StringComparison.Ordinal)
                || normalized.Contains("STOP", StringComparison.OrdinalIgnoreCase))
            {
                return 3;
            }

            if (normalized.Contains("繝医Λ繝悶Ν", StringComparison.Ordinal)
                || normalized.Contains("繧ｨ繝ｩ繝ｼ", StringComparison.Ordinal)
                || normalized.Contains("ERROR", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("ALARM", StringComparison.OrdinalIgnoreCase))
            {
                return 4;
            }

            return 1;
        }

        private static ProductionImportSettings LoadSettings()
        {
            var eventsDirectory = StoragePathResolver.GetRequiredPath("ProductionEventsDirectory");
            var batchPath = StoragePathResolver.GetOptionalPath("ProductionImportBatchPath") ?? string.Empty;
            var completionFile = StoragePathResolver.GetOptionalPath("ProductionImportCompletionFile") ?? string.Empty;
            var sourceEventsDirectory = StoragePathResolver.GetOptionalPath("ProductionSourceEventsDirectory") ?? string.Empty;
            var sourceDatDirectory = StoragePathResolver.GetOptionalPath("ProductionSourceDatDirectory") ?? string.Empty;
            var timeoutSecondsText = ConfigurationManager.AppSettings["ProductionImportTimeoutSeconds"] ?? "180";

            Directory.CreateDirectory(eventsDirectory);

            if (!int.TryParse(timeoutSecondsText, out var timeoutSeconds) || timeoutSeconds <= 0)
            {
                timeoutSeconds = 180;
            }

            return new ProductionImportSettings(
                eventsDirectory,
                batchPath,
                completionFile,
                sourceEventsDirectory,
                sourceDatDirectory,
                timeoutSeconds);
        }

        private static void ExecuteBatchIfConfigured(ProductionImportSettings settings, ProductionImportResult result)
        {
            if (string.IsNullOrWhiteSpace(settings.BatchPath))
            {
                return;
            }

            if (!File.Exists(settings.BatchPath))
            {
                throw new FileNotFoundException($"Arquivo BAT nao encontrado: {settings.BatchPath}");
            }

            if (!string.IsNullOrWhiteSpace(settings.CompletionFile))
            {
                var completionDir = Path.GetDirectoryName(settings.CompletionFile);
                if (!string.IsNullOrWhiteSpace(completionDir))
                {
                    Directory.CreateDirectory(completionDir);
                }

                if (File.Exists(settings.CompletionFile))
                {
                    File.Delete(settings.CompletionFile);
                }
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{settings.BatchPath}\"",
                WorkingDirectory = Path.GetDirectoryName(settings.BatchPath) ?? AppContext.BaseDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            startInfo.Environment["TEAMOPS_PRODUCTION_EVENTS_DIR"] = settings.EventsDirectory;
            startInfo.Environment["TEAMOPS_PRODUCTION_SOURCE_EVENTS_DIR"] = settings.SourceEventsDirectory;
            startInfo.Environment["TEAMOPS_PRODUCTION_SOURCE_DAT_DIR"] = settings.SourceDatDirectory;
            startInfo.Environment["TEAMOPS_PRODUCTION_COMPLETION_FILE"] = settings.CompletionFile;

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Nao foi possivel iniciar o BAT de importacao.");

            if (!process.WaitForExit(settings.TimeoutSeconds * 1000))
            {
                try
                {
                    process.Kill(true);
                }
                catch
                {
                    // Ignora falha ao encerrar processo.
                }

                throw new TimeoutException("O BAT de importacao excedeu o tempo limite configurado.");
            }

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"O BAT de importacao retornou codigo {process.ExitCode}.");
            }

            if (!string.IsNullOrWhiteSpace(settings.CompletionFile))
            {
                var deadline = DateTime.UtcNow.AddSeconds(settings.TimeoutSeconds);
                while (DateTime.UtcNow <= deadline)
                {
                    if (File.Exists(settings.CompletionFile))
                    {
                        result.BatchExecuted = true;
                        result.BatchMessage = File.ReadAllText(settings.CompletionFile).Trim();
                        return;
                    }

                    Thread.Sleep(500);
                }

                throw new TimeoutException("O BAT terminou, mas o arquivo de retorno nao foi encontrado.");
            }

            result.BatchExecuted = true;
            result.BatchMessage = "BAT executado com sucesso.";
        }

        private static void CleanupExistingEvents(
            System.Data.IDbConnection conn,
            System.Data.IDbTransaction tx,
            ProductionDashboardFilter filter,
            ProductionImportResult result)
        {
            var start = filter.Date.Date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            var end = filter.Date.Date.AddDays(1).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

            var conditions = new StringBuilder("WHERE EventDateTime >= @start AND EventDateTime < @end");
            var parameters = new DynamicParameters();
            parameters.Add("@start", start);
            parameters.Add("@end", end);

            if (filter.SectorId > 0)
            {
                conditions.Append(" AND SectorId = @sectorId");
                parameters.Add("@sectorId", filter.SectorId);
            }

            if (filter.LocalId > 0)
            {
                conditions.Append(" AND LocalId = @localId");
                parameters.Add("@localId", filter.LocalId);
            }

            if (filter.MachineId > 0)
            {
                conditions.Append(" AND MachineId = @machineId");
                parameters.Add("@machineId", filter.MachineId);
            }

            var machineIds = conn.Query<int>(
                    $@"SELECT DISTINCT MachineId FROM MachineEvents {conditions};",
                    parameters,
                    tx)
                .Distinct()
                .ToList();

            var deletedEvents = conn.Execute(
                $@"DELETE FROM MachineEvents {conditions};",
                parameters,
                tx);

            var deletedCurrent = machineIds.Count == 0
                ? 0
                : conn.Execute(
                    "DELETE FROM MachineCurrentStatus WHERE MachineId IN @machineIds;",
                    new { machineIds },
                    tx);

            result.CleanupPerformed = true;
            result.CleanupEventsDeleted = deletedEvents;
            result.CleanupCurrentStatusesDeleted = deletedCurrent;
            result.CleanupMessage = $"Limpeza executada: {deletedEvents} evento(s) e {deletedCurrent} status atual(is) removidos.";
        }

        private readonly record struct ProductionImportSettings(
            string EventsDirectory,
            string BatchPath,
            string CompletionFile,
            string SourceEventsDirectory,
            string SourceDatDirectory,
            int TimeoutSeconds);

        public sealed class ProductionImportOptions
        {
            public bool CleanExistingEvents { get; init; }
            public ProductionDashboardFilter? CleanupFilter { get; init; }
        }

        private struct ParsedLine
        {
            public string LineCode { get; set; }
            public string MachineCode { get; set; }
            public int? SectorId { get; set; }
            public string InternalState { get; set; }
            public int StatusCode { get; set; }
            public string StatusText { get; set; }
            public string RecipeName { get; set; }
            public string LotNo { get; set; }
            public DateTime EventDateTime { get; set; }
            public string SourceFile { get; set; }
        }

        private sealed class StatusKeyRow
        {
            public int? SectorId { get; set; }
            public int StatusCode { get; set; }
        }

        private sealed class ExistingEventKeyRow
        {
            public int MachineId { get; set; }
            public int StatusCode { get; set; }
            public string InternalState { get; set; } = string.Empty;
            public string EventDateTime { get; set; } = string.Empty;
        }
    }
}
