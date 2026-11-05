// Project: TeamOps.UI
// File: Forms/FormDashboardHtml.cs

using Dapper;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using TeamOps.Core.Common;
using TeamOps.Core.Entities;
using TeamOps.Data.Db;
using TeamOps.Data.Repositories;
using TeamOps.UI.Services;
using TeamOps.Core.Security;
using TeamOps.Data.Security;

namespace TeamOps.UI.Forms
{
    public partial class FormDashboardHtml : Form
    {
        private readonly User _user;
        private readonly Operator _currentOperator;
        private readonly Shift _currentShift;
        private readonly SqliteConnectionFactory _factory;
        private readonly bool _hasOperationalIdentity;
        private readonly AuthorizationService _authorization;
        private readonly SessionValidationService _sessionValidation;
        private readonly System.Windows.Forms.Timer _sessionValidationTimer;
        private bool _sessionEnding;

        public FormDashboardHtml(User user)
        {
            InitializeComponent();
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            WindowState = FormWindowState.Maximized;

            _user = user;

            var opRepo = new OperatorRepository(Program.ConnectionFactory);
            var operatorRecord = string.IsNullOrWhiteSpace(_user.CodigoFJ)
                ? null
                : opRepo.GetByCodigoFJ(_user.CodigoFJ);
            _hasOperationalIdentity = operatorRecord != null;
            _currentOperator = operatorRecord ?? new Operator
            {
                CodigoFJ = string.Empty,
                NameRomanji = _user.Name,
                NameNihongo = _user.Name,
                Status = true
            };

            var shiftRepo = new ShiftRepository(Program.ConnectionFactory);
            _currentShift = _hasOperationalIdentity
                ? shiftRepo.GetById(_currentOperator.ShiftId)
                    ?? throw new InvalidOperationException("Turno nao encontrado.")
                : new Shift { Id = 0, NamePt = "Administracao", NameJp = "管理" };

            _factory = Program.ConnectionFactory;
            _authorization = new AuthorizationService(_factory);
            _sessionValidation = new SessionValidationService(_factory);
            _sessionValidationTimer = new System.Windows.Forms.Timer { Interval = 5_000 };
            _sessionValidationTimer.Tick += (_, _) => EnsureCurrentSession();
            _sessionValidationTimer.Start();
            FormClosed += (_, _) => _sessionValidationTimer.Dispose();

            InitializeWebView();
        }

        private async void InitializeWebView()
        {
            await webViewDashboard.EnsureCoreWebView2Async(null);

            var core = webViewDashboard.CoreWebView2;

            core.Settings.IsWebMessageEnabled = true;
            core.Settings.AreDefaultScriptDialogsEnabled = true;
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.Settings.AreDevToolsEnabled = true;

            core.WebMessageReceived += WebMessageReceived;

            core.SetVirtualHostNameToFolderMapping(
                "app",
                Path.Combine(Application.StartupPath, "ui", "dashboard"),
                CoreWebView2HostResourceAccessKind.Allow
            );

            core.Navigate("https://app/index.html");

            core.NavigationCompleted += (s, e) => SendDashboard();
        }

        private void WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            if (!EnsureCurrentSession())
                return;

            using var json = JsonDocument.Parse(e.WebMessageAsJson);
            var root = json.RootElement;

            var action = ReadAction(root);
            var locale = ReadString(root, "locale");

