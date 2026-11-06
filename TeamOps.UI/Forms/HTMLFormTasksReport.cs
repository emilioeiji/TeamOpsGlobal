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

namespace TeamOps.UI.Forms
{
    public partial class HTMLFormTasksReport : Form
    {
        private readonly SqliteConnectionFactory _factory;
        private readonly User _currentUser;
        private readonly AuthorizationService _authorization;

        private static readonly (string Value, string LabelPt, string LabelJp)[] TaskStatuses =
        {
            ("pending", "Pendente", "\u672a\u7740\u624b"),
            ("in_progress", "Em andamento", "\u9032\u884c\u4e2d"),
            ("blocked", "Bloqueada", "\u4fdd\u7559"),
            ("completed", "Concluida", "\u5b8c\u4e86"),
            ("cancelled", "Cancelada", "\u53d6\u6d88")
        };

        public HTMLFormTasksReport(SqliteConnectionFactory factory, User currentUser)
        {
            InitializeComponent();
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Text = L("Relatorio de Tasks", "Tasks Report");

            _factory = factory;
            _currentUser = currentUser;
            _authorization = new AuthorizationService(factory);

            InitializeWebView();
        }

        private async void InitializeWebView()
        {
            await webViewTasksReport.EnsureCoreWebView2Async(null);

            var core = webViewTasksReport.CoreWebView2;
            core.Settings.IsWebMessageEnabled = true;
            core.Settings.AreDefaultScriptDialogsEnabled = true;
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.Settings.AreDevToolsEnabled = true;

            core.WebMessageReceived += WebMessageReceived;

            core.SetVirtualHostNameToFolderMapping(
                "app",
                Path.Combine(Application.StartupPath, "ui", "tasks-report"),
                CoreWebView2HostResourceAccessKind.Allow
            );

            core.Navigate("https://app/index.html");
        }

        private void WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                using var json = JsonDocument.Parse(e.WebMessageAsJson);
                var root = json.RootElement;
                var action = ReadString(root, "action");

                switch (action)
                {
                    case "load":
                        LoadInitial();
                        break;

                    case "apply":
                        SendRows(ReadFilter(root));
                        break;

                    case "details":
                        SendDetails(ReadInt(root, "id"));
                        break;
                }
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

        private void LoadInitial()
        {
            _authorization.Demand(_currentUser.Id, ModuleCodes.Tasks, PermissionActions.View);
            var context = _authorization.GetContext(_currentUser.Id);
            using var conn = _factory.CreateOpenConnection();

            var filter = CreateDefaultFilter();
            filter.SectorId = Program.CurrentSession?.SelectedSectorId
                ?? (string.Equals(context.ScopeMode, "OrganizationTree", StringComparison.Ordinal)
                    ? 0
                    : context.AuthorizedSectorIds.FirstOrDefault());

            PostJson(new
            {
                type = "init",
                data = new
                {
                    locale = Program.CurrentLocale,
                    allowAggregate = string.Equals(context.ScopeMode, "OrganizationTree", StringComparison.Ordinal),
                    filters = new
                    {
                        shifts = QueryShifts(conn).ToList(),
                        sectors = _authorization.GetAuthorizedSectors(context).Select(item => new
                        {
                            id = item.Id,
                            namePt = item.NamePt,
                            nameJp = string.IsNullOrWhiteSpace(item.NameJp) ? item.NamePt : item.NameJp
                        }).ToList(),
                        statuses = TaskStatuses.Select(status => new
                        {
                            value = status.Value,
                            labelPt = status.LabelPt,
                            labelJp = status.LabelJp
                        }).ToList()
                    },
                    defaults = new
                    {
                        dtInicial = filter.Start.ToString("yyyy-MM-dd"),
                        dtFinal = filter.End.ToString("yyyy-MM-dd"),
                        shiftId = 0,
                        sectorId = filter.SectorId,
                        status = "",
                        search = ""
                    }
                }
            });

            SendRows(filter);
        }

