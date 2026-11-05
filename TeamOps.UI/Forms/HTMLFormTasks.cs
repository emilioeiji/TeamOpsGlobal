using Dapper;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using TeamOps.Core.Entities;
using TeamOps.Core.Security;
using TeamOps.Data.Db;
using TeamOps.Data.Security;
using TeamOps.UI.Forms.Models;

namespace TeamOps.UI.Forms
{
    public partial class HTMLFormTasks : Form
    {
        private readonly SqliteConnectionFactory _factory;
        private readonly User _currentUser;
        private readonly Operator _currentOperator;
        private readonly AuthorizationService _authorization;

        private static readonly (string Value, string LabelPt, string LabelJp)[] TaskStatuses =
        {
            ("pending", "Pendente", "未着手"),
            ("in_progress", "Em andamento", "進行中"),
            ("blocked", "Bloqueada", "保留"),
            ("completed", "Concluida", "完了"),
            ("cancelled", "Cancelada", "取消")
        };

        public HTMLFormTasks(
            SqliteConnectionFactory factory,
            User currentUser,
            Operator currentOperator)
        {
            InitializeComponent();
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            _factory = factory;
            _currentUser = currentUser;
            _currentOperator = currentOperator;
            _authorization = new AuthorizationService(factory);

            InitializeWebView();
        }

        private async void InitializeWebView()
        {
            await webViewTasks.EnsureCoreWebView2Async(null);

            var core = webViewTasks.CoreWebView2;

            core.Settings.IsWebMessageEnabled = true;
            core.Settings.AreDefaultScriptDialogsEnabled = true;
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.Settings.AreDevToolsEnabled = true;

            core.WebMessageReceived += WebMessageReceived;

            core.SetVirtualHostNameToFolderMapping(
                "app",
                Path.Combine(Application.StartupPath, "ui", "tasks"),
                CoreWebView2HostResourceAccessKind.Allow
            );

            core.Navigate("https://app/index.html");
        }

        private void WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            var msg = JsonSerializer.Deserialize<JsRequest>(e.WebMessageAsJson);
            if (msg == null)
                return;

            switch (msg.action)
            {
                case "load":
                    LoadInitialData();
                    break;

                case "load_details":
                    LoadTaskDetails(msg.id);
                    break;

                case "save":
                    SaveTask(msg);
                    break;

                case "update":
                    UpdateTask(msg);
                    break;

                case "change_status":
                    ChangeStatus(msg);
                    break;
            }
        }

        private void LoadInitialData()
        {
            _authorization.Demand(_currentUser.Id, ModuleCodes.Tasks, PermissionActions.View);
            var context = _authorization.GetContext(_currentUser.Id);
            using var conn = _factory.CreateOpenConnection();
            EnsureSchema(conn);

            PostJson(new
            {
                type = "init",
                locale = Program.CurrentLocale,
                currentOperatorNamePt = _currentOperator.NameRomanji,
                currentOperatorNameJp = string.IsNullOrWhiteSpace(_currentOperator.NameNihongo)
                    ? _currentOperator.NameRomanji
                    : _currentOperator.NameNihongo,
                currentUser = _currentUser.Name,
                statuses = TaskStatuses.Select(status => new
                {
                    value = status.Value,
                    labelPt = status.LabelPt,
                    labelJp = status.LabelJp
                }),
                shifts = QueryShifts(conn).ToList(),
                sectors = _authorization.GetAuthorizedSectors(context).Select(item => new
                {
                    id = item.Id,
                    namePt = item.NamePt,
                    nameJp = string.IsNullOrWhiteSpace(item.NameJp) ? item.NamePt : item.NameJp
                }).ToList(),
                defaultSectorId = Program.CurrentSession?.SelectedSectorId
                    ?? context.AuthorizedSectorIds.FirstOrDefault(),
                leaders = QueryLeaders(conn, context.AuthorizedSectorIds).ToList(),
                rows = QueryTasks(conn, context.AuthorizedSectorIds).ToList()
            });
        }

        private void LoadTaskDetails(int taskId)
        {
            try
            {
                using var conn = _factory.CreateOpenConnection();
                EnsureSchema(conn);

                var current = GetTaskState(conn, taskId);
                if (current == null)
                    throw new InvalidOperationException(L("Task nao encontrada.", "Task が見つかりません。"));
                _authorization.Demand(_currentUser.Id, ModuleCodes.Tasks, PermissionActions.View, current.SectorId);

                PostJson(new
                {
                    type = "task_details",
                    id = taskId,
                    history = QueryTaskHistory(conn, taskId).ToList()
                });
            }
            catch (Exception ex)
            {
                PostJson(new
                {
                    type = "error",
                    message = ex.Message
                });
            }
        }

