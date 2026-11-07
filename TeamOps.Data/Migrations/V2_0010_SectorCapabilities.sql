CREATE TABLE IF NOT EXISTS SectorCapabilities (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    SectorId INTEGER NOT NULL,
    FeatureCode TEXT NOT NULL COLLATE NOCASE,
    IsActive INTEGER NOT NULL DEFAULT 1 CHECK (IsActive IN (0, 1)),
    SettingsJson TEXT,
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (SectorId) REFERENCES Sectors(Id) ON DELETE RESTRICT,
    UNIQUE (SectorId, FeatureCode)
);

CREATE UNIQUE INDEX IF NOT EXISTS UX_SectorCapabilities_ActiveFeature
ON SectorCapabilities(FeatureCode) WHERE IsActive = 1;

CREATE INDEX IF NOT EXISTS IX_SectorCapabilities_Sector
ON SectorCapabilities(SectorId, IsActive);

-- Seeds iniciais: os codigos sao usados somente para associar os processos da
-- instalacao padrao. Em execucao, os modulos consultam exclusivamente a capacidade.
INSERT OR IGNORE INTO SectorCapabilities (SectorId, FeatureCode)
SELECT Id, 'production.import.gbareru'
FROM Sectors WHERE Code = 'G-BARERU' COLLATE NOCASE;

INSERT OR IGNORE INTO SectorCapabilities (SectorId, FeatureCode)
SELECT Id, 'production.import.dad'
FROM Sectors WHERE Code = 'DAD' COLLATE NOCASE;

INSERT OR IGNORE INTO SectorCapabilities (SectorId, FeatureCode)
SELECT Id, 'production.capacity-forecast'
FROM Sectors WHERE Code = 'G-BARERU' COLLATE NOCASE;

INSERT OR IGNORE INTO SectorCapabilities (SectorId, FeatureCode)
SELECT Id, 'sobra-de-peca'
FROM Sectors WHERE Code = 'G-BARERU' COLLATE NOCASE;