        private void SendRows(TaskReportFilter filter)
        {
            _authorization.DemandSectorOrAggregate(
                _currentUser.Id,
                ModuleCodes.Tasks,
                PermissionActions.View,
                filter.SectorId);
            var authorizedSectorIds = _authorization.GetContext(_currentUser.Id).AuthorizedSectorIds;
            using var conn = _factory.CreateOpenConnection();

            var rows = QueryTasks(conn, filter, authorizedSectorIds).ToList();

            PostJson(new
            {
                type = "rows",
                data = new
                {
                    totals = new
                    {
                        total = rows.Count,
                        open = rows.Count(item => item.TaskStatus is not ("completed" or "cancelled")),
                        completed = rows.Count(item => item.TaskStatus == "completed"),
                        overdue = rows.Count(item => item.TaskStatus is not ("completed" or "cancelled") && item.DueDate.Date < DateTime.Today)
                    },
                    rows = rows.Select(item => new
                    {
                        id = item.Id,
                        description = item.Description,
                        dueDate = item.DueDate.ToString("yyyy-MM-dd"),
                        shiftNamePt = item.ShiftNamePt,
                        shiftNameJp = item.ShiftNameJp,
                        sectorNamePt = item.SectorNamePt,
                        sectorNameJp = item.SectorNameJp,
                        leaderNamePt = item.LeaderNamePt,
                        leaderNameJp = item.LeaderNameJp,
                        createdByNamePt = item.CreatedByNamePt,
                        createdByNameJp = item.CreatedByNameJp,
                        taskStatus = item.TaskStatus,
                        createdAt = item.CreatedAt,
                        updatedAt = item.UpdatedAt,
                        startedAt = item.StartedAt,
                        completedAt = item.CompletedAt,
                        cancelledAt = item.CancelledAt,
                        historyCount = item.HistoryCount
                    }).ToList()
                }
            });
        }

        private void SendDetails(int taskId)
        {
            using var conn = _factory.CreateOpenConnection();

            var row = QueryTasks(conn, new TaskReportFilter
            {
                TaskId = taskId,
                Start = DateTime.MinValue,
                End = DateTime.MaxValue
            }, _authorization.GetContext(_currentUser.Id).AuthorizedSectorIds).FirstOrDefault();

            if (row == null)
                throw new InvalidOperationException(L("Task nao encontrada.", "Task \u304c\u898b\u3064\u304b\u308a\u307e\u305b\u3093\u3002"));
            _authorization.Demand(_currentUser.Id, ModuleCodes.Tasks, PermissionActions.View, row.SectorId);

            var history = conn.Query(
                @"
                    SELECT
                        h.Id AS Id,
                        COALESCE(h.PreviousStatus, '') AS PreviousStatus,
                        h.NewStatus AS NewStatus,
                        COALESCE(changer.NameRomanji, h.ChangedByCodigoFJ) AS ChangedByNamePt,
                        COALESCE(NULLIF(changer.NameNihongo, ''), changer.NameRomanji, h.ChangedByCodigoFJ) AS ChangedByNameJp,
                        substr(h.ChangedAt, 1, 16) AS ChangedAt,
                        COALESCE(h.Note, '') AS Note
                    FROM TaskStatusHistory h
                    LEFT JOIN Operators changer ON changer.CodigoFJ = h.ChangedByCodigoFJ
                    WHERE h.TaskId = @taskId
                    ORDER BY h.Id DESC;",
                new { taskId }
            ).Select(item => new
            {
                id = item.Id,
                previousStatus = item.PreviousStatus,
                previousStatusLabelPt = string.IsNullOrWhiteSpace((string)item.PreviousStatus)
                    ? string.Empty
                    : GetStatusLabel((string)item.PreviousStatus, "pt-BR"),
                previousStatusLabelJp = string.IsNullOrWhiteSpace((string)item.PreviousStatus)
                    ? string.Empty
                    : GetStatusLabel((string)item.PreviousStatus, "ja-JP"),
                newStatus = item.NewStatus,
                newStatusLabelPt = GetStatusLabel((string)item.NewStatus, "pt-BR"),
                newStatusLabelJp = GetStatusLabel((string)item.NewStatus, "ja-JP"),
                changedByNamePt = item.ChangedByNamePt,
                changedByNameJp = item.ChangedByNameJp,
                changedAt = item.ChangedAt,
                note = item.Note
            }).ToList();

            PostJson(new
            {
                type = "details",
                data = new
                {
                    id = row.Id,
                    description = row.Description,
                    dueDate = row.DueDate.ToString("yyyy-MM-dd"),
                    shiftNamePt = row.ShiftNamePt,
                    shiftNameJp = row.ShiftNameJp,
                    sectorNamePt = row.SectorNamePt,
                    sectorNameJp = row.SectorNameJp,
                    leaderNamePt = row.LeaderNamePt,
                    leaderNameJp = row.LeaderNameJp,
                    createdByNamePt = row.CreatedByNamePt,
                    createdByNameJp = row.CreatedByNameJp,
                    taskStatus = row.TaskStatus,
                    createdAt = row.CreatedAt,
                    updatedAt = row.UpdatedAt,
                    startedAt = row.StartedAt,
                    completedAt = row.CompletedAt,
                    cancelledAt = row.CancelledAt,
                    historyCount = row.HistoryCount,
                    history
                }
            });
        }

