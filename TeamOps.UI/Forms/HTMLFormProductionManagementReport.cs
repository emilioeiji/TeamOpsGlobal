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
    public sealed class HTMLFormProductionManagementReport : Form
    {
        private readonly Operator _currentOperator;
        private readonly ProductionManagementReportService _service;
        private readonly Microsoft.Web.WebView2.WinForms.WebView2 _webView;

        public HTMLFormProductionManagementReport(
            SqliteConnectionFactory factory,
            User currentUser,
            Operator currentOperator)
        {
            _currentOperator = currentOperator;
            var authorization = new AuthorizationService(factory);
            authorization.Demand(currentUser.Id, ModuleCodes.Reports, PermissionActions.View);
            authorization.Demand(currentUser.Id, ModuleCodes.Production, PermissionActions.View);
            _service = new ProductionManagementReportService(
                factory,
                authorization.GetContext(currentUser.Id).AuthorizedSectorIds);

            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Text = L("Relatorio Gerencial de Producao", "Production Management Report");
            Width = 1540;
            Height = 920;
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
                Path.Combine(Application.StartupPath, "ui", "production-management-report"),
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
                        var filter = new ProductionManagementReportFilter(
                            ReadString(root, "startDateIso"),
                            ReadString(root, "endDateIso"),
                            ReadInt(root, "sectorId"),
                            ReadInt(root, "localId"),
                            ReadInt(root, "shiftId"),
                            ReadInt(root, "groupId"),
                            ReadInt(root, "groupAId"),
                            ReadInt(root, "groupBId"),
                            ReadString(root, "operatorCode"),
                            ReadInt(root, "machineId"),
                            ReadString(root, "partCode"),
                            ReadString(root, "leaderCode"),
                            ReadBool(root, "onlyActive", true),
                            ReadBool(root, "onlyProduction", false));
                        _ = RunBackgroundAsync(() => SendReport(filter));
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
            var init = _service.GetInitialPayload();
            var endDate = DateTime.Today;
            var startDate = endDate.AddDays(-29);
            var defaultSectorId = init.Sectors.Any(item => item.Id == _currentOperator.SectorId)
                ? _currentOperator.SectorId
                : init.Sectors.FirstOrDefault()?.Id ?? 0;

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
                        sectorId = defaultSectorId,
                        localId = 0,
                        shiftId = 0,
                        groupId = 0,
                        groupAId = 0,
                        groupBId = 0,
                        operatorCode = string.Empty,
                        machineId = 0,
                        partCode = string.Empty,
                        leaderCode = string.Empty,
                        onlyActive = true,
                        onlyProduction = false
                    },
                    shifts = init.Shifts.Select(item => new { id = item.Id, namePt = item.NamePt, nameJp = item.NameJp }),
                    sectors = init.Sectors.Select(item => new { id = item.Id, namePt = item.NamePt, nameJp = item.NameJp }),
                    groups = init.Groups.Select(item => new { id = item.Id, namePt = item.NamePt, nameJp = item.NameJp }),
                    locals = init.Locals.Select(item => new { id = item.Id, sectorId = item.SectorId, namePt = item.NamePt, nameJp = item.NameJp }),
                    machines = init.Machines.Select(item => new { id = item.Id, sectorId = item.SectorId, localId = item.LocalId, machineCode = item.MachineCode, namePt = item.NamePt, nameJp = item.NameJp }),
                    operators = init.Operators.Select(item => new { codigoFJ = item.CodigoFJ, shiftId = item.ShiftId, sectorId = item.SectorId, groupId = item.GroupId, isLeader = item.IsLeader, isActive = item.IsActive, namePt = item.NamePt, nameJp = item.NameJp }),
                    partCodes = init.PartCodes
                }
            });
        }

        private void SendReport(ProductionManagementReportFilter filter)
        {
            var watch = Stopwatch.StartNew();
            var report = _service.BuildReport(filter, _currentOperator.CodigoFJ);
            watch.Stop();

            PostJson(new
            {
                type = "report",
                data = new
                {
                    report.StartDateIso,
                    report.EndDateIso,
                    report.Summary,
                    report.Operators,
                    report.Rankings,
                    report.ShiftComparison,
                    report.GroupComparison,
                    report.DailyTrend,
                    report.Sectors,
                    report.Machines,
                    report.PresenceCrossing,
                    report.Alerts,
                    report.Performance,
                    QueryMs = watch.ElapsedMilliseconds
                }
            });
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

        private static bool ReadBool(JsonElement root, string propertyName, bool defaultValue)
        {
            if (!root.TryGetProperty(propertyName, out var property))
            {
                return defaultValue;
            }

            return property.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String when bool.TryParse(property.GetString(), out var value) => value,
                JsonValueKind.Number when property.TryGetInt32(out var number) => number != 0,
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
