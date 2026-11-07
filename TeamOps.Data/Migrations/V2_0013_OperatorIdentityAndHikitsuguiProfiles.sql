ALTER TABLE Operators ADD COLUMN OperatorType TEXT NOT NULL DEFAULT 'Regular'
    CHECK (OperatorType IN ('Regular', 'Relief'));

CREATE TABLE IF NOT EXISTS OperatorSectorAssignments (
    OperatorCodigoFJ TEXT NOT NULL,
    SectorId INTEGER NOT NULL,
    IsPrimary INTEGER NOT NULL DEFAULT 0 CHECK (IsPrimary IN (0, 1)),
    AssignedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (OperatorCodigoFJ, SectorId),
    FOREIGN KEY (OperatorCodigoFJ) REFERENCES Operators(CodigoFJ) ON DELETE CASCADE,
    FOREIGN KEY (SectorId) REFERENCES Sectors(Id) ON DELETE RESTRICT
);

CREATE UNIQUE INDEX IF NOT EXISTS UX_OperatorSectorAssignments_Primary
ON OperatorSectorAssignments(OperatorCodigoFJ)
WHERE IsPrimary = 1;

CREATE INDEX IF NOT EXISTS IX_OperatorSectorAssignments_Sector
ON OperatorSectorAssignments(SectorId, OperatorCodigoFJ);

INSERT OR IGNORE INTO OperatorSectorAssignments (OperatorCodigoFJ, SectorId, IsPrimary)
SELECT CodigoFJ, SectorId, 1 FROM Operators;

CREATE TABLE IF NOT EXISTS HikitsuguiAudienceProfiles (
    HikitsuguiId INTEGER NOT NULL,
    ProfileId INTEGER NOT NULL,
    PRIMARY KEY (HikitsuguiId, ProfileId),
    FOREIGN KEY (HikitsuguiId) REFERENCES Hikitsugui(Id) ON DELETE CASCADE,
    FOREIGN KEY (ProfileId) REFERENCES AccessProfiles(Id) ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS IX_HikitsuguiAudienceProfiles_Profile_Message
ON HikitsuguiAudienceProfiles(ProfileId, HikitsuguiId);

-- Preserva a visibilidade dos registros anteriores. Novos registros passam a
-- exigir seleção explícita dos perfis no formulário.
INSERT OR IGNORE INTO HikitsuguiAudienceProfiles (HikitsuguiId, ProfileId)
SELECT h.Id, p.Id
FROM Hikitsugui h
CROSS JOIN AccessProfiles p
WHERE p.IsActive = 1;