        private void SaveTask(JsRequest msg)
        {
            try
            {
                using var conn = _factory.CreateOpenConnection();
                EnsureSchema(conn);

                var description = NormalizeDescription(msg.description);
                var dueDate = ParseDueDate(msg.dueDate);
                var taskStatus = NormalizeTaskStatus(msg.taskStatus);
                var leaderCodigoFJ = NormalizeLeader(msg.leaderCodigoFJ);
                _authorization.Demand(_currentUser.Id, ModuleCodes.Tasks, PermissionActions.Create, msg.sectorId);

                ValidatePayload(conn, description, dueDate, msg.shiftId, msg.sectorId, leaderCodigoFJ, taskStatus, false, 0);

                var now = DateTime.Now;
                var timestamps = CreateTimestampsForNewTask(taskStatus, now);

                using var tx = conn.BeginTransaction();

                var newId = conn.QuerySingle<int>(
                    @"
                        INSERT INTO Tasks
                        (
                            Description,
                            DueDate,
                            ShiftId,
                            SectorId,
                            AssigneeCodigoFJ,
                            Status,
                            CreatedByCodigoFJ,
                            CreatedAt,
                            UpdatedAt,
                            StartedAt,
                            CompletedAt,
                            CancelledAt
                        )
                        VALUES
                        (
                            @Description,
                            @DueDate,
                            @ShiftId,
                            @SectorId,
                            @AssigneeCodigoFJ,
                            @Status,
                            @CreatedByCodigoFJ,
                            @CreatedAt,
                            @UpdatedAt,
                            @StartedAt,
                            @CompletedAt,
                            @CancelledAt
                        );
                        SELECT last_insert_rowid();",
                    new
                    {
                        Description = description,
                        DueDate = dueDate.ToString("yyyy-MM-dd"),
                        ShiftId = msg.shiftId,
                        SectorId = msg.sectorId,
                        AssigneeCodigoFJ = string.IsNullOrWhiteSpace(leaderCodigoFJ) ? null : leaderCodigoFJ,
                        Status = taskStatus,
                        CreatedByCodigoFJ = _currentOperator.CodigoFJ,
                        CreatedAt = now.ToString("yyyy-MM-dd HH:mm:ss"),
                        UpdatedAt = now.ToString("yyyy-MM-dd HH:mm:ss"),
                        StartedAt = timestamps.StartedAt?.ToString("yyyy-MM-dd HH:mm:ss"),
                        CompletedAt = timestamps.CompletedAt?.ToString("yyyy-MM-dd HH:mm:ss"),
                        CancelledAt = timestamps.CancelledAt?.ToString("yyyy-MM-dd HH:mm:ss")
                    },
                    tx
                );

                InsertTaskHistory(
                    conn,
                    tx,
                    newId,
                    null,
                    taskStatus,
                    _currentOperator.CodigoFJ,
                    L("Task criada.", "Task を作成しました。")
                );
                tx.Commit();

                PostJson(new
                {
                    type = "saved",
                    message = L("Task cadastrada com sucesso.", "Task を登録しました。")
                });

                SendRows(conn);
            }
            catch (Exception ex)
            {
                PostJson(new
                {
                    type = "error",
                    message = ex.Message
                });
            }
        }

