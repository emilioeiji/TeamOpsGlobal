CREATE TABLE IF NOT EXISTS HikitsuguiAudiences (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    HikitsuguiId INTEGER NOT NULL,
    AudienceType TEXT NOT NULL CHECK (AudienceType IN ('Sector', 'Organization', 'General')),
    SectorId INTEGER,
    OrganizationNodeId INTEGER,
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (HikitsuguiId) REFERENCES Hikitsugui(Id) ON DELETE CASCADE,
    FOREIGN KEY (SectorId) REFERENCES Sectors(Id) ON DELETE RESTRICT,
    FOREIGN KEY (OrganizationNodeId) REFERENCES OrganizationNodes(Id) ON DELETE RESTRICT,
    CHECK (
        (AudienceType = 'Sector' AND SectorId IS NOT NULL AND OrganizationNodeId IS NULL)
        OR (AudienceType = 'Organization' AND SectorId IS NULL AND OrganizationNodeId IS NOT NULL)
        OR (AudienceType = 'General' AND SectorId IS NULL AND OrganizationNodeId IS NULL)
    )
);

CREATE UNIQUE INDEX IF NOT EXISTS UX_HikitsuguiAudiences_Target
ON HikitsuguiAudiences(
    HikitsuguiId,
    AudienceType,
    COALESCE(SectorId, 0),
    COALESCE(OrganizationNodeId, 0)
);

CREATE INDEX IF NOT EXISTS IX_HikitsuguiAudiences_Sector_Message
ON HikitsuguiAudiences(SectorId, HikitsuguiId)
WHERE AudienceType = 'Sector';

CREATE INDEX IF NOT EXISTS IX_HikitsuguiAudiences_Organization_Message
ON HikitsuguiAudiences(OrganizationNodeId, HikitsuguiId)
WHERE AudienceType = 'Organization';

CREATE TRIGGER IF NOT EXISTS TR_HikitsuguiAudiences_SingleMode
BEFORE INSERT ON HikitsuguiAudiences
WHEN EXISTS (
    SELECT 1 FROM HikitsuguiAudiences existing
    WHERE existing.HikitsuguiId = NEW.HikitsuguiId
      AND existing.AudienceType <> NEW.AudienceType
)
BEGIN
    SELECT RAISE(ABORT, 'Hikitsugui cannot mix audience modes');
END;

CREATE TRIGGER IF NOT EXISTS TR_HikitsuguiAudiences_OrganizationMustBeUnit
BEFORE INSERT ON HikitsuguiAudiences
WHEN NEW.AudienceType = 'Organization'
 AND COALESCE((SELECT NodeType = 'Unit' AND IsActive = 1 FROM OrganizationNodes WHERE Id = NEW.OrganizationNodeId), 0) <> 1
BEGIN
    SELECT RAISE(ABORT, 'Hikitsugui organization audience must target an active unit');
END;
