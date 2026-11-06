using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace TeamOps.UI.Services
{
    internal static class MasterCardModuleService
    {
        internal static readonly (string Value, string LabelPt, string LabelJp)[] Statuses =
        {
            ("in_progress", "Andamento", "進行中"),
            ("follow", "Follow", "フォロー"),
            ("completed", "Finalizado", "完了")
        };

        internal static IEnumerable<object> QueryStatuses()
        {
            return Statuses.Select(status => new
            {
                value = status.Value,
                labelPt = status.LabelPt,
                labelJp = status.LabelJp
            });
        }

        internal static IEnumerable<object> QueryOperators(IDbConnection conn, IReadOnlyCollection<int>? authorizedSectorIds = null)
        {
            const string sql = @"
                SELECT
                    CodigoFJ,
                    COALESCE(NameRomanji, CodigoFJ) AS NamePt,
                    COALESCE(NULLIF(NameNihongo, ''), NameRomanji, CodigoFJ) AS NameJp,
                    ShiftId,
                    SectorId
                FROM Operators
                WHERE Status = 1
                ORDER BY NameRomanji, CodigoFJ;";

            return conn.Query<LookupRow>(sql)
                .Where(item => authorizedSectorIds == null || authorizedSectorIds.Contains(item.SectorId))
                .Select(item => new
            {
                codigoFJ = item.CodigoFJ,
                namePt = item.NamePt,
                nameJp = item.NameJp,
                shiftId = item.ShiftId,
                sectorId = item.SectorId
            });
        }

        internal static IEnumerable<object> QueryTrainers(IDbConnection conn, IReadOnlyCollection<int>? authorizedSectorIds = null)
        {
            const string sql = @"
                SELECT
                    CodigoFJ,
                    COALESCE(NameRomanji, CodigoFJ) AS NamePt,
                    COALESCE(NULLIF(NameNihongo, ''), NameRomanji, CodigoFJ) AS NameJp,
                    ShiftId,
                    SectorId
                FROM Operators
                WHERE Status = 1
                  AND Trainer = 1
                ORDER BY NameRomanji, CodigoFJ;";

            return conn.Query<LookupRow>(sql)
                .Where(item => authorizedSectorIds == null || authorizedSectorIds.Contains(item.SectorId))
                .Select(item => new
            {
                codigoFJ = item.CodigoFJ,
                namePt = item.NamePt,
                nameJp = item.NameJp,
                shiftId = item.ShiftId,
                sectorId = item.SectorId
            });
        }

        internal static IEnumerable<object> QuerySectors(IDbConnection conn, IReadOnlyCollection<int>? authorizedSectorIds = null)
        {
            const string sql = @"
                SELECT
                    Id,
                    COALESCE(NamePt, '') AS NamePt,
                    COALESCE(NULLIF(NameJp, ''), NamePt, '') AS NameJp
                FROM Sectors
                ORDER BY Id;";

            return conn.Query<SectorLookupRow>(sql)
                .Where(item => authorizedSectorIds == null || authorizedSectorIds.Contains(item.Id))
                .Select(item => new
            {
                id = item.Id,
                namePt = item.NamePt,
                nameJp = item.NameJp
            });
        }

        internal static IEnumerable<object> QueryEquipments(IDbConnection conn)
        {
            const string sql = @"
                SELECT
                    Id,
                    COALESCE(NamePt, '') AS NamePt,
                    COALESCE(NULLIF(NameJp, ''), NamePt, '') AS NameJp
                FROM Equipments
                ORDER BY Id;";

            return conn.Query<SectorLookupRow>(sql).Select(item => new
            {
                id = item.Id,
                namePt = item.NamePt,
                nameJp = item.NameJp
            });
        }

        internal static List<MasterCardRow> QueryMasterCards(IDbConnection conn, IReadOnlyCollection<int>? authorizedSectorIds = null)
        {
            const string sql = @"
                SELECT
                    m.Id,
                    m.OperatorCodigoFJ,
                    COALESCE(op.NameRomanji, m.OperatorCodigoFJ) AS OperatorNamePt,
                    COALESCE(NULLIF(op.NameNihongo, ''), op.NameRomanji, m.OperatorCodigoFJ) AS OperatorNameJp,
                    m.TrainerCodigoFJ,
                    COALESCE(tr.NameRomanji, m.TrainerCodigoFJ) AS TrainerNamePt,
                    COALESCE(NULLIF(tr.NameNihongo, ''), tr.NameRomanji, m.TrainerCodigoFJ) AS TrainerNameJp,
                    m.SectorId,
                    COALESCE(sec.NamePt, '') AS SectorNamePt,
                    COALESCE(NULLIF(sec.NameJp, ''), sec.NamePt, '') AS SectorNameJp,
                    m.EquipmentId,
                    COALESCE(eq.NamePt, '') AS EquipmentNamePt,
                    COALESCE(NULLIF(eq.NameJp, ''), eq.NamePt, '') AS EquipmentNameJp,
                    m.ProcessDescription AS Description,
                    COALESCE(m.Notes, '') AS Notes,
                    substr(m.StartDate, 1, 10) AS StartDate,
                    m.Status,
                    CASE WHEN m.ConcludedAt IS NULL THEN '' ELSE substr(m.ConcludedAt, 1, 16) END AS ConcludedAt,
                    CASE WHEN m.FollowDate IS NULL THEN '' ELSE substr(m.FollowDate, 1, 10) END AS FollowDate,
                    CASE WHEN m.FinalizedAt IS NULL THEN '' ELSE substr(m.FinalizedAt, 1, 16) END AS FinalizedAt,
                    substr(m.CreatedAt, 1, 16) AS CreatedAt,
                    substr(m.UpdatedAt, 1, 16) AS UpdatedAt,
                    COALESCE(creator.NameRomanji, m.CreatedByCodigoFJ) AS CreatedByNamePt,
                    COALESCE(NULLIF(creator.NameNihongo, ''), creator.NameRomanji, m.CreatedByCodigoFJ) AS CreatedByNameJp,
                    (
                        SELECT COUNT(1)
                        FROM MasterCardStatusHistory h
                        WHERE h.MasterCardId = m.Id
                    ) AS HistoryCount
                FROM MasterCards m
                LEFT JOIN Operators op ON op.CodigoFJ = m.OperatorCodigoFJ
                LEFT JOIN Operators tr ON tr.CodigoFJ = m.TrainerCodigoFJ
                LEFT JOIN Operators creator ON creator.CodigoFJ = m.CreatedByCodigoFJ
                LEFT JOIN Sectors sec ON sec.Id = m.SectorId
                LEFT JOIN Equipments eq ON eq.Id = m.EquipmentId
                ORDER BY
                    CASE m.Status
                        WHEN 'in_progress' THEN 0
                        WHEN 'follow' THEN 1
                        ELSE 2
                    END,
                    date(m.FollowDate) ASC,
                    date(m.StartDate) DESC,
                    m.Id DESC;";

            return conn.Query<MasterCardRow>(sql)
                .Where(item => authorizedSectorIds == null || authorizedSectorIds.Contains(item.SectorId))
                .ToList();
        }

        internal static MasterCardRow? GetMasterCard(IDbConnection conn, int id)
        {
            return QueryMasterCards(conn).FirstOrDefault(item => item.Id == id);
        }

        internal static List<object> QueryHistory(IDbConnection conn, int masterCardId)
        {
            const string sql = @"
                SELECT
                    h.Id,
                    COALESCE(h.PreviousStatus, '') AS PreviousStatus,
                    h.NewStatus AS NewStatus,
                    COALESCE(changer.NameRomanji, h.ChangedByCodigoFJ) AS ChangedByNamePt,
                    COALESCE(NULLIF(changer.NameNihongo, ''), changer.NameRomanji, h.ChangedByCodigoFJ) AS ChangedByNameJp,
                    substr(h.ChangedAt, 1, 16) AS ChangedAt,
                    COALESCE(h.Note, '') AS Note
                FROM MasterCardStatusHistory h
                LEFT JOIN Operators changer ON changer.CodigoFJ = h.ChangedByCodigoFJ
                WHERE h.MasterCardId = @masterCardId
                ORDER BY h.Id DESC;";

            return conn.Query<HistoryRow>(sql, new { masterCardId })
                .Select(item => new
                {
                    id = item.Id,
                    previousStatus = item.PreviousStatus,
                    previousStatusLabelPt = string.IsNullOrWhiteSpace(item.PreviousStatus)
                        ? string.Empty
                        : GetStatusLabel(item.PreviousStatus, "pt-BR"),
                    previousStatusLabelJp = string.IsNullOrWhiteSpace(item.PreviousStatus)
                        ? string.Empty
                        : GetStatusLabel(item.PreviousStatus, "ja-JP"),
                    newStatus = item.NewStatus,
                    newStatusLabelPt = GetStatusLabel(item.NewStatus, "pt-BR"),
                    newStatusLabelJp = GetStatusLabel(item.NewStatus, "ja-JP"),
                    changedByNamePt = item.ChangedByNamePt,
                    changedByNameJp = item.ChangedByNameJp,
                    changedAt = item.ChangedAt,
                    note = item.Note
                })
                .Cast<object>()
                .ToList();
        }

        internal static int SaveMasterCard(
            IDbConnection conn,
            IDbTransaction tx,
            MasterCardInput input,
            string createdByCodigoFJ)
        {
            ValidateInput(conn, input, false, 0);
            var resolvedDescription = ResolveEquipmentDescription(conn, input.EquipmentId);

            var now = DateTime.Now;
            var newId = conn.QuerySingle<int>(
                @"
                    INSERT INTO MasterCards
                    (
                        OperatorCodigoFJ,
                        TrainerCodigoFJ,
                        SectorId,
                        EquipmentId,
                        ProcessDescription,
                        Notes,
                        StartDate,
                        Status,
                        CreatedByCodigoFJ,
                        CreatedAt,
                        UpdatedAt
                    )
                    VALUES
                    (
                        @OperatorCodigoFJ,
                        @TrainerCodigoFJ,
                        @SectorId,
                        @EquipmentId,
                        @Description,
                        @Notes,
                        @StartDate,
                        'in_progress',
                        @CreatedByCodigoFJ,
                        @CreatedAt,
                        @UpdatedAt
                    );
                    SELECT last_insert_rowid();",
                new
                {
                    OperatorCodigoFJ = input.OperatorCodigoFJ,
                    TrainerCodigoFJ = input.TrainerCodigoFJ,
                    SectorId = input.SectorId,
                    EquipmentId = input.EquipmentId,
                    Description = resolvedDescription,
                    Notes = NullIfEmpty(input.Notes),
                    StartDate = input.StartDate,
                    CreatedByCodigoFJ = createdByCodigoFJ,
                    CreatedAt = now.ToString("yyyy-MM-dd HH:mm:ss"),
                    UpdatedAt = now.ToString("yyyy-MM-dd HH:mm:ss")
                },
                tx
            );

            InsertHistory(
                conn,
                tx,
                newId,
                null,
                "in_progress",
                createdByCodigoFJ,
                "MasterCard criado."
            );

            return newId;
        }

        internal static void UpdateMasterCard(
            IDbConnection conn,
            IDbTransaction tx,
            int id,
            MasterCardInput input)
        {
            ValidateInput(conn, input, true, id);
            var resolvedDescription = ResolveEquipmentDescription(conn, input.EquipmentId);

            var current = GetMasterCardState(conn, id)
                ?? throw new InvalidOperationException(L("MasterCard nao encontrado para edicao.", "編集中の MasterCard が見つかりません。"));

            conn.Execute(
                @"
                    UPDATE MasterCards
                    SET
                        OperatorCodigoFJ = @OperatorCodigoFJ,
                        TrainerCodigoFJ = @TrainerCodigoFJ,
                        SectorId = @SectorId,
                        EquipmentId = @EquipmentId,
                        ProcessDescription = @Description,
                        Notes = @Notes,
                        StartDate = @StartDate,
                        UpdatedAt = @UpdatedAt
                    WHERE Id = @Id;",
                new
                {
                    Id = id,
                    OperatorCodigoFJ = input.OperatorCodigoFJ,
                    TrainerCodigoFJ = input.TrainerCodigoFJ,
                    SectorId = input.SectorId,
                    EquipmentId = input.EquipmentId,
                    Description = resolvedDescription,
                    Notes = NullIfEmpty(input.Notes),
                    StartDate = input.StartDate,
                    UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                },
                tx
            );

            InsertHistory(
                conn,
                tx,
                id,
                current.Status,
                current.Status,
                input.ChangedByCodigoFJ,
                "Cadastro atualizado."
            );
        }

        internal static string AdvanceStatus(
            IDbConnection conn,
            IDbTransaction tx,
            int id,
            string changedByCodigoFJ)
        {
            var current = GetMasterCardState(conn, id)
                ?? throw new InvalidOperationException(L("MasterCard nao encontrado para atualizar status.", "ステータス更新対象の MasterCard が見つかりません。"));

            var now = DateTime.Now;

            if (string.Equals(current.Status, "in_progress", StringComparison.OrdinalIgnoreCase))
            {
                conn.Execute(
                    @"
                        UPDATE MasterCards
                        SET
                            Status = 'follow',
                            ConcludedAt = @ConcludedAt,
                            FollowDate = @FollowDate,
                            UpdatedAt = @UpdatedAt
                        WHERE Id = @Id;",
                    new
                    {
                        Id = id,
                        ConcludedAt = now.ToString("yyyy-MM-dd HH:mm:ss"),
                        FollowDate = now.Date.AddDays(30).ToString("yyyy-MM-dd"),
                        UpdatedAt = now.ToString("yyyy-MM-dd HH:mm:ss")
                    },
                    tx
                );

                InsertHistory(
                    conn,
                    tx,
                    id,
                    current.Status,
                    "follow",
                    changedByCodigoFJ,
                    "MasterCard concluido. Follow agendado para 30 dias."
                );

                return "follow";
            }

            if (string.Equals(current.Status, "follow", StringComparison.OrdinalIgnoreCase))
            {
                conn.Execute(
                    @"
                        UPDATE MasterCards
                        SET
                            Status = 'completed',
                            FinalizedAt = @FinalizedAt,
                            UpdatedAt = @UpdatedAt
                        WHERE Id = @Id;",
                    new
                    {
                        Id = id,
                        FinalizedAt = now.ToString("yyyy-MM-dd HH:mm:ss"),
                        UpdatedAt = now.ToString("yyyy-MM-dd HH:mm:ss")
                    },
                    tx
                );

                InsertHistory(
                    conn,
                    tx,
                    id,
                    current.Status,
                    "completed",
                    changedByCodigoFJ,
                    "Follow concluido. MasterCard finalizado."
                );

                return "completed";
            }

            throw new InvalidOperationException(L("Este MasterCard ja esta finalizado.", "この MasterCard はすでに完了しています。"));
        }

        internal static int CountByStatus(IDbConnection conn, string status)
        {

            return conn.ExecuteScalar<int>(
                "SELECT COUNT(1) FROM MasterCards WHERE Status = @status;",
                new { status }
            );
        }

        internal static List<MasterCardRow> QueryReportRows(IDbConnection conn, MasterCardReportFilter filter)
        {
            const string sql = @"
                SELECT
                    m.Id,
                    m.OperatorCodigoFJ,
                    COALESCE(op.NameRomanji, m.OperatorCodigoFJ) AS OperatorNamePt,
                    COALESCE(NULLIF(op.NameNihongo, ''), op.NameRomanji, m.OperatorCodigoFJ) AS OperatorNameJp,
                    m.TrainerCodigoFJ,
                    COALESCE(tr.NameRomanji, m.TrainerCodigoFJ) AS TrainerNamePt,
                    COALESCE(NULLIF(tr.NameNihongo, ''), tr.NameRomanji, m.TrainerCodigoFJ) AS TrainerNameJp,
                    m.SectorId,
                    COALESCE(sec.NamePt, '') AS SectorNamePt,
                    COALESCE(NULLIF(sec.NameJp, ''), sec.NamePt, '') AS SectorNameJp,
                    m.EquipmentId,
                    COALESCE(eq.NamePt, '') AS EquipmentNamePt,
                    COALESCE(NULLIF(eq.NameJp, ''), eq.NamePt, '') AS EquipmentNameJp,
                    m.ProcessDescription AS Description,
                    COALESCE(m.Notes, '') AS Notes,
                    substr(m.StartDate, 1, 10) AS StartDate,
                    m.Status,
                    CASE WHEN m.ConcludedAt IS NULL THEN '' ELSE substr(m.ConcludedAt, 1, 16) END AS ConcludedAt,
                    CASE WHEN m.FollowDate IS NULL THEN '' ELSE substr(m.FollowDate, 1, 10) END AS FollowDate,
                    CASE WHEN m.FinalizedAt IS NULL THEN '' ELSE substr(m.FinalizedAt, 1, 16) END AS FinalizedAt,
                    substr(m.CreatedAt, 1, 16) AS CreatedAt,
                    substr(m.UpdatedAt, 1, 16) AS UpdatedAt,
                    COALESCE(creator.NameRomanji, m.CreatedByCodigoFJ) AS CreatedByNamePt,
                    COALESCE(NULLIF(creator.NameNihongo, ''), creator.NameRomanji, m.CreatedByCodigoFJ) AS CreatedByNameJp,
                    (
                        SELECT COUNT(1)
                        FROM MasterCardStatusHistory h
                        WHERE h.MasterCardId = m.Id
                    ) AS HistoryCount
                FROM MasterCards m
                LEFT JOIN Operators op ON op.CodigoFJ = m.OperatorCodigoFJ
                LEFT JOIN Operators tr ON tr.CodigoFJ = m.TrainerCodigoFJ
                LEFT JOIN Operators creator ON creator.CodigoFJ = m.CreatedByCodigoFJ
                LEFT JOIN Sectors sec ON sec.Id = m.SectorId
                LEFT JOIN Equipments eq ON eq.Id = m.EquipmentId
                WHERE (@masterCardId = 0 OR m.Id = @masterCardId)
                  AND (
                        @masterCardId > 0
                        OR (
                            (m.Status = 'completed' AND date(COALESCE(m.FinalizedAt, m.ConcludedAt, m.StartDate)) BETWEEN date(@start) AND date(@end))
                            OR (m.Status <> 'completed' AND date(m.StartDate) <= date(@end))
                        )
                  )
                  AND (@sectorId = 0 OR m.SectorId = @sectorId)
                  AND (@equipmentId = 0 OR m.EquipmentId = @equipmentId)
                  AND (@status = '' OR m.Status = @status)
                  AND (@operatorCodigoFJ = '' OR m.OperatorCodigoFJ = @operatorCodigoFJ)
                  AND (@trainerCodigoFJ = '' OR m.TrainerCodigoFJ = @trainerCodigoFJ)
                  AND (
                        @search = ''
                        OR COALESCE(m.ProcessDescription, '') LIKE '%' || @search || '%'
                        OR COALESCE(m.Notes, '') LIKE '%' || @search || '%'
                        OR COALESCE(op.NameRomanji, '') LIKE '%' || @search || '%'
                        OR COALESCE(op.NameNihongo, '') LIKE '%' || @search || '%'
                        OR COALESCE(tr.NameRomanji, '') LIKE '%' || @search || '%'
                        OR COALESCE(tr.NameNihongo, '') LIKE '%' || @search || '%'
                        OR COALESCE(sec.NamePt, '') LIKE '%' || @search || '%'
                        OR COALESCE(sec.NameJp, '') LIKE '%' || @search || '%'
                        OR COALESCE(eq.NamePt, '') LIKE '%' || @search || '%'
                        OR COALESCE(eq.NameJp, '') LIKE '%' || @search || '%'
                    )
                ORDER BY
                    CASE m.Status
                        WHEN 'in_progress' THEN 0
                        WHEN 'follow' THEN 1
                        ELSE 2
                    END,
                    date(m.FollowDate) ASC,
                    date(m.StartDate) DESC,
                    m.Id DESC;";

            return conn.Query<MasterCardRow>(sql, new
            {
                masterCardId = filter.MasterCardId,
                start = filter.Start.ToString("yyyy-MM-dd"),
                end = filter.End.ToString("yyyy-MM-dd"),
                sectorId = filter.SectorId,
                equipmentId = filter.EquipmentId,
                status = filter.Status,
                operatorCodigoFJ = filter.OperatorCodigoFJ,
                trainerCodigoFJ = filter.TrainerCodigoFJ,
                search = filter.Search
            }).ToList();
        }

        internal static MasterCardReportFilter CreateDefaultFilter()
        {
            var today = DateTime.Today;
            return new MasterCardReportFilter
            {
                Start = today.AddDays(-90),
                End = today,
                Status = string.Empty,
                Search = string.Empty
            };
        }

        private static void ValidateInput(IDbConnection conn, MasterCardInput input, bool isUpdate, int id)
        {
            if (isUpdate && id <= 0)
                throw new InvalidOperationException(L("Selecione um MasterCard valido para editar.", "編集対象の MasterCard を選択してください。"));

            if (string.IsNullOrWhiteSpace(input.OperatorCodigoFJ))
                throw new InvalidOperationException(L("Selecione o operador do MasterCard.", "MasterCard の作業者を選択してください。"));

            if (string.IsNullOrWhiteSpace(input.TrainerCodigoFJ))
                throw new InvalidOperationException(L("Selecione o treinador do MasterCard.", "MasterCard の指導者を選択してください。"));

            if (string.Equals(input.OperatorCodigoFJ, input.TrainerCodigoFJ, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(L("Operador e treinador nao podem ser a mesma pessoa.", "作業者と指導者は同一にできません。"));

            if (input.SectorId <= 0)
                throw new InvalidOperationException(L("Selecione o setor do MasterCard.", "MasterCard のセクターを選択してください。"));

            if (input.EquipmentId <= 0)
                throw new InvalidOperationException(L("Selecione o equipamento do MasterCard.", "MasterCard の設備を選択してください。"));

            if (!DateTime.TryParse(input.StartDate, out _))
                throw new InvalidOperationException(L("Informe uma data de inicio valida.", "有効な開始日を入力してください。"));

            var operatorExists = conn.ExecuteScalar<int>(
                @"
                    SELECT COUNT(1)
                    FROM Operators
                    WHERE CodigoFJ = @codigoFJ
                      AND Status = 1
                      AND SectorId = @sectorId;",
                new { codigoFJ = input.OperatorCodigoFJ, sectorId = input.SectorId }
            ) > 0;

            if (!operatorExists)
                throw new InvalidOperationException(L("O operador selecionado nao esta ativo.", "選択した作業者は有効ではありません。"));

            var trainerExists = conn.ExecuteScalar<int>(
                @"
                    SELECT COUNT(1)
                    FROM Operators
                    WHERE CodigoFJ = @codigoFJ
                      AND Status = 1
                      AND Trainer = 1
                      AND SectorId = @sectorId;",
                new { codigoFJ = input.TrainerCodigoFJ, sectorId = input.SectorId }
            ) > 0;

            if (!trainerExists)
                throw new InvalidOperationException(L("O treinador selecionado nao esta habilitado como treinador.", "選択した指導者は Trainer として登録されていません。"));
        }

        private static string ResolveEquipmentDescription(IDbConnection conn, int equipmentId)
        {
            var equipmentName = conn.ExecuteScalar<string?>(
                @"
                    SELECT COALESCE(NULLIF(NamePt, ''), NameJp, '')
                    FROM Equipments
                    WHERE Id = @equipmentId;",
                new { equipmentId });

            if (string.IsNullOrWhiteSpace(equipmentName))
                throw new InvalidOperationException(L("Equipamento nao encontrado para o MasterCard.", "MasterCard 用の設備が見つかりません。"));

            return equipmentName.Trim();
        }

        private static MasterCardStateRow? GetMasterCardState(IDbConnection conn, int id)
        {
            return conn.QueryFirstOrDefault<MasterCardStateRow>(
                @"
                    SELECT
                        Id,
                        Status
                    FROM MasterCards
                    WHERE Id = @id;",
                new { id }
            );
        }

        private static void InsertHistory(
            IDbConnection conn,
            IDbTransaction tx,
            int masterCardId,
            string? previousStatus,
            string newStatus,
            string changedByCodigoFJ,
            string note)
        {
            conn.Execute(
                @"
                    INSERT INTO MasterCardStatusHistory
                    (
                        MasterCardId,
                        PreviousStatus,
                        NewStatus,
                        ChangedByCodigoFJ,
                        ChangedAt,
                        Note
                    )
                    VALUES
                    (
                        @MasterCardId,
                        @PreviousStatus,
                        @NewStatus,
                        @ChangedByCodigoFJ,
                        @ChangedAt,
                        @Note
                    );",
                new
                {
                    MasterCardId = masterCardId,
                    PreviousStatus = previousStatus,
                    NewStatus = newStatus,
                    ChangedByCodigoFJ = changedByCodigoFJ,
                    ChangedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    Note = note
                },
                tx
            );
        }

        internal static string GetStatusLabel(string value, string locale)
        {
            var match = Statuses.FirstOrDefault(item => item.Value == value);
            if (match == default)
                return value;

            return string.Equals(locale, "ja-JP", StringComparison.OrdinalIgnoreCase)
                ? match.LabelJp
                : match.LabelPt;
        }

        private static string? NullIfEmpty(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string L(string pt, string jp)
        {
            return string.Equals(Program.CurrentLocale, "ja-JP", StringComparison.OrdinalIgnoreCase)
                ? jp
                : pt;
        }

        internal sealed class MasterCardInput
        {
            public string OperatorCodigoFJ { get; set; } = string.Empty;
            public string TrainerCodigoFJ { get; set; } = string.Empty;
            public int SectorId { get; set; }
            public int EquipmentId { get; set; }
            public string Notes { get; set; } = string.Empty;
            public string StartDate { get; set; } = string.Empty;
            public string ChangedByCodigoFJ { get; set; } = string.Empty;
        }

        internal sealed class MasterCardReportFilter
        {
            public int MasterCardId { get; set; }
            public DateTime Start { get; set; }
            public DateTime End { get; set; }
            public int SectorId { get; set; }
            public int EquipmentId { get; set; }
            public string Status { get; set; } = string.Empty;
            public string OperatorCodigoFJ { get; set; } = string.Empty;
            public string TrainerCodigoFJ { get; set; } = string.Empty;
            public string Search { get; set; } = string.Empty;
        }

        internal sealed class MasterCardRow
        {
            public int Id { get; set; }
            public string OperatorCodigoFJ { get; set; } = string.Empty;
            public string OperatorNamePt { get; set; } = string.Empty;
            public string OperatorNameJp { get; set; } = string.Empty;
            public string TrainerCodigoFJ { get; set; } = string.Empty;
            public string TrainerNamePt { get; set; } = string.Empty;
            public string TrainerNameJp { get; set; } = string.Empty;
            public int SectorId { get; set; }
            public string SectorNamePt { get; set; } = string.Empty;
            public string SectorNameJp { get; set; } = string.Empty;
            public int EquipmentId { get; set; }
            public string EquipmentNamePt { get; set; } = string.Empty;
            public string EquipmentNameJp { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string Notes { get; set; } = string.Empty;
            public string StartDate { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public string ConcludedAt { get; set; } = string.Empty;
            public string FollowDate { get; set; } = string.Empty;
            public string FinalizedAt { get; set; } = string.Empty;
            public string CreatedAt { get; set; } = string.Empty;
            public string UpdatedAt { get; set; } = string.Empty;
            public string CreatedByNamePt { get; set; } = string.Empty;
            public string CreatedByNameJp { get; set; } = string.Empty;
            public int HistoryCount { get; set; }
        }

        private sealed class LookupRow
        {
            public string CodigoFJ { get; set; } = string.Empty;
            public string NamePt { get; set; } = string.Empty;
            public string NameJp { get; set; } = string.Empty;
            public int ShiftId { get; set; }
            public int SectorId { get; set; }
        }

        private sealed class SectorLookupRow
        {
            public int Id { get; set; }
            public string NamePt { get; set; } = string.Empty;
            public string NameJp { get; set; } = string.Empty;
        }

        private sealed class HistoryRow
        {
            public int Id { get; set; }
            public string PreviousStatus { get; set; } = string.Empty;
            public string NewStatus { get; set; } = string.Empty;
            public string ChangedByNamePt { get; set; } = string.Empty;
            public string ChangedByNameJp { get; set; } = string.Empty;
            public string ChangedAt { get; set; } = string.Empty;
            public string Note { get; set; } = string.Empty;
        }

        private sealed class MasterCardStateRow
        {
            public int Id { get; set; }
            public string Status { get; set; } = string.Empty;
        }
    }
}