        private void UpdateTask(JsRequest msg)
        {
            try
            {
                using var conn = _factory.CreateOpenConnection();
                EnsureSchema(conn);

                var current = GetTaskState(conn, msg.id)
                    ?? throw new InvalidOperationException(L("Task nao encontrada para edicao.", "編集対象の Task が見つかりません。"));

                var description = NormalizeDescription(msg.description);
                var dueDate = ParseDueDate(msg.dueDate);
                var taskStatus = NormalizeTaskStatus(msg.taskStatus);
                var leaderCodigoFJ = NormalizeLeader(msg.leaderCodigoFJ);
                _authorization.Demand(_currentUser.Id, ModuleCodes.Tasks, PermissionActions.Edit, current.SectorId);
                _authorization.Demand(_currentUser.Id, ModuleCodes.Tasks, PermissionActions.Edit, msg.sectorId);

                ValidatePayload(conn, description, dueDate, msg.shiftId, msg.sectorId, leaderCodigoFJ, taskStatus, true, msg.id);

                var now = DateTime.Now;
                var timestamps = ResolveTimestampsForStatus(current, taskStatus, now);
                var statusChanged = !string.Equals(current.Status, taskStatus, StringComparison.OrdinalIgnoreCase);

                using var tx = conn.BeginTransaction();

                conn.Execute(
                    @"
                        UPDATE Tasks
                        SET
                            Description = @Description,
                            DueDate = @DueDate,
                            ShiftId = @ShiftId,
                            SectorId = @SectorId,
                            AssigneeCodigoFJ = @AssigneeCodigoFJ,
                            Status = @Status,
                            UpdatedAt = @UpdatedAt,
                            StartedAt = @StartedAt,
                            CompletedAt = @CompletedAt,
                            CancelledAt = @CancelledAt
                        WHERE Id = @Id;",
                    new
                    {
                        Id = msg.id,
                        Description = description,
                        DueDate = dueDate.ToString("yyyy-MM-dd"),
                        ShiftId = msg.shiftId,
                        SectorId = msg.sectorId,
                        AssigneeCodigoFJ = string.IsNullOrWhiteSpace(leaderCodigoFJ) ? null : leaderCodigoFJ,
                        Status = taskStatus,
                        UpdatedAt = now.ToString("yyyy-MM-dd HH:mm:ss"),
                        StartedAt = timestamps.StartedAt?.ToString("yyyy-MM-dd HH:mm:ss"),
                        CompletedAt = timestamps.CompletedAt?.ToString("yyyy-MM-dd HH:mm:ss"),
                        CancelledAt = timestamps.CancelledAt?.ToString("yyyy-MM-dd HH:mm:ss")
                    },
                    tx
                );

                if (statusChanged)
                {
                    InsertTaskHistory(
                        conn,
                        tx,
                        msg.id,
                        current.Status,
                        taskStatus,
                        _currentOperator.CodigoFJ,
                        L("Status alterado na edicao da task.", "Task 編集でステータスを変更しました。")
                    );
                }

                tx.Commit();

                PostJson(new
                {
                    type = "updated",
                    message = L("Task atualizada com sucesso.", "Task を更新しました。")
                });

                SendRows(conn);
            }
            catch (Exception ex)
            {
                PostJson(new
                {
                    type = "error",
                    message = ex.Message
                });
            }
        }

        private void ChangeStatus(JsRequest msg)
        {
            try
            {
                using var conn = _factory.CreateOpenConnection();
                EnsureSchema(conn);

                var current = GetTaskState(conn, msg.id)
                    ?? throw new InvalidOperationException(L("Task nao encontrada para atualizar status.", "ステータス更新対象の Task が見つかりません。"));
                _authorization.Demand(_currentUser.Id, ModuleCodes.Tasks, PermissionActions.Edit, current.SectorId);

                var taskStatus = NormalizeTaskStatus(msg.taskStatus);
                if (string.Equals(current.Status, taskStatus, StringComparison.OrdinalIgnoreCase))
                {
                    PostJson(new
                    {
                        type = "status_changed",
                        message = L("A task ja estava com este status.", "Task はすでにこのステータスです。")
                    });
                    return;
                }

                var now = DateTime.Now;
                var timestamps = ResolveTimestampsForStatus(current, taskStatus, now);

                using var tx = conn.BeginTransaction();

                conn.Execute(
                    @"
                        UPDATE Tasks
                        SET
                            Status = @Status,
                            UpdatedAt = @UpdatedAt,
                            StartedAt = @StartedAt,
                            CompletedAt = @CompletedAt,
                            CancelledAt = @CancelledAt
                        WHERE Id = @Id;",
                    new
                    {
                        Id = msg.id,
                        Status = taskStatus,
                        UpdatedAt = now.ToString("yyyy-MM-dd HH:mm:ss"),
                        StartedAt = timestamps.StartedAt?.ToString("yyyy-MM-dd HH:mm:ss"),
                        CompletedAt = timestamps.CompletedAt?.ToString("yyyy-MM-dd HH:mm:ss"),
                        CancelledAt = timestamps.CancelledAt?.ToString("yyyy-MM-dd HH:mm:ss")
                    },
                    tx
                );

                InsertTaskHistory(
                    conn,
                    tx,
                    msg.id,
                    current.Status,
                    taskStatus,
                    _currentOperator.CodigoFJ,
                    L("Status alterado rapidamente.", "ステータスを簡易更新しました。")
                );
                tx.Commit();

                PostJson(new
                {
                    type = "status_changed",
                    message = taskStatus == "completed"
                        ? L("Task finalizada com sucesso.", "Task を完了しました。")
                        : L("Status da task atualizado com sucesso.", "Task のステータスを更新しました。")
                });

                SendRows(conn);
            }
            catch (Exception ex)
            {
                PostJson(new
                {
                    type = "error",
                    message = ex.Message
                });
            }
        }

