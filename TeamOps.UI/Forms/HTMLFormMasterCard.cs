using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using TeamOps.Core.Entities;
using TeamOps.Core.Security;
using TeamOps.Data.Db;
using TeamOps.Data.Security;
using TeamOps.UI.Forms.Models;
using TeamOps.UI.Services;

namespace TeamOps.UI.Forms
{
    public partial class HTMLFormMasterCard : Form
    {
        private readonly SqliteConnectionFactory _factory;
        private readonly User _currentUser;
        private readonly Operator _currentOperator;
        private readonly AuthorizationService _authorization;

        public HTMLFormMasterCard(
            SqliteConnectionFactory factory,
            User currentUser,
            Operator currentOperator)
        {
            InitializeComponent();
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Text = L("Controle de MasterCard", "MasterCard Control");

            _factory = factory;
            _currentUser = currentUser;
            _currentOperator = currentOperator;
            _authorization = new AuthorizationService(factory);

            InitializeWebView();
        }

        private async void InitializeWebView()
        {
            await webViewMasterCard.EnsureCoreWebView2Async(null);

            var core = webViewMasterCard.CoreWebView2;
            core.Settings.IsWebMessageEnabled = true;
            core.Settings.AreDefaultScriptDialogsEnabled = true;
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.Settings.AreDevToolsEnabled = true;

            core.WebMessageReceived += WebMessageReceived;
            core.SetVirtualHostNameToFolderMapping(
                "app",
                Path.Combine(Application.StartupPath, "ui", "mastercard"),
                CoreWebView2HostResourceAccessKind.Allow);

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
                    LoadDetails(msg.id);
                    break;
                case "save":
                    SaveMasterCard(msg);
                    break;
                case "update":
                    UpdateMasterCard(msg);
                    break;
                case "advance_status":
                    AdvanceStatus(msg);
                    break;
            }
        }

        private void LoadInitialData()
        {
            _authorization.Demand(_currentUser.Id, ModuleCodes.MasterCard, PermissionActions.View);
            var authorizedSectorIds = _authorization.GetContext(_currentUser.Id).AuthorizedSectorIds;
            using var conn = _factory.CreateOpenConnection();
            MasterCardModuleService.EnsureSchema(conn);

            PostJson(new
            {
                type = "init",
                locale = Program.CurrentLocale,
                currentOperatorNamePt = _currentOperator.NameRomanji,
                currentOperatorNameJp = string.IsNullOrWhiteSpace(_currentOperator.NameNihongo)
                    ? _currentOperator.NameRomanji
                    : _currentOperator.NameNihongo,
                currentUser = _currentUser.Name,
                statuses = MasterCardModuleService.QueryStatuses().ToList(),
                operators = MasterCardModuleService.QueryOperators(conn, authorizedSectorIds).ToList(),
                trainers = MasterCardModuleService.QueryTrainers(conn, authorizedSectorIds).ToList(),
                sectors = MasterCardModuleService.QuerySectors(conn, authorizedSectorIds).ToList(),
                equipments = MasterCardModuleService.QueryEquipments(conn).ToList(),
                rows = MasterCardModuleService.QueryMasterCards(conn, authorizedSectorIds).Select(ToRowPayload).ToList()
            });
        }

        private void LoadDetails(int id)
        {
            try
            {
                using var conn = _factory.CreateOpenConnection();
                MasterCardModuleService.EnsureSchema(conn);

                var current = MasterCardModuleService.GetMasterCard(conn, id)
                    ?? throw new InvalidOperationException(L("MasterCard nao encontrado.", "MasterCard が見つかりません。"));
                _authorization.Demand(_currentUser.Id, ModuleCodes.MasterCard, PermissionActions.View, current.SectorId);

                PostJson(new
                {
                    type = "mastercard_details",
                    id,
                    detail = ToRowPayload(current),
                    history = MasterCardModuleService.QueryHistory(conn, id)
                });
            }
            catch (Exception ex)
            {
                PostJson(new { type = "error", message = ex.Message });
            }
        }
        private void SaveMasterCard(JsRequest msg)
        {
            try
            {
                using var conn = _factory.CreateOpenConnection();
                MasterCardModuleService.EnsureSchema(conn);

                var input = BuildInput(msg);
                _authorization.Demand(_currentUser.Id, ModuleCodes.MasterCard, PermissionActions.Create, input.SectorId);

                using var tx = conn.BeginTransaction();
                MasterCardModuleService.SaveMasterCard(conn, tx, input, _currentOperator.CodigoFJ);
                tx.Commit();

                PostJson(new
                {
                    type = "saved",
                    message = L("MasterCard cadastrado com sucesso.", "MasterCard を登録しました。")
                });

                SendRows(conn);
            }
            catch (Exception ex)
            {
                PostJson(new { type = "error", message = ex.Message });
            }
        }

