SELECT
    o.NameRomanji AS TakenByName,
    c.TakenAt
FROM YukyuConferencia c
JOIN Operators o ON o.CodigoFJ = c.TakenBy
WHERE c.AcompYukyuId = @AcompYukyuId
ORDER BY c.TakenAt DESC
LIMIT 1;