        private void SendRows(System.Data.IDbConnection conn)
        {
            var authorizedSectorIds = _authorization.GetContext(_currentUser.Id).AuthorizedSectorIds;
            PostJson(new
            {
                type = "rows",
                data = QueryTasks(conn, authorizedSectorIds).ToList()
            });
        }

        private static string NormalizeDescription(string? description)
        {
            return (description ?? string.Empty).Trim();
        }

        private static string NormalizeLeader(string? leaderCodigoFJ)
        {
            return (leaderCodigoFJ ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static DateTime ParseDueDate(string? dueDate)
        {
            if (!DateTime.TryParse(dueDate, out var parsed))
                throw new InvalidOperationException(L("Informe uma data valida para a task.", "Task の有効な日付を入力してください。"));

            return parsed.Date;
        }

        private static string NormalizeTaskStatus(string? taskStatus)
        {
            var normalized = (taskStatus ?? string.Empty).Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(normalized))
                return "pending";

            if (!TaskStatuses.Any(status => status.Value == normalized))
                throw new InvalidOperationException(L("Selecione um status valido para a task.", "Task の有効なステータスを選択してください。"));

            return normalized;
        }

        private static string GetTaskStatusLabel(string taskStatus, string locale)
        {
            var match = TaskStatuses.FirstOrDefault(status => status.Value == taskStatus);
            if (match == default)
                return taskStatus;

            return string.Equals(locale, "ja-JP", StringComparison.OrdinalIgnoreCase)
                ? match.LabelJp
                : match.LabelPt;
        }

        private static void ValidatePayload(
            System.Data.IDbConnection conn,
            string description,
            DateTime dueDate,
            int shiftId,
            int sectorId,
            string leaderCodigoFJ,
            string taskStatus,
            bool isUpdate,
            int taskId)
        {
            if (isUpdate && taskId <= 0)
                throw new InvalidOperationException(L("Selecione uma task valida para editar.", "編集対象の Task を選択してください。"));

            if (string.IsNullOrWhiteSpace(description))
                throw new InvalidOperationException(L("Informe o que deve ser feito na task.", "Task の内容を入力してください。"));

            if (shiftId <= 0)
                throw new InvalidOperationException(L("Selecione o turno da task.", "Task のシフトを選択してください。"));

            if (sectorId <= 0)
                throw new InvalidOperationException(L("Selecione o setor da task.", "Task のセクターを選択してください。"));

            if (dueDate == default)
                throw new InvalidOperationException(L("Informe quando a task deve ser concluida.", "Task の期限を入力してください。"));

            if (!TaskStatuses.Any(status => status.Value == taskStatus))
                throw new InvalidOperationException(L("Status da task invalido.", "Task のステータスが無効です。"));

            if (!string.IsNullOrWhiteSpace(leaderCodigoFJ))
            {
                var isValidLeader = conn.ExecuteScalar<int>(
                    @"
                        SELECT COUNT(1)
                        FROM Operators
                        WHERE CodigoFJ = @codigoFJ
                          AND Status = 1
                          AND IsLeader = 1
                          AND ShiftId = @shiftId
                          AND SectorId = @sectorId;",
                    new
                    {
                        codigoFJ = leaderCodigoFJ,
                        shiftId,
                        sectorId
                    }
                ) > 0;

                if (!isValidLeader)
                    throw new InvalidOperationException(L("O lider selecionado nao pertence ao turno informado.", "選択したリーダーは指定したシフトに属していません。"));
            }
        }

        private static bool TaskExists(System.Data.IDbConnection conn, int taskId)
        {
            return conn.ExecuteScalar<int>(
                "SELECT COUNT(1) FROM Tasks WHERE Id = @id;",
                new { id = taskId }
            ) > 0;
        }

        private static TaskStateRow? GetTaskState(System.Data.IDbConnection conn, int taskId)
        {
            return conn.QueryFirstOrDefault<TaskStateRow>(
                @"
                    SELECT
                        Id,
                        SectorId,
                        Status,
                        StartedAt,
                        CompletedAt,
                        CancelledAt
                    FROM Tasks
                    WHERE Id = @id;",
                new { id = taskId }
            );
        }

        private static TaskTimestamps CreateTimestampsForNewTask(string status, DateTime now)
        {
            DateTime? startedAt = null;
            DateTime? completedAt = null;
            DateTime? cancelledAt = null;

            if (status is "in_progress" or "blocked" or "completed")
                startedAt = now;

            if (status == "completed")
                completedAt = now;

            if (status == "cancelled")
                cancelledAt = now;

            return new TaskTimestamps(startedAt, completedAt, cancelledAt);
        }

        private static TaskTimestamps ResolveTimestampsForStatus(TaskStateRow current, string newStatus, DateTime now)
        {
            DateTime? startedAt = current.StartedAt;
            DateTime? completedAt = current.CompletedAt;
            DateTime? cancelledAt = current.CancelledAt;

            if (newStatus is "in_progress" or "blocked" or "completed")
                startedAt ??= now;

            if (newStatus == "completed")
            {
                completedAt ??= now;
                cancelledAt = null;
            }
            else if (newStatus == "cancelled")
            {
                cancelledAt = now;
                completedAt = null;
            }
            else
            {
                if (string.Equals(current.Status, "completed", StringComparison.OrdinalIgnoreCase))
                    completedAt = null;

                if (string.Equals(current.Status, "cancelled", StringComparison.OrdinalIgnoreCase))
                    cancelledAt = null;
            }

            return new TaskTimestamps(startedAt, completedAt, cancelledAt);
        }

        private static void InsertTaskHistory(
            System.Data.IDbConnection conn,
            System.Data.IDbTransaction tx,
            int taskId,
            string? previousStatus,
            string newStatus,
            string changedByCodigoFJ,
            string note)
        {
            conn.Execute(
                @"
                    INSERT INTO TaskStatusHistory
                    (
                        TaskId,
                        PreviousStatus,
                        NewStatus,
                        ChangedByCodigoFJ,
                        ChangedAt,
                        Note
                    )
                    VALUES
                    (
                        @TaskId,
                        @PreviousStatus,
                        @NewStatus,
                        @ChangedByCodigoFJ,
                        @ChangedAt,
                        @Note
                    );",
                new
                {
                    TaskId = taskId,
                    PreviousStatus = previousStatus,
                    NewStatus = newStatus,
                    ChangedByCodigoFJ = changedByCodigoFJ,
                    ChangedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    Note = note
                },
                tx
            );
        }

        private static IEnumerable<object> QueryShifts(System.Data.IDbConnection conn)
        {
            const string sql = @"
                SELECT
                    Id AS id,
                    COALESCE(NamePt, '') AS namePt,
                    COALESCE(NULLIF(NameJp, ''), NamePt, '') AS nameJp
                FROM Shifts
                ORDER BY Id;";

            return conn.Query(sql).Select(row => new
            {
                id = row.id,
                namePt = row.namePt,
                nameJp = row.nameJp
            });
        }

        private static IEnumerable<object> QueryLeaders(
            System.Data.IDbConnection conn,
            IReadOnlyCollection<int> authorizedSectorIds)
        {
            const string sql = @"
                SELECT
                    CodigoFJ AS codigoFJ,
                    COALESCE(NameRomanji, CodigoFJ) AS namePt,
                    COALESCE(NULLIF(NameNihongo, ''), NameRomanji, CodigoFJ) AS nameJp,
                    ShiftId AS shiftId,
                    SectorId AS sectorId
                FROM Operators
                WHERE Status = 1
                  AND IsLeader = 1
                  AND SectorId IN @authorizedSectorIds
                ORDER BY ShiftId, NameRomanji;";

            return conn.Query(sql, new { authorizedSectorIds }).Select(row => new
            {
                codigoFJ = row.codigoFJ,
                namePt = row.namePt,
                nameJp = row.nameJp,
                shiftId = row.shiftId,
                sectorId = row.sectorId
            });
        }

        private static IEnumerable<object> QueryTasks(
            System.Data.IDbConnection conn,
            IReadOnlyCollection<int> authorizedSectorIds)
        {
            const string sql = @"
                SELECT
                    t.Id AS id,
                    t.Description AS description,
                    substr(t.DueDate, 1, 10) AS dueDate,
                    t.ShiftId AS shiftId,
                    t.SectorId AS sectorId,
                    COALESCE(sec.NamePt, '') AS sectorNamePt,
                    COALESCE(NULLIF(sec.NameJp, ''), sec.NamePt, '') AS sectorNameJp,
                    COALESCE(sh.NamePt, '') AS shiftNamePt,
                    COALESCE(NULLIF(sh.NameJp, ''), sh.NamePt, '') AS shiftNameJp,
                    COALESCE(t.AssigneeCodigoFJ, '') AS leaderCodigoFJ,
                    COALESCE(op.NameRomanji, '') AS leaderNamePt,
                    COALESCE(NULLIF(op.NameNihongo, ''), op.NameRomanji, '') AS leaderNameJp,
                    t.Status AS taskStatus,
                    substr(t.CreatedAt, 1, 16) AS createdAt,
                    substr(t.UpdatedAt, 1, 16) AS updatedAt,
                    CASE WHEN t.StartedAt IS NULL THEN '' ELSE substr(t.StartedAt, 1, 16) END AS startedAt,
                    CASE WHEN t.CompletedAt IS NULL THEN '' ELSE substr(t.CompletedAt, 1, 16) END AS completedAt,
                    CASE WHEN t.CancelledAt IS NULL THEN '' ELSE substr(t.CancelledAt, 1, 16) END AS cancelledAt,
                    COALESCE(creator.NameRomanji, t.CreatedByCodigoFJ) AS createdByNamePt,
                    COALESCE(NULLIF(creator.NameNihongo, ''), creator.NameRomanji, t.CreatedByCodigoFJ) AS createdByNameJp,
                    t.CreatedByCodigoFJ AS createdByCodigoFJ
                FROM Tasks t
                LEFT JOIN Shifts sh ON sh.Id = t.ShiftId
                LEFT JOIN Sectors sec ON sec.Id = t.SectorId
                LEFT JOIN Operators op ON op.CodigoFJ = t.AssigneeCodigoFJ
                LEFT JOIN Operators creator ON creator.CodigoFJ = t.CreatedByCodigoFJ
                WHERE t.SectorId IN @authorizedSectorIds
                ORDER BY
                    CASE WHEN t.Status IN ('completed', 'cancelled') THEN 1 ELSE 0 END,
                    date(t.DueDate) ASC,
                    t.Id DESC;";

            return conn.Query(sql, new { authorizedSectorIds }).Select(row => new
            {
                id = row.id,
                description = row.description,
                dueDate = row.dueDate,
                shiftId = row.shiftId,
                sectorId = row.sectorId,
                sectorNamePt = row.sectorNamePt,
                sectorNameJp = row.sectorNameJp,
                shiftNamePt = row.shiftNamePt,
                shiftNameJp = row.shiftNameJp,
                leaderCodigoFJ = row.leaderCodigoFJ,
                leaderNamePt = row.leaderNamePt,
                leaderNameJp = row.leaderNameJp,
                taskStatus = row.taskStatus,
                createdAt = row.createdAt,
                updatedAt = row.updatedAt,
                startedAt = row.startedAt,
                completedAt = row.completedAt,
                cancelledAt = row.cancelledAt,
                createdByNamePt = row.createdByNamePt,
                createdByNameJp = row.createdByNameJp,
                createdByCodigoFJ = row.createdByCodigoFJ
            });
        }

        private static IEnumerable<object> QueryTaskHistory(System.Data.IDbConnection conn, int taskId)
        {
            const string sql = @"
                SELECT
                    h.Id AS id,
                    COALESCE(h.PreviousStatus, '') AS previousStatus,
                    h.NewStatus AS newStatus,
                    COALESCE(changer.NameRomanji, h.ChangedByCodigoFJ) AS changedByNamePt,
                    COALESCE(NULLIF(changer.NameNihongo, ''), changer.NameRomanji, h.ChangedByCodigoFJ) AS changedByNameJp,
                    substr(h.ChangedAt, 1, 16) AS changedAt,
                    COALESCE(h.Note, '') AS note
                FROM TaskStatusHistory h
                LEFT JOIN Operators changer ON changer.CodigoFJ = h.ChangedByCodigoFJ
                WHERE h.TaskId = @taskId
                ORDER BY h.Id DESC;";

            return conn.Query(sql, new { taskId }).Select(row => new
            {
                id = row.id,
                previousStatus = row.previousStatus,
                previousStatusLabelPt = string.IsNullOrWhiteSpace((string)row.previousStatus)
                    ? string.Empty
                    : GetTaskStatusLabel((string)row.previousStatus, "pt-BR"),
                previousStatusLabelJp = string.IsNullOrWhiteSpace((string)row.previousStatus)
                    ? string.Empty
                    : GetTaskStatusLabel((string)row.previousStatus, "ja-JP"),
                newStatus = row.newStatus,
                newStatusLabelPt = GetTaskStatusLabel((string)row.newStatus, "pt-BR"),
                newStatusLabelJp = GetTaskStatusLabel((string)row.newStatus, "ja-JP"),
                changedByNamePt = row.changedByNamePt,
                changedByNameJp = row.changedByNameJp,
                changedAt = row.changedAt,
                note = row.note
            });
        }

        private static void EnsureSchema(System.Data.IDbConnection conn)
        {
            conn.Execute(
                @"
                    CREATE TABLE IF NOT EXISTS Tasks (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Description TEXT NOT NULL,
                        DueDate DATE NOT NULL,
                        ShiftId INTEGER NOT NULL,
                        SectorId INTEGER NOT NULL,
                        AssigneeCodigoFJ TEXT,
                        Status TEXT NOT NULL,
                        CreatedByCodigoFJ TEXT NOT NULL,
                        CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        StartedAt DATETIME,
                        CompletedAt DATETIME,
                        CancelledAt DATETIME,
                        FOREIGN KEY (ShiftId) REFERENCES Shifts(Id),
                        FOREIGN KEY (SectorId) REFERENCES Sectors(Id),
                        FOREIGN KEY (AssigneeCodigoFJ) REFERENCES Operators(CodigoFJ),
                        FOREIGN KEY (CreatedByCodigoFJ) REFERENCES Operators(CodigoFJ)
                    );

                    CREATE TABLE IF NOT EXISTS TaskStatusHistory (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        TaskId INTEGER NOT NULL,
                        PreviousStatus TEXT,
                        NewStatus TEXT NOT NULL,
                        ChangedByCodigoFJ TEXT NOT NULL,
                        ChangedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        Note TEXT,
                        FOREIGN KEY (TaskId) REFERENCES Tasks(Id),
                        FOREIGN KEY (ChangedByCodigoFJ) REFERENCES Operators(CodigoFJ)
                    );

                    CREATE INDEX IF NOT EXISTS IX_Tasks_Status_DueDate
                    ON Tasks(Status, DueDate);

                    CREATE INDEX IF NOT EXISTS IX_Tasks_Shift_Assignee
                    ON Tasks(ShiftId, AssigneeCodigoFJ);

                    CREATE INDEX IF NOT EXISTS IX_Tasks_Sector_Status_DueDate
                    ON Tasks(SectorId, Status, DueDate);

                    CREATE INDEX IF NOT EXISTS IX_TaskStatusHistory_TaskId
                    ON TaskStatusHistory(TaskId, ChangedAt);"
            );
        }

        private void PostJson(object payload)
        {
            webViewTasks.CoreWebView2.PostWebMessageAsJson(
                JsonSerializer.Serialize(payload)
            );
        }

        private static string L(string pt, string jp)
        {
            return string.Equals(Program.CurrentLocale, "ja-JP", StringComparison.OrdinalIgnoreCase)
                ? jp
                : pt;
        }

        private sealed class TaskStateRow
        {
            public int Id { get; set; }
            public int SectorId { get; set; }
            public string Status { get; set; } = string.Empty;
            public DateTime? StartedAt { get; set; }
            public DateTime? CompletedAt { get; set; }
            public DateTime? CancelledAt { get; set; }
        }

        private sealed class TaskTimestamps
        {
            public TaskTimestamps(DateTime? startedAt, DateTime? completedAt, DateTime? cancelledAt)
            {
                StartedAt = startedAt;
                CompletedAt = completedAt;
                CancelledAt = cancelledAt;
            }

            public DateTime? StartedAt { get; }
            public DateTime? CompletedAt { get; }
            public DateTime? CancelledAt { get; }
        }
    }
}
