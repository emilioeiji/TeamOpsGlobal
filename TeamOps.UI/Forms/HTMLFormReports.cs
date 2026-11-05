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
using TeamOps.Data.Repositories;
using TeamOps.Data.Security;
using TeamOps.UI.Services;

namespace TeamOps.UI.Forms
{
    public partial class HTMLFormReports : Form
    {
        private readonly Operator _currentOperator;
        private readonly Shift _currentShift;
        private readonly SqliteConnectionFactory _factory;
        private readonly User _currentUser;
        private readonly AuthorizationService _authorization;

        public HTMLFormReports(
            Operator currentOperator,
            Shift currentShift,
            HikitsuguiRepository hikRepo,
            HikitsuguiReadRepository readRepo,
            OperatorRepository opRepo,
            SqliteConnectionFactory factory)
        {
            InitializeComponent();
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Text = L("Relatorios", "\u30ec\u30dd\u30fc\u30c8");

            _currentOperator = currentOperator;
            _currentShift = currentShift;
            _factory = factory;
            _currentUser = Program.CurrentUser ?? throw new InvalidOperationException("Usuario nao autenticado.");
            _authorization = new AuthorizationService(factory);
            _authorization.Demand(_currentUser.Id, ModuleCodes.Reports, PermissionActions.View);

            InitializeWebView();
        }

        private async void InitializeWebView()
        {
            await webViewReports.EnsureCoreWebView2Async(null);

            var core = webViewReports.CoreWebView2;

            core.Settings.IsWebMessageEnabled = true;
            core.Settings.AreDefaultScriptDialogsEnabled = true;
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.Settings.AreDevToolsEnabled = true;

            core.WebMessageReceived += WebMessageReceived;

            core.SetVirtualHostNameToFolderMapping(
                "app",
                Path.Combine(Application.StartupPath, "ui", "reports"),
                CoreWebView2HostResourceAccessKind.Allow
            );

            core.Navigate("https://app/index.html");

            core.NavigationCompleted += (_, __) =>
            {
                var allowedActions = GetAllowedActions();
                PostJson(new
                {
                    type = "init",
                    data = new
                    {
                        locale = Program.CurrentLocale,
                        operatorNamePt = _currentOperator.NameRomanji,
                        operatorNameJp = string.IsNullOrWhiteSpace(_currentOperator.NameNihongo)
                            ? _currentOperator.NameRomanji
                            : _currentOperator.NameNihongo,
                        shiftNamePt = _currentShift.NamePt,
                        shiftNameJp = string.IsNullOrWhiteSpace(_currentShift.NameJp)
                            ? _currentShift.NamePt
                            : _currentShift.NameJp,
                        dateIso = DateTime.Now.ToString("O"),
                        availableCount = allowedActions.Count,
                        totalCount = 11,
                        allowedActions
                    }
                });
            };
        }

        private void WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string? action = null;

            try
            {
                using var json = JsonDocument.Parse(e.WebMessageAsJson);
                var root = json.RootElement;

                if (root.ValueKind == JsonValueKind.String)
                {
                    action = root.GetString();
                }
                else if (root.ValueKind == JsonValueKind.Object &&
                         root.TryGetProperty("action", out var actionProp))
                {
                    action = actionProp.GetString();
                }
            }
            catch
            {
                action = null;
            }

            if (string.IsNullOrWhiteSpace(action))
                return;

            if (!GetAllowedActions().Contains(action, StringComparer.OrdinalIgnoreCase))
            {
                SendNotify(
                    L("Acesso negado", "Access denied"),
                    L("Seu perfil nao possui permissao para este relatorio.", "Your profile cannot access this report."));
                return;
            }

