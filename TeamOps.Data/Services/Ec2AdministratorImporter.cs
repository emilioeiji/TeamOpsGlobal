using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using TeamOps.Config;
using TeamOps.Core.Entities;
using TeamOps.Data.Db;
using TeamOps.Data.Repositories;

namespace TeamOps.Services
{
    internal sealed class Ec2AdministratorImporter
    {
        private static readonly StringComparer TextComparer = StringComparer.OrdinalIgnoreCase;

        private readonly SqliteConnectionFactory _factory;
        private readonly ProductionMachineRepository _machineRepository;
        private readonly ProductionSectorIds _sectorIds;

        public Ec2AdministratorImporter(
            SqliteConnectionFactory factory,
            ProductionMachineRepository machineRepository,
            ProductionSectorIds sectorIds)
        {
            _factory = factory;
            _machineRepository = machineRepository;
            _sectorIds = sectorIds;
        }

        public void ImportIfConfigured(ProductionImportResult result)
        {
            var settings = LoadSettings();
            result.Ec2FilePath = settings.FilePath;
            result.Ec2ResolvedFullPath = ResolveFullPath(settings.FilePath);
            result.Ec2FileExists = !string.IsNullOrWhiteSpace(settings.FilePath) && File.Exists(settings.FilePath);

            if (string.IsNullOrWhiteSpace(settings.FilePath))
            {
                result.Ec2ImportSkipped = true;
                result.Ec2ImportMessage = "EC2 Administrator nao configurado.";
                LogFileDiagnostics(result);
                return;
            }

            result.Ec2ImportAttempted = true;

            if (!File.Exists(settings.FilePath))
            {
                result.Ec2ImportSkipped = true;
                result.Ec2ImportMessage = $"Arquivo EC2 Administrator nao encontrado: {settings.FilePath}";
                LogFileDiagnostics(result);
                return;
            }

            try
            {
                ImportFile(settings, result);
            }
            catch (IOException ex)
            {
                result.Ec2ImportSkipped = true;
                result.Ec2ImportMessage = $"EC2 Administrator nao importado: arquivo indisponivel ({ex.Message}).";
                PushError(result, result.Ec2ImportMessage);
            }
            catch (UnauthorizedAccessException ex)
            {
                result.Ec2ImportSkipped = true;
                result.Ec2ImportMessage = $"EC2 Administrator nao importado: acesso negado ({ex.Message}).";
                PushError(result, result.Ec2ImportMessage);
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 5 || ex.SqliteErrorCode == 6)
            {
                result.Ec2ImportSkipped = true;
                result.Ec2ImportMessage = $"EC2 Administrator nao importado: SQLite ocupado ({ex.Message}).";
                PushError(result, result.Ec2ImportMessage);
            }
            catch (Exception ex)
            {
                result.Ec2ImportSkipped = true;
                result.Ec2ImportMessage = $"EC2 Administrator nao importado: {ex.Message}";
                PushError(result, result.Ec2ImportMessage);
            }
        }