        private static IEnumerable<object> QueryShifts(System.Data.IDbConnection conn)
        {
            return conn.Query(
                @"
                    SELECT
                        Id AS id,
                        COALESCE(NamePt, '') AS namePt,
                        COALESCE(NULLIF(NameJp, ''), NamePt, '') AS nameJp
                    FROM Shifts
                    ORDER BY Id;"
            );
        }

        private static IEnumerable<TaskRow> QueryTasks(
            System.Data.IDbConnection conn,
            TaskReportFilter filter,
            IReadOnlyCollection<int> authorizedSectorIds)
        {
            const string sql = @"
                SELECT
                    t.Id,
                    t.Description,
                    date(t.DueDate) AS DueDate,
                    t.SectorId,
                    COALESCE(sec.NamePt, '') AS SectorNamePt,
                    COALESCE(NULLIF(sec.NameJp, ''), sec.NamePt, '') AS SectorNameJp,
                    COALESCE(sh.NamePt, '') AS ShiftNamePt,
                    COALESCE(NULLIF(sh.NameJp, ''), sh.NamePt, '') AS ShiftNameJp,
                    COALESCE(leader.NameRomanji, '') AS LeaderNamePt,
                    COALESCE(NULLIF(leader.NameNihongo, ''), leader.NameRomanji, '') AS LeaderNameJp,
                    COALESCE(creator.NameRomanji, t.CreatedByCodigoFJ) AS CreatedByNamePt,
                    COALESCE(NULLIF(creator.NameNihongo, ''), creator.NameRomanji, t.CreatedByCodigoFJ) AS CreatedByNameJp,
                    t.Status AS TaskStatus,
                    CASE WHEN t.CreatedAt IS NULL THEN '' ELSE substr(t.CreatedAt, 1, 16) END AS CreatedAt,
                    CASE WHEN t.UpdatedAt IS NULL THEN '' ELSE substr(t.UpdatedAt, 1, 16) END AS UpdatedAt,
                    CASE WHEN t.StartedAt IS NULL THEN '' ELSE substr(t.StartedAt, 1, 16) END AS StartedAt,
                    CASE WHEN t.CompletedAt IS NULL THEN '' ELSE substr(t.CompletedAt, 1, 16) END AS CompletedAt,
                    CASE WHEN t.CancelledAt IS NULL THEN '' ELSE substr(t.CancelledAt, 1, 16) END AS CancelledAt,
                    (
                        SELECT COUNT(1)
                        FROM TaskStatusHistory h
                        WHERE h.TaskId = t.Id
                    ) AS HistoryCount
                FROM Tasks t
                LEFT JOIN Shifts sh ON sh.Id = t.ShiftId
                LEFT JOIN Sectors sec ON sec.Id = t.SectorId
                LEFT JOIN Operators leader ON leader.CodigoFJ = t.AssigneeCodigoFJ
                LEFT JOIN Operators creator ON creator.CodigoFJ = t.CreatedByCodigoFJ
                WHERE (@taskId = 0 OR t.Id = @taskId)
                  AND t.SectorId IN @authorizedSectorIds
                  AND (@sectorId = 0 OR t.SectorId = @sectorId)
                  AND (@taskId > 0 OR date(t.DueDate) BETWEEN date(@start) AND date(@end))
                  AND (@shiftId = 0 OR t.ShiftId = @shiftId)
                  AND (@status = '' OR t.Status = @status)
                  AND (
                        @search = ''
                        OR COALESCE(t.Description, '') LIKE '%' || @search || '%'
                        OR COALESCE(leader.NameRomanji, '') LIKE '%' || @search || '%'
                        OR COALESCE(leader.NameNihongo, '') LIKE '%' || @search || '%'
                        OR COALESCE(creator.NameRomanji, '') LIKE '%' || @search || '%'
                        OR COALESCE(creator.NameNihongo, '') LIKE '%' || @search || '%'
                        OR COALESCE(sh.NamePt, '') LIKE '%' || @search || '%'
                        OR COALESCE(sh.NameJp, '') LIKE '%' || @search || '%'
                    )
                ORDER BY
                    CASE WHEN t.Status IN ('completed', 'cancelled') THEN 1 ELSE 0 END,
                    date(t.DueDate) ASC,
                    t.Id DESC;";

            return conn.Query<TaskRow>(sql, new
            {
                taskId = filter.TaskId,
                authorizedSectorIds,
                sectorId = filter.SectorId,
                start = filter.Start.ToString("yyyy-MM-dd"),
                end = filter.End.ToString("yyyy-MM-dd"),
                shiftId = filter.ShiftId,
                status = filter.Status,
                search = filter.Search
            });
        }

