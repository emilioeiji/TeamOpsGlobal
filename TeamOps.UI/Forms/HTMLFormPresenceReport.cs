using Microsoft.Web.WebView2.Core;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using TeamOps.Core.Entities;
using TeamOps.Core.Security;
using TeamOps.Data.Db;
using TeamOps.Data.Security;
using TeamOps.UI.Services;

namespace TeamOps.UI.Forms
{
    public sealed class HTMLFormPresenceReport : Form
    {
        private readonly SqliteConnectionFactory _factory;
        private readonly Operator _currentOperator;
        private readonly User _currentUser;
        private readonly OperatorManagerReportService _service;
        private readonly Microsoft.Web.WebView2.WinForms.WebView2 _webView;

        public HTMLFormPresenceReport(SqliteConnectionFactory factory, User currentUser, Operator currentOperator)
        {
            _factory = factory;
            _currentUser = currentUser;
            _currentOperator = currentOperator;
            var authorization = new AuthorizationService(factory);
            authorization.Demand(currentUser.Id, ModuleCodes.Reports, PermissionActions.View);
            authorization.Demand(currentUser.Id, ModuleCodes.Presence, PermissionActions.View);
            _service = new OperatorManagerReportService(
                factory,
                authorization.GetContext(currentUser.Id).AuthorizedSectorIds);

            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Text = L("Relatorio de Presenca", "Attendance Report");
            Width = 1460;
            Height = 900;
            StartPosition = FormStartPosition.CenterParent;

            _webView = new Microsoft.Web.WebView2.WinForms.WebView2
            {
                Dock = DockStyle.Fill
            };

            Controls.Add(_webView);
            InitializeWebView();
        }