        private void ImportFile(Ec2ImportSettings settings, ProductionImportResult result)
        {
            var totalWatch = Stopwatch.StartNew();
            var fileInfo = new FileInfo(settings.FilePath);
            result.Ec2FilePath = settings.FilePath;
            result.Ec2ResolvedFullPath = fileInfo.FullName;
            result.Ec2FileExists = fileInfo.Exists;
            result.Ec2FileSize = fileInfo.Exists ? fileInfo.Length : 0;
            result.Ec2FileLastWriteTime = fileInfo.Exists
                ? fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                : string.Empty;

            var readWatch = Stopwatch.StartNew();
            var bytes = File.ReadAllBytes(settings.FilePath);
            var fileHash = Convert.ToHexString(SHA256.HashData(bytes));
            var decoded = DecodeLines(bytes);
            var lines = decoded.Lines;
            result.Ec2EncodingDetected = decoded.EncodingName;
            result.Ec2DelimiterDetected = decoded.DelimiterLabel;
            result.Ec2RawLinePreview = PreviewRawLine(bytes);
            result.Ec2DecodedLinePreview = PreviewLine(FindFirstDataLine(lines) ?? lines.FirstOrDefault());
            result.Ec2ContainsReplacementChar = decoded.ContainsReplacementChar;
            result.Ec2FirstLinePreview = PreviewLine(lines.FirstOrDefault());
            result.Ec2HeaderLinePreview = PreviewLine(FindHeaderLine(lines));
            result.Ec2FirstDataLinePreview = PreviewLine(FindFirstDataLine(lines));
            result.Ec2RowsRead = lines.Count;
            LogFileDiagnostics(result);
            Record(result, "Ec2ReadFile", readWatch);

            if (LooksLikeProductionEventFile(fileInfo, lines))
            {
                result.Ec2ImportSkipped = true;
                result.Ec2ImportMessage = "Arquivo ignorado: parece ser arquivo de eventos de producao, nao EC2 Administrator.";
                PushError(result, $"EC2 Administrator nao importado: {fileInfo.FullName} parece arquivo de eventos.");
                return;
            }

            if (lines.Count == 0)
            {
                result.Ec2ImportSkipped = true;
                result.Ec2ImportMessage = "Arquivo EC2 Administrator vazio.";
                return;
            }

            var parseWatch = Stopwatch.StartNew();
            var parsedRows = LooksLikeAdministratorDatSample(lines)
                ? ParseAdministratorDatSampleRows(lines, fileInfo.LastWriteTime, settings, result)
                : ParseRows(lines, fileInfo.LastWriteTime, settings, result);
            Record(result, "Ec2Parse", parseWatch);
            result.Ec2AreaCount = parsedRows
                .Select(row => row.AreaLabel)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            using var conn = _factory.CreateOpenConnection();

            var existingImport = conn.QueryFirstOrDefault<Ec2ExistingImportRow>(
                @"
                    SELECT
                        Id,
                        RowsImported
                    FROM Ec2AdministratorImports
                    WHERE FileHash = @fileHash
                      AND COALESCE(SourceType, 'Administrator') = 'Administrator'
                    ORDER BY Id DESC
                    LIMIT 1;",
                new
                {
                    fileHash
                });

            var existingImportHasBrokenText = existingImport != null && ExistingImportHasBrokenText(conn, existingImport.Id);

            if (existingImport != null && existingImport.RowsImported > 0 && !existingImportHasBrokenText)
            {
                ReconcileCurrentStateForImport(conn, null, existingImport.Id, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                result.Ec2ImportSkipped = true;
                result.Ec2ImportMessage = "EC2 Administrator sem alteracao.";
                return;
            }

            if (existingImport != null && existingImport.RowsImported <= 0 && parsedRows.Count == 0)
            {
                result.Ec2ImportSkipped = true;
                result.Ec2ImportMessage = "EC2 Administrator sem linhas validas; importacao anterior do mesmo arquivo tambem estava zerada.";
                return;
            }

            var dbWatch = Stopwatch.StartNew();
            using var tx = conn.BeginTransaction();

            if (existingImport != null && (existingImport.RowsImported <= 0 || existingImportHasBrokenText) && parsedRows.Count > 0)
            {
                conn.Execute("DELETE FROM Ec2MachineSnapshots WHERE ImportId = @importId;", new { importId = existingImport.Id }, tx);
                conn.Execute("DELETE FROM Ec2AdministratorImports WHERE Id = @importId;", new { importId = existingImport.Id }, tx);
                Console.WriteLine($"[EC2 Administrator] Reprocessando hash ja registrado. PreviousImportId={existingImport.Id} RowsImported={existingImport.RowsImported} BrokenText={existingImportHasBrokenText}");
            }

            var importId = conn.ExecuteScalar<long>(
                @"
                    INSERT INTO Ec2AdministratorImports
                    (
                        SourceType,
                        SourceFile,
                        FileLastWriteTime,
                        FileLength,
                        FileHash,
                        ImportedAt,
                        RowsRead,
                        RowsImported,
                        RowsIgnored
                    )
                    VALUES
                    (
                        'Administrator',
                        @sourceFile,
                        @fileLastWriteTime,
                        @fileLength,
                        @fileHash,
                        @importedAt,
                        @rowsRead,
                        0,
                        @rowsIgnored
                    );
                    SELECT last_insert_rowid();",
                new
                {
                    sourceFile = settings.FilePath,
                    fileLastWriteTime = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    fileLength = fileInfo.Length,
                    fileHash,
                    importedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    rowsRead = result.Ec2RowsRead,
                    rowsIgnored = result.Ec2RowsIgnored
                },
                tx);

            var existingStates = conn.Query<Ec2CurrentStateRow>(
                    @"
                        SELECT
                            MachineId,
                            MachineCode,
                            IsRunning,
                            StoppedSinceAt,
                            SnapshotAt
                        FROM Ec2MachineCurrentState;",
                    transaction: tx)
                .Where(row => row.MachineId > 0)
                .GroupBy(row => row.MachineId)
                .ToDictionary(group => group.Key, group => group.First());

            var snapshots = new List<Ec2WriteRow>();
            var currentStates = new List<Ec2WriteRow>();
            var snapshotAt = fileInfo.LastWriteTime;

            foreach (var parsed in parsedRows)
            {
                var lineCode = ResolveLineCode(parsed.AreaLabel, parsed.SectorId);
                var machine = _machineRepository.EnsureMachine(conn, parsed.MachineCode, lineCode, parsed.SectorId, parsed.LocalId);

                var machineCode = NormalizeCode(parsed.MachineCode);
                existingStates.TryGetValue(machine.Id, out var previousState);
                var stoppedSince = ResolveStoppedSince(parsed, previousState, snapshotAt);
                var isIgnored = ShouldIgnore(parsed, stoppedSince, snapshotAt, settings.StoppedIgnoreAfterMinutes);
                var ignoreReason = isIgnored
                    ? $"EC2_STOPPED_{settings.StoppedIgnoreAfterMinutes}_MIN"
                    : string.Empty;
                var rawJson = JsonSerializer.Serialize(parsed.RawColumns);

                var row = new Ec2WriteRow
                {
                    ImportId = importId,
                    MachineId = machine.Id,
                    SectorId = parsed.SectorId ?? machine.SectorId,
                    LocalId = parsed.LocalId ?? machine.LocalId,
                    AreaLabel = parsed.AreaLabel,
                    MachineCode = machineCode,
                    MachineName = parsed.MachineName,
                    StatusText = parsed.StatusText,
                    IsRunning = parsed.IsRunning ? 1 : 0,
                    IsIgnored = isIgnored ? 1 : 0,
                    IgnoreReason = ignoreReason,
                    PartCode = parsed.PartCode,
                    PlannedProcessMinutes = parsed.PlannedProcessMinutes,
                    CapabilityType = parsed.CapabilityType,
                    OperationRate = parsed.OperationRate,
                    CurrentDifference = parsed.CurrentDifference,
                    LotNo = parsed.LotNo,
                    PlannedEndAt = parsed.PlannedEndAt?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    ProcessMinutes = parsed.ProcessMinutes,
                    DailyProduction = parsed.DailyProduction,
                    RawColumnsJson = rawJson,
                    SnapshotAt = snapshotAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    ImportedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    StoppedSinceAt = stoppedSince?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                };

                snapshots.Add(row);
                currentStates.Add(row);

                PushImportedSample(
                    result,
                    $"Machine={row.MachineCode} RawStatus={parsed.StatusText} DecodedStatus={row.StatusText} PartCode={row.PartCode} LotNo={(row.LotNo ?? string.Empty)} LotFromAdministratorIgnored=true ProcessMinutes={FormatNullable(row.ProcessMinutes)} EncodingUsed={result.Ec2EncodingDetected} Imported=yes IgnoreReason={row.IgnoreReason}");
            }

            if (snapshots.Count > 0)
            {
                conn.Execute(
                    @"
                        INSERT INTO Ec2MachineSnapshots
                        (
                            ImportId,
                            MachineId,
                            SectorId,
                            LocalId,
                            AreaLabel,
                            MachineCode,
                            MachineName,
                            StatusText,
                            IsRunning,
                            IsIgnored,
                            IgnoreReason,
                            PartCode,
                            PlannedProcessMinutes,
                            CapabilityType,
                            OperationRate,
                            CurrentDifference,
                            LotNo,
                            PlannedEndAt,
                            ProcessMinutes,
                            DailyProduction,
                            RawColumnsJson,
                            SnapshotAt,
                            ImportedAt
                        )
                        VALUES
                        (
                            @ImportId,
                            @MachineId,
                            @SectorId,
                            @LocalId,
                            @AreaLabel,
                            @MachineCode,
                            @MachineName,
                            @StatusText,
                            @IsRunning,
                            @IsIgnored,
                            @IgnoreReason,
                            @PartCode,
                            @PlannedProcessMinutes,
                            @CapabilityType,
                            @OperationRate,
                            @CurrentDifference,
                            @LotNo,
                            @PlannedEndAt,
                            @ProcessMinutes,
                            @DailyProduction,
                            @RawColumnsJson,
                            @SnapshotAt,
                            @ImportedAt
                        );",
                    snapshots,
                    tx);

                conn.Execute(
                    @"
                        INSERT INTO Ec2MachineCurrentState
                        (
                            ImportId,
                            SourceType,
                            IsStale,
                            MachineId,
                            MachineCode,
                            SectorId,
                            LocalId,
                            AreaLabel,
                            MachineName,
                            StatusText,
                            IsRunning,
                            IsIgnored,
                            IgnoreReason,
                            PartCode,
                            PlannedProcessMinutes,
                            CapabilityType,
                            OperationRate,
                            CurrentDifference,
                            LotNo,
                            PlannedEndAt,
                            ProcessMinutes,
                            DailyProduction,
                            RawColumnsJson,
                            SnapshotAt,
                            ImportedAt,
                            LastSeenAt,
                            StoppedSinceAt
                        )
                        VALUES
                        (
                            @ImportId,
                            @SourceType,
                            @IsStale,
                            @MachineId,
                            @MachineCode,
                            @SectorId,
                            @LocalId,
                            @AreaLabel,
                            @MachineName,
                            @StatusText,
                            @IsRunning,
                            @IsIgnored,
                            @IgnoreReason,
                            @PartCode,
                            @PlannedProcessMinutes,
                            @CapabilityType,
                            @OperationRate,
                            @CurrentDifference,
                            @LotNo,
                            @PlannedEndAt,
                            @ProcessMinutes,
                            @DailyProduction,
                            @RawColumnsJson,
                            @SnapshotAt,
                            @ImportedAt,
                            @SnapshotAt,
                            @StoppedSinceAt
                        )
                        ON CONFLICT(MachineId) DO UPDATE SET
                            ImportId = excluded.ImportId,
                            SourceType = excluded.SourceType,
                            IsStale = excluded.IsStale,
                            MachineCode = excluded.MachineCode,
                            SectorId = excluded.SectorId,
                            LocalId = excluded.LocalId,
                            AreaLabel = excluded.AreaLabel,
                            MachineName = excluded.MachineName,
                            StatusText = excluded.StatusText,
                            IsRunning = excluded.IsRunning,
                            IsIgnored = excluded.IsIgnored,
                            IgnoreReason = excluded.IgnoreReason,
                            PartCode = excluded.PartCode,
                            PlannedProcessMinutes = excluded.PlannedProcessMinutes,
                            CapabilityType = excluded.CapabilityType,
                            OperationRate = excluded.OperationRate,
                            CurrentDifference = excluded.CurrentDifference,
                            LotNo = excluded.LotNo,
                            PlannedEndAt = excluded.PlannedEndAt,
                            ProcessMinutes = excluded.ProcessMinutes,
                            DailyProduction = excluded.DailyProduction,
                            RawColumnsJson = excluded.RawColumnsJson,
                            SnapshotAt = excluded.SnapshotAt,
                            ImportedAt = excluded.ImportedAt,
                            LastSeenAt = excluded.LastSeenAt,
                            StoppedSinceAt = excluded.StoppedSinceAt;",
                    currentStates,
                    tx);

                MarkMachinesMissingFromLatestSnapshot(conn, tx, importId, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            }

            conn.Execute(
                @"
                    UPDATE Ec2AdministratorImports
                    SET
                        RowsImported = @rowsImported,
                        RowsIgnored = @rowsIgnored
                    WHERE Id = @importId;",
                new
                {
                    importId,
                    rowsImported = currentStates.Count,
                    rowsIgnored = result.Ec2RowsIgnored
                },
                tx);

            tx.Commit();
            result.Ec2RowsImported = currentStates.Count;
            result.Ec2RunningCount = currentStates.Count(item => item.IsRunning == 1);
            result.Ec2IgnoredCount = currentStates.Count(item => item.IsIgnored == 1);
            result.Ec2StoppedCount = Math.Max(0, currentStates.Count - result.Ec2RunningCount);
            result.Ec2ImportMessage = $"EC2 Administrator: {currentStates.Count} linha(s) importada(s).";
            Record(result, "Ec2WriteDb", dbWatch);
            Record(result, "Ec2Total", totalWatch);
        }

        private static DateTime? ResolveStoppedSince(
            Ec2ParsedRow row,
            Ec2CurrentStateRow? previousState,
            DateTime snapshotAt)
        {
            if (row.IsRunning)
            {
                return null;
            }

            if (previousState != null
                && previousState.IsRunning == 0
                && DateTime.TryParse(previousState.StoppedSinceAt, out var parsedStoppedSince))
            {
                return parsedStoppedSince;
            }

            if (previousState != null
                && previousState.IsRunning == 0
                && DateTime.TryParse(previousState.SnapshotAt, out var parsedSnapshotAt))
            {
                return parsedSnapshotAt;
            }

            return snapshotAt;
        }

        private static bool ShouldIgnore(
            Ec2ParsedRow row,
            DateTime? stoppedSince,
            DateTime snapshotAt,
            int stoppedIgnoreAfterMinutes)
        {
            if (row.IsRunning || !stoppedSince.HasValue)
            {
                return false;
            }

            return (snapshotAt - stoppedSince.Value).TotalMinutes >= stoppedIgnoreAfterMinutes;
        }

        private static void ReconcileCurrentStateForImport(
            System.Data.IDbConnection conn,
            System.Data.IDbTransaction? tx,
            long importId,
            string importedAt)
        {
            conn.Execute(
                @"
                    UPDATE Ec2MachineSnapshots
                    SET LotNo = NULL
                    WHERE ImportId = @importId;",
                new { importId },
                tx);

            conn.Execute(
                @"
                    UPDATE Ec2MachineCurrentState
                    SET
                        ImportId = @importId,
                        SourceType = 'Administrator',
                        IsStale = 0,
                        LotNo = NULL
                    WHERE EXISTS (
                        SELECT 1
                        FROM Ec2MachineSnapshots s
                        WHERE s.ImportId = @importId
                          AND s.MachineId = Ec2MachineCurrentState.MachineId
                    );",
                new { importId },
                tx);

            MarkMachinesMissingFromLatestSnapshot(conn, tx, importId, importedAt);
        }

        private static void MarkMachinesMissingFromLatestSnapshot(
            System.Data.IDbConnection conn,
            System.Data.IDbTransaction? tx,
            long importId,
            string importedAt)
        {
            conn.Execute(
                @"
                    UPDATE Ec2MachineCurrentState
                    SET
                        ImportId = COALESCE((
                            SELECT s.ImportId
                            FROM Ec2MachineSnapshots s
                            WHERE s.MachineId = Ec2MachineCurrentState.MachineId
                            ORDER BY s.ImportId DESC
                            LIMIT 1
                        ), ImportId),
                        SourceType = 'Administrator',
                        IsStale = 1,
                        IsIgnored = 1,
                        IgnoreReason = 'NOT_PRESENT_IN_LATEST_ADMINISTRATOR_SNAPSHOT',
                        IsRunning = 0,
                        LotNo = NULL,
                        ImportedAt = @importedAt
                    WHERE COALESCE(SourceType, 'Administrator') = 'Administrator'
                      AND NOT EXISTS (
                          SELECT 1
                          FROM Ec2MachineSnapshots s
                          WHERE s.ImportId = @importId
                            AND s.MachineId = Ec2MachineCurrentState.MachineId
                      );",
                new
                {
                    importId,
                    importedAt
                },
                tx);
        }

        private static bool LooksLikeProductionEventFile(FileInfo fileInfo, IReadOnlyList<string> lines)
        {
            if (Regex.IsMatch(fileInfo.Name, @"^\d{6}_\d{4}_E\.txt$", RegexOptions.IgnoreCase))
            {
                return true;
            }

            var firstDataLine = lines.FirstOrDefault(line => !string.IsNullOrWhiteSpace(line));
            if (string.IsNullOrWhiteSpace(firstDataLine))
            {
                return false;
            }

            var columns = firstDataLine.Split('|');
            return columns.Length >= 12
                && string.Equals(columns[0].Trim(), "E", StringComparison.OrdinalIgnoreCase)
                && DateTime.TryParse(columns[1].Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
        }

        private static bool ExistingImportHasBrokenText(System.Data.IDbConnection conn, long importId)
        {
            var values = conn.Query<string>(
                    @"
                        SELECT COALESCE(StatusText, '')
                        FROM Ec2MachineSnapshots
                        WHERE ImportId = @importId
                        UNION ALL
                        SELECT COALESCE(AreaLabel, '')
                        FROM Ec2MachineSnapshots
                        WHERE ImportId = @importId;",
                    new { importId })
                .ToList();

            return values.Any(ContainsBrokenEncoding);
        }

        private List<Ec2ParsedRow> ParseRows(
            IReadOnlyList<string> lines,
            DateTime snapshotAt,
            Ec2ImportSettings settings,
            ProductionImportResult result)
        {
            return ParseAdministratorRowsFixed(lines, snapshotAt, settings, result);
        }

        private List<Ec2ParsedRow> ParseAdministratorDatSampleRows(
            IReadOnlyList<string> lines,
            DateTime snapshotAt,
            Ec2ImportSettings settings,
            ProductionImportResult result)
        {
            return ParseAdministratorRowsFixed(lines, snapshotAt, settings, result);
        }

        private List<Ec2ParsedRow> ParseAdministratorRowsFixed(
            IReadOnlyList<string> lines,
            DateTime snapshotAt,
            Ec2ImportSettings settings,
            ProductionImportResult result)
        {
            var rows = new List<Ec2ParsedRow>();
            var currentArea = string.Empty;
            var areaStats = new Dictionary<string, Ec2AreaParseStats>(StringComparer.OrdinalIgnoreCase);

            for (var index = 0; index < lines.Count; index++)
            {
                var rawLine = lines[index];
                var line = (rawLine ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(line))
                {
                    IgnoreEc2Line(result, "EMPTY_LINE", index + 1, rawLine, null, currentArea);
                    continue;
                }

                if (line.StartsWith("\u203B", StringComparison.Ordinal)
                    || line.StartsWith("\u6700\u7D42", StringComparison.Ordinal))
                {
                    IgnoreEc2Line(result, "NOT_AREA_BLOCK", index + 1, rawLine, null, currentArea);
                    continue;
                }

                var columns = SplitCsvLine(line);
                if (columns.Count == 0 || columns.All(string.IsNullOrWhiteSpace))
                {
                    IgnoreEc2Line(result, "EMPTY_LINE", index + 1, rawLine, columns, currentArea);
                    continue;
                }

                if (IsSummaryLine(columns))
                {
                    IgnoreEc2Line(result, "HEADER_OR_SUMMARY_LINE", index + 1, rawLine, columns, currentArea);
                    GetAreaStats(areaStats, currentArea).IgnoredRows++;
                    continue;
                }

                if (TryDetectArea(columns, out var detectedArea))
                {
                    currentArea = detectedArea;
                    _ = GetAreaStats(areaStats, currentArea);
                    continue;
                }

                if (LooksLikeHeader(columns))
                {
                    IgnoreEc2Line(result, "HEADER_OR_SUMMARY_LINE", index + 1, rawLine, columns, currentArea);
                    GetAreaStats(areaStats, currentArea).IgnoredRows++;
                    continue;
                }

                result.Ec2RowsCandidate++;
                GetAreaStats(areaStats, currentArea).CandidateRows++;

                if (columns.Count < 3)
                {
                    IgnoreEc2Line(result, "TOO_FEW_COLUMNS", index + 1, rawLine, columns, currentArea);
                    GetAreaStats(areaStats, currentArea).IgnoredRows++;
                    continue;
                }

                var selectedLayout = ResolveAdministratorLayout(columns);

                var machineRaw = GetColumn(columns, selectedLayout.MachineIndex);
                var statusRaw = GetColumn(columns, selectedLayout.StatusIndex);
                var partCodeRaw = GetColumn(columns, selectedLayout.PartCodeIndex);
                var processRaw = GetColumn(columns, selectedLayout.ProcessIndex);

                var machineCode = machineRaw;
                if (string.IsNullOrWhiteSpace(machineCode))
                {
                    IgnoreEc2Line(result, "MISSING_MACHINE", index + 1, rawLine, columns, currentArea, machineCandidate: machineRaw, statusCandidate: statusRaw, partCodeCandidate: partCodeRaw, timeCandidate: processRaw);
                    PushDiagnosticLine(index + 1, machineRaw, statusRaw, partCodeRaw, processRaw, string.Empty, string.Empty, string.Empty, string.Empty, false, "missing_machine", selectedLayout.Name);
                    GetAreaStats(areaStats, currentArea).IgnoredRows++;
                    continue;
                }

                if (!ProductionMachineRepository.IsValidProductionMachineCode(machineCode))
                {
                    IgnoreEc2Line(result, "INVALID_MACHINE_CODE", index + 1, rawLine, columns, currentArea, machineCandidate: machineRaw, statusCandidate: statusRaw, partCodeCandidate: partCodeRaw, timeCandidate: processRaw);
                    PushDiagnosticLine(index + 1, machineRaw, statusRaw, partCodeRaw, processRaw, machineCode, string.Empty, string.Empty, string.Empty, false, "invalid_machine_code", selectedLayout.Name);
                    GetAreaStats(areaStats, currentArea).IgnoredRows++;
                    continue;
                }

                var statusText = statusRaw;
                var partCode = partCodeRaw;
                var processMinutes = ParseDouble(processRaw);
                var isRunning = IsRunningStatus(statusText, settings.RunningKeywords);
                var importReason = "ok";

                if (string.IsNullOrWhiteSpace(statusText) || ContainsBrokenEncoding(statusText))
                {
                    importReason = "invalid_status";
                    IgnoreEc2Line(result, "INVALID_STATUS", index + 1, rawLine, columns, currentArea, machineCandidate: machineCode, statusCandidate: statusText, partCodeCandidate: partCode, timeCandidate: processRaw);
                    PushDiagnosticLine(index + 1, machineRaw, statusRaw, partCodeRaw, processRaw, machineCode, statusText, partCode, FormatNullable(processMinutes), false, importReason, selectedLayout.Name, isRunning);
                    GetAreaStats(areaStats, currentArea).IgnoredRows++;
                    continue;
                }

                if (!processMinutes.HasValue || !double.IsFinite(processMinutes.Value) || processMinutes.Value <= 0)
                {
                    processMinutes = null;
                    importReason = "missing_process_minutes_imported_status_only";
                }

                var parsedRow = new Ec2ParsedRow
                {
                    AreaLabel = currentArea,
                    MachineCode = machineCode,
                    MachineName = machineCode,
                    StatusText = statusText,
                    IsRunning = isRunning,
                    SectorId = ResolveSectorId(currentArea),
                    LocalId = ResolveLocalId(currentArea),
                    PartCode = partCode,
                    PlannedProcessMinutes = processMinutes,
                    CapabilityType = GetColumn(columns, 4),
                    OperationRate = ParseDouble(GetColumn(columns, 8)),
                    CurrentDifference = ParseDouble(GetColumn(columns, 9)),
                    LotNo = null,
                    PlannedEndAt = ParseDateTime(GetColumn(columns, 10), snapshotAt),
                    ProcessMinutes = processMinutes,
                    DailyProduction = ParseDouble(GetColumn(columns, 12)),
                    RawColumns = BuildRawColumns(Array.Empty<string>(), columns, false)
                };

                GetAreaStats(areaStats, currentArea).ImportedRows++;
                PushDiagnosticLine(index + 1, machineRaw, statusRaw, partCodeRaw, processRaw, machineCode, statusText, partCode, FormatNullable(processMinutes), true, importReason, selectedLayout.Name, isRunning);
                rows.Add(parsedRow);
            }

            foreach (var stats in areaStats.Values)
            {
                Console.WriteLine($"[EC2 Administrator][Area] Area={stats.Area} HeaderDetected={stats.HeaderDetected} HeaderColumns={stats.HeaderColumns} CandidateRows={stats.CandidateRows} ImportedRows={stats.ImportedRows} IgnoredRows={stats.IgnoredRows}");
            }

            return rows;
        }

        private static AdministratorLayout ResolveAdministratorLayout(IReadOnlyList<string> columns)
        {
            var layouts = new List<AdministratorLayout>
            {
                new("requested", 2, 3, 7, 13),
                new("sample_offset", 1, 2, 3, 13),
                new("machine_first", 0, 1, 5, 13),
                new("machine_fourth", 3, 4, 8, 13)
            };

            var withMachineAndProcess = layouts.FirstOrDefault(layout =>
                IsValidMachineAt(columns, layout.MachineIndex)
                && IsValidProcessAt(columns, layout.ProcessIndex));

            if (withMachineAndProcess != null)
            {
                return withMachineAndProcess;
            }

            var withMachine = layouts.FirstOrDefault(layout => IsValidMachineAt(columns, layout.MachineIndex));
            if (withMachine != null)
            {
                return withMachine with
                {
                    ProcessIndex = ResolveProcessIndex(columns, withMachine.ProcessIndex)
                };
            }

            for (var index = 0; index < columns.Count; index++)
            {
                if (!ProductionMachineRepository.IsValidProductionMachineCode(GetColumn(columns, index)))
                {
                    continue;
                }

                var statusIndex = Math.Min(index + 1, columns.Count - 1);
                var partCodeIndex = ResolvePartCodeIndex(columns, index);
                return new AdministratorLayout(
                    $"dynamic_machine_col_{index}",
                    index,
                    statusIndex,
                    partCodeIndex,
                    ResolveProcessIndex(columns, 13));
            }

            return layouts[0];
        }

        private static bool IsValidMachineAt(IReadOnlyList<string> columns, int index)
        {
            return ProductionMachineRepository.IsValidProductionMachineCode(GetColumn(columns, index));
        }

        private static bool IsValidProcessAt(IReadOnlyList<string> columns, int index)
        {
            var parsed = ParseDouble(GetColumn(columns, index));
            return parsed.HasValue
                && double.IsFinite(parsed.Value)
                && parsed.Value > 0;
        }

        private static int ResolveProcessIndex(IReadOnlyList<string> columns, int preferredIndex)
        {
            foreach (var index in new[] { preferredIndex, 13, 12, 14, 11 })
            {
                if (index >= 0 && index < columns.Count && IsValidProcessAt(columns, index))
                {
                    return index;
                }
            }

            for (var index = columns.Count - 1; index >= 0; index--)
            {
                if (IsValidProcessAt(columns, index))
                {
                    return index;
                }
            }

            return Math.Min(Math.Max(preferredIndex, 0), Math.Max(columns.Count - 1, 0));
        }

        private static int ResolvePartCodeIndex(IReadOnlyList<string> columns, int machineIndex)
        {
            foreach (var offset in new[] { 5, 2, 4, 3 })
            {
                var index = machineIndex + offset;
                if (index >= 0 && index < columns.Count && LooksLikePartCode(GetColumn(columns, index)))
                {
                    return index;
                }
            }

            return Math.Min(machineIndex + 2, Math.Max(columns.Count - 1, 0));
        }

        private static bool LooksLikePartCode(string value)
        {
            var normalized = NormalizeCode(value);
            return normalized.Length is >= 4 and <= 12
                && normalized.Any(char.IsLetter)
                && normalized.Any(char.IsDigit)
                && !ProductionMachineRepository.IsValidProductionMachineCode(normalized);
        }

        private static Ec2DecodeResult DecodeLines(byte[] bytes)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var candidates = new List<Ec2DecodedText>();
            foreach (var item in new[]
            {
                new { Name = "cp932", Priority = 0, Encoding = Encoding.GetEncoding(932, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback) },
                new { Name = "shift_jis", Priority = 1, Encoding = Encoding.GetEncoding("shift_jis", EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback) },
                new { Name = "utf-8", Priority = 2, Encoding = (Encoding)new UTF8Encoding(false, true) },
                new { Name = "ansi-default", Priority = 3, Encoding = Encoding.Default }
            })
            {
                try
                {
                    var text = item.Encoding.GetString(bytes);
                    if (text.Contains(',') || text.Contains('\t') || text.Contains(';') || text.Contains('|'))
                    {
                        candidates.Add(new Ec2DecodedText(item.Name, text, ScoreDecodedText(text), item.Name == "ansi-default" ? 3 : item.Priority));
                    }
                }
                catch (DecoderFallbackException)
                {
                    // Try the next encoding.
                }
            }

            var best = candidates
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.Priority)
                .FirstOrDefault();

            if (best == null || ContainsBrokenEncoding(best.Text))
            {
                best = candidates
                    .Where(item => !ContainsBrokenEncoding(item.Text))
                    .OrderByDescending(item => item.Score)
                    .ThenBy(item => item.Priority)
                    .FirstOrDefault()
                    ?? best;
            }

            if (best == null || string.IsNullOrEmpty(best.Text))
            {
                return new Ec2DecodeResult(new List<string>(), string.Empty, string.Empty, false);
            }

            var lines = best.Text
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split('\n', StringSplitOptions.None)
                .ToList();

            return new Ec2DecodeResult(lines, best.EncodingName, DetectDelimiterLabel(best.Text), best.Text.Contains('\uFFFD', StringComparison.Ordinal));
        }

        private static int ScoreDecodedText(string text)
        {
            var score = 0;
            foreach (var token in new[]
            {
                "\u30A8\u30EA\u30A2",
                "\u8A2D\u5099\u53F7\u6A5F",
                "\u6307\u793A",
                "\u8A2D\u5099\u72B6\u614B",
                "\u52A0\u5DE5\u6642\u9593",
                "\u7A3C\u50CD",
                "\u904B\u8EE2",
                "\u505C\u6B62"
            })
            {
                if (text.Contains(token, StringComparison.Ordinal))
                {
                    score += 20;
                }
            }

            score -= text.Count(ch => ch == '\uFFFD') * 10;
            score -= Regex.Matches(text, "[ÃÂ�]").Count * 5;
            score -= Regex.Matches(text, "[繧蜒蛯蟾謖逅邨譛]").Count;
            return score;
        }

        private static bool ContainsBrokenEncoding(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            return text.Contains('\uFFFD', StringComparison.Ordinal)
                || text.Contains("ç¨¼å", StringComparison.Ordinal)
                || text.Contains("é\u0081", StringComparison.Ordinal)
                || text.Contains("����", StringComparison.Ordinal);
        }

        private static string DetectDelimiterLabel(string text)
        {
            var firstDataLine = text
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split('\n')
                .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line))
                ?? string.Empty;

