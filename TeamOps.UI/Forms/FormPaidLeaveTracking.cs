using System;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using Dapper;
using Microsoft.Web.WebView2.Core;
using TeamOps.Core.Entities;
using TeamOps.Core.Security;
using TeamOps.Data.Db;
using TeamOps.Data.Repositories;
using TeamOps.Data.Security;
using TeamOps.UI.Forms.Models;

namespace TeamOps.UI.Forms
{
    public partial class FormPaidLeaveTracking : Form
    {
        private readonly SqliteConnectionFactory _factory;
        private readonly Operator _currentOperator;
        private readonly User _currentUser;
        private readonly PaidLeaveAuthorizationService _authorization;

        public FormPaidLeaveTracking(User user, Operator op, Shift shift, SqliteConnectionFactory factory)
        {
            InitializeComponent();

            _factory = factory;
            _currentOperator = op;
            _currentUser = user;
            _authorization = new PaidLeaveAuthorizationService(factory);

            InitializeWebView();
        }

        private async void InitializeWebView()
        {
            await webViewPaidLeave.EnsureCoreWebView2Async(null);

            webViewPaidLeave.CoreWebView2.WebMessageReceived += WebMessageReceived;

            var htmlPath = Path.Combine(
                Application.StartupPath,
                "ui", "paidleave", "index.html");

            webViewPaidLeave.Source = new Uri(htmlPath);
        }

        private void WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            var msg = JsonSerializer.Deserialize<JsRequest>(e.WebMessageAsJson);
            if (msg == null) return;

            try
            {
                switch (msg.action)
                {
                    case "paidleave_load":
                        var authorizedSectorIds = _authorization.GetAuthorizedSectorIds(_currentUser.Id);
                        SendPermissions();
                        RefreshTable(0);
                        SendJsonFromSql("select_operators.sql", new { AuthorizedSectorIds = authorizedSectorIds });
                        SendJsonFromSql("select_motivos.sql");
                        break;

                    case "toggle_todoke":
                        _authorization.DemandRequest(_currentUser.Id, PermissionActions.Edit, msg.id);
                        ExecuteSql("insert_todoke.sql", new
                        {
                            AcompYukyuId = msg.id,
                            TakenBy = _currentOperator.CodigoFJ,
                            TakenAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                        });

                        RefreshTable(msg.shiftId);
                        break;

                    case "toggle_folha":
                        _authorization.DemandRequest(_currentUser.Id, PermissionActions.Edit, msg.id);
                        ExecuteSql("insert_folha.sql", new
                        {
                            AcompYukyuId = msg.id,
                            TakenBy = _currentOperator.CodigoFJ,
                            TakenAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                        });

                        RefreshTable(msg.shiftId);
                        break;

                    case "get_todoke_info":
                        _authorization.DemandRequest(_currentUser.Id, PermissionActions.View, msg.id);
                        SendJsonFromSql("get_todoke_info.sql", new { AcompYukyuId = msg.id });
                        break;

                    case "get_folha_info":
                        _authorization.DemandRequest(_currentUser.Id, PermissionActions.View, msg.id);
                        SendJsonFromSql("get_folha_info.sql", new { AcompYukyuId = msg.id });
                        break;

                    case "add_request":
                        _authorization.DemandOperator(_currentUser.Id, PermissionActions.Create, msg.opCodigoFJ);
                        ExecuteSql("insert_acomp.sql", new
                        {
                            OperatorCodigoFJ = msg.opCodigoFJ,
                            RequestDate = msg.reqDate,
                            AuthorizedByCodigoFJ = _currentOperator.CodigoFJ,
                            Notes = msg.notes,
                            TodokeMotivoId = msg.motivoId
                        });

                        RefreshTable(msg.shiftId);
                        break;

                    case "update_request":
                        _authorization.DemandRequestMove(
                            _currentUser.Id,
                            PermissionActions.Edit,
                            msg.id,
                            msg.opCodigoFJ);
                        ExecuteSql("update_acomp.sql", new
                        {
                            Id = msg.id,
                            OperatorCodigoFJ = msg.opCodigoFJ,
                            RequestDate = msg.reqDate,
                            Notes = msg.notes,
                            TodokeMotivoId = msg.motivoId
                        });

                        TryLogSystem("PaidLeave", "Editou", msg.id, $"Motivo={msg.motivoId}");
                        RefreshTable(msg.shiftId);
                        break;

                    case "delete_request":
                        _authorization.DemandRequest(_currentUser.Id, PermissionActions.Delete, msg.id);
                        ExecuteSql("delete_acomp.sql", new
                        {
                            Id = msg.id
                        });

                        TryLogSystem("PaidLeave", "Excluiu", msg.id);
                        RefreshTable(msg.shiftId);
                        break;

                    case "filter_shift":
                        RefreshTable(msg.shiftId);
                        break;

                    case "toggle_conferencia":
                        _authorization.DemandRequest(_currentUser.Id, PermissionActions.Edit, msg.id);
                        ExecuteSql("insert_conferencia.sql", new
                        {
                            AcompYukyuId = msg.id,
                            TakenBy = _currentOperator.CodigoFJ,
                            TakenAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                        });

                        RefreshTable(msg.shiftId);
                        break;

                    case "get_conferencia_info":
                        _authorization.DemandRequest(_currentUser.Id, PermissionActions.View, msg.id);
                        SendJsonFromSql("get_conferencia_info.sql", new { AcompYukyuId = msg.id });
                        break;
                }
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show(
                    "Acesso negado para a solicitacao ou setor informado.",
                    "Acesso negado",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void SendJsonFromSql(string sqlFile, object? param = null)
        {
            var sqlPath = Path.Combine(
               Application.StartupPath,
                "Sql", "paidleave", sqlFile);

            var sql = File.ReadAllText(sqlPath);

            using var conn = _factory.CreateOpenConnection();
            var rows = conn.Query(sql, param); // dynamic

            var json = JsonSerializer.Serialize(new
            {
                type = Path.GetFileNameWithoutExtension(sqlFile),
                data = rows
            });

            webViewPaidLeave.CoreWebView2.PostWebMessageAsJson(json);
        }

        private void SendPermissions()
        {
            var json = JsonSerializer.Serialize(new
            {
                type = "paidleave_permissions",
                canCreate = _authorization.IsAllowed(_currentUser.Id, PermissionActions.Create),
                canEdit = _authorization.IsAllowed(_currentUser.Id, PermissionActions.Edit),
                canDelete = _authorization.IsAllowed(_currentUser.Id, PermissionActions.Delete)
            });
            webViewPaidLeave.CoreWebView2.PostWebMessageAsJson(json);
        }

        private void ExecuteSql(string sqlFile, object param)
        {
            var sqlPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Sql", "paidleave", sqlFile);

            var sql = File.ReadAllText(sqlPath);

            using var conn = _factory.CreateOpenConnection();
            conn.Execute(sql, param);
        }

        private void RefreshTable(int shiftId)
        {
            var authorizedSectorIds = _authorization.GetAuthorizedSectorIds(_currentUser.Id);
            SendJsonFromSql("select_all_by_shift.sql", new
            {
                ShiftId = Math.Max(shiftId, 0),
                AuthorizedSectorIds = authorizedSectorIds
            });
        }

        private void TryLogSystem(string module, string action, int? targetId = null, string? details = null)
        {
            try
            {
                var logRepo = new SystemLogRepository(_factory);
                logRepo.Log(_currentOperator.CodigoFJ, module, action, targetId, details);
            }
            catch (Exception ex)
            {
                Console.WriteLine("SYSTEM LOG ERROR: " + ex);
            }
        }
    }
}
