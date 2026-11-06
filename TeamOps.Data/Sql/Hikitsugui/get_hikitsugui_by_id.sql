SELECT
    h.Id,
    h.Date,
    h.ShiftId,
    h.CreatorCodigoFJ,
    h.CategoryId,
    h.EquipmentId,
    h.LocalId,
    h.SectorId,
    h.Description,
    h.Description AS DescriptionHtml,
    o.NameRomanji AS OperatorName,
    c.NamePt AS Category,
    e.NamePt AS Equipment,
    l.NamePt AS Local,
    s.NamePt AS Sector
FROM Hikitsugui h
JOIN Operators o ON o.CodigoFJ = h.CreatorCodigoFJ
LEFT JOIN Categories c ON c.Id = h.CategoryId
LEFT JOIN Equipments e ON e.Id = h.EquipmentId
LEFT JOIN Locals l ON l.Id = h.LocalId
LEFT JOIN Sectors s ON s.Id = h.SectorId
WHERE h.Id = @id;