            var delimiters = new[]
            {
                new { Label = "tab", Count = firstDataLine.Count(ch => ch == '\t') },
                new { Label = "pipe", Count = firstDataLine.Count(ch => ch == '|') },
                new { Label = "semicolon", Count = firstDataLine.Count(ch => ch == ';') },
                new { Label = "comma", Count = firstDataLine.Count(ch => ch == ',') }
            };

            return delimiters
                .OrderByDescending(item => item.Count)
                .FirstOrDefault(item => item.Count > 0)
                ?.Label ?? "unknown";
        }
        private static List<string> SplitCsvLine(string line)
        {
            var separator = line.Contains('\t')
                ? '\t'
                : line.Contains('|')
                    ? '|'
                : line.Contains(';') && !line.Contains(',')
                    ? ';'
                    : ',';
            var values = new List<string>();
            var current = new StringBuilder();
            var quoted = false;

            for (var index = 0; index < line.Length; index++)
            {
                var ch = line[index];
                if (ch == '"')
                {
                    if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                    {
                        current.Append('"');
                        index++;
                    }
                    else
                    {
                        quoted = !quoted;
                    }
                    continue;
                }

                if (ch == separator && !quoted)
                {
                    values.Add(current.ToString().Trim());
                    current.Clear();
                    continue;
                }

                current.Append(ch);
            }

            values.Add(current.ToString().Trim());
            return values;
        }

