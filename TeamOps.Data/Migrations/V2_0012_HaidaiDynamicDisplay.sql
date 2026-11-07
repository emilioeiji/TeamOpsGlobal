CREATE TABLE IF NOT EXISTS HaidaiAreas (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    SectorId INTEGER NOT NULL,
    Code TEXT NOT NULL,
    NamePt TEXT NOT NULL,
    NameJp TEXT NOT NULL DEFAULT '',
    SortOrder INTEGER NOT NULL DEFAULT 0,
    IsActive INTEGER NOT NULL DEFAULT 1 CHECK (IsActive IN (0, 1)),
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (SectorId) REFERENCES Sectors(Id) ON DELETE RESTRICT,
    UNIQUE (SectorId, Code)
);

CREATE INDEX IF NOT EXISTS IX_HaidaiAreas_Sector_Display
ON HaidaiAreas(SectorId, IsActive, SortOrder, Id);

ALTER TABLE Locals ADD COLUMN HaidaiAreaId INTEGER REFERENCES HaidaiAreas(Id) ON DELETE RESTRICT;
ALTER TABLE Locals ADD COLUMN SortOrder INTEGER NOT NULL DEFAULT 0;
ALTER TABLE Locals ADD COLUMN IsActive INTEGER NOT NULL DEFAULT 1 CHECK (IsActive IN (0, 1));

-- Cada local existente vira inicialmente uma area independente. Depois, a administracao
-- pode agrupar varios locais na mesma area sem alterar codigo ou perder atribuicoes.
INSERT OR IGNORE INTO HaidaiAreas (SectorId, Code, NamePt, NameJp, SortOrder, IsActive)
SELECT
    l.SectorId,
    COALESCE(NULLIF(trim(l.ShortCode), ''), 'L' || l.Id),
    COALESCE(NULLIF(trim(l.NamePt), ''), 'Area ' || l.Id),
    COALESCE(l.NameJp, ''),
    l.Id,
    1
FROM Locals l;

UPDATE Locals
SET HaidaiAreaId = (
    SELECT a.Id
    FROM HaidaiAreas a
    WHERE a.SectorId = Locals.SectorId
      AND upper(a.Code) = upper(COALESCE(NULLIF(trim(Locals.ShortCode), ''), 'L' || Locals.Id))
    ORDER BY a.Id
    LIMIT 1
)
WHERE HaidaiAreaId IS NULL;

CREATE INDEX IF NOT EXISTS IX_Locals_HaidaiArea_Display
ON Locals(HaidaiAreaId, IsActive, SortOrder, Id);

CREATE TRIGGER IF NOT EXISTS TR_Locals_HaidaiAreaSector_Insert
BEFORE INSERT ON Locals
WHEN NEW.HaidaiAreaId IS NOT NULL
 AND COALESCE((SELECT SectorId FROM HaidaiAreas WHERE Id = NEW.HaidaiAreaId), -1) <> NEW.SectorId
BEGIN
    SELECT RAISE(ABORT, 'Haidai area and local must belong to the same sector');
END;

CREATE TRIGGER IF NOT EXISTS TR_Locals_HaidaiAreaSector_Update
BEFORE UPDATE OF HaidaiAreaId, SectorId ON Locals
WHEN NEW.HaidaiAreaId IS NOT NULL
 AND COALESCE((SELECT SectorId FROM HaidaiAreas WHERE Id = NEW.HaidaiAreaId), -1) <> NEW.SectorId
BEGIN
    SELECT RAISE(ABORT, 'Haidai area and local must belong to the same sector');
END;

CREATE TABLE IF NOT EXISTS HaidaiDisplaySettings (
    SectorId INTEGER PRIMARY KEY,
    ReferenceWidth INTEGER NOT NULL DEFAULT 3840 CHECK (ReferenceWidth BETWEEN 1280 AND 7680),
    ReferenceHeight INTEGER NOT NULL DEFAULT 2160 CHECK (ReferenceHeight BETWEEN 720 AND 4320),
    RotationSeconds INTEGER NOT NULL DEFAULT 12 CHECK (RotationSeconds BETWEEN 5 AND 300),
    UpdatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (SectorId) REFERENCES Sectors(Id) ON DELETE CASCADE
);

INSERT OR IGNORE INTO HaidaiDisplaySettings (SectorId)
SELECT Id FROM Sectors WHERE IsActive = 1;