        private void UpdateMasterCard(JsRequest msg)
        {
            try
            {
                using var conn = _factory.CreateOpenConnection();
                MasterCardModuleService.EnsureSchema(conn);

                var input = BuildInput(msg);
                var current = MasterCardModuleService.GetMasterCard(conn, msg.id)
                    ?? throw new InvalidOperationException(L("MasterCard nao encontrado.", "MasterCard が見つかりません。"));
                _authorization.Demand(_currentUser.Id, ModuleCodes.MasterCard, PermissionActions.Edit, current.SectorId);
                _authorization.Demand(_currentUser.Id, ModuleCodes.MasterCard, PermissionActions.Edit, input.SectorId);

                using var tx = conn.BeginTransaction();
                MasterCardModuleService.UpdateMasterCard(conn, tx, msg.id, input);
                tx.Commit();

                PostJson(new
                {
                    type = "updated",
                    message = L("MasterCard atualizado com sucesso.", "MasterCard を更新しました。")
                });

                SendRows(conn);
            }
            catch (Exception ex)
            {
                PostJson(new { type = "error", message = ex.Message });
            }
        }

        private void AdvanceStatus(JsRequest msg)
        {
            try
            {
                using var conn = _factory.CreateOpenConnection();
                MasterCardModuleService.EnsureSchema(conn);

                var current = MasterCardModuleService.GetMasterCard(conn, msg.id)
                    ?? throw new InvalidOperationException(L("MasterCard nao encontrado.", "MasterCard が見つかりません。"));
                _authorization.Demand(_currentUser.Id, ModuleCodes.MasterCard, PermissionActions.Approve, current.SectorId);

                using var tx = conn.BeginTransaction();
                var newStatus = MasterCardModuleService.AdvanceStatus(conn, tx, msg.id, _currentOperator.CodigoFJ);
                tx.Commit();

                PostJson(new
                {
                    type = "status_changed",
                    message = newStatus == "follow"
                        ? L("MasterCard concluido e enviado para follow.", "MasterCard を完了し、Follow に送りました。")
                        : L("Follow realizado. MasterCard finalizado.", "Follow を完了し、MasterCard を終了しました。")
                });

                SendRows(conn);
            }
            catch (Exception ex)
            {
                PostJson(new { type = "error", message = ex.Message });
            }
        }

        private void SendRows(System.Data.IDbConnection conn)
        {
            var authorizedSectorIds = _authorization.GetContext(_currentUser.Id).AuthorizedSectorIds;
            PostJson(new
            {
                type = "rows",
                data = MasterCardModuleService.QueryMasterCards(conn, authorizedSectorIds).Select(ToRowPayload).ToList()
            });
        }

        private MasterCardModuleService.MasterCardInput BuildInput(JsRequest msg)
        {
            return new MasterCardModuleService.MasterCardInput
            {
                OperatorCodigoFJ = (msg.operatorCodigoFJ ?? string.Empty).Trim().ToUpperInvariant(),
                TrainerCodigoFJ = (msg.trainerCodigoFJ ?? string.Empty).Trim().ToUpperInvariant(),
                SectorId = msg.sectorId,
                EquipmentId = msg.equipmentId,
                Notes = (msg.notes ?? string.Empty).Trim(),
                StartDate = msg.startDate,
                ChangedByCodigoFJ = _currentOperator.CodigoFJ
            };
        }

        private static object ToRowPayload(MasterCardModuleService.MasterCardRow row)
        {
            return new
            {
                id = row.Id,
                operatorCodigoFJ = row.OperatorCodigoFJ,
                operatorNamePt = row.OperatorNamePt,
                operatorNameJp = row.OperatorNameJp,
                trainerCodigoFJ = row.TrainerCodigoFJ,
                trainerNamePt = row.TrainerNamePt,
                trainerNameJp = row.TrainerNameJp,
                sectorId = row.SectorId,
                sectorNamePt = row.SectorNamePt,
                sectorNameJp = row.SectorNameJp,
                equipmentId = row.EquipmentId,
                equipmentNamePt = row.EquipmentNamePt,
                equipmentNameJp = row.EquipmentNameJp,
                description = row.Description,
                notes = row.Notes,
                startDate = row.StartDate,
                masterCardStatus = row.Status,
                concludedAt = row.ConcludedAt,
                followDate = row.FollowDate,
                finalizedAt = row.FinalizedAt,
                createdAt = row.CreatedAt,
                updatedAt = row.UpdatedAt,
                createdByNamePt = row.CreatedByNamePt,
                createdByNameJp = row.CreatedByNameJp,
                historyCount = row.HistoryCount
            };
        }

        private void PostJson(object payload)
        {
            webViewMasterCard.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(payload));
        }

        private static string L(string pt, string jp)
        {
            return string.Equals(Program.CurrentLocale, "ja-JP", StringComparison.OrdinalIgnoreCase)
                ? jp
                : pt;
        }
    }
}