        private static bool LooksLikeHeader(IReadOnlyList<string> columns)
        {
            if (columns.Any(column => ProductionMachineRepository.IsValidProductionMachineCode(column)))
            {
                return false;
            }

            var joined = string.Join(" ", columns).ToLowerInvariant();
            var score = 0;
            if (joined.Contains("equipment", StringComparison.Ordinal)
                || joined.Contains("status", StringComparison.Ordinal)
                || joined.Contains("maquina", StringComparison.Ordinal)
                || joined.Contains("m\u00E1quina", StringComparison.Ordinal)
                || joined.Contains("\u8A2D\u5099\u53F7\u6A5F", StringComparison.Ordinal))
            {
                score++;
            }

            if (joined.Contains("\u6307\u793A", StringComparison.Ordinal)
                || joined.Contains("\u8A2D\u5099\u72B6\u614B", StringComparison.Ordinal))
            {
                score++;
            }

            if (joined.Contains("\u914D\u5408\u6307\u793A", StringComparison.Ordinal)
                || joined.Contains("\u80FD\u529B\u67A0", StringComparison.Ordinal)
                || joined.Contains("recipe", StringComparison.Ordinal)
                || joined.Contains("part", StringComparison.Ordinal))
            {
                score++;
            }

            if (joined.Contains("\u52A0\u5DE5\u6642\u9593", StringComparison.Ordinal)
                || joined.Contains("\u4E88\u5B9A\u52A0\u5DE5\u6642\u9593", StringComparison.Ordinal)
                || joined.Contains("process", StringComparison.Ordinal))
            {
                score++;
            }

            return score >= 2;
        }
        private static Dictionary<string, int> BuildHeaderMap(IReadOnlyList<string> header)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < header.Count; index++)
            {
                var key = NormalizeHeader(header[index]);
                if (!string.IsNullOrWhiteSpace(key) && !map.ContainsKey(key))
                {
                    map[key] = index;
                }
            }

            return map;
        }

        private static string Pick(
            IReadOnlyList<string> columns,
            IReadOnlyDictionary<string, int> map,
            int fallbackIndex,
            params string[] names)
        {
            foreach (var name in names)
            {
                if (map.TryGetValue(NormalizeHeader(name), out var index)
                    && index >= 0
                    && index < columns.Count)
                {
                    return columns[index].Trim();
                }
            }

            return fallbackIndex >= 0 && fallbackIndex < columns.Count
                ? columns[fallbackIndex].Trim()
                : string.Empty;
        }

        private static Dictionary<string, string> BuildRawColumns(
            IReadOnlyList<string> header,
            IReadOnlyList<string> columns,
            bool hasHeader)
        {
            var raw = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < columns.Count; index++)
            {
                var key = hasHeader && index < header.Count && !string.IsNullOrWhiteSpace(header[index])
                    ? header[index].Trim()
                    : $"Column{index + 1}";
                raw[key] = columns[index];
            }

            return raw;
        }

        private static string NormalizeHeader(string value)
        {
            var normalized = (value ?? string.Empty)
                .Trim()
                .Replace(" ", string.Empty, StringComparison.Ordinal)
                .Replace("_", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .ToLowerInvariant();

            return normalized switch
            {
                "\u8A2D\u5099\u53F7\u6A5F" or "\u53F7\u6A5F" => "equipment",
                "\u6307\u793A" or "\u7A3C\u50CD\u6307\u793A" or "\u904B\u8EE2\u6307\u793A" => "status",
                "\u914D\u53F0\u6307\u793A" or "\u914D\u5408\u6307\u793A" => "recipe",
                "\u4E88\u5B9A\u52A0\u5DE5\u6642\u9593" => "plannedprocessminutes",
                "\u8A2D\u5099\u72B6\u614B" => "machinestatus",
                "\u80FD\u529B\u67A0" => "capability",
                "\u30BF\u30A4\u30D7" => "tipo",
                "\u8A2D\u5B9A\u7A3C\u50CD\u7387" or "\u63A8\u5B9A\u7A3C\u50CD\u7387" => "operationrate",
                "\u73FE\u72B6\u5DEE\u7570" => "difference",
                "\u52A0\u5DE5\u30ED\u30C3\u30C8no." or "\u52A0\u5DE5\u30ED\u30C3\u30C8no" => "lot",
                "\u30B5\u30A4\u30AF\u30EB\u7D42\u4E86\u4E88\u5B9A\u6642\u523B" => "plannedend",
                "\u52A0\u5DE5\u6642\u9593" => "processminutes",
                "day\u52A0\u5DE5\u53EF\u80FD\u6570\u91CF(k\u500B)" or "day\u751F\u7523\u6570\u91CF(k\u500B)" => "dailyproduction",
                _ => normalized
            };
        }
        private static bool IsRunningStatus(string statusText, IReadOnlyCollection<string> runningKeywords)
        {
            var normalized = (statusText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            return runningKeywords.Any(keyword => normalized.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        private static double? ParseDouble(string value)
        {
            var normalized = (value ?? string.Empty)
                .Trim()
                .Replace("%", string.Empty, StringComparison.Ordinal)
                .Replace(",", ".", StringComparison.Ordinal);

            return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;
        }

        private static DateTime? ParseDateTime(string value, DateTime snapshotAt)
        {
            var normalized = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            if (DateTime.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                return parsed.Year <= 1
                    ? snapshotAt.Date.Add(parsed.TimeOfDay)
                    : parsed;
            }

            return TimeSpan.TryParse(normalized, CultureInfo.InvariantCulture, out var time)
                ? snapshotAt.Date.Add(time)
                : null;
        }

        private int? ResolveSectorId(string areaLabel)
        {
            var normalized = (areaLabel ?? string.Empty).Trim();
            if (normalized.Contains("DAD", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("211D", StringComparison.OrdinalIgnoreCase))
            {
                return _sectorIds.Dad;
            }

            if (normalized.Contains("GBARERU", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("G-BARERU", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("G\u30D0\u30EC\u30EB", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("2400", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("\u30A8\u30EA\u30A2", StringComparison.OrdinalIgnoreCase))
            {
                return _sectorIds.GBareru;
            }

            return null;
        }

        private static int? ResolveLocalId(string areaLabel)
        {
            var areaNumber = ResolveAreaNumber(areaLabel);
            if (!areaNumber.HasValue)
            {
                return null;
            }

            return areaNumber.Value == 1
                ? 1
                : areaNumber.Value + 1;
        }

        private string ResolveLineCode(string areaLabel, int? sectorId)
        {
            if (sectorId == _sectorIds.Dad
                || (areaLabel ?? string.Empty).Contains("211D", StringComparison.OrdinalIgnoreCase)
                || (areaLabel ?? string.Empty).Contains("DAD", StringComparison.OrdinalIgnoreCase))
            {
                return "211D";
            }

            return "2400";
        }

        private static int? ResolveAreaNumber(string areaLabel)
        {
            var normalized = (areaLabel ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            var match = Regex.Match(normalized, @"(?:Area|エリア)\s*(\d{1,2})", RegexOptions.IgnoreCase);
            return match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var areaNumber)
                ? areaNumber
                : null;
        }

        private static string NormalizeCode(string value)
        {
            return (value ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static void Record(ProductionImportResult result, string name, Stopwatch stopwatch)
        {
            stopwatch.Stop();
            result.PerformanceMs[name] = stopwatch.ElapsedMilliseconds;
        }

        private static void PushError(ProductionImportResult result, string message)
        {
            if (!string.IsNullOrWhiteSpace(message) && result.Errors.Count < 30)
            {
                result.Errors.Add(message);
            }
        }

        private static void PushImportedSample(ProductionImportResult result, string message)
        {
            if (!string.IsNullOrWhiteSpace(message) && result.Ec2ImportedSamples.Count < 10)
            {
                result.Ec2ImportedSamples.Add(message);
            }
        }

        private static void IgnoreEc2Line(
            ProductionImportResult result,
            string reason,
            int lineNumber,
            string? rawLine,
            IReadOnlyList<string>? columns = null,
            string area = "",
            string machineCandidate = "",
            string statusCandidate = "",
            string partCodeCandidate = "",
            string timeCandidate = "")
        {
            result.Ec2RowsIgnored++;

            switch (reason)
            {
                case "EMPTY_LINE":
                    result.Ec2IgnoredByEmptyLine++;
                    break;
                case "NOT_AREA_BLOCK":
                    result.Ec2IgnoredByNotAreaBlock++;
                    break;
                case "TOO_FEW_COLUMNS":
                    result.Ec2IgnoredByTooFewColumns++;
                    break;
                case "MISSING_MACHINE":
                    result.Ec2IgnoredByMissingMachine++;
                    break;
                case "INVALID_MACHINE_CODE":
                    result.Ec2IgnoredByInvalidMachineCode++;
                    break;
                case "HEADER_OR_SUMMARY_LINE":
                    result.Ec2IgnoredByHeaderOrSummaryLine++;
                    break;
                case "INVALID_STATUS":
                    result.Ec2IgnoredByInvalidStatus++;
                    break;
                case "INVALID_TIME":
                    result.Ec2IgnoredByInvalidTime++;
                    break;
                case "INVALID_PROCESS_MINUTES":
                case "invalid_process_minutes":
                    result.Ec2IgnoredByInvalidTime++;
                    break;
                default:
                    result.Ec2IgnoredByUnknownFormat++;
                    break;
            }

            if (result.Ec2DiscardSamples.Count >= 10)
            {
                return;
            }

            result.Ec2DiscardSamples.Add(
                $"LineNumber={lineNumber} RawLine={PreviewLine(rawLine)} ColumnCount={(columns?.Count ?? 0)} DetectedArea={area} MachineCandidate={machineCandidate} StatusCandidate={statusCandidate} PartCodeCandidate={partCodeCandidate} TimeCandidate={timeCandidate} IgnoreReason={reason}");
        }

        private static void PushDiagnosticLine(
            int lineNumber,
            string machineRaw,
            string statusRaw,
            string partCodeRaw,
            string processRaw,
            string machineParsed,
            string statusParsed,
            string partCodeParsed,
            string processMinutesParsed,
            bool imported,
            string ignoreReason,
            string layoutUsed = "",
            bool? isRunning = null)
        {
            Console.WriteLine(
                $"[EC2 Administrator][Diagnostic] LineNumber={lineNumber} Column3_MachineRaw={machineRaw} Column4_ShijiRaw={statusRaw} Column8_PartCodeRaw={partCodeRaw} Column14_TimeRaw={processRaw} MachineParsed={machineParsed} StatusParsed={statusParsed} PartCodeParsed={partCodeParsed} ProcessMinutesParsed={processMinutesParsed} LotFromAdministratorIgnored=true Imported={(imported ? "true" : "false")} IgnoreReason={ignoreReason}{(string.IsNullOrWhiteSpace(layoutUsed) ? string.Empty : $" LayoutUsed={layoutUsed}")}{(isRunning.HasValue ? $" IsRunning={(isRunning.Value ? "true" : "false")}" : string.Empty)}");
        }

        private static string GetColumn(IReadOnlyList<string> columns, int zeroBasedIndex)
        {
            return zeroBasedIndex >= 0 && zeroBasedIndex < columns.Count
                ? columns[zeroBasedIndex].Trim()
                : string.Empty;
        }

        private static string ResolveFullPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFullPath(path);
            }
            catch
            {
                return path;
            }
        }

        private static string PreviewRawLine(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return string.Empty;
            }

            var raw = Encoding.GetEncoding(28591).GetString(bytes);
            raw = raw.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
            var line = raw.Split('\n', StringSplitOptions.None).FirstOrDefault() ?? string.Empty;
            return PreviewLine(line);
        }

        private static string PreviewLine(string? value)
        {
            var line = (value ?? string.Empty).Trim();
            if (line.Length <= 240)
            {
                return line;
            }

            return line[..240] + "...";
        }

        private static string? FindHeaderLine(IReadOnlyList<string> lines)
        {
            return lines.FirstOrDefault(line => LooksLikeHeader(SplitCsvLine(line)));
        }

        private static string? FindFirstDataLine(IReadOnlyList<string> lines)
        {
            foreach (var line in lines)
            {
                var columns = SplitCsvLine(line);
                if (columns.Count >= 6
                    && columns.Any(column => ProductionMachineRepository.IsValidProductionMachineCode(column)))
                {
                    return line;
                }
            }

            return null;
        }

        private static void LogFileDiagnostics(ProductionImportResult result)
        {
            Console.WriteLine("[EC2 Administrator][File]");
            Console.WriteLine($"Ec2AdministratorFilePath={result.Ec2FilePath}");
            Console.WriteLine($"ResolvedFullPath={result.Ec2ResolvedFullPath}");
            Console.WriteLine($"FileExists={result.Ec2FileExists}");
            Console.WriteLine($"FileSize={result.Ec2FileSize}");
            Console.WriteLine($"LastWriteTime={result.Ec2FileLastWriteTime}");
            Console.WriteLine($"DetectedEncoding={result.Ec2EncodingDetected}");
            Console.WriteLine($"DelimiterDetected={result.Ec2DelimiterDetected}");
            Console.WriteLine($"RawLinePreview={result.Ec2RawLinePreview}");
            Console.WriteLine($"DecodedLinePreview={result.Ec2DecodedLinePreview}");
            Console.WriteLine($"ContainsReplacementChar={result.Ec2ContainsReplacementChar}");
            Console.WriteLine($"FirstLinePreview={result.Ec2FirstLinePreview}");
            Console.WriteLine($"HeaderLinePreview={result.Ec2HeaderLinePreview}");
            Console.WriteLine($"FirstDataLinePreview={result.Ec2FirstDataLinePreview}");
        }

        private static string FormatNullable(double? value)
        {
            return value.HasValue
                ? value.Value.ToString("0.###", CultureInfo.InvariantCulture)
                : "null";
        }

        private static bool TryDetectArea(IReadOnlyList<string> columns, out string area)
        {
            foreach (var column in columns)
            {
                var value = (column ?? string.Empty).Trim();
                if (Regex.IsMatch(value, @"^\u30A8\u30EA\u30A2\s*\d+", RegexOptions.IgnoreCase)
                    || Regex.IsMatch(value, @"^area\s*\d+", RegexOptions.IgnoreCase))
                {
                    area = value;
                    return true;
                }
            }

            area = string.Empty;
            return false;
        }

        private static bool IsSummaryLine(IReadOnlyList<string> columns)
        {
            if (columns.Any(column => ProductionMachineRepository.IsValidProductionMachineCode(column)))
            {
                return false;
            }

            var joined = string.Join(" ", columns.Where(value => !string.IsNullOrWhiteSpace(value))).Trim();
            if (string.IsNullOrWhiteSpace(joined))
            {
                return true;
            }

            return joined.Contains("\u914D\u53F0\u6307\u793A\u30BF\u30AF\u30C8\u52A0\u91CD\u5E73\u5747", StringComparison.Ordinal)
                || joined.Contains("\u5B9F\u914D\u53F0\u30BF\u30AF\u30C8\u52A0\u91CD\u5E73\u5747", StringComparison.Ordinal)
                || joined.Contains("\u914D\u53F0 G/NG", StringComparison.OrdinalIgnoreCase)
                || joined.Contains("\u63A8\u5B9A\u7A3C\u50CD\u7387", StringComparison.Ordinal)
                || string.Equals(joined, "G", StringComparison.OrdinalIgnoreCase);
        }

        private static Ec2AreaParseStats GetAreaStats(
            IDictionary<string, Ec2AreaParseStats> statsByArea,
            string area)
        {
            var key = string.IsNullOrWhiteSpace(area) ? "(sem area)" : area;
            if (!statsByArea.TryGetValue(key, out var stats))
            {
                stats = new Ec2AreaParseStats
                {
                    Area = key
                };
                statsByArea[key] = stats;
            }

            return stats;
        }

        private static bool LooksLikeAdministratorDatSample(IReadOnlyList<string> lines)
        {
            foreach (var rawLine in lines)
            {
                var line = (rawLine ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (line.StartsWith("\u203B", StringComparison.Ordinal)
                    || line.StartsWith("\u6700\u7D42", StringComparison.Ordinal)
                    || line.StartsWith("AREA", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (TryDetectArea(SplitCsvLine(line), out _))
                {
                    return true;
                }

                if (line.Contains("\u8A2D\u5099\u53F7\u6A5F", StringComparison.Ordinal)
                    && line.Contains("\u52A0\u5DE5\u6642\u9593", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
        private static Ec2ImportSettings LoadSettings()
        {
            var filePath = StoragePathResolver.GetOptionalPath("Ec2AdministratorFilePath") ?? string.Empty;
            var ignoreAfterText = ConfigurationManager.AppSettings["MachineStoppedIgnoreAfterMinutes"] ?? "0";
            var keywordsText = ConfigurationManager.AppSettings["Ec2AdministratorRunningStatusKeywords"]
                ?? "operando,rodando,running,run,\u7A3C\u50CD,\u904B\u8EE2,\u7A3C\u50CD\u4E2D,\u904B\u8EE2\u4E2D";

            if (!int.TryParse(ignoreAfterText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ignoreAfter)
                || ignoreAfter < 0)
            {
                ignoreAfter = 0;
            }

            var keywords = keywordsText
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return new Ec2ImportSettings(filePath, ignoreAfter, keywords);
        }

        private readonly record struct Ec2ImportSettings(
            string FilePath,
            int StoppedIgnoreAfterMinutes,
            IReadOnlyCollection<string> RunningKeywords);

        private sealed record Ec2DecodedText(string EncodingName, string Text, int Score, int Priority);

        private sealed record Ec2DecodeResult(List<string> Lines, string EncodingName, string DelimiterLabel, bool ContainsReplacementChar);

        private sealed record AdministratorLayout(
            string Name,
            int MachineIndex,
            int StatusIndex,
            int PartCodeIndex,
            int ProcessIndex);

        private sealed class Ec2ExistingImportRow
        {
            public long Id { get; set; }
            public int RowsImported { get; set; }
        }

        private sealed class Ec2AreaParseStats
        {
            public string Area { get; set; } = string.Empty;
            public bool HeaderDetected { get; set; }
            public string HeaderColumns { get; set; } = string.Empty;
            public int CandidateRows { get; set; }
            public int ImportedRows { get; set; }
            public int IgnoredRows { get; set; }
        }

        private sealed class Ec2ParsedRow
        {
            public string AreaLabel { get; set; } = string.Empty;
            public string MachineCode { get; set; } = string.Empty;
            public string MachineName { get; set; } = string.Empty;
            public string StatusText { get; set; } = string.Empty;
            public bool IsRunning { get; set; }
            public int? SectorId { get; set; }
            public int? LocalId { get; set; }
            public string PartCode { get; set; } = string.Empty;
            public double? PlannedProcessMinutes { get; set; }
            public string CapabilityType { get; set; } = string.Empty;
            public double? OperationRate { get; set; }
            public double? CurrentDifference { get; set; }
            public string? LotNo { get; set; }
            public DateTime? PlannedEndAt { get; set; }
            public double? ProcessMinutes { get; set; }
            public double? DailyProduction { get; set; }
            public Dictionary<string, string> RawColumns { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        }

        private sealed class Ec2CurrentStateRow
        {
            public int MachineId { get; set; }
            public string MachineCode { get; set; } = string.Empty;
            public int IsRunning { get; set; }
            public string StoppedSinceAt { get; set; } = string.Empty;
            public string SnapshotAt { get; set; } = string.Empty;
        }

        private sealed class Ec2WriteRow
        {
            public long ImportId { get; set; }
            public string SourceType { get; set; } = "Administrator";
            public int IsStale { get; set; }
            public int MachineId { get; set; }
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
            public string? LotNo { get; set; }
            public string? PlannedEndAt { get; set; }
            public double? ProcessMinutes { get; set; }
            public double? DailyProduction { get; set; }
            public string RawColumnsJson { get; set; } = string.Empty;
            public string SnapshotAt { get; set; } = string.Empty;
            public string ImportedAt { get; set; } = string.Empty;
            public string? StoppedSinceAt { get; set; }
        }
    }
}
