using System.Data;
using Dapper;

namespace TeamOps.Data.Db
{
    public static class ProductionSchemaMigrator
    {
        private static readonly object SyncRoot = new();
        private static bool _ensured;

        public static void Ensure(IDbConnection conn)
        {
            if (_ensured)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (_ensured)
                {
                    return;
                }

                EnsureMachineColumns(conn);

                conn.Execute(
                    @"
                    CREATE TABLE IF NOT EXISTS MachineEvents (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        MachineId INTEGER NOT NULL,
                        MachineCode TEXT NOT NULL,
                        LineCode TEXT NOT NULL,
                        LocalId INTEGER,
                        SectorId INTEGER,
                        RecipeName TEXT,
                        LotNo TEXT,
                        StatusCode INTEGER NOT NULL,
                        StatusText TEXT NOT NULL,
                        InternalState TEXT NOT NULL,
                        EventDateTime TEXT NOT NULL,
                        SourceFile TEXT NOT NULL,
                        ImportedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        FOREIGN KEY (MachineId) REFERENCES Machines(Id),
                        FOREIGN KEY (LocalId) REFERENCES Locals(Id),
                        FOREIGN KEY (SectorId) REFERENCES Sectors(Id)
                    );

                    CREATE TABLE IF NOT EXISTS MachineCurrentStatus (
                        MachineId INTEGER PRIMARY KEY,
                        MachineCode TEXT NOT NULL,
                        LineCode TEXT NOT NULL,
                        LocalId INTEGER,
                        SectorId INTEGER,
                        RecipeName TEXT,
                        LotNo TEXT,
                        StatusCode INTEGER NOT NULL,
                        StatusText TEXT NOT NULL,
                        InternalState TEXT NOT NULL,
                        EventDateTime TEXT NOT NULL,
                        UpdatedAt TEXT NOT NULL,
                        FOREIGN KEY (MachineId) REFERENCES Machines(Id),
                        FOREIGN KEY (LocalId) REFERENCES Locals(Id),
                        FOREIGN KEY (SectorId) REFERENCES Sectors(Id)
                    );

                    CREATE TABLE IF NOT EXISTS MachineStatuses (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        SectorId INTEGER,
                        StatusCode INTEGER NOT NULL,
                        DisplayCode INTEGER NOT NULL,
                        Classification TEXT NOT NULL DEFAULT 'StopCounts',
                        NamePt TEXT NOT NULL,
                        NameJp TEXT NOT NULL,
                        ColorHex TEXT NOT NULL DEFAULT '#5B88E8',
                        TextColorHex TEXT NOT NULL DEFAULT '#FFFFFF',
                        SortOrder INTEGER NOT NULL DEFAULT 0,
                        IsActive INTEGER NOT NULL DEFAULT 1,
                        FOREIGN KEY (SectorId) REFERENCES Sectors(Id)
                    );

                    CREATE TABLE IF NOT EXISTS ProductionPlanSnapshots (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        SourceFile TEXT NOT NULL,
                        ExportedAt TEXT,
                        LastUpdatedAt TEXT,
                        ImportedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                    );

                    CREATE TABLE IF NOT EXISTS ProductionPlanRows (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        SnapshotId INTEGER NOT NULL,
                        AreaLabel TEXT NOT NULL,
                        MachineCode TEXT NOT NULL,
                        AssignmentText TEXT,
                        PlannedProcessMinutes REAL,
                        MachineStatusText TEXT,
                        CapabilityFrame TEXT,
                        WorkType TEXT,
                        TargetKadouritsu REAL,
                        CurrentDifference REAL,
                        LotNo TEXT,
                        CycleEndAt TEXT,
                        DailyPlannedQuantity REAL,
                        DailyEstimatedQuantity REAL,
                        EstimatedKadouritsu REAL,
                        RawColumnsJson TEXT,
                        ImportedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        FOREIGN KEY (SnapshotId) REFERENCES ProductionPlanSnapshots(Id)
                    );

                    CREATE TABLE IF NOT EXISTS Ec2AdministratorImports (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        SourceType TEXT NOT NULL DEFAULT 'Administrator',
                        SourceFile TEXT NOT NULL,
                        FileLastWriteTime TEXT,
                        FileLength INTEGER NOT NULL DEFAULT 0,
                        FileHash TEXT NOT NULL,
                        ImportedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        RowsRead INTEGER NOT NULL DEFAULT 0,
                        RowsImported INTEGER NOT NULL DEFAULT 0,
                        RowsIgnored INTEGER NOT NULL DEFAULT 0
                    );

                    CREATE TABLE IF NOT EXISTS Ec2MachineSnapshots (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        ImportId INTEGER NOT NULL,
                        MachineId INTEGER,
                        SectorId INTEGER,
                        LocalId INTEGER,
                        AreaLabel TEXT,
                        MachineCode TEXT NOT NULL,
                        MachineName TEXT,
                        StatusText TEXT,
                        IsRunning INTEGER NOT NULL DEFAULT 0,
                        IsIgnored INTEGER NOT NULL DEFAULT 0,
                        IgnoreReason TEXT,
                        PartCode TEXT,
                        PlannedProcessMinutes REAL,
                        CapabilityType TEXT,
                        OperationRate REAL,
                        CurrentDifference REAL,
                        LotNo TEXT,
                        PlannedEndAt TEXT,
                        ProcessMinutes REAL,
                        DailyProduction REAL,
                        RawColumnsJson TEXT,
                        SnapshotAt TEXT NOT NULL,
                        ImportedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        FOREIGN KEY (ImportId) REFERENCES Ec2AdministratorImports(Id),
                        FOREIGN KEY (MachineId) REFERENCES Machines(Id),
                        FOREIGN KEY (SectorId) REFERENCES Sectors(Id),
                        FOREIGN KEY (LocalId) REFERENCES Locals(Id)
                    );

                    CREATE TABLE IF NOT EXISTS Ec2MachineCurrentState (
                        MachineId INTEGER PRIMARY KEY,
                        MachineCode TEXT NOT NULL,
                        ImportId INTEGER,
                        SourceType TEXT NOT NULL DEFAULT 'Administrator',
                        IsStale INTEGER NOT NULL DEFAULT 0,
                        SectorId INTEGER,
                        LocalId INTEGER,
                        AreaLabel TEXT,
                        MachineName TEXT,
                        StatusText TEXT,
                        IsRunning INTEGER NOT NULL DEFAULT 0,
                        IsIgnored INTEGER NOT NULL DEFAULT 0,
                        IgnoreReason TEXT,
                        PartCode TEXT,
                        PlannedProcessMinutes REAL,
                        CapabilityType TEXT,
                        OperationRate REAL,
                        CurrentDifference REAL,
                        LotNo TEXT,
                        PlannedEndAt TEXT,
                        ProcessMinutes REAL,
                        DailyProduction REAL,
                        RawColumnsJson TEXT,
                        SnapshotAt TEXT NOT NULL,
                        ImportedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        LastSeenAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        StoppedSinceAt TEXT,
                        FOREIGN KEY (ImportId) REFERENCES Ec2AdministratorImports(Id),
                        FOREIGN KEY (MachineId) REFERENCES Machines(Id),
                        FOREIGN KEY (SectorId) REFERENCES Sectors(Id),
                        FOREIGN KEY (LocalId) REFERENCES Locals(Id)
                    );

                    CREATE TABLE IF NOT EXISTS ProductionPartCodeStyles (
                        PartCode TEXT PRIMARY KEY,
                        ColorHex TEXT NOT NULL DEFAULT '#D93F3F',
                        TextColorHex TEXT NOT NULL DEFAULT '#FFFFFF',
                        Description TEXT,
                        IsActive INTEGER NOT NULL DEFAULT 1,
                        UpdatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                    );

                    CREATE TABLE IF NOT EXISTS ProductionProcedureTimes (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        SectorId INTEGER NOT NULL,
                        LocalId INTEGER,
                        ProcedureCode TEXT NOT NULL,
                        StandardMinutes REAL NOT NULL,
                        IsActive INTEGER NOT NULL DEFAULT 1,
                        UpdatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        FOREIGN KEY (SectorId) REFERENCES Sectors(Id),
                        FOREIGN KEY (LocalId) REFERENCES Locals(Id)
                    );

                    UPDATE Machines
                    SET
                        MachineCode = upper(trim(COALESCE(MachineCode, ''))),
                        LineCode = upper(trim(COALESCE(LineCode, ''))),
                        MachineKey = CASE
                            WHEN trim(COALESCE(MachineCode, '')) = '' THEN NULL
                            ELSE upper(trim(COALESCE(LineCode, ''))) || ':' || upper(trim(COALESCE(MachineCode, '')))
                        END
                    WHERE trim(COALESCE(MachineCode, '')) <> ''
                      AND (
                          MachineCode <> upper(trim(COALESCE(MachineCode, '')))
                          OR LineCode <> upper(trim(COALESCE(LineCode, '')))
                          OR COALESCE(MachineKey, '') <> upper(trim(COALESCE(LineCode, ''))) || ':' || upper(trim(COALESCE(MachineCode, '')))
                      );

                    CREATE UNIQUE INDEX IF NOT EXISTS IX_MachineEvents_UniqueEvent
                    ON MachineEvents(MachineId, EventDateTime, StatusCode);

                    CREATE UNIQUE INDEX IF NOT EXISTS IX_MachineEvents_UniqueRawEvent
                    ON MachineEvents(MachineId, EventDateTime, InternalState);

                    CREATE INDEX IF NOT EXISTS IX_MachineEvents_EventDateTime
                    ON MachineEvents(EventDateTime);

                    CREATE INDEX IF NOT EXISTS IX_MachineEvents_Machine_EventTime
                    ON MachineEvents(MachineId, EventDateTime);

                    CREATE INDEX IF NOT EXISTS IX_MachineEvents_SectorLocal_EventTime
                    ON MachineEvents(SectorId, LocalId, EventDateTime);

                    CREATE INDEX IF NOT EXISTS IX_MachineCurrentStatus_LocalSector
                    ON MachineCurrentStatus(LocalId, SectorId);

                    DROP INDEX IF EXISTS IX_MachineEvents_Sector_EventTime;
                    DROP INDEX IF EXISTS IX_MachineEvents_StatusCode_EventTime;
                    DROP INDEX IF EXISTS IX_MachineEvents_Sector_Status_EventTime;

                    CREATE INDEX IF NOT EXISTS IX_Machines_MachineCode
                    ON Machines(MachineCode);

                    CREATE INDEX IF NOT EXISTS IX_Machines_MachineCode_LineCode
                    ON Machines(MachineCode, LineCode);

                    CREATE UNIQUE INDEX IF NOT EXISTS IX_Machines_MachineKey_Unique
                    ON Machines(MachineKey);

                    CREATE INDEX IF NOT EXISTS IX_Machines_LocalId
                    ON Machines(LocalId);

                    CREATE INDEX IF NOT EXISTS IX_Machines_SectorId
                    ON Machines(SectorId);

                    CREATE INDEX IF NOT EXISTS IX_Locals_Sector_Name
                    ON Locals(SectorId, NamePt, Id);

                    CREATE INDEX IF NOT EXISTS IX_Operators_Status_Shift_Sector_Name
                    ON Operators(Status, ShiftId, SectorId, NameRomanji);

                    CREATE INDEX IF NOT EXISTS IX_Hikitsugui_Local_Date
                    ON Hikitsugui(LocalId, Date);

                    CREATE INDEX IF NOT EXISTS IX_HikitsuguiReads_Reader_Hikitsugui
                    ON HikitsuguiReads(ReaderCodigoFJ, HikitsuguiId);

                    CREATE INDEX IF NOT EXISTS IX_HikitsuguiReads_Hikitsugui
                    ON HikitsuguiReads(HikitsuguiId);

                    CREATE INDEX IF NOT EXISTS IX_HikitsuguiAttachments_Hikitsugui
                    ON HikitsuguiAttachments(HikitsuguiId);

                    CREATE UNIQUE INDEX IF NOT EXISTS IX_ProductionPlanSnapshots_SourceFile_ExportedAt
                    ON ProductionPlanSnapshots(SourceFile, ExportedAt);

                    CREATE UNIQUE INDEX IF NOT EXISTS IX_ProductionPlanRows_Snapshot_Area_Machine
                    ON ProductionPlanRows(SnapshotId, AreaLabel, MachineCode);

                    CREATE INDEX IF NOT EXISTS IX_ProductionPlanRows_MachineCode
                    ON ProductionPlanRows(MachineCode);

                    CREATE UNIQUE INDEX IF NOT EXISTS IX_Ec2AdministratorImports_FileHash
                    ON Ec2AdministratorImports(FileHash);

                    CREATE UNIQUE INDEX IF NOT EXISTS IX_ProductionProcedureTimes_Unique
                    ON ProductionProcedureTimes(SectorId, COALESCE(LocalId, 0), ProcedureCode);

                    CREATE INDEX IF NOT EXISTS IX_Ec2MachineSnapshots_ImportId
                    ON Ec2MachineSnapshots(ImportId);

                    CREATE INDEX IF NOT EXISTS IX_Ec2MachineSnapshots_Machine
                    ON Ec2MachineSnapshots(MachineCode, SnapshotAt);

                    CREATE INDEX IF NOT EXISTS IX_Ec2MachineSnapshots_Area
                    ON Ec2MachineSnapshots(AreaLabel);

                    CREATE INDEX IF NOT EXISTS IX_Ec2MachineSnapshots_Status
                    ON Ec2MachineSnapshots(StatusText);

                    CREATE INDEX IF NOT EXISTS IX_Ec2MachineSnapshots_PartCode
                    ON Ec2MachineSnapshots(PartCode);

                    CREATE INDEX IF NOT EXISTS IX_Ec2MachineSnapshots_LotNo
                    ON Ec2MachineSnapshots(LotNo);

                    CREATE INDEX IF NOT EXISTS IX_Ec2MachineCurrentState_MachineId
                    ON Ec2MachineCurrentState(MachineId);

                    CREATE INDEX IF NOT EXISTS IX_Ec2MachineCurrentState_Area
                    ON Ec2MachineCurrentState(AreaLabel);

                    CREATE INDEX IF NOT EXISTS IX_Ec2MachineCurrentState_Status
                    ON Ec2MachineCurrentState(StatusText);

                    CREATE INDEX IF NOT EXISTS IX_Ec2MachineCurrentState_PartCode
                    ON Ec2MachineCurrentState(PartCode);"
            );

                EnsureMachineStatusColumns(conn);
                EnsureEc2Columns(conn);
                EnsureEc2CurrentStateMachineIdKey(conn);
                RebuildMachineStatusesForSectorScope(conn);
                DropLegacyMachineStatusCodeIndex(conn);

                conn.Execute(
                    @"
                    CREATE UNIQUE INDEX IF NOT EXISTS IX_MachineStatuses_Sector_StatusCode
                    ON MachineStatuses(COALESCE(SectorId, 0), StatusCode);

                    CREATE INDEX IF NOT EXISTS IX_MachineStatuses_SectorId
                    ON MachineStatuses(SectorId);

                    CREATE INDEX IF NOT EXISTS IX_MachineStatuses_StatusCode
                    ON MachineStatuses(StatusCode);

                    CREATE INDEX IF NOT EXISTS IX_Ec2MachineCurrentState_ImportId
                    ON Ec2MachineCurrentState(ImportId);

                    CREATE INDEX IF NOT EXISTS IX_Ec2MachineCurrentState_SourceType_Stale
                    ON Ec2MachineCurrentState(SourceType, IsStale);"
                );

                SeedMachineStatuses(conn);
                SeedDadMachineStatuses(conn);
                SeedProductionPartCodeStyles(conn);
                NormalizeProductionStatuses(conn);
                NormalizeKnownMachineStatusLabels(conn);
                _ensured = true;
            }
        }