        private async void InitializeWebView()
        {
            await _webView.EnsureCoreWebView2Async(null);

            var core = _webView.CoreWebView2;
            core.Settings.IsWebMessageEnabled = true;
            core.Settings.AreDefaultScriptDialogsEnabled = true;
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.Settings.AreDevToolsEnabled = true;
            core.WebMessageReceived += WebMessageReceived;

            core.SetVirtualHostNameToFolderMapping(
                "app",
                Path.Combine(Application.StartupPath, "ui", "presence-report"),
                CoreWebView2HostResourceAccessKind.Allow);

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
                        _ = RunBackgroundAsync(SendInit);
                        break;

                    case "apply_filters":
                        var filter = new OperatorPresenceReportFilter(
                            ReadString(root, "startDateIso"),
                            ReadString(root, "endDateIso"),
                            ReadInt(root, "shiftId"),
                            ReadInt(root, "sectorId"),
                            ReadInt(root, "groupId"),
                            ReadString(root, "status"),
                            ReadString(root, "search"));
                        _ = RunBackgroundAsync(() => SendReport(filter));
                        break;

                    case "open_operator_report":
                        OpenOperatorReport(
                            ReadString(root, "codigoFJ"),
                            ReadString(root, "startDateIso"),
                            ReadString(root, "endDateIso"));
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

        private void SendInit()
        {
            var init = _service.GetInitialPayload(_currentOperator.ShiftId, _currentOperator.SectorId);
            var endDate = DateTime.Today;
            var startDate = endDate.AddDays(-29);

            PostJson(new
            {
                type = "init",
                data = new
                {
                    locale = Program.CurrentLocale,
                    defaults = new
                    {
                        startDateIso = startDate.ToString("yyyy-MM-dd"),
                        endDateIso = endDate.ToString("yyyy-MM-dd"),
                        shiftId = init.DefaultShiftId,
                        sectorId = init.DefaultSectorId,
                        groupId = 0,
                        status = "all"
                    },
                    shifts = init.Shifts.Select(item => new { id = item.Id, name = item.Name }),
                    sectors = init.Sectors.Select(item => new { id = item.Id, name = item.Name }),
                    groups = init.Groups.Select(item => new { id = item.Id, name = item.Name })
                }
            });
        }

        private void SendReport(OperatorPresenceReportFilter filter)
        {
            var watch = Stopwatch.StartNew();
            var report = _service.GetPresenceReport(filter);
            watch.Stop();

            PostJson(new
            {
                type = "report",
                data = new
                {
                    startDateIso = report.StartDateIso,
                    endDateIso = report.EndDateIso,
                    summary = new
                    {
                        operatorCount = report.Summary.OperatorCount,
                        scheduledDays = report.Summary.ScheduledDays,
                        presentDays = report.Summary.PresentDays,
                        yukyuDays = report.Summary.YukyuDays,
                        faltaDays = report.Summary.FaltaDays,
                        lateDays = report.Summary.LateDays,
                        earlyLeaveDays = report.Summary.EarlyLeaveDays,
                        presencePercent = report.Summary.PresencePercent,
                        scheduledDaysWithoutSunday = report.Summary.ScheduledDaysWithoutSunday,
                        scheduledDaysWithSunday = report.Summary.ScheduledDaysWithSunday,
                        absencesWithoutSunday = report.Summary.AbsencesWithoutSunday,
                        absencesWithSunday = report.Summary.AbsencesWithSunday,
                        presencePercentWithoutSunday = report.Summary.PresencePercentWithoutSunday,
                        presencePercentWithSunday = report.Summary.PresencePercentWithSunday,
                        overtimeOver45Count = report.Summary.OvertimeOver45Count,
                        overtimeBetween35And45Count = report.Summary.OvertimeBetween35And45Count,
                        overtimeBelow35Count = report.Summary.OvertimeBelow35Count,
                        totalCurrentMonthOvertimeHours = report.Summary.TotalCurrentMonthOvertimeHours,
                        totalWorkedSundays = report.Summary.TotalWorkedSundays,
                        totalHolidayWorkDays = report.Summary.TotalHolidayWorkDays
                    },
                    topOvertime = report.TopOvertime.Select(item => new
                    {
                        rank = item.Rank,
                        codigoFJ = item.CodigoFJ,
                        name = item.Name,
                        nameJp = item.NameJp,
                        shiftName = item.ShiftName,
                        overtimeHours = item.OvertimeHours,
                        holidayWorkDays = item.HolidayWorkDays
                    }),
                    topAbsences = report.TopAbsences.Select(item => new
                    {
                        rank = item.Rank,
                        codigoFJ = item.CodigoFJ,
                        name = item.Name,
                        nameJp = item.NameJp,
                        shiftName = item.ShiftName,
                        faltaDays = item.FaltaDays,
                        lateDays = item.LateDays,
                        earlyLeaveDays = item.EarlyLeaveDays,
                        yukyuDays = item.YukyuDays
                    }),
                    rows = report.Rows.Select(item => new
                    {
                        codigoFJ = item.CodigoFJ,
                        name = item.Name,
                        nameJp = item.NameJp,
                        shiftName = item.ShiftName,
                        sectorName = item.SectorName,
                        groupName = item.GroupName,
                        scheduledDays = item.ScheduledDays,
                        presentDays = item.PresentDays,
                        yukyuDays = item.YukyuDays,
                        faltaDays = item.FaltaDays,
                        lateDays = item.LateDays,
                        earlyLeaveDays = item.EarlyLeaveDays,
                        pendingTodokeCount = item.PendingTodokeCount,
                        presencePercent = item.PresencePercent,
                        scheduledDaysWithoutSunday = item.ScheduledDaysWithoutSunday,
                        scheduledDaysWithSunday = item.ScheduledDaysWithSunday,
                        absencesWithoutSunday = item.AbsencesWithoutSunday,
                        absencesWithSunday = item.AbsencesWithSunday,
                        presencePercentWithoutSunday = item.PresencePercentWithoutSunday,
                        presencePercentWithSunday = item.PresencePercentWithSunday,
                        below82WithoutSunday = item.Below82WithoutSunday,
                        below82WithSunday = item.Below82WithSunday,
                        currentMonthOvertimeHours = item.CurrentMonthOvertimeHours,
                        previousMonthOvertimeHours = item.PreviousMonthOvertimeHours,
                        realizedOvertimeHours = item.RealizedOvertimeHours,
                        projectedRemainingOvertimeHours = item.ProjectedRemainingOvertimeHours,
                        projectedFinalOvertimeHours = item.ProjectedFinalOvertimeHours,
                        workedSundays = item.WorkedSundays,
                        holidayWorkDays = item.HolidayWorkDays,
                        holidayWorkHours = item.HolidayWorkHours,
                        totalOvertimeHours = item.TotalOvertimeHours,
                        domingoShukkinTotalHours = item.DomingoShukkinTotalHours,
                        overtimeLimitDifferenceHours = item.OvertimeLimitDifferenceHours,
                        overtimeRiskLevel = item.OvertimeRiskLevel,
                        totalOvertimeRiskLevel = item.TotalOvertimeRiskLevel,
                        overtimePlusSundaysLabel = item.OvertimePlusSundaysLabel,
                        lastStatus = item.LastStatus,
                        lastDateIso = item.LastDateIso,
                        lastArea = item.LastArea
                    }),
                    performance = new
                    {
                        queryMs = watch.ElapsedMilliseconds,
                        rowCount = report.Rows.Count,
                        loadPresenceMs = report.Performance.LoadPresenceMs,
                        loadHaidaiMs = report.Performance.LoadHaidaiMs,
                        buildOvertimeMs = report.Performance.BuildOvertimeMs,
                        buildProjectionMs = report.Performance.BuildProjectionMs,
                        buildRankingMs = report.Performance.BuildRankingMs
                    }
                }
            });
        }

        private void OpenOperatorReport(string codigoFJ, string startDateIso, string endDateIso)
        {
            if (string.IsNullOrWhiteSpace(codigoFJ))
            {
                throw new InvalidOperationException(L("Selecione um operador para abrir o relatorio.", "Choose an operator to open the report."));
            }

            var periodDays = CalculatePeriodDays(startDateIso, endDateIso);

            BeginInvoke(new Action(() =>
            {
                if (IsDisposed)
                {
                    return;
                }

                using var form = new HTMLFormOperatorManagerReport(
                    _factory,
                    _currentUser,
                    _currentOperator,
                    codigoFJ.Trim(),
                    periodDays);

                form.ShowDialog(this);
            }));
        }

        private static int CalculatePeriodDays(string startDateIso, string endDateIso)
        {
            if (DateTime.TryParse(startDateIso, out var startDate)
                && DateTime.TryParse(endDateIso, out var endDate))
            {
                var days = (endDate.Date - startDate.Date).Days + 1;
                return Math.Clamp(days, 1, 365);
            }

            return 90;
        }

        private void PostJson(object payload)
        {
            var json = JsonSerializer.Serialize(payload);

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() =>
                {
                    if (_webView.CoreWebView2 != null)
                    {
                        _webView.CoreWebView2.PostWebMessageAsJson(json);
                    }
                }));
                return;
            }

            if (_webView.CoreWebView2 != null)
            {
                _webView.CoreWebView2.PostWebMessageAsJson(json);
            }
        }

        private async Task RunBackgroundAsync(Action action)
        {
            try
            {
                await Task.Run(action);
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

        private static int ReadInt(JsonElement root, string propertyName, int defaultValue = 0)
        {
            if (!root.TryGetProperty(propertyName, out var property))
            {
                return defaultValue;
            }

            return property.ValueKind switch
            {
                JsonValueKind.Number when property.TryGetInt32(out var number) => number,
                JsonValueKind.String when int.TryParse(property.GetString(), out var number) => number,
                _ => defaultValue
            };
        }

        private static string ReadString(JsonElement root, string propertyName)
        {
            return root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
                ? property.GetString() ?? string.Empty
                : string.Empty;
        }

        private static string L(string pt, string en)
        {
            return string.Equals(Program.CurrentLocale, "ja-JP", StringComparison.OrdinalIgnoreCase)
                ? en
                : pt;
        }
    }
}