        private static string GetStatusLabel(string value, string locale)
        {
            var match = TaskStatuses.FirstOrDefault(item => item.Value == value);
            if (match == default)
                return value;

            return string.Equals(locale, "ja-JP", StringComparison.OrdinalIgnoreCase)
                ? match.LabelJp
                : match.LabelPt;
        }

        private void PostJson(object payload)
        {
            var json = JsonSerializer.Serialize(payload);
            webViewTasksReport.CoreWebView2.PostWebMessageAsJson(json);
        }

        private static TaskReportFilter CreateDefaultFilter()
        {
            var today = DateTime.Today;
            return new TaskReportFilter
            {
                Start = new DateTime(today.Year, today.Month, 1).AddMonths(-1),
                End = new DateTime(today.Year, today.Month, 1).AddMonths(2).AddDays(-1),
                Search = string.Empty,
                Status = string.Empty
            };
        }

        private static TaskReportFilter ReadFilter(JsonElement root)
        {
            return new TaskReportFilter
            {
                Start = ReadDate(root, "dtInicial", DateTime.Today.AddMonths(-1)),
                End = ReadDate(root, "dtFinal", DateTime.Today),
                ShiftId = ReadInt(root, "shiftId"),
                SectorId = ReadInt(root, "sectorId"),
                Status = ReadString(root, "status"),
                Search = ReadString(root, "search")
            };
        }

        private static DateTime ReadDate(JsonElement root, string propertyName, DateTime fallback)
        {
            var raw = ReadString(root, propertyName);
            return DateTime.TryParse(raw, out var parsed) ? parsed.Date : fallback.Date;
        }

        private static int ReadInt(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var prop))
                return 0;

            return prop.ValueKind switch
            {
                JsonValueKind.Number => prop.GetInt32(),
                JsonValueKind.String when int.TryParse(prop.GetString(), out var parsed) => parsed,
                _ => 0
            };
        }

        private static string ReadString(JsonElement root, string propertyName)
        {
            return root.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
                ? prop.GetString() ?? string.Empty
                : string.Empty;
        }

        private static string L(string pt, string jp)
        {
            return string.Equals(Program.CurrentLocale, "ja-JP", StringComparison.OrdinalIgnoreCase)
                ? jp
                : pt;
        }

        private sealed class TaskReportFilter
        {
            public int TaskId { get; set; }
            public DateTime Start { get; set; }
            public DateTime End { get; set; }
            public int ShiftId { get; set; }
            public int SectorId { get; set; }
            public string Status { get; set; } = string.Empty;
            public string Search { get; set; } = string.Empty;
        }

        private sealed class TaskRow
        {
            public int Id { get; set; }
            public string Description { get; set; } = string.Empty;
            public DateTime DueDate { get; set; }
            public int SectorId { get; set; }
            public string SectorNamePt { get; set; } = string.Empty;
            public string SectorNameJp { get; set; } = string.Empty;
            public string ShiftNamePt { get; set; } = string.Empty;
            public string ShiftNameJp { get; set; } = string.Empty;
            public string LeaderNamePt { get; set; } = string.Empty;
            public string LeaderNameJp { get; set; } = string.Empty;
            public string CreatedByNamePt { get; set; } = string.Empty;
            public string CreatedByNameJp { get; set; } = string.Empty;
            public string TaskStatus { get; set; } = string.Empty;
            public string CreatedAt { get; set; } = string.Empty;
            public string UpdatedAt { get; set; } = string.Empty;
            public string StartedAt { get; set; } = string.Empty;
            public string CompletedAt { get; set; } = string.Empty;
            public string CancelledAt { get; set; } = string.Empty;
            public int HistoryCount { get; set; }
        }
    }
}