        private static void EnsureMachineColumns(IDbConnection conn)
        {
            EnsureColumn(conn, "Machines", "MachineCode", "TEXT");
            EnsureColumn(conn, "Machines", "MachineKey", "TEXT");
            EnsureColumn(conn, "Machines", "LineCode", "TEXT");
            EnsureColumn(conn, "Machines", "LocalId", "INTEGER");
            EnsureColumn(conn, "Machines", "SectorId", "INTEGER");
            EnsureColumn(conn, "Machines", "IsActive", "INTEGER NOT NULL DEFAULT 1");
        }

        private static void EnsureColumn(IDbConnection conn, string tableName, string columnName, string definition)
        {
            var exists = conn.ExecuteScalar<int>(
                $@"
                    SELECT COUNT(1)
                    FROM pragma_table_info('{tableName}')
                    WHERE name = @columnName;",
                new
                {
                    columnName
                }
            ) > 0;

            if (!exists)
            {
                conn.Execute($"ALTER TABLE {tableName} ADD COLUMN {columnName} {definition};");
            }
        }

        private static void EnsureMachineStatusColumns(IDbConnection conn)
        {
            EnsureColumn(conn, "MachineStatuses", "SectorId", "INTEGER");
            EnsureColumn(conn, "MachineStatuses", "Classification", "TEXT NOT NULL DEFAULT 'StopCounts'");
        }

