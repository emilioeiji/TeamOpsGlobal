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

CREATE INDEX IF NOT EXISTS IX_MachineEvents_SectorLocal_EventTime
ON MachineEvents(SectorId, LocalId, EventDateTime);

CREATE INDEX IF NOT EXISTS IX_MachineCurrentStatus_LocalSector
ON MachineCurrentStatus(LocalId, SectorId);

CREATE INDEX IF NOT EXISTS IX_Machines_MachineCode
ON Machines(MachineCode);

CREATE INDEX IF NOT EXISTS IX_Machines_LocalId
ON Machines(LocalId);

CREATE INDEX IF NOT EXISTS IX_Machines_SectorId
ON Machines(SectorId);

CREATE INDEX IF NOT EXISTS IX_Locals_Sector_Name
ON Locals(SectorId, NamePt, Id);

CREATE INDEX IF NOT EXISTS IX_Operators_Status_Shift_Sector_Name
ON Operators(Status, ShiftId, SectorId, NameRomanji);

CREATE UNIQUE INDEX IF NOT EXISTS IX_ProductionPlanSnapshots_SourceFile_ExportedAt
ON ProductionPlanSnapshots(SourceFile, ExportedAt);

CREATE UNIQUE INDEX IF NOT EXISTS IX_ProductionPlanRows_Snapshot_Area_Machine
ON ProductionPlanRows(SnapshotId, AreaLabel, MachineCode);

CREATE INDEX IF NOT EXISTS IX_ProductionPlanRows_MachineCode
ON ProductionPlanRows(MachineCode);

CREATE INDEX IF NOT EXISTS IX_Ec2MachineCurrentState_ImportId
ON Ec2MachineCurrentState(ImportId);

CREATE INDEX IF NOT EXISTS IX_Ec2MachineCurrentState_SourceType_Stale
ON Ec2MachineCurrentState(SourceType, IsStale);

INSERT OR IGNORE INTO MachineStatuses
    (SectorId, StatusCode, DisplayCode, Classification, NamePt, NameJp,
     ColorHex, TextColorHex, SortOrder, IsActive)
SELECT sector.Id, seed.StatusCode, seed.DisplayCode, seed.Classification,
       seed.NamePt, seed.NameJp, seed.ColorHex, seed.TextColorHex,
       seed.SortOrder, 1
FROM Sectors sector
CROSS JOIN (
    SELECT 0 AS StatusCode, 0 AS DisplayCode, 'Running' AS Classification,
           'Rodando' AS NamePt, '運転' AS NameJp,
           '#5B88E8' AS ColorHex, '#FFFFFF' AS TextColorHex, 0 AS SortOrder
    UNION ALL SELECT 1, 3, 'StopCounts', 'Parado DAD', '停止中', '#F2CB58', '#4A3200', 1
    UNION ALL SELECT 3, 3, 'StopNoCount', 'Limpeza programada', '清掃', '#8EC5A8', '#123524', 3
    UNION ALL SELECT 4, 4, 'Error', 'Erro', '異常', '#FFFFFF', '#516174', 4
    UNION ALL SELECT 17, 1, 'StopNoCount', 'Intervalo', 'レス処理', '#8EC5A8', '#123524', 17
    UNION ALL SELECT 18, 1, 'StopNoCount', 'Limpeza programada', '吸引時間', '#8EC5A8', '#123524', 18
    UNION ALL SELECT 19, 1, 'StopNoCount', 'Amostra', 'サンプル', '#8EC5A8', '#123524', 19
) seed
WHERE sector.Code = 'DAD' COLLATE NOCASE
  AND sector.IsActive = 1;