            if (!_hasOperationalIdentity
                && action.StartsWith("open:", StringComparison.OrdinalIgnoreCase)
                && !CanOpenWithoutOperationalIdentity(action))
            {
                MessageBox.Show(
                    "Cadastre e associe um codigo FJ antes de acessar modulos operacionais.",
                    "Identidade operacional necessaria",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            Console.WriteLine($"[Dashboard HTML] Mensagem recebida: {action}");

            switch (action)
            {
                case "select_sector":
                    try
                    {
                        var requestedSectorId = ReadInt(root, "sectorId");
                        Program.CurrentSession?.SelectSector(requestedSectorId <= 0 ? null : requestedSectorId);
                        SendDashboard();
                    }
                    catch (UnauthorizedAccessException)
                    {
                        ShowAccessDeniedAsync();
                    }
                    break;

                case "set_locale":
                    Program.SetCurrentLocale(locale);
                    SendLocale();
                    break;

                case "open:operadores":
                    if (!_authorization.IsAllowed(_user.Id, ModuleCodes.Organization, PermissionActions.View))
                        ShowAccessDeniedAsync();
                    else
                        OpenDialog(() => new HTMLFormOperators());
                    break;

                case "open:atribuir":
                    if (!CanOpen(ModuleCodes.AccessControl, PermissionActions.Administer))
                        ShowAccessDeniedAsync();
                    else
                        OpenDialog(() => new FormAssignments());
                    break;

                case "open:relatorios":
                    if (!CanOpen(ModuleCodes.Reports))
                        ShowAccessDeniedAsync();
                    else
                        OpenDialog(() => new HTMLFormReports(
                            _currentOperator,
                            _currentShift,
                            new HikitsuguiRepository(_factory),
                            new HikitsuguiReadRepository(_factory),
                            new OperatorRepository(_factory),
                            _factory
                        ));
                    break;

                case "open:presence":
                    if (!CanOpen(ModuleCodes.Presence))
                        ShowAccessDeniedAsync();
                    else
                        OpenDialog(() => new HTMLFormPresenceLayout(
                            _factory,
                            _user,
                            _currentShift.Id
                        ));
                    break;

                case "open:haidai":
                    if (!CanOpen(ModuleCodes.Haidai))
                        ShowAccessDeniedAsync();
                    else
                        OpenDialog(() => new HTMLFormHaidai(
                            _factory,
                            _user,
                            _currentOperator
                        ));
                    break;

                case "open:followup":
                    if (!CanOpen(ModuleCodes.FollowUp))
                        ShowAccessDeniedAsync();
                    else
                        OpenDialog(() => new HTMLFormFollowUp(
                            _factory,
                            _user,
                            _currentOperator
                        ));
                    break;

                case "open:tasks":
                    if (!CanOpen(ModuleCodes.Tasks))
                        ShowAccessDeniedAsync();
                    else
                        OpenDialog(() => new HTMLFormTasks(
                            _factory,
                            _user,
                            _currentOperator
                        ));
                    break;

                case "open:mastercard":
                    if (!CanOpen(ModuleCodes.MasterCard))
                        ShowAccessDeniedAsync();
                    else
                        OpenDialog(() => new HTMLFormMasterCard(
                            _factory,
                            _user,
                            _currentOperator
                        ));
                    break;

                case "open:production":
                    if (!CanOpen(ModuleCodes.Production))
                        ShowAccessDeniedAsync();
                    else
                        OpenDialog(() => new HTMLFormProductionMonitor(
                            _factory,
                            _user,
                            _currentOperator
                        ));
                    break;

                case "open:hikitsugui":
                    if (!CanOpen(ModuleCodes.Hikitsugui, PermissionActions.Create))
                        ShowAccessDeniedAsync();
                    else
                        OpenDialog(() => new HTMLHikitsuguiCreate(
                            _factory,
                            _user,
                            _currentOperator
                        ));
                    break;

                case "open:hikitsugui_read":
                    if (!CanOpen(ModuleCodes.Hikitsugui))
                        ShowAccessDeniedAsync();
                    else
                        OpenDialog(() => new HTMLHikitsuguiLeaderRead(
                            _factory,
                            _user,
                            _currentOperator
                        ));
                    break;

                case "open:sobradepeca":
                    if (!CanOpen(ModuleCodes.SobraDePeca))
                        ShowAccessDeniedAsync();
                    else
                        OpenDialog(() => new HTMLFormSobraDePeca(
                            _factory,
                            _user,
                            _currentOperator
                        ));
                    break;

                case "open:pr":
                    if (!CanOpen(ModuleCodes.PrCl, PermissionActions.Create))
                        ShowAccessDeniedAsync();
                    else
                        OpenDialog(() => new HTMLFormPrCl(_factory, _user, _currentOperator, isPr: true));
                    break;

                case "open:cl":
                    if (!CanOpen(ModuleCodes.PrCl, PermissionActions.Create))
                        ShowAccessDeniedAsync();
                    else
                        OpenDialog(() => new HTMLFormPrCl(_factory, _user, _currentOperator, isPr: false));
                    break;

                case "open:yukyu":
                    if (!CanOpen(ModuleCodes.PaidLeave))
                        ShowAccessDeniedAsync();
                    else
                        OpenDialog(() => new FormPaidLeaveTracking(
                            _user,
                            _currentOperator,
                            _currentShift,
                            _factory
                        ));
                    break;

                case "open:admin":
                    if (!_authorization.IsAllowed(_user.Id, ModuleCodes.Organization, PermissionActions.Administer))
                        ShowAccessDeniedAsync();
                    else
                        OpenDialog(() => new HTMLFormAdmin());
                    break;

                case "open:accesscontrol":
                    if (!_authorization.IsAllowed(_user.Id, ModuleCodes.AccessControl, PermissionActions.Administer))
                        ShowAccessDeniedAsync();
                    else
                        OpenDialog(() => new HTMLFormAccessControl());
                    break;
            }
        }

        private void SendDashboard()
        {
            PostJson(new
            {
                type = "init",
                user = _user.Name,
                locale = Program.CurrentLocale,
                operatorNamePt = _currentOperator.NameRomanji,
                operatorNameJp = string.IsNullOrWhiteSpace(_currentOperator.NameNihongo)
                    ? _currentOperator.NameRomanji
                    : _currentOperator.NameNihongo,
                accessLevel = (int)_user.AccessLevel,
                shiftNamePt = _currentShift.NamePt,
                shiftNameJp = string.IsNullOrWhiteSpace(_currentShift.NameJp)
                    ? _currentShift.NamePt
                    : _currentShift.NameJp,
                dateIso = DateTime.Now.ToString("O"),
                openTasksForShift = GetOpenTasksForCurrentShift(),
                masterCardsInProgress = GetMasterCardCountByStatus("in_progress"),
                masterCardsFollow = GetMasterCardCountByStatus("follow"),
                pending = new
                {
                    tasks = GetPendingTaskItems(),
                    masterCards = GetPendingMasterCardItems()
                },
                permissions = GetDashboardPermissions(),
                profileCode = Program.CurrentSession?.Access.ProfileCode ?? _user.AccessLevel.ToString(),
                selectedSectorId = Program.CurrentSession?.SelectedSectorId,
                canUseAggregateScope = Program.CurrentSession?.CanUseAggregateScope ?? false,
                sectors = Program.CurrentSession?.Sectors.Select(sector => new
                {
                    id = sector.Id,
                    code = sector.Code,
                    namePt = sector.NamePt,
                    nameJp = sector.NameJp
                }).ToArray() ?? []
            });
        }

        private void SendLocale()
        {
            PostJson(new
            {
                type = "locale_changed",
                locale = Program.CurrentLocale
            });
        }

        private int GetOpenTasksForCurrentShift()
        {
            if (!CanOpen(ModuleCodes.Tasks))
                return 0;

            var sectorIds = GetDashboardSectorIds();
            if (sectorIds.Length == 0)
                return 0;

            using var conn = _factory.CreateOpenConnection();

            var tasksTableExists = conn.ExecuteScalar<int>(
                @"SELECT COUNT(1)
                  FROM sqlite_master
                  WHERE type = 'table'
                    AND name = 'Tasks'"
            ) > 0;

            if (!tasksTableExists)
                return 0;

            return conn.ExecuteScalar<int>(
                @"SELECT COUNT(1)
                  FROM Tasks
                  WHERE ShiftId = @ShiftId
                    AND SectorId IN @SectorIds
                    AND Status NOT IN ('completed', 'cancelled')",
                new
                {
                    ShiftId = _currentShift.Id,
                    SectorIds = sectorIds
                }
            );
        }

        private IReadOnlyList<object> GetPendingTaskItems()
        {
            if (!CanOpen(ModuleCodes.Tasks))
                return Array.Empty<object>();

            var sectorIds = GetDashboardSectorIds();
            if (sectorIds.Length == 0)
                return Array.Empty<object>();

            using var conn = _factory.CreateOpenConnection();

            var tasksTableExists = conn.ExecuteScalar<int>(
                @"SELECT COUNT(1)
                  FROM sqlite_master
                  WHERE type = 'table'
                    AND name = 'Tasks'"
            ) > 0;

            if (!tasksTableExists)
                return Array.Empty<object>();

            return conn.Query<DashboardTaskPendingRow>(
                @"SELECT
                      t.Id,
                      COALESCE(t.Description, '') AS Description,
                      substr(t.DueDate, 1, 10) AS DueDate,
                      COALESCE(t.Status, '') AS Status,
                      COALESCE(op.NameRomanji, t.AssigneeCodigoFJ, '') AS AssigneeNamePt,
                      COALESCE(NULLIF(op.NameNihongo, ''), op.NameRomanji, t.AssigneeCodigoFJ, '') AS AssigneeNameJp
                  FROM Tasks t
                  LEFT JOIN Operators op ON op.CodigoFJ = t.AssigneeCodigoFJ
                  WHERE t.ShiftId = @ShiftId
                    AND t.SectorId IN @SectorIds
                    AND t.Status NOT IN ('completed', 'cancelled')
                  ORDER BY date(t.DueDate) ASC, t.Id DESC
                  LIMIT 6;",
                new
                {
                    ShiftId = _currentShift.Id,
                    SectorIds = sectorIds
                })
                .Select(item => new
                {
                    id = item.Id,
                    description = item.Description,
                    dueDate = item.DueDate,
                    status = item.Status,
                    assigneeNamePt = item.AssigneeNamePt,
                    assigneeNameJp = item.AssigneeNameJp
                })
                .Cast<object>()
                .ToList();
        }

        private IReadOnlyList<object> GetPendingMasterCardItems()
        {
            if (!CanOpen(ModuleCodes.MasterCard))
                return Array.Empty<object>();

            var sectorIds = GetDashboardSectorIds();
            if (sectorIds.Length == 0)
                return Array.Empty<object>();

            using var conn = _factory.CreateOpenConnection();
            MasterCardModuleService.EnsureSchema(conn);

            return conn.Query<DashboardMasterCardPendingRow>(
                @"SELECT
                      m.Id,
                      COALESCE(op.NameRomanji, m.OperatorCodigoFJ, '') AS OperatorNamePt,
                      COALESCE(NULLIF(op.NameNihongo, ''), op.NameRomanji, m.OperatorCodigoFJ, '') AS OperatorNameJp,
                      COALESCE(eq.NamePt, eq.NameJp, '') AS EquipmentNamePt,
                      COALESCE(NULLIF(eq.NameJp, ''), eq.NamePt, '') AS EquipmentNameJp,
                      COALESCE(m.Status, '') AS Status,
                      COALESCE(substr(m.FollowDate, 1, 10), '') AS FollowDate
                  FROM MasterCards m
                  LEFT JOIN Operators op ON op.CodigoFJ = m.OperatorCodigoFJ
                  LEFT JOIN Equipments eq ON eq.Id = m.EquipmentId
                  WHERE m.Status IN ('in_progress', 'follow')
                    AND m.SectorId IN @SectorIds
                  ORDER BY
                      CASE m.Status WHEN 'follow' THEN 0 ELSE 1 END,
                      date(COALESCE(m.FollowDate, m.StartDate)) ASC,
                      m.Id DESC
                  LIMIT 6;",
                new { SectorIds = sectorIds })
                .Select(item => new
                {
                    id = item.Id,
                    operatorNamePt = item.OperatorNamePt,
                    operatorNameJp = item.OperatorNameJp,
                    equipmentNamePt = item.EquipmentNamePt,
                    equipmentNameJp = item.EquipmentNameJp,
                    status = item.Status,
                    followDate = item.FollowDate
                })
                .Cast<object>()
                .ToList();
        }

        private int GetMasterCardCountByStatus(string status)
        {
            if (!CanOpen(ModuleCodes.MasterCard))
                return 0;

            var sectorIds = GetDashboardSectorIds();
            if (sectorIds.Length == 0)
                return 0;

            using var conn = _factory.CreateOpenConnection();
            MasterCardModuleService.EnsureSchema(conn);
            return conn.ExecuteScalar<int>(
                @"SELECT COUNT(1)
                  FROM MasterCards
                  WHERE Status = @Status
                    AND SectorId IN @SectorIds;",
                new { Status = status, SectorIds = sectorIds });
        }

        private int[] GetDashboardSectorIds()
        {
            var authorized = _authorization.GetContext(_user.Id).AuthorizedSectorIds;
            var selected = Program.CurrentSession?.SelectedSectorId;
            if (selected.HasValue && authorized.Contains(selected.Value))
                return [selected.Value];

            return authorized.ToArray();
        }

        private bool CanOpen(string moduleCode, string actionCode = PermissionActions.View)
        {
            return _authorization.IsAllowed(_user.Id, moduleCode, actionCode);
        }

        private object GetDashboardPermissions()
        {
            return new
            {
                operators = CanOpen(ModuleCodes.Organization),
                reports = CanOpen(ModuleCodes.Reports),
                presence = CanOpen(ModuleCodes.Presence),
                haidai = CanOpen(ModuleCodes.Haidai),
                followup = CanOpen(ModuleCodes.FollowUp),
                tasks = CanOpen(ModuleCodes.Tasks),
                mastercard = CanOpen(ModuleCodes.MasterCard),
                production = CanOpen(ModuleCodes.Production),
                hikitsuguiCreate = CanOpen(ModuleCodes.Hikitsugui, PermissionActions.Create),
                hikitsuguiRead = CanOpen(ModuleCodes.Hikitsugui),
                sobraDePeca = CanOpen(ModuleCodes.SobraDePeca),
                prcl = CanOpen(ModuleCodes.PrCl, PermissionActions.Create),
                paidleave = CanOpen(ModuleCodes.PaidLeave),
                organizationAdmin = CanOpen(ModuleCodes.Organization, PermissionActions.Administer),
                accessControl = CanOpen(ModuleCodes.AccessControl, PermissionActions.Administer)
            };
        }

        private bool CanOpenWithoutOperationalIdentity(string action)
        {
            if (action is "open:admin" or "open:accesscontrol")
                return true;

            return action == "open:operadores"
                && _authorization.IsAllowed(_user.Id, ModuleCodes.Organization, PermissionActions.View);
        }

        private void ShowAccessDenied()
        {
            MessageBox.Show(
                "Acesso negado. Permissao insuficiente.",
                "Acesso Negado",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
        }

        private void ShowAccessDeniedAsync()
        {
            BeginInvoke(new Action(ShowAccessDenied));
        }

        private bool EnsureCurrentSession()
        {
            if (_sessionEnding)
                return false;

            try
            {
                _sessionValidation.DemandCurrent(_user);
                return true;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException)
            {
                _sessionEnding = true;
                _sessionValidationTimer.Stop();
                Program.CurrentSession = null;
                Program.CurrentUser = null;

                foreach (Form ownedForm in OwnedForms)
                    ownedForm.Close();

                MessageBox.Show(
                    "Sua sessao foi encerrada. Entre novamente para continuar.",
                    "Sessao encerrada",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                BeginInvoke(new Action(Close));
                return false;
            }
        }

        private void OpenDialog(Func<Form> factory)
        {
            BeginInvoke(new Action(() =>
            {
                using var form = factory();
                form.ShowDialog(this);
            }));
        }

        private void PostJson(object payload)
        {
            var json = JsonSerializer.Serialize(payload);

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() =>
                {
                    if (webViewDashboard.CoreWebView2 != null)
                        webViewDashboard.CoreWebView2.PostWebMessageAsJson(json);
                }));
                return;
            }

            if (webViewDashboard.CoreWebView2 != null)
                webViewDashboard.CoreWebView2.PostWebMessageAsJson(json);
        }