        private static void EnsureEc2Columns(IDbConnection conn)
        {
            EnsureColumn(conn, "Ec2MachineCurrentState", "StoppedSinceAt", "TEXT");
            EnsureColumn(conn, "Ec2AdministratorImports", "SourceType", "TEXT NOT NULL DEFAULT 'Administrator'");
            EnsureColumn(conn, "Ec2MachineCurrentState", "ImportId", "INTEGER");
            EnsureColumn(conn, "Ec2MachineCurrentState", "SourceType", "TEXT NOT NULL DEFAULT 'Administrator'");
            EnsureColumn(conn, "Ec2MachineCurrentState", "IsStale", "INTEGER NOT NULL DEFAULT 0");

            conn.Execute(
                @"
                    UPDATE Ec2AdministratorImports
                    SET SourceType = 'Administrator'
                    WHERE trim(COALESCE(SourceType, '')) = '';

                    UPDATE Ec2MachineCurrentState
                    SET SourceType = 'Administrator'
                    WHERE trim(COALESCE(SourceType, '')) = '';");
        }

        private static void EnsureEc2CurrentStateMachineIdKey(IDbConnection conn)
        {
            var machineCodePk = conn.ExecuteScalar<int>(
                @"
                    SELECT COUNT(1)
                    FROM pragma_table_info('Ec2MachineCurrentState')
                    WHERE name = 'MachineCode'
                      AND pk > 0;") > 0;

            if (!machineCodePk)
            {
                return;
            }

            conn.Execute(
                @"
                    DROP TABLE IF EXISTS Ec2MachineCurrentState_New;

                    CREATE TABLE Ec2MachineCurrentState_New (
                        MachineId INTEGER PRIMARY KEY,
                        MachineCode TEXT NOT NULL,
                        ImportId INTEGER,
                        SourceType TEXT NOT NULL DEFAULT 'Administrator',
                        IsStale INTEGER NOT NULL DEFAULT 0,
                        SectorId INTEGER,
                        LocalId INTEGER,
                        AreaLabel TEXT,
                        MachineName TEXT,
                        StatusText TEXT,
                        IsRunning INTEGER NOT NULL DEFAULT 0,
                        IsIgnored INTEGER NOT NULL DEFAULT 0,
                        IgnoreReason TEXT,
                        PartCode TEXT,
                        PlannedProcessMinutes REAL,
                        CapabilityType TEXT,
                        OperationRate REAL,
                        CurrentDifference REAL,
                        LotNo TEXT,
                        PlannedEndAt TEXT,
                        ProcessMinutes REAL,
                        DailyProduction REAL,
                        RawColumnsJson TEXT,
                        SnapshotAt TEXT NOT NULL,
                        ImportedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        LastSeenAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        StoppedSinceAt TEXT,
                        FOREIGN KEY (ImportId) REFERENCES Ec2AdministratorImports(Id),
                        FOREIGN KEY (MachineId) REFERENCES Machines(Id),
                        FOREIGN KEY (SectorId) REFERENCES Sectors(Id),
                        FOREIGN KEY (LocalId) REFERENCES Locals(Id)
                    );

                    INSERT OR REPLACE INTO Ec2MachineCurrentState_New
                    (
                        MachineId,
                        MachineCode,
                        ImportId,
                        SourceType,
                        IsStale,
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
                    SELECT
                        MachineId,
                        MachineCode,
                        ImportId,
                        SourceType,
                        IsStale,
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
                    FROM Ec2MachineCurrentState
                    WHERE COALESCE(MachineId, 0) > 0;

                    DROP TABLE Ec2MachineCurrentState;
                    ALTER TABLE Ec2MachineCurrentState_New RENAME TO Ec2MachineCurrentState;

                    CREATE INDEX IF NOT EXISTS IX_Ec2MachineCurrentState_Area
                    ON Ec2MachineCurrentState(AreaLabel);

                    CREATE INDEX IF NOT EXISTS IX_Ec2MachineCurrentState_MachineId
                    ON Ec2MachineCurrentState(MachineId);

                    CREATE INDEX IF NOT EXISTS IX_Ec2MachineCurrentState_Status
                    ON Ec2MachineCurrentState(StatusText);

                    CREATE INDEX IF NOT EXISTS IX_Ec2MachineCurrentState_PartCode
                    ON Ec2MachineCurrentState(PartCode);

                    CREATE INDEX IF NOT EXISTS IX_Ec2MachineCurrentState_ImportId
                    ON Ec2MachineCurrentState(ImportId);

                    CREATE INDEX IF NOT EXISTS IX_Ec2MachineCurrentState_SourceType_Stale
                    ON Ec2MachineCurrentState(SourceType, IsStale);");
        }

        private static void RebuildMachineStatusesForSectorScope(IDbConnection conn)
        {
            var createSql = conn.ExecuteScalar<string>(
                @"
                    SELECT COALESCE(sql, '')
                    FROM sqlite_master
                    WHERE type = 'table'
                      AND name = 'MachineStatuses';"
            ) ?? string.Empty;

            if (!createSql.Contains("StatusCode INTEGER NOT NULL UNIQUE", System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            conn.Execute(
                @"
                    DROP INDEX IF EXISTS IX_MachineStatuses_StatusCode;

                    CREATE TABLE IF NOT EXISTS MachineStatuses_SectorMigration (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        SectorId INTEGER,
                        StatusCode INTEGER NOT NULL,
                        DisplayCode INTEGER NOT NULL,
                        Classification TEXT NOT NULL DEFAULT 'StopCounts',
                        NamePt TEXT NOT NULL,
                        NameJp TEXT NOT NULL,
                        ColorHex TEXT NOT NULL DEFAULT '#5B88E8',
                        TextColorHex TEXT NOT NULL DEFAULT '#FFFFFF',
                        SortOrder INTEGER NOT NULL DEFAULT 0,
                        IsActive INTEGER NOT NULL DEFAULT 1,
                        FOREIGN KEY (SectorId) REFERENCES Sectors(Id)
                    );

                    INSERT OR IGNORE INTO MachineStatuses_SectorMigration
                    (
                        Id,
                        SectorId,
                        StatusCode,
                        DisplayCode,
                        Classification,
                        NamePt,
                        NameJp,
                        ColorHex,
                        TextColorHex,
                        SortOrder,
                        IsActive
                    )
                    SELECT
                        Id,
                        SectorId,
                        StatusCode,
                        DisplayCode,
                        CASE
                            WHEN trim(COALESCE(Classification, '')) = '' THEN
                                CASE DisplayCode
                                    WHEN 0 THEN 'Running'
                                    WHEN 4 THEN 'Error'
                                    ELSE 'StopCounts'
                                END
                            ELSE Classification
                        END,
                        NamePt,
                        NameJp,
                        ColorHex,
                        TextColorHex,
                        SortOrder,
                        IsActive
                    FROM MachineStatuses;

                    DROP TABLE MachineStatuses;

                    ALTER TABLE MachineStatuses_SectorMigration
                    RENAME TO MachineStatuses;"
            );

        }

        private static void DropLegacyMachineStatusCodeIndex(IDbConnection conn)
        {
            var indexSql = conn.ExecuteScalar<string>(
                @"
                    SELECT COALESCE(sql, '')
                    FROM sqlite_master
                    WHERE type = 'index'
                      AND name = 'IX_MachineStatuses_StatusCode';"
            ) ?? string.Empty;

            if (indexSql.Contains("UNIQUE", System.StringComparison.OrdinalIgnoreCase))
            {
                conn.Execute("DROP INDEX IF EXISTS IX_MachineStatuses_StatusCode;");
            }
        }

        private static void NormalizeProductionStatuses(IDbConnection conn)
        {
            conn.Execute(
                @"
                    UPDATE MachineCurrentStatus
                    SET StatusCode = CAST(trim(InternalState) AS INTEGER)
                    WHERE trim(COALESCE(InternalState, '')) GLOB '[0-9]*'
                      AND StatusCode <> CAST(trim(InternalState) AS INTEGER);

                    UPDATE MachineStatuses
                    SET
                        DisplayCode = 0,
                        Classification = 'Running',
                        NamePt = CASE WHEN trim(COALESCE(NamePt, '')) = '' THEN 'Rodando' ELSE NamePt END,
                        NameJp = CASE WHEN trim(COALESCE(NameJp, '')) = '' THEN '稼働中' ELSE NameJp END,
                        ColorHex = CASE WHEN trim(COALESCE(ColorHex, '')) = '' THEN '#5B88E8' ELSE ColorHex END,
                        TextColorHex = CASE WHEN trim(COALESCE(TextColorHex, '')) = '' THEN '#FFFFFF' ELSE TextColorHex END
                    WHERE SectorId IS NULL AND StatusCode = 0;

                    UPDATE MachineStatuses
                    SET
                        DisplayCode = 1,
                        Classification = CASE
                            WHEN trim(COALESCE(Classification, '')) = '' THEN 'StopCounts'
                            ELSE Classification
                        END,
                        NamePt = CASE WHEN trim(COALESCE(NamePt, '')) = '' THEN 'Inativo' ELSE NamePt END,
                        NameJp = CASE WHEN trim(COALESCE(NameJp, '')) = '' THEN '非稼働' ELSE NameJp END,
                        ColorHex = CASE WHEN trim(COALESCE(ColorHex, '')) = '' THEN '#EF6F63' ELSE ColorHex END,
                        TextColorHex = CASE WHEN trim(COALESCE(TextColorHex, '')) = '' THEN '#FFFFFF' ELSE TextColorHex END
                    WHERE SectorId IS NULL AND StatusCode = 1;

                    UPDATE MachineStatuses
                    SET
                        DisplayCode = 3,
                        Classification = CASE
                            WHEN trim(COALESCE(Classification, '')) = '' THEN 'StopCounts'
                            ELSE Classification
                        END,
                        NamePt = CASE WHEN trim(COALESCE(NamePt, '')) = '' THEN 'Parado' ELSE NamePt END,
                        NameJp = CASE WHEN trim(COALESCE(NameJp, '')) = '' THEN '停止' ELSE NameJp END,
                        ColorHex = CASE WHEN trim(COALESCE(ColorHex, '')) = '' THEN '#F2CB58' ELSE ColorHex END,
                        TextColorHex = CASE WHEN trim(COALESCE(TextColorHex, '')) = '' THEN '#4A3200' ELSE TextColorHex END
                    WHERE SectorId IS NULL AND StatusCode = 3;

                    UPDATE MachineStatuses
                    SET
                        DisplayCode = 4,
                        Classification = 'Error',
                        NamePt = CASE WHEN trim(COALESCE(NamePt, '')) = '' THEN 'Erro' ELSE NamePt END,
                        NameJp = CASE WHEN trim(COALESCE(NameJp, '')) = '' THEN 'エラー' ELSE NameJp END,
                        ColorHex = CASE WHEN trim(COALESCE(ColorHex, '')) = '' THEN '#FFFFFF' ELSE ColorHex END,
                        TextColorHex = CASE WHEN trim(COALESCE(TextColorHex, '')) = '' THEN '#516174' ELSE TextColorHex END
                    WHERE SectorId IS NULL AND StatusCode = 4;

                    UPDATE MachineStatuses
                    SET Classification = CASE DisplayCode
                        WHEN 0 THEN 'Running'
                        WHEN 4 THEN 'Error'
                        ELSE 'StopCounts'
                    END
                    WHERE trim(COALESCE(Classification, '')) = '';"
            );
        }

        private static void NormalizeKnownMachineStatusLabels(IDbConnection conn)
        {
            var gBareruSectorId = ResolveSectorId(conn, "G-BARERU");
            var dadSectorId = ResolveSectorId(conn, "DAD");
            conn.Execute(
                @"
                    UPDATE MachineStatuses
                    SET NameJp = '稼働中', NamePt = 'Rodando', DisplayCode = 0, Classification = 'Running'
                    WHERE (SectorId IS NULL OR SectorId = @GBareruSectorId)
                      AND StatusCode = 0;

                    UPDATE MachineStatuses
                    SET NameJp = '停止中'
                    WHERE SectorId = @GBareruSectorId
                      AND StatusCode = 1;

                    UPDATE MachineStatuses
                    SET NameJp = 'レス処理'
                    WHERE SectorId = @GBareruSectorId
                      AND StatusCode IN (2, 17);

                    UPDATE MachineStatuses
                    SET NameJp = '停止中', DisplayCode = 3
                    WHERE SectorId = @GBareruSectorId
                      AND StatusCode = 3;

                    UPDATE MachineStatuses
                    SET NameJp = '吸引時間'
                    WHERE SectorId = @GBareruSectorId
                      AND StatusCode = 18;

                    UPDATE MachineStatuses
                    SET NameJp = 'サンプル'
                    WHERE SectorId = @GBareruSectorId
                      AND StatusCode = 19;

                    UPDATE MachineStatuses
                    SET NameJp = 'レス処理'
                    WHERE SectorId IS NULL
                      AND StatusCode = 17;

                    UPDATE MachineStatuses
                    SET NameJp = '吸引時間'
                    WHERE SectorId IS NULL
                      AND StatusCode = 18;

                    UPDATE MachineStatuses
                    SET NameJp = 'サンプル'
                    WHERE SectorId IS NULL
                      AND StatusCode = 19;

                    UPDATE MachineStatuses
                    SET NameJp = '運転'
                    WHERE SectorId = @DadSectorId
                      AND StatusCode = 0;

                    UPDATE MachineStatuses
                    SET NameJp = '停止中'
                    WHERE SectorId = @DadSectorId
                      AND StatusCode = 1;

                    UPDATE MachineStatuses
                    SET NameJp = '清掃'
                    WHERE SectorId = @DadSectorId
                      AND StatusCode = 3;

                    UPDATE MachineStatuses
                    SET NameJp = '異常'
                    WHERE SectorId = @DadSectorId
                      AND StatusCode = 4;

                    UPDATE MachineStatuses
                    SET NameJp = 'レス処理'
                    WHERE SectorId = @DadSectorId
                      AND StatusCode = 17;

                    UPDATE MachineStatuses
                    SET NameJp = '吸引時間'
                    WHERE SectorId = @DadSectorId
                      AND StatusCode = 18;

                    UPDATE MachineStatuses
                    SET NameJp = 'サンプル'
                    WHERE SectorId = @DadSectorId
                      AND StatusCode = 19;"
                , new { GBareruSectorId = gBareruSectorId, DadSectorId = dadSectorId }
            );
        }

        private static void SeedMachineStatuses(IDbConnection conn)
        {
            conn.Execute(
                @"
                    INSERT OR IGNORE INTO MachineStatuses
                    (StatusCode, DisplayCode, NamePt, NameJp, ColorHex, TextColorHex, SortOrder, IsActive)
                    VALUES
                    (0, 0, 'Rodando', '稼働中', '#5B88E8', '#FFFFFF', 0, 1),
                    (1, 1, 'Inativo', '非稼働', '#EF6F63', '#FFFFFF', 1, 1),
                    (3, 3, 'Parado', '停止', '#F2CB58', '#4A3200', 3, 1),
                    (4, 4, 'Erro', 'エラー', '#FFFFFF', '#516174', 4, 1);"
            );
        }

        private static void SeedDadMachineStatuses(IDbConnection conn)
        {
            var dadSectorId = ResolveSectorId(conn, "DAD");
            conn.Execute(
                @"
                    INSERT OR IGNORE INTO MachineStatuses
                    (SectorId, StatusCode, DisplayCode, Classification, NamePt, NameJp, ColorHex, TextColorHex, SortOrder, IsActive)
                    VALUES
                    (@DadSectorId, 0, 0, 'Running', 'Rodando', '運転', '#5B88E8', '#FFFFFF', 0, 1),
                    (@DadSectorId, 1, 3, 'StopCounts', 'Parado DAD', '停止中', '#F2CB58', '#4A3200', 1, 1),
                    (@DadSectorId, 3, 3, 'StopNoCount', 'Limpeza programada', '清掃', '#8EC5A8', '#123524', 3, 1),
                    (@DadSectorId, 4, 4, 'Error', 'Erro', '異常', '#FFFFFF', '#516174', 4, 1),
                    (@DadSectorId, 17, 1, 'StopNoCount', 'Intervalo', 'レス処理', '#8EC5A8', '#123524', 17, 1),
                    (@DadSectorId, 18, 1, 'StopNoCount', 'Limpeza programada', '吸引時間', '#8EC5A8', '#123524', 18, 1),
                    (@DadSectorId, 19, 1, 'StopNoCount', 'Amostra', 'サンプル', '#8EC5A8', '#123524', 19, 1);",
                new { DadSectorId = dadSectorId }
            );

            NormalizeDadMachineStatusSeeds(conn);
        }

        private static void SeedProductionPartCodeStyles(IDbConnection conn)
        {
            conn.Execute(
                @"
                    INSERT OR IGNORE INTO ProductionPartCodeStyles
                    (PartCode, ColorHex, TextColorHex, Description, IsActive)
                    VALUES
                    ('RJ2A7', '#D93F3F', '#FFFFFF', 'Destaque EC2 Administrator', 1);"
            );
        }

        private static void NormalizeDadMachineStatusSeeds(IDbConnection conn)
        {
            var dadSectorId = ResolveSectorId(conn, "DAD");
            conn.Execute(
                @"
                    UPDATE MachineStatuses
                    SET
                        DisplayCode = 0,
                        Classification = 'Running',
                        NamePt = CASE WHEN trim(COALESCE(NamePt, '')) = '' THEN 'Rodando' ELSE NamePt END,
                        ColorHex = CASE WHEN trim(COALESCE(ColorHex, '')) = '' THEN '#5B88E8' ELSE ColorHex END,
                        TextColorHex = CASE WHEN trim(COALESCE(TextColorHex, '')) = '' THEN '#FFFFFF' ELSE TextColorHex END,
                        IsActive = 1
                    WHERE SectorId = @DadSectorId AND StatusCode = 0;

                    UPDATE MachineStatuses
                    SET
                        DisplayCode = 3,
                        Classification = 'StopCounts',
                        NamePt = CASE WHEN trim(COALESCE(NamePt, '')) = '' THEN 'Parado DAD' ELSE NamePt END,
                        ColorHex = CASE WHEN trim(COALESCE(ColorHex, '')) = '' THEN '#F2CB58' ELSE ColorHex END,
                        TextColorHex = CASE WHEN trim(COALESCE(TextColorHex, '')) = '' THEN '#4A3200' ELSE TextColorHex END,
                        IsActive = 1
                    WHERE SectorId = @DadSectorId AND StatusCode = 1;

                    UPDATE MachineStatuses
                    SET
                        DisplayCode = 3,
                        Classification = 'StopNoCount',
                        NamePt = CASE WHEN trim(COALESCE(NamePt, '')) = '' THEN 'Limpeza programada' ELSE NamePt END,
                        ColorHex = CASE WHEN trim(COALESCE(ColorHex, '')) = '' THEN '#8EC5A8' ELSE ColorHex END,
                        TextColorHex = CASE WHEN trim(COALESCE(TextColorHex, '')) = '' THEN '#123524' ELSE TextColorHex END,
                        IsActive = 1
                    WHERE SectorId = @DadSectorId AND StatusCode = 3;

                    UPDATE MachineStatuses
                    SET
                        DisplayCode = 4,
                        Classification = 'Error',
                        NamePt = CASE WHEN trim(COALESCE(NamePt, '')) = '' THEN 'Erro' ELSE NamePt END,
                        ColorHex = CASE WHEN trim(COALESCE(ColorHex, '')) = '' THEN '#FFFFFF' ELSE ColorHex END,
                        TextColorHex = CASE WHEN trim(COALESCE(TextColorHex, '')) = '' THEN '#516174' ELSE TextColorHex END,
                        IsActive = 1
                    WHERE SectorId = @DadSectorId AND StatusCode = 4;

                    UPDATE MachineStatuses
                    SET
                        DisplayCode = 1,
                        Classification = 'StopNoCount',
                        ColorHex = CASE WHEN trim(COALESCE(ColorHex, '')) = '' THEN '#8EC5A8' ELSE ColorHex END,
                        TextColorHex = CASE WHEN trim(COALESCE(TextColorHex, '')) = '' THEN '#123524' ELSE TextColorHex END,
                        IsActive = 1
                    WHERE SectorId = @DadSectorId AND StatusCode IN (17, 18, 19);",
                new { DadSectorId = dadSectorId }
            );
        }

        private static int ResolveSectorId(IDbConnection conn, string code)
        {
            return conn.QuerySingleOrDefault<int?>(
                    @"SELECT Id
                      FROM Sectors
                      WHERE Code = @Code COLLATE NOCASE
                        AND IsActive = 1
                      LIMIT 1;",
                    new { Code = code })
                ?? throw new InvalidOperationException($"Setor {code} nao configurado.");
        }
    }
}
