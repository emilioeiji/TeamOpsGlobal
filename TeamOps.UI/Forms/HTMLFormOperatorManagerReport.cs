using Microsoft.Web.WebView2.Core;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using TeamOps.Core.Entities;
using TeamOps.Core.Security;
using TeamOps.Data.Db;
using TeamOps.Data.Repositories;
using TeamOps.Data.Security;
using TeamOps.UI.Services;

namespace TeamOps.UI.Forms
{
    public sealed class HTMLFormOperatorManagerReport : Form
    {
        private readonly SqliteConnectionFactory _factory;
        private readonly Operator _currentOperator;
        private readonly OperatorManagerReportService _service;
        private readonly Microsoft.Web.WebView2.WinForms.WebView2 _webView;
        private readonly string _initialCodigoFJ;
        private readonly int _initialPeriodDays;

        public HTMLFormOperatorManagerReport(
            SqliteConnectionFactory factory,
            User currentUser,
            Operator currentOperator,
            string initialCodigoFJ = "",
            int initialPeriodDays = 90)
        {
            _factory = factory;
            _currentOperator = currentOperator;
            var authorization = new AuthorizationService(factory);
            authorization.Demand(currentUser.Id, ModuleCodes.Reports, PermissionActions.View);
            _service = new OperatorManagerReportService(
                factory,
                authorization.GetContext(currentUser.Id).AuthorizedSectorIds);
            _initialCodigoFJ = initialCodigoFJ?.Trim() ?? string.Empty;
            _initialPeriodDays = initialPeriodDays > 0 ? initialPeriodDays : 90;

            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Text = L("Relatorio Gerencial de Operadores", "Operator Management Report");
            Width = 1540;
            Height = 960;
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
                Path.Combine(Application.StartupPath, "ui", "operator-manager-report"),
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
                        SendInit();
                        break;

                    case "apply_filters":
                        SendDirectory(new OperatorManagerDirectoryFilter(
                            ReadInt(root, "shiftId"),
                            ReadInt(root, "sectorId"),
                            ReadInt(root, "groupId"),
                            ReadInt(root, "periodDays", 90),
                            ReadString(root, "search")));
                        break;

                    case "select_operator":
                        SendReport(
                            ReadString(root, "codigoFJ"),
                            ReadInt(root, "periodDays", 90));
                        break;

                    case "open_follow_history":
                        OpenFollowHistory(ReadString(root, "codigoFJ"));
                        break;

                    case "print":
                        PrintReport();
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

            PostJson(new
            {
                type = "init",
                data = new
                {
                    locale = Program.CurrentLocale,
                    defaults = new
                    {
                        shiftId = init.DefaultShiftId,
                        sectorId = init.DefaultSectorId,
                        periodDays = _initialPeriodDays,
                        groupId = 0,
                        search = _initialCodigoFJ,
                        selectedCodigoFJ = _initialCodigoFJ
                    },
                    shifts = init.Shifts.Select(item => new { id = item.Id, name = item.Name }),
                    sectors = init.Sectors.Select(item => new { id = item.Id, name = item.Name }),
                    groups = init.Groups.Select(item => new { id = item.Id, name = item.Name })
                }
            });
        }

        private void SendDirectory(OperatorManagerDirectoryFilter filter)
        {
            var directory = _service.GetDirectory(filter);

            PostJson(new
            {
                type = "directory",
                data = new
                {
                    startDateIso = directory.StartDateIso,
                    endDateIso = directory.EndDateIso,
                    items = directory.Items.Select(item => new
                    {
                        codigoFJ = item.CodigoFJ,
                        name = item.Name,
                        nameJp = item.NameJp,
                        shiftId = item.ShiftId,
                        shiftName = item.ShiftName,
                        sectorId = item.SectorId,
                        sectorName = item.SectorName,
                        groupId = item.GroupId,
                        groupName = item.GroupName,
                        trainer = item.Trainer,
                        isLeader = item.IsLeader,
                        scheduledDays = item.ScheduledDays,
                        presentDays = item.PresentDays,
                        yukyuDays = item.YukyuDays,
                        faltaDays = item.FaltaDays,
                        lateDays = item.LateDays,
                        earlyLeaveDays = item.EarlyLeaveDays,
                        followUpCount = item.FollowUpCount,
                        pendingTodokeCount = item.PendingTodokeCount,
                        presencePercent = item.PresencePercent,
                        coveragePercent = item.CoveragePercent
                    })
                }
            });
        }