        private static string ReadAction(JsonElement root)
        {
            if (root.ValueKind == JsonValueKind.String)
                return root.GetString() ?? "";

            if (root.ValueKind == JsonValueKind.Object)
                return ReadString(root, "action");

            return "";
        }

        private static string ReadString(JsonElement root, string name)
        {
            return root.ValueKind == JsonValueKind.Object &&
                   root.TryGetProperty(name, out var prop)
                ? prop.GetString() ?? ""
                : "";
        }

        private static int ReadInt(JsonElement root, string name)
        {
            return root.ValueKind == JsonValueKind.Object
                   && root.TryGetProperty(name, out var prop)
                   && prop.TryGetInt32(out var value)
                ? value
                : 0;
        }

        private sealed class DashboardTaskPendingRow
        {
            public int Id { get; set; }
            public string Description { get; set; } = string.Empty;
            public string DueDate { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public string AssigneeNamePt { get; set; } = string.Empty;
            public string AssigneeNameJp { get; set; } = string.Empty;
        }

        private sealed class DashboardMasterCardPendingRow
        {
            public int Id { get; set; }
            public string OperatorNamePt { get; set; } = string.Empty;
            public string OperatorNameJp { get; set; } = string.Empty;
            public string EquipmentNamePt { get; set; } = string.Empty;
            public string EquipmentNameJp { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public string FollowDate { get; set; } = string.Empty;
        }
    }
}
