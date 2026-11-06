ALTER TABLE Locals ADD COLUMN ShortCode TEXT;

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
ON HaidaiAssignments(ScheduleDate, ShiftId, SectorId);

CREATE TABLE IF NOT EXISTS HaidaiMovements (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ScheduleDate TEXT NOT NULL,
    ShiftId INTEGER NOT NULL,
    SectorId INTEGER NOT NULL,
    OperatorCodigoFJ TEXT NOT NULL,
    MovementType TEXT NOT NULL,
    EventTime TEXT,
    EventDateTime TEXT,
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
ON HaidaiMovements(ScheduleDate, ShiftId, SectorId, OperatorCodigoFJ);

CREATE TABLE IF NOT EXISTS MasterCards (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    OperatorCodigoFJ TEXT NOT NULL,
    TrainerCodigoFJ TEXT NOT NULL,
    SectorId INTEGER NOT NULL,
    EquipmentId INTEGER NOT NULL,
    ProcessDescription TEXT NOT NULL,
    Notes TEXT,
    StartDate DATE NOT NULL,
    Status TEXT NOT NULL DEFAULT 'in_progress',
    ConcludedAt DATETIME,
    FollowDate DATE,
    FinalizedAt DATETIME,
    CreatedByCodigoFJ TEXT NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (OperatorCodigoFJ) REFERENCES Operators(CodigoFJ),
    FOREIGN KEY (TrainerCodigoFJ) REFERENCES Operators(CodigoFJ),
    FOREIGN KEY (SectorId) REFERENCES Sectors(Id),
    FOREIGN KEY (EquipmentId) REFERENCES Equipments(Id),
    FOREIGN KEY (CreatedByCodigoFJ) REFERENCES Operators(CodigoFJ)
);

CREATE TABLE IF NOT EXISTS MasterCardStatusHistory (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    MasterCardId INTEGER NOT NULL,
    PreviousStatus TEXT,
    NewStatus TEXT NOT NULL,
    ChangedByCodigoFJ TEXT NOT NULL,
    ChangedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    Note TEXT,
    FOREIGN KEY (MasterCardId) REFERENCES MasterCards(Id),
    FOREIGN KEY (ChangedByCodigoFJ) REFERENCES Operators(CodigoFJ)
);

CREATE INDEX IF NOT EXISTS IX_MasterCards_Status_StartDate
ON MasterCards(Status, StartDate);

CREATE INDEX IF NOT EXISTS IX_MasterCards_FollowDate
ON MasterCards(FollowDate, Status);

CREATE INDEX IF NOT EXISTS IX_MasterCards_Operator_Trainer
ON MasterCards(OperatorCodigoFJ, TrainerCodigoFJ);

CREATE INDEX IF NOT EXISTS IX_MasterCards_ReportScope
ON MasterCards(SectorId, EquipmentId, Status, StartDate);

CREATE INDEX IF NOT EXISTS IX_MasterCardStatusHistory_MasterCardId
ON MasterCardStatusHistory(MasterCardId, ChangedAt);

CREATE INDEX IF NOT EXISTS IX_SystemLog_Timestamp
ON SystemLog(Timestamp DESC, Id DESC);

CREATE INDEX IF NOT EXISTS IX_SystemLog_ModuleAction
ON SystemLog(Module, Action, Timestamp DESC);
