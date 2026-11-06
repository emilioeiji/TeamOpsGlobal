CREATE TABLE IF NOT EXISTS HaidaiSharedSectorGroups (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    SharedSectorId INTEGER NOT NULL UNIQUE,
    IsActive INTEGER NOT NULL DEFAULT 1 CHECK (IsActive IN (0, 1)),
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (SharedSectorId) REFERENCES Sectors(Id) ON DELETE RESTRICT
);

CREATE UNIQUE INDEX IF NOT EXISTS UX_HaidaiSharedSectorGroups_OneActive
ON HaidaiSharedSectorGroups(IsActive) WHERE IsActive = 1;

CREATE TABLE IF NOT EXISTS HaidaiSharedSectorMembers (
    GroupId INTEGER NOT NULL,
    MemberSectorId INTEGER NOT NULL,
    SortOrder INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY (GroupId, MemberSectorId),
    FOREIGN KEY (GroupId) REFERENCES HaidaiSharedSectorGroups(Id) ON DELETE CASCADE,
    FOREIGN KEY (MemberSectorId) REFERENCES Sectors(Id) ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS IX_HaidaiSharedSectorMembers_MemberSectorId
ON HaidaiSharedSectorMembers(MemberSectorId);

CREATE TRIGGER IF NOT EXISTS TR_HaidaiSharedSectorMembers_DifferentSector_Insert
BEFORE INSERT ON HaidaiSharedSectorMembers
WHEN NEW.MemberSectorId = (
    SELECT SharedSectorId FROM HaidaiSharedSectorGroups WHERE Id = NEW.GroupId
)
BEGIN
    SELECT RAISE(ABORT, 'Shared sector cannot also be a member sector');
END;

CREATE TRIGGER IF NOT EXISTS TR_HaidaiSharedSectorMembers_DifferentSector_Update
BEFORE UPDATE OF GroupId, MemberSectorId ON HaidaiSharedSectorMembers
WHEN NEW.MemberSectorId = (
    SELECT SharedSectorId FROM HaidaiSharedSectorGroups WHERE Id = NEW.GroupId
)
BEGIN
    SELECT RAISE(ABORT, 'Shared sector cannot also be a member sector');
END;

-- Compatibilidade controlada: se uma instalacao ja cadastrou o agrupador GDAD,
-- registra a topologia inicial. O codigo da aplicacao consulta somente estes IDs.
INSERT OR IGNORE INTO HaidaiSharedSectorGroups (Name, SharedSectorId)
SELECT 'Agrupamento inicial Haidai', Id
FROM Sectors
WHERE upper(replace(Code, '-', '')) = 'GDAD'
  AND IsActive = 1
LIMIT 1;

INSERT OR IGNORE INTO HaidaiSharedSectorMembers (GroupId, MemberSectorId, SortOrder)
SELECT groups.Id, sector.Id,
       CASE upper(replace(sector.Code, '-', '')) WHEN 'GBARERU' THEN 1 ELSE 2 END
FROM HaidaiSharedSectorGroups groups
JOIN Sectors sector
  ON upper(replace(sector.Code, '-', '')) IN ('GBARERU', 'DAD')
WHERE groups.IsActive = 1
  AND sector.IsActive = 1;
