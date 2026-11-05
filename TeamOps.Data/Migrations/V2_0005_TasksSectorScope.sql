ALTER TABLE Tasks ADD COLUMN SectorId INTEGER REFERENCES Sectors(Id);

UPDATE Tasks
SET SectorId = COALESCE(
    (
        SELECT assignee.SectorId
        FROM Operators assignee
        WHERE assignee.CodigoFJ = Tasks.AssigneeCodigoFJ
        LIMIT 1
    ),
    (
        SELECT creator.SectorId
        FROM Operators creator
        WHERE creator.CodigoFJ = Tasks.CreatedByCodigoFJ
        LIMIT 1
    )
)
WHERE SectorId IS NULL;

CREATE INDEX IF NOT EXISTS IX_Tasks_Sector_Status_DueDate
ON Tasks(SectorId, Status, DueDate);

CREATE TRIGGER IF NOT EXISTS TR_Tasks_RequireValidSector_Insert
BEFORE INSERT ON Tasks
FOR EACH ROW
WHEN NEW.SectorId IS NULL
  OR NOT EXISTS (
      SELECT 1
      FROM Sectors sector
      WHERE sector.Id = NEW.SectorId
        AND sector.IsActive = 1
  )
BEGIN
    SELECT RAISE(ABORT, 'Task requer um setor ativo.');
END;

CREATE TRIGGER IF NOT EXISTS TR_Tasks_RequireValidSector_Update
BEFORE UPDATE OF SectorId ON Tasks
FOR EACH ROW
WHEN NEW.SectorId IS NULL
  OR NOT EXISTS (
      SELECT 1
      FROM Sectors sector
      WHERE sector.Id = NEW.SectorId
        AND sector.IsActive = 1
  )
BEGIN
    SELECT RAISE(ABORT, 'Task requer um setor ativo.');
END;
