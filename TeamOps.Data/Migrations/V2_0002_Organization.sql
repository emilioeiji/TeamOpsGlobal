CREATE TABLE IF NOT EXISTS OrganizationNodes (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ParentId INTEGER,
    NodeType TEXT NOT NULL CHECK (NodeType IN ('Unit', 'Sector')),
    Code TEXT NOT NULL COLLATE NOCASE UNIQUE,
    NamePt TEXT NOT NULL,
    NameJp TEXT NOT NULL DEFAULT '',
    IsActive INTEGER NOT NULL DEFAULT 1 CHECK (IsActive IN (0, 1)),
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (ParentId) REFERENCES OrganizationNodes(Id) ON DELETE RESTRICT,
    CHECK (ParentId IS NULL OR ParentId <> Id)
);

CREATE INDEX IF NOT EXISTS IX_OrganizationNodes_ParentId
ON OrganizationNodes(ParentId);

CREATE INDEX IF NOT EXISTS IX_OrganizationNodes_Type_Active
ON OrganizationNodes(NodeType, IsActive);

CREATE TRIGGER IF NOT EXISTS TR_OrganizationNodes_ParentMustBeUnit_Insert
BEFORE INSERT ON OrganizationNodes
WHEN NEW.ParentId IS NOT NULL
 AND COALESCE((SELECT NodeType = 'Unit' AND IsActive = 1 FROM OrganizationNodes WHERE Id = NEW.ParentId), 0) <> 1
BEGIN
    SELECT RAISE(ABORT, 'Organization parent must be an active unit');
END;

CREATE TRIGGER IF NOT EXISTS TR_OrganizationNodes_ParentMustBeUnit_Update
BEFORE UPDATE OF ParentId ON OrganizationNodes
WHEN NEW.ParentId IS NOT NULL
 AND COALESCE((SELECT NodeType = 'Unit' AND IsActive = 1 FROM OrganizationNodes WHERE Id = NEW.ParentId), 0) <> 1
BEGIN
    SELECT RAISE(ABORT, 'Organization parent must be an active unit');
END;

CREATE TRIGGER IF NOT EXISTS TR_OrganizationNodes_PreventCycle
BEFORE UPDATE OF ParentId ON OrganizationNodes
WHEN NEW.ParentId IS NOT NULL
BEGIN
    WITH RECURSIVE descendants(Id) AS (
        SELECT Id FROM OrganizationNodes WHERE ParentId = OLD.Id
        UNION ALL
        SELECT n.Id
        FROM OrganizationNodes n
        JOIN descendants d ON n.ParentId = d.Id
    )
    SELECT CASE
        WHEN NEW.ParentId = OLD.Id OR EXISTS (SELECT 1 FROM descendants WHERE Id = NEW.ParentId)
        THEN RAISE(ABORT, 'Organization hierarchy cycle')
    END;
END;

CREATE TABLE IF NOT EXISTS AccessProfiles (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Code TEXT NOT NULL COLLATE NOCASE UNIQUE,
    NamePt TEXT NOT NULL,
    NameJp TEXT NOT NULL DEFAULT '',
    HierarchyRank INTEGER NOT NULL UNIQUE CHECK (HierarchyRank BETWEEN 1 AND 6),
    ScopeMode TEXT NOT NULL CHECK (ScopeMode IN ('OrganizationTree', 'AssignedSectors')),
    IsActive INTEGER NOT NULL DEFAULT 1 CHECK (IsActive IN (0, 1))
);

CREATE TABLE IF NOT EXISTS ModulePermissions (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ProfileId INTEGER NOT NULL,
    ModuleCode TEXT NOT NULL COLLATE NOCASE,
    ActionCode TEXT NOT NULL COLLATE NOCASE,
    IsAllowed INTEGER NOT NULL DEFAULT 0 CHECK (IsAllowed IN (0, 1)),
    FOREIGN KEY (ProfileId) REFERENCES AccessProfiles(Id) ON DELETE CASCADE,
    UNIQUE (ProfileId, ModuleCode, ActionCode)
);

INSERT OR IGNORE INTO OrganizationNodes (ParentId, NodeType, Code, NamePt, NameJp)
VALUES (NULL, 'Unit', '5500', 'Unidade 5500', '5500');

INSERT OR IGNORE INTO OrganizationNodes (ParentId, NodeType, Code, NamePt, NameJp)
SELECT Id, 'Unit', '5552', 'Unidade 5552', '5552'
FROM OrganizationNodes WHERE Code = '5500';

INSERT OR IGNORE INTO OrganizationNodes (ParentId, NodeType, Code, NamePt, NameJp)
SELECT Id, 'Sector', 'G-BARERU', 'G-Bareru', 'G-Bareru'
FROM OrganizationNodes WHERE Code = '5552';

INSERT OR IGNORE INTO OrganizationNodes (ParentId, NodeType, Code, NamePt, NameJp)
SELECT Id, 'Sector', 'DAD', 'DAD', 'DAD'
FROM OrganizationNodes WHERE Code = '5552';

INSERT OR IGNORE INTO OrganizationNodes (ParentId, NodeType, Code, NamePt, NameJp)
SELECT Id, 'Sector', 'IAS', 'IAS', 'IAS'
FROM OrganizationNodes WHERE Code = '5552';

INSERT OR IGNORE INTO OrganizationNodes (ParentId, NodeType, Code, NamePt, NameJp)
SELECT Id, 'Sector', 'APL', 'APL', 'APL'
FROM OrganizationNodes WHERE Code = '5552';

INSERT OR IGNORE INTO Sectors (Code, NamePt, NameJp, OrganizationNodeId, IsActive)
SELECT Code, NamePt, NameJp, Id, IsActive
FROM OrganizationNodes
WHERE NodeType = 'Sector';

INSERT OR IGNORE INTO AccessProfiles (Code, NamePt, NameJp, HierarchyRank, ScopeMode) VALUES
('DIRECTOR', 'Diretor', 'ディレクター', 6, 'OrganizationTree'),
('MANAGER', 'Gerente', 'マネージャー', 5, 'OrganizationTree'),
('SV', 'SV', 'SV', 4, 'OrganizationTree'),
('GL', 'GL', 'GL', 3, 'AssignedSectors'),
('KL', 'KL', 'KL', 2, 'AssignedSectors'),
('OP', 'OP', 'OP', 1, 'AssignedSectors');

INSERT OR IGNORE INTO ModulePermissions (ProfileId, ModuleCode, ActionCode, IsAllowed)
SELECT Id, '*', 'view', 1 FROM AccessProfiles;

INSERT OR IGNORE INTO ModulePermissions (ProfileId, ModuleCode, ActionCode, IsAllowed)
SELECT Id, 'administration', action.ActionCode, 1
FROM AccessProfiles
CROSS JOIN (
    SELECT 'view' AS ActionCode UNION ALL
    SELECT 'create' UNION ALL SELECT 'edit' UNION ALL
    SELECT 'delete' UNION ALL SELECT 'administer'
) action
WHERE Code IN ('DIRECTOR', 'MANAGER');