            switch (action)
            {
                case "open:hikitsugui":
                    OpenDialog(() => new HTMLFormHikitsuguiReader(
                        _factory,
                        _currentUser,
                        _currentOperator));
                    break;

                case "open:follow_report":
                    OpenDialog(() => new HTMLFormFollowReport(
                        new FollowUpRepository(_factory),
                        new OperatorRepository(_factory),
                        new ShiftRepository(_factory),
                        new SectorRepository(_factory),
                        new FollowUpReasonRepository(_factory),
                        new FollowUpTypeRepository(_factory),
                        new EquipmentRepository(_factory),
                        new LocalRepository(_factory),
                        _factory,
                        _currentUser
                    ));
                    break;

                case "open:follow_chart":
                    OpenDialog(() => new HTMLFormFollowChart(
                        new FollowUpRepository(_factory),
                        new OperatorRepository(_factory),
                        new ShiftRepository(_factory),
                        new SectorRepository(_factory),
                        new FollowUpReasonRepository(_factory),
                        new FollowUpTypeRepository(_factory),
                        new EquipmentRepository(_factory),
                        new LocalRepository(_factory),
                        _factory,
                        _currentUser
                    ));
                    break;

                case "open:tasks_report":
                    OpenDialog(() => new HTMLFormTasksReport(
                        _factory,
                        _currentUser));
                    break;

                case "open:operators":
                    OpenDialog(() => new HTMLFormOperatorManagerReport(_factory, _currentUser, _currentOperator));
                    break;

                case "open:presence_report":
                    OpenDialog(() => new HTMLFormPresenceReport(_factory, _currentUser, _currentOperator));
                    break;

                case "open:production_management_report":
                    OpenDialog(() => new HTMLFormProductionManagementReport(_factory, _currentUser, _currentOperator));
                    break;

                case "open:mastercard_report":
                    OpenDialog(() => new HTMLFormMasterCardReport(
                        _factory,
                        _currentUser));
                    break;

                case "open:pr_report":
                    OpenDialog(() => new HTMLFormPrClReport(
                        _factory,
                        _currentUser,
                        isPr: true));
                    break;

                case "open:cl_report":
                    OpenDialog(() => new HTMLFormPrClReport(
                        _factory,
                        _currentUser,
                        isPr: false));
                    break;

                case "open:sobra_report":
                    OpenDialog(() => new HTMLFormSobraDePecaReport(
                        _factory,
                        _currentUser));
                    break;
            }
        }

        private void OpenDialog(Func<Form> factory)
        {
            if (IsDisposed)
                return;

            BeginInvoke(new Action(() =>
            {
                if (IsDisposed)
                    return;

                try
                {
                    using var form = factory();
                    form.ShowDialog(this);
                }
                catch (UnauthorizedAccessException ex)
                {
                    SendNotify(L("Acesso negado", "Access denied"), ex.Message);
                }
            }));
        }

        private List<string> GetAllowedActions()
        {
            var actions = new List<string> { "open:operators" };
            AddIfAllowed(actions, "open:hikitsugui", ModuleCodes.Hikitsugui);
            AddIfAllowed(actions, "open:follow_report", ModuleCodes.FollowUp);
            AddIfAllowed(actions, "open:follow_chart", ModuleCodes.FollowUp);
            AddIfAllowed(actions, "open:tasks_report", ModuleCodes.Tasks);
            AddIfAllowed(actions, "open:presence_report", ModuleCodes.Presence);
            AddIfAllowed(actions, "open:production_management_report", ModuleCodes.Production);
            AddIfAllowed(actions, "open:mastercard_report", ModuleCodes.MasterCard);
            AddIfAllowed(actions, "open:pr_report", ModuleCodes.PrCl);
            AddIfAllowed(actions, "open:cl_report", ModuleCodes.PrCl);
            AddIfAllowed(actions, "open:sobra_report", ModuleCodes.SobraDePeca);
            return actions;
        }

        private void AddIfAllowed(List<string> actions, string action, string moduleCode)
        {
            if (_authorization.IsAllowed(_currentUser.Id, moduleCode, PermissionActions.View))
                actions.Add(action);
        }

        private void SendNotify(string title, string message)
        {
            PostJson(new
            {
                type = "notify",
                data = new
                {
                    title,
                    message
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
                    if (webViewReports.CoreWebView2 != null)
                        webViewReports.CoreWebView2.PostWebMessageAsJson(json);
                }));

                return;
            }

            if (webViewReports.CoreWebView2 != null)
                webViewReports.CoreWebView2.PostWebMessageAsJson(json);
        }

        private static string L(string pt, string jp)
        {
            return string.Equals(Program.CurrentLocale, "ja-JP", StringComparison.OrdinalIgnoreCase)
                ? jp
                : pt;
        }
    }
}
