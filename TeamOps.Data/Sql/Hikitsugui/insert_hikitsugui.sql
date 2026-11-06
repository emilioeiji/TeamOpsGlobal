INSERT INTO Hikitsugui
(
    Date,
    ShiftId,
    CreatorCodigoFJ,
    CategoryId,
    EquipmentId,
    LocalId,
    SectorId,
    Description
)
VALUES
(
    @date,
    @shiftId,
    @creator,
    @categoryId,
    @equipmentId,
    @localId,
    @sectorId,
    @description
);

SELECT last_insert_rowid();
