CREATE UNIQUE INDEX IF NOT EXISTS UX_OrganizationNodes_OneActiveRoot
ON OrganizationNodes((1))
WHERE ParentId IS NULL AND NodeType = 'Unit' AND IsActive = 1;

CREATE TRIGGER IF NOT EXISTS TR_OrganizationNodes_SectorRequiresParent_Insert
BEFORE INSERT ON OrganizationNodes
WHEN NEW.NodeType = 'Sector' AND NEW.ParentId IS NULL
BEGIN
    SELECT RAISE(ABORT, 'Sector node requires an organization unit parent');
END;

CREATE TRIGGER IF NOT EXISTS TR_OrganizationNodes_SectorRequiresParent_Update
BEFORE UPDATE OF ParentId, NodeType ON OrganizationNodes
WHEN NEW.NodeType = 'Sector' AND NEW.ParentId IS NULL
BEGIN
    SELECT RAISE(ABORT, 'Sector node requires an organization unit parent');
END;

CREATE TRIGGER IF NOT EXISTS TR_UserSectorAssignments_ValidateScope_Update
BEFORE UPDATE OF UserId, SectorId ON UserSectorAssignments
BEGIN
    SELECT CASE
        WHEN NOT EXISTS (
            SELECT 1
            FROM Users u
            JOIN AccessProfiles p ON p.Id = u.ProfileId
            JOIN Sectors s ON s.Id = NEW.SectorId AND s.IsActive = 1
            WHERE u.Id = NEW.UserId
              AND u.IsActive = 1
              AND p.ScopeMode = 'AssignedSectors'
              AND u.ReferenceNodeId IS NOT NULL
              AND (
                    s.OrganizationNodeId = u.ReferenceNodeId
                    OR s.OrganizationNodeId IN (
                        WITH RECURSIVE descendants(Id) AS (
                            SELECT Id FROM OrganizationNodes WHERE ParentId = u.ReferenceNodeId
                            UNION ALL
                            SELECT n.Id
                            FROM OrganizationNodes n
                            JOIN descendants d ON n.ParentId = d.Id
                        )
                        SELECT Id FROM descendants
                    )
              )
        )
        THEN RAISE(ABORT, 'Sector assignment outside user reference scope')
    END;
END;
