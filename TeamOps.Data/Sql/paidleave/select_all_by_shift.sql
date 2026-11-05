SELECT 
    a.Id AS id,
    a.OperatorCodigoFJ AS operatorCodigoFJ,
    a.TodokeMotivoId AS todokeMotivoId,
    o.NameRomanji AS operatorName,
    a.RequestDate AS requestDate,
    COALESCE(l.NameRomanji, '') AS authorizedBy,
    (
        SELECT m.NomePt
        FROM TodokeMotivo m
        WHERE m.Id = a.TodokeMotivoId
    ) AS todokeMotivoName,
    a.Notes AS notes,
    CASE 
        WHEN EXISTS (
            SELECT 1 
            FROM YukyuTodoke t 
            WHERE t.AcompYukyuId = a.Id
        ) THEN 1 ELSE 0
    END AS hasTodoke,
    CASE 
        WHEN EXISTS (
            SELECT 1 
            FROM YukyuFolhaControle f 
            WHERE f.AcompYukyuId = a.Id
        ) THEN 1 ELSE 0
    END AS hasFolha,
    CASE 
        WHEN EXISTS (
            SELECT 1 
            FROM YukyuConferencia c 
            WHERE c.AcompYukyuId = a.Id
        ) THEN 1 ELSE 0
    END AS hasConferencia,
    (
        SELECT c.TakenBy || '|' || c.TakenAt
        FROM YukyuConferencia c
        WHERE c.AcompYukyuId = a.Id
        ORDER BY c.Id DESC
        LIMIT 1
    ) AS conferenciaInfo
FROM AcompYukyu a
JOIN Operators o ON o.CodigoFJ = a.OperatorCodigoFJ
LEFT JOIN Operators l ON l.CodigoFJ = a.AuthorizedByCodigoFJ
WHERE (@ShiftId = 0 OR o.ShiftId = @ShiftId)
  AND o.SectorId IN @AuthorizedSectorIds
ORDER BY a.RequestDate DESC;
