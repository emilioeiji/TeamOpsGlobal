SELECT 
    CodigoFJ,
    NameRomanji,
    NameNihongo
FROM Operators
WHERE Status = 1
  AND SectorId IN @AuthorizedSectorIds
ORDER BY NameRomanji;
