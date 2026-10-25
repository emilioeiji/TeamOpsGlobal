INSERT INTO YukyuConferencia (AcompYukyuId, TakenBy, TakenAt)
SELECT @AcompYukyuId, @TakenBy, @TakenAt
WHERE EXISTS (
    SELECT 1
    FROM AcompYukyu a
    WHERE a.Id = @AcompYukyuId
      AND EXISTS (
          SELECT 1
          FROM YukyuTodoke t
          WHERE t.AcompYukyuId = a.Id
      )
      AND (
          a.TodokeMotivoId <> 1
          OR EXISTS (
              SELECT 1
              FROM YukyuFolhaControle f
              WHERE f.AcompYukyuId = a.Id
          )
      )
      AND NOT EXISTS (
          SELECT 1
          FROM YukyuConferencia c
          WHERE c.AcompYukyuId = a.Id
      )
);
