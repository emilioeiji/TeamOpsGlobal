-- File: TeamOps.Data/Migrations/InitialSchema.sql

CREATE TABLE IF NOT EXISTS Operators (
    CodigoFJ    TEXT     PRIMARY KEY,              -- Código único do operador
    NameRomanji TEXT     NOT NULL,                 -- Nome em Romanji
    NameNihongo TEXT     NOT NULL,                 -- Nome em Kanji/Katakana
    ShiftId     INTEGER  NOT NULL,                 -- FK para Shifts
    GroupId     INTEGER  NOT NULL,                 -- FK para Groups
    SectorId    INTEGER  NOT NULL,                 -- FK para Sectors
    StartDate   DATE     NOT NULL,
    EndDate     DATE,
    Trainer     BOOLEAN  NOT NULL DEFAULT 0,       -- Se é treinador
    Status      BOOLEAN  NOT NULL DEFAULT 1,       -- Ativo/Inativo
    CreatedAt   DATETIME DEFAULT CURRENT_TIMESTAMP,
    IsLeader    INTEGER  NOT NULL DEFAULT 0,
    Telefone    TEXT,
    Endereco    TEXT,
    Nascimento  DATE,
    BadgeCode   TEXT,

    FOREIGN KEY (ShiftId) REFERENCES Shifts(Id),
    FOREIGN KEY (GroupId) REFERENCES Groups(Id),
    FOREIGN KEY (SectorId) REFERENCES Sectors(Id)
);
-- ALTER TABLE Operators ADD COLUMN Telefone TEXT;
-- ALTER TABLE Operators ADD COLUMN Endereco TEXT;

 
CREATE TABLE IF NOT EXISTS Shifts (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    NamePt TEXT NOT NULL,
    NameJp TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS Groups (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    NamePt TEXT NOT NULL,
    NameJp TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS Sectors (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Code TEXT UNIQUE,
    NamePt TEXT NOT NULL,
    NameJp TEXT NOT NULL,
    OrganizationNodeId INTEGER UNIQUE,
    IsActive INTEGER NOT NULL DEFAULT 1,
    FOREIGN KEY (OrganizationNodeId) REFERENCES OrganizationNodes(Id)
);

CREATE TABLE IF NOT EXISTS Users (
    Id           INTEGER  PRIMARY KEY AUTOINCREMENT,
    Login        TEXT     UNIQUE,
    CodigoFJ     TEXT,
    Name         TEXT,
    PasswordHash TEXT     NOT NULL,
    CreatedAt    DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (
        CodigoFJ
    )
    REFERENCES Operators (CodigoFJ) 
);

CREATE TABLE IF NOT EXISTS OperatorLeaderAssignments (
    LeaderCodigoFJ   TEXT NOT NULL,
    OperatorCodigoFJ TEXT NOT NULL,
    AssignedAt       DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    AssignedByUserId INTEGER,
    PRIMARY KEY (LeaderCodigoFJ, OperatorCodigoFJ),
    CHECK (LeaderCodigoFJ <> OperatorCodigoFJ),
    FOREIGN KEY (LeaderCodigoFJ) REFERENCES Operators(CodigoFJ) ON DELETE CASCADE,
    FOREIGN KEY (OperatorCodigoFJ) REFERENCES Operators(CodigoFJ) ON DELETE CASCADE,
    FOREIGN KEY (AssignedByUserId) REFERENCES Users(Id) ON DELETE SET NULL
);

CREATE TABLE IF NOT EXISTS Locals (
    Id       INTEGER PRIMARY KEY AUTOINCREMENT,
    NamePt   TEXT NOT NULL,
    NameJp   TEXT NOT NULL,
    SectorId INTEGER NOT NULL,
    FOREIGN KEY (SectorId) REFERENCES Sectors(Id)
);

CREATE TABLE IF NOT EXISTS FollowUpReasons (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    NamePt TEXT NOT NULL,
    NameJp TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS FollowUpTypes (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    NamePt TEXT NOT NULL,
    NameJp TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS Equipments (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    NamePt TEXT NOT NULL,
    NameJp TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS FollowUps (
    Id               INTEGER  PRIMARY KEY AUTOINCREMENT,
    Date             DATETIME NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    ShiftId          INTEGER  NOT NULL,
    OperatorCodigoFJ TEXT     NOT NULL,
    ExecutorCodigoFJ TEXT     NOT NULL,
    WitnessCodigoFJ  TEXT,
    ReasonId         INTEGER  NOT NULL,
    TypeId           INTEGER  NOT NULL,
    LocalId          INTEGER  NOT NULL,
    EquipmentId      INTEGER  NOT NULL,
    SectorId         INTEGER  NOT NULL,
    Description      TEXT     NOT NULL,
    Guidance         TEXT     NOT NULL,
    FOREIGN KEY (ShiftId)          REFERENCES Shifts (Id),
    FOREIGN KEY (OperatorCodigoFJ) REFERENCES Operators (CodigoFJ),
    FOREIGN KEY (ExecutorCodigoFJ) REFERENCES Operators (CodigoFJ),
    FOREIGN KEY (WitnessCodigoFJ)  REFERENCES Operators (CodigoFJ),
    FOREIGN KEY (ReasonId)         REFERENCES FollowUpReasons (Id),
    FOREIGN KEY (TypeId)           REFERENCES FollowUpTypes (Id),
    FOREIGN KEY (LocalId)          REFERENCES Locals (Id),
    FOREIGN KEY (EquipmentId)      REFERENCES Equipments (Id),
    FOREIGN KEY (SectorId)         REFERENCES Sectors (Id)
);

CREATE TABLE IF NOT EXISTS Categories (
    Id     INTEGER PRIMARY KEY AUTOINCREMENT,
    NamePt TEXT    NOT NULL,
    NameJp TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS Hikitsugui (
    Id              INTEGER  PRIMARY KEY AUTOINCREMENT,
    Date            DATETIME NOT NULL,
    ShiftId         INTEGER  NOT NULL,
    CreatorCodigoFJ TEXT     NOT NULL,
    CategoryId      INTEGER  NOT NULL,
    EquipmentId     INTEGER,
    LocalId         INTEGER,
    SectorId        INTEGER,   -- NOVO
    ForLeaders      INTEGER  NOT NULL,
    ForOperators    INTEGER  NOT NULL,
    Description     TEXT     NOT NULL,
    AttachmentPath  TEXT,
    ForMaSv         INTEGER  NOT NULL DEFAULT 0,
    FOREIGN KEY (ShiftId)         REFERENCES Shifts (Id),
    FOREIGN KEY (CreatorCodigoFJ) REFERENCES Operators (CodigoFJ),
    FOREIGN KEY (CategoryId)      REFERENCES Categories (Id),
    FOREIGN KEY (EquipmentId)     REFERENCES Equipments (Id),
    FOREIGN KEY (LocalId)         REFERENCES Locals (Id),
    FOREIGN KEY (SectorId)        REFERENCES Sectors (Id)
);
-- ALTER TABLE Hikitsugui ADD COLUMN ForMaSv INTEGER NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS HikitsuguiResponses (
    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
    HikitsuguiId    INTEGER NOT NULL,
    Date            DATETIME NOT NULL,
    ResponderCodigoFJ TEXT NOT NULL,
    Message         TEXT NOT NULL,
    FOREIGN KEY (HikitsuguiId) REFERENCES Hikitsugui(Id),
    FOREIGN KEY (ResponderCodigoFJ) REFERENCES Operators(CodigoFJ)
);

CREATE TABLE IF NOT EXISTS HikitsuguiCorrections (
    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
    HikitsuguiId    INTEGER NOT NULL,
    Date            DATETIME NOT NULL,
    CorrectorCodigoFJ TEXT NOT NULL,
    Correction      TEXT NOT NULL,
    FOREIGN KEY (HikitsuguiId) REFERENCES Hikitsugui(Id),
    FOREIGN KEY (CorrectorCodigoFJ) REFERENCES Operators(CodigoFJ)
);

CREATE TABLE IF NOT EXISTS HikitsuguiReads (
    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
    HikitsuguiId    INTEGER NOT NULL,
    ReaderCodigoFJ  TEXT NOT NULL,
    ReadAt          DATETIME NOT NULL,
    FOREIGN KEY (HikitsuguiId) REFERENCES Hikitsugui(Id),
    FOREIGN KEY (ReaderCodigoFJ) REFERENCES Operators(CodigoFJ)
);

CREATE TABLE IF NOT EXISTS HikitsuguiAttachments (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    HikitsuguiId INTEGER NOT NULL,
    FileName TEXT NOT NULL,
    FilePath TEXT NOT NULL,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (HikitsuguiId) REFERENCES Hikitsugui(Id)
);

CREATE INDEX IF NOT EXISTS IX_Hikitsugui_OperatorRead
ON Hikitsugui(ForOperators, LocalId, Date);

CREATE INDEX IF NOT EXISTS IX_HikitsuguiReads_Reader_Hikitsugui
ON HikitsuguiReads(ReaderCodigoFJ, HikitsuguiId);

CREATE INDEX IF NOT EXISTS IX_HikitsuguiReads_Hikitsugui
ON HikitsuguiReads(HikitsuguiId);

CREATE INDEX IF NOT EXISTS IX_HikitsuguiAttachments_Hikitsugui
ON HikitsuguiAttachments(HikitsuguiId);

CREATE TABLE IF NOT EXISTS SobraDePeca (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Data TEXT NOT NULL,
    TurnoId INTEGER NOT NULL,
    Lote TEXT NOT NULL,
    OperadorId TEXT NOT NULL,
    Tanjuu REAL NOT NULL,
    PesoGramas REAL NOT NULL,
    Quantidade REAL NOT NULL,
    MachineId INTEGER NOT NULL,
    ShainId INTEGER NOT NULL,
    Observacao TEXT,
    Lider TEXT NOT NULL,
    CreatedAt TEXT NOT NULL,
    Item TEXT NOT NULL,

    FOREIGN KEY (TurnoId) REFERENCES Shifts(Id),
    FOREIGN KEY (OperadorId) REFERENCES Operators(CodigoFJ),
    FOREIGN KEY (MachineId) REFERENCES Machines(Id),
    FOREIGN KEY (ShainId) REFERENCES Shain(Id)
);

CREATE TABLE IF NOT EXISTS Shain (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    NameRomanji TEXT NOT NULL,
    NameNihongo TEXT,
    Ativo INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS PRCategorias (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    NamePt TEXT NOT NULL,
    NameJp TEXT NOT NULL
);
-- 1 Sobre Operacao
-- 2 Sobre Seguranca
-- 3 Sobre Trabalho
-- 4 Outros

CREATE TABLE IF NOT EXISTS PRPrioridades (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    NamePt TEXT NOT NULL,
    NameJp TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS PR (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,

    SetorId INTEGER NOT NULL,                 -- FK para Sectors
    CategoriaId INTEGER NOT NULL,             -- FK para PRCategorias
    PrioridadeId INTEGER NOT NULL,            -- FK para PRPrioridades

    Titulo TEXT NOT NULL,
    NomeArquivo TEXT NOT NULL,                -- Gerado automaticamente

    DataEmissao TEXT NOT NULL,                -- Data atual
    DataRetornoHiru TEXT,                     -- Para uso futuro
    DataRetornoYakin TEXT,                    -- Para uso futuro

    AutorCodigoFJ TEXT NOT NULL,              -- Nome do usuário (Operators)

    CreatedAt TEXT NOT NULL DEFAULT (CURRENT_TIMESTAMP),

    FOREIGN KEY (SetorId) REFERENCES Sectors(Id),
    FOREIGN KEY (CategoriaId) REFERENCES PRCategorias(Id),
    FOREIGN KEY (PrioridadeId) REFERENCES PRPrioridades(Id),
    FOREIGN KEY (AutorCodigoFJ) REFERENCES Operators(CodigoFJ)
);

CREATE TABLE IF NOT EXISTS CLCategorias (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    NamePt TEXT NOT NULL,
    NameJp TEXT NOT NULL
);
-- 1 Sobre Operacao
-- 2 Sobre Seguranca
-- 3 Sobre Trabalho
-- 4 Outros

CREATE TABLE IF NOT EXISTS CLPrioridades (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    NamePt TEXT NOT NULL,
    NameJp TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS CL (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,

    SetorId INTEGER NOT NULL,                 -- FK para Sectors
    CategoriaId INTEGER NOT NULL,             -- FK para PRCategorias
    PrioridadeId INTEGER NOT NULL,            -- FK para PRPrioridades

    Titulo TEXT NOT NULL,
    NomeArquivo TEXT NOT NULL,                -- Gerado automaticamente

    DataEmissao TEXT NOT NULL,                -- Data atual
    DataRetornoHiru TEXT,                     -- Para uso futuro
    DataRetornoYakin TEXT,                    -- Para uso futuro

    AutorCodigoFJ TEXT NOT NULL,              -- Nome do usuário (Operators)

    CreatedAt TEXT NOT NULL DEFAULT (CURRENT_TIMESTAMP),

    FOREIGN KEY (SetorId) REFERENCES Sectors(Id),
    FOREIGN KEY (CategoriaId) REFERENCES PRCategorias(Id),
    FOREIGN KEY (PrioridadeId) REFERENCES PRPrioridades(Id),
    FOREIGN KEY (AutorCodigoFJ) REFERENCES Operators(CodigoFJ)
);

CREATE TABLE IF NOT EXISTS Machines (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    NamePt TEXT NOT NULL,
    NameJp TEXT NOT NULL,
    MachineCode TEXT,
    MachineKey TEXT,
    LineCode TEXT,
    LocalId INTEGER,
    SectorId INTEGER,
    IsActive INTEGER NOT NULL DEFAULT 1,
    FOREIGN KEY (LocalId) REFERENCES Locals(Id),
    FOREIGN KEY (SectorId) REFERENCES Sectors(Id)
);

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

CREATE TABLE IF NOT EXISTS TodokeMotivo (
    Id      INTEGER PRIMARY KEY AUTOINCREMENT,
    NomePt  TEXT NOT NULL,
    NomeJp  TEXT NOT NULL
);

INSERT INTO TodokeMotivo (Id, NomePt, NomeJp)
SELECT 1, 'Yukyu', '有休'
WHERE NOT EXISTS (SELECT 1 FROM TodokeMotivo WHERE Id = 1);

INSERT INTO TodokeMotivo (Id, NomePt, NomeJp)
SELECT 2, 'Falta', '欠勤'
WHERE NOT EXISTS (SELECT 1 FROM TodokeMotivo WHERE Id = 2);

INSERT INTO TodokeMotivo (Id, NomePt, NomeJp)
SELECT 3, 'Atraso', '遅刻'
WHERE NOT EXISTS (SELECT 1 FROM TodokeMotivo WHERE Id = 3);

INSERT INTO TodokeMotivo (Id, NomePt, NomeJp)
SELECT 4, 'Saida Retorno', '外出'
WHERE NOT EXISTS (SELECT 1 FROM TodokeMotivo WHERE Id = 4);

INSERT INTO TodokeMotivo (Id, NomePt, NomeJp)
SELECT 5, 'Sair Cedo', '早退'
WHERE NOT EXISTS (SELECT 1 FROM TodokeMotivo WHERE Id = 5);

INSERT INTO TodokeMotivo (Id, NomePt, NomeJp)
SELECT 6, 'Shukin', '休日出勤'
WHERE NOT EXISTS (SELECT 1 FROM TodokeMotivo WHERE Id = 6);

INSERT INTO TodokeMotivo (Id, NomePt, NomeJp)
SELECT 7, 'Hr Extra', '残業'
WHERE NOT EXISTS (SELECT 1 FROM TodokeMotivo WHERE Id = 7);

INSERT INTO TodokeMotivo (Id, NomePt, NomeJp)
SELECT 8, 'Domingo', '法定休日'
WHERE NOT EXISTS (SELECT 1 FROM TodokeMotivo WHERE Id = 8);

CREATE TABLE IF NOT EXISTS AcompYukyu (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,

    OperatorCodigoFJ TEXT NOT NULL,
    RequestDate TEXT NOT NULL,

    AuthorizedByCodigoFJ TEXT,
    Notes TEXT,

    TodokeMotivoId INTEGER,
    Conferencia INTEGER NOT NULL DEFAULT 0,

    FOREIGN KEY (OperatorCodigoFJ) REFERENCES Operators (CodigoFJ),
    FOREIGN KEY (AuthorizedByCodigoFJ) REFERENCES Operators (CodigoFJ),
    FOREIGN KEY (TodokeMotivoId) REFERENCES TodokeMotivo (Id)
);

CREATE TABLE IF NOT EXISTS YukyuConferencia (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    AcompYukyuId INTEGER NOT NULL,
    TakenBy TEXT NOT NULL,
    TakenAt TEXT NOT NULL,

    FOREIGN KEY (AcompYukyuId) REFERENCES AcompYukyu(Id),
    FOREIGN KEY (TakenBy) REFERENCES Operators(CodigoFJ)
);

CREATE TABLE IF NOT EXISTS YukyuTodoke (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    AcompYukyuId INTEGER NOT NULL,
    TakenBy INTEGER NOT NULL,
    TakenAt TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS YukyuFolhaControle (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    AcompYukyuId INTEGER NOT NULL,
    TakenBy INTEGER NOT NULL,
    TakenAt TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS OperatorPresence (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    CodigoFJ TEXT NOT NULL,
    SectorId INTEGER NOT NULL,   -- reutilizado do Local
    LocalId INTEGER NOT NULL,
    ShiftId INTEGER NOT NULL,
    Date DATETIME NOT NULL,
    FOREIGN KEY (CodigoFJ) REFERENCES Operators(CodigoFJ),
    FOREIGN KEY (LocalId) REFERENCES Locals(Id),
    FOREIGN KEY (SectorId) REFERENCES Sectors(Id)
);

CREATE INDEX IF NOT EXISTS IX_OperatorPresence_DaySectorShiftOperator
ON OperatorPresence(date(Date), SectorId, ShiftId, CodigoFJ, Date);

CREATE INDEX IF NOT EXISTS IX_OperatorPresence_OperatorDay
ON OperatorPresence(CodigoFJ, date(Date), Date);

CREATE TABLE IF NOT EXISTS OperatorPositions (
    Id        INTEGER PRIMARY KEY AUTOINCREMENT,
    SectorId  INTEGER NOT NULL,
    LocalId   INTEGER NOT NULL,
    X         INTEGER NOT NULL,
    Y         INTEGER NOT NULL,

    FOREIGN KEY (SectorId) REFERENCES Sectors(Id),
    FOREIGN KEY (LocalId)  REFERENCES Locals(Id)
);

CREATE TABLE IF NOT EXISTS OperatorSchedule (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,

    CodigoFJ TEXT NOT NULL,
    SectorId INTEGER NOT NULL,
    LocalId INTEGER NOT NULL,
    ShiftId INTEGER NOT NULL,
    ScheduleDate DATE NOT NULL,

    ImportedAt DATETIME DEFAULT CURRENT_TIMESTAMP,

    FOREIGN KEY (CodigoFJ) REFERENCES Operators(CodigoFJ),
    FOREIGN KEY (SectorId) REFERENCES Sectors(Id),
    FOREIGN KEY (LocalId) REFERENCES Locals(Id)
);

CREATE TABLE IF NOT EXISTS SystemLog (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Timestamp DATETIME NOT NULL,
    UserFJ TEXT NOT NULL,
    Module TEXT NOT NULL,
    Action TEXT NOT NULL,
    TargetId INTEGER NULL,
    Details TEXT NULL
);

CREATE TABLE IF NOT EXISTS Tasks (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Description TEXT NOT NULL,
    DueDate DATE NOT NULL,
    ShiftId INTEGER NOT NULL,
    AssigneeCodigoFJ TEXT,
    Status TEXT NOT NULL,
    CreatedByCodigoFJ TEXT NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    StartedAt DATETIME,
    CompletedAt DATETIME,
    CancelledAt DATETIME,
    FOREIGN KEY (ShiftId) REFERENCES Shifts(Id),
    FOREIGN KEY (AssigneeCodigoFJ) REFERENCES Operators(CodigoFJ),
    FOREIGN KEY (CreatedByCodigoFJ) REFERENCES Operators(CodigoFJ)
);

CREATE TABLE IF NOT EXISTS TaskStatusHistory (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    TaskId INTEGER NOT NULL,
    PreviousStatus TEXT,
    NewStatus TEXT NOT NULL,
    ChangedByCodigoFJ TEXT NOT NULL,
    ChangedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    Note TEXT,
    FOREIGN KEY (TaskId) REFERENCES Tasks(Id),
    FOREIGN KEY (ChangedByCodigoFJ) REFERENCES Operators(CodigoFJ)
);

CREATE INDEX IF NOT EXISTS IX_Tasks_Status_DueDate ON Tasks(Status, DueDate);
CREATE INDEX IF NOT EXISTS IX_Tasks_Shift_Assignee ON Tasks(ShiftId, AssigneeCodigoFJ);
CREATE INDEX IF NOT EXISTS IX_TaskStatusHistory_TaskId ON TaskStatusHistory(TaskId, ChangedAt);

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

CREATE UNIQUE INDEX IF NOT EXISTS IX_MachineStatuses_Sector_StatusCode ON MachineStatuses(COALESCE(SectorId, 0), StatusCode);
CREATE INDEX IF NOT EXISTS IX_MachineStatuses_SectorId ON MachineStatuses(SectorId);
CREATE INDEX IF NOT EXISTS IX_MachineStatuses_StatusCode ON MachineStatuses(StatusCode);
CREATE UNIQUE INDEX IF NOT EXISTS IX_MachineEvents_UniqueEvent ON MachineEvents(MachineId, EventDateTime, StatusCode);
CREATE UNIQUE INDEX IF NOT EXISTS IX_MachineEvents_UniqueRawEvent ON MachineEvents(MachineId, EventDateTime, InternalState);
CREATE INDEX IF NOT EXISTS IX_MachineEvents_EventDateTime ON MachineEvents(EventDateTime);
CREATE INDEX IF NOT EXISTS IX_MachineEvents_Machine_EventTime ON MachineEvents(MachineId, EventDateTime);
CREATE UNIQUE INDEX IF NOT EXISTS IX_Machines_MachineKey_Unique ON Machines(MachineKey);
CREATE INDEX IF NOT EXISTS IX_Machines_MachineCode_LineCode ON Machines(MachineCode, LineCode);

INSERT OR IGNORE INTO MachineStatuses
(SectorId, StatusCode, DisplayCode, Classification, NamePt, NameJp, ColorHex, TextColorHex, SortOrder, IsActive)
VALUES
 (NULL, 0, 0, 'Running', 'Rodando', '稼働中', '#5B88E8', '#FFFFFF', 0, 1),
(NULL, 1, 1, 'StopCounts', 'Inativo', '非稼働', '#EF6F63', '#FFFFFF', 1, 1),
(NULL, 3, 3, 'StopCounts', 'Parado', '停止', '#F2CB58', '#4A3200', 3, 1),
(NULL, 4, 4, 'Error', 'Erro', 'エラー', '#FFFFFF', '#516174', 4, 1);

CREATE INDEX IF NOT EXISTS IX_Operators_BadgeCode ON Operators(BadgeCode);

CREATE TABLE IF NOT EXISTS Ec2AdministratorImports (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
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

CREATE UNIQUE INDEX IF NOT EXISTS IX_Ec2AdministratorImports_FileHash ON Ec2AdministratorImports(FileHash);
CREATE UNIQUE INDEX IF NOT EXISTS IX_ProductionProcedureTimes_Unique ON ProductionProcedureTimes(SectorId, COALESCE(LocalId, 0), ProcedureCode);
CREATE INDEX IF NOT EXISTS IX_Ec2MachineSnapshots_ImportId ON Ec2MachineSnapshots(ImportId);
CREATE INDEX IF NOT EXISTS IX_Ec2MachineSnapshots_Machine ON Ec2MachineSnapshots(MachineCode, SnapshotAt);
CREATE INDEX IF NOT EXISTS IX_Ec2MachineSnapshots_Area ON Ec2MachineSnapshots(AreaLabel);
CREATE INDEX IF NOT EXISTS IX_Ec2MachineSnapshots_Status ON Ec2MachineSnapshots(StatusText);
CREATE INDEX IF NOT EXISTS IX_Ec2MachineSnapshots_PartCode ON Ec2MachineSnapshots(PartCode);
CREATE INDEX IF NOT EXISTS IX_Ec2MachineSnapshots_LotNo ON Ec2MachineSnapshots(LotNo);
CREATE INDEX IF NOT EXISTS IX_Ec2MachineCurrentState_MachineId ON Ec2MachineCurrentState(MachineId);
CREATE INDEX IF NOT EXISTS IX_Ec2MachineCurrentState_Area ON Ec2MachineCurrentState(AreaLabel);
CREATE INDEX IF NOT EXISTS IX_Ec2MachineCurrentState_Status ON Ec2MachineCurrentState(StatusText);
CREATE INDEX IF NOT EXISTS IX_Ec2MachineCurrentState_PartCode ON Ec2MachineCurrentState(PartCode);

INSERT OR IGNORE INTO ProductionPartCodeStyles
(PartCode, ColorHex, TextColorHex, Description, IsActive)
VALUES
('RJ2A7', '#D93F3F', '#FFFFFF', 'Destaque EC2 Administrator', 1);
CREATE INDEX IF NOT EXISTS IX_OperatorLeaderAssignments_Operator
ON OperatorLeaderAssignments(OperatorCodigoFJ, LeaderCodigoFJ);