        private void SendReport(string codigoFJ, int periodDays)
        {
            if (string.IsNullOrWhiteSpace(codigoFJ))
            {
                throw new InvalidOperationException(L("Selecione um operador para continuar.", "Choose an operator to continue."));
            }

            var report = _service.GetReport(codigoFJ, periodDays);

            PostJson(new
            {
                type = "report",
                data = new
                {
                    codigoFJ = report.CodigoFJ,
                    name = report.Name,
                    nameJp = report.NameJp,
                    shiftName = report.ShiftName,
                    sectorName = report.SectorName,
                    groupName = report.GroupName,
                    startDateIso = report.StartDateIso,
                    trainer = report.Trainer,
                    isLeader = report.IsLeader,
                    presence = new
                    {
                        scheduledDays = report.Presence.ScheduledDays,
                        presentDays = report.Presence.PresentDays,
                        yukyuDays = report.Presence.YukyuDays,
                        faltaDays = report.Presence.FaltaDays,
                        lateDays = report.Presence.LateDays,
                        earlyLeaveDays = report.Presence.EarlyLeaveDays,
                        pendingTodokeCount = report.Presence.PendingTodokeCount,
                        followUpCount = report.Presence.FollowUpCount,
                        presencePercent = report.Presence.PresencePercent,
                        coveragePercent = report.Presence.CoveragePercent
                    },
                    masterCards = new
                    {
                        totalCount = report.MasterCards.TotalCount,
                        inProgressCount = report.MasterCards.InProgressCount,
                        followCount = report.MasterCards.FollowCount,
                        completedCount = report.MasterCards.CompletedCount,
                        overdueFollowCount = report.MasterCards.OverdueFollowCount,
                        dueSoonFollowCount = report.MasterCards.DueSoonFollowCount,
                        items = report.MasterCards.Items.Select(item => new
                        {
                            id = item.Id,
                            equipmentName = item.EquipmentName,
                            sectorName = item.SectorName,
                            status = item.Status,
                            startDateIso = item.StartDateIso,
                            concludedAt = item.ConcludedAt,
                            followDateIso = item.FollowDateIso,
                            finalizedAt = item.FinalizedAt,
                            notes = item.Notes,
                            followState = item.FollowState
                        })
                    },
                    production = new
                    {
                        estimatedRunningMinutes = report.Production.EstimatedRunningMinutes,
                        estimatedKadouritsuPercent = report.Production.EstimatedKadouritsuPercent,
                        localNames = report.Production.LocalNames,
                        days = report.Production.Days.Select(item => new
                        {
                            dateIso = item.DateIso,
                            estimatedRunningMinutes = item.EstimatedRunningMinutes,
                            estimatedKadouritsuPercent = item.EstimatedKadouritsuPercent,
                            localNames = item.LocalNames
                        })
                    },
                    followUps = report.FollowUps.Select(item => new
                    {
                        id = item.Id,
                        dateLabel = item.DateLabel,
                        shiftName = item.ShiftName,
                        reasonName = item.ReasonName,
                        typeName = item.TypeName,
                        localName = item.LocalName,
                        equipmentName = item.EquipmentName,
                        description = item.Description,
                        guidance = item.Guidance
                    }),
                    dailyHistory = report.DailyHistory.Select(item => new
                    {
                        dateIso = item.DateIso,
                        status = item.Status,
                        area = item.Area,
                        notes = item.Notes,
                        hasPendingTodoke = item.HasPendingTodoke
                    })
                }
            });
        }

        private void OpenFollowHistory(string codigoFJ)
        {
            if (string.IsNullOrWhiteSpace(codigoFJ))
            {
                throw new InvalidOperationException(L("Selecione um operador para abrir o historico.", "Choose an operator to open history."));
            }

            BeginInvoke(new Action(() =>
            {
                if (IsDisposed)
                {
                    return;
                }

                using var form = new HTMLFormFollowOperatorReport(
                    codigoFJ.Trim(),
                    new FollowUpRepository(_factory),
                    new OperatorRepository(_factory),
                    new ShiftRepository(_factory),
                    new SectorRepository(_factory),
                    new FollowUpReasonRepository(_factory),
                    new FollowUpTypeRepository(_factory),
                    new EquipmentRepository(_factory),
                    new LocalRepository(_factory));

                form.ShowDialog(this);
            }));
        }

        private async void PrintReport()
        {
            if (_webView.CoreWebView2 != null)
            {
                await _webView.CoreWebView2.ExecuteScriptAsync("window.print();");
            }
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

        private static int ReadInt(JsonElement root, string propertyName, int defaultValue = 0)
        {
            if (!root.TryGetProperty(propertyName, out var property))
            {
                return defaultValue;
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var number))
            {
                return number;
            }

            if (property.ValueKind == JsonValueKind.String && int.TryParse(property.GetString(), out number))
            {
                return number;
            }

            return defaultValue;
        }

        private static string ReadString(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var property))
            {
                return string.Empty;
            }

            return property.ValueKind switch
            {
                JsonValueKind.String => property.GetString() ?? string.Empty,
                JsonValueKind.Number => property.GetRawText(),
                JsonValueKind.True => bool.TrueString,
                JsonValueKind.False => bool.FalseString,
                _ => string.Empty
            };
        }

        private static string L(string pt, string en)
        {
            return string.Equals(Program.CurrentLocale, "ja-JP", StringComparison.OrdinalIgnoreCase)
                ? en
                : pt;
        }
    }
}
