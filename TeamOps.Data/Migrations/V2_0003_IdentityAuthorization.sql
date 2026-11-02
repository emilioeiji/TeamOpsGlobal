ALTER TABLE Users ADD COLUMN ProfileId INTEGER REFERENCES AccessProfiles(Id);
ALTER TABLE Users ADD COLUMN ReferenceNodeId INTEGER REFERENCES OrganizationNodes(Id);
ALTER TABLE Users ADD COLUMN IsActive INTEGER NOT NULL DEFAULT 1 CHECK (IsActive IN (0, 1));
ALTER TABLE Users ADD COLUMN FailedLoginAttempts INTEGER NOT NULL DEFAULT 0 CHECK (FailedLoginAttempts >= 0);
ALTER TABLE Users ADD COLUMN LockedUntil TEXT;
ALTER TABLE Users ADD COLUMN PasswordChangedAt TEXT;
ALTER TABLE Users ADD COLUMN LastLoginAt TEXT;
ALTER TABLE Users ADD COLUMN SessionVersion INTEGER NOT NULL DEFAULT 1 CHECK (SessionVersion > 0);

CREATE UNIQUE INDEX IF NOT EXISTS UX_Users_CodigoFJ
ON Users(CodigoFJ) WHERE CodigoFJ IS NOT NULL;

CREATE INDEX IF NOT EXISTS IX_Users_Profile_Reference
ON Users(ProfileId, ReferenceNodeId, IsActive);

CREATE TABLE IF NOT EXISTS UserSectorAssignments (
    UserId INTEGER NOT NULL,
    SectorId INTEGER NOT NULL,
    AssignedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    AssignedByUserId INTEGER,
    PRIMARY KEY (UserId, SectorId),
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
    FOREIGN KEY (SectorId) REFERENCES Sectors(Id) ON DELETE RESTRICT,
    FOREIGN KEY (AssignedByUserId) REFERENCES Users(Id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS IX_UserSectorAssignments_Sector_User
ON UserSectorAssignments(SectorId, UserId);

CREATE TABLE IF NOT EXISTS AuthenticationAudit (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId INTEGER,
    Login TEXT NOT NULL,
    EventType TEXT NOT NULL CHECK (EventType IN ('LoginSucceeded', 'LoginFailed', 'Locked', 'PasswordChanged', 'PasswordReset', 'SessionInvalidated')),
    OccurredAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    Workstation TEXT,
    Details TEXT,
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS IX_AuthenticationAudit_User_OccurredAt
ON AuthenticationAudit(UserId, OccurredAt DESC);

CREATE TABLE IF NOT EXISTS SecurityAudit (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ActorUserId INTEGER,
    EventType TEXT NOT NULL,
    TargetType TEXT NOT NULL,
    TargetId TEXT,
    OccurredAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    Details TEXT,
    FOREIGN KEY (ActorUserId) REFERENCES Users(Id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS IX_SecurityAudit_Target_OccurredAt
ON SecurityAudit(TargetType, TargetId, OccurredAt DESC);

CREATE TRIGGER IF NOT EXISTS TR_UserSectorAssignments_ValidateScope
BEFORE INSERT ON UserSectorAssignments
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

DELETE FROM ModulePermissions WHERE ModuleCode = '*';

INSERT OR IGNORE INTO ModulePermissions (ProfileId, ModuleCode, ActionCode, IsAllowed)
SELECT p.Id, rules.ModuleCode, actions.ActionCode, 1
FROM AccessProfiles p
JOIN (
    SELECT 'dashboard' AS ModuleCode, 1 AS MinimumRank UNION ALL
    SELECT 'hikitsugui', 1 UNION ALL
    SELECT 'followup', 2 UNION ALL
    SELECT 'mastercard', 2 UNION ALL
    SELECT 'sobradepeca', 2 UNION ALL
    SELECT 'prcl', 2 UNION ALL
    SELECT 'reports', 3 UNION ALL
    SELECT 'presence', 3 UNION ALL
    SELECT 'haidai', 3 UNION ALL
    SELECT 'tasks', 3 UNION ALL
    SELECT 'production', 3 UNION ALL
    SELECT 'organization', 5 UNION ALL
    SELECT 'accesscontrol', 5
) rules ON p.HierarchyRank >= rules.MinimumRank
CROSS JOIN (
    SELECT 'view' AS ActionCode UNION ALL
    SELECT 'create' UNION ALL
    SELECT 'edit' UNION ALL
    SELECT 'export'
) actions;

INSERT OR IGNORE INTO ModulePermissions (ProfileId, ModuleCode, ActionCode, IsAllowed)
SELECT p.Id, rules.ModuleCode, actions.ActionCode, 1
FROM AccessProfiles p
JOIN (
    SELECT 'hikitsugui' AS ModuleCode, 3 AS MinimumRank UNION ALL
    SELECT 'followup', 3 UNION ALL
    SELECT 'mastercard', 3 UNION ALL
    SELECT 'sobradepeca', 3 UNION ALL
    SELECT 'prcl', 3 UNION ALL
    SELECT 'tasks', 3 UNION ALL
    SELECT 'organization', 5 UNION ALL
    SELECT 'accesscontrol', 5
) rules ON p.HierarchyRank >= rules.MinimumRank
CROSS JOIN (
    SELECT 'delete' AS ActionCode UNION ALL
    SELECT 'approve' UNION ALL
    SELECT 'administer'
) actions;
