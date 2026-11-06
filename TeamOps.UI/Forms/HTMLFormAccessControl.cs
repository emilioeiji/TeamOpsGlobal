using Microsoft.Web.WebView2.Core;
using System.Text.Json;
using TeamOps.Core.Security;
using TeamOps.Data.Security;
using TeamOps.UI.Forms.Models;

namespace TeamOps.UI.Forms
{
    public partial class HTMLFormAccessControl : Form
    {
        private readonly UserAdministrationService _administration;
        private readonly PermissionMatrixAdministrationService _permissionMatrix;

        public HTMLFormAccessControl()
        {
            InitializeComponent();
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Text = L("Controle de acesso", "アクセス管理");
            _administration = new UserAdministrationService(Program.ConnectionFactory);
            _permissionMatrix = new PermissionMatrixAdministrationService(Program.ConnectionFactory);
            _administration.GetPayload(CurrentUserId);
            InitializeWebView();
        }

        private static int CurrentUserId => Program.CurrentUser?.Id
            ?? throw new UnauthorizedAccessException("Sessao de usuario indisponivel.");

        private async void InitializeWebView()
        {
            await webViewAccessControl.EnsureCoreWebView2Async(null);
            var core = webViewAccessControl.CoreWebView2;
            core.Settings.IsWebMessageEnabled = true;
            core.Settings.AreDefaultScriptDialogsEnabled = true;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDevToolsEnabled = false;
            core.WebMessageReceived += WebMessageReceived;
            core.SetVirtualHostNameToFolderMapping("app", Path.Combine(Application.StartupPath, "ui", "access-control"), CoreWebView2HostResourceAccessKind.Allow);
            core.Navigate("https://app/index.html");
        }

        private void WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var msg = JsonSerializer.Deserialize<JsRequest>(e.WebMessageAsJson)
                    ?? throw new InvalidOperationException("Solicitacao invalida.");
                switch (msg.action)
                {
                    case "load": SendPayload("init"); break;
                    case "create": CreateUser(msg); break;
                    case "update": UpdateUser(msg); break;
                    case "reset_password": ResetPassword(msg); break;
                    case "save_permissions": SavePermissions(msg); break;
                }
            }
            catch (Exception ex)
            {
                PostJson(new { type = "error", message = ex.Message });
            }
        }

        private void CreateUser(JsRequest msg)
        {
            DemandMatchingPasswords(msg);
            _administration.CreateUser(CurrentUserId, ToRequest(msg), msg.password);
            PostJson(new { type = "created", message = L("Usuario cadastrado com sucesso.", "ユーザーを登録しました。") });
            SendPayload("rows");
        }

        private void UpdateUser(JsRequest msg)
        {
            if (msg.userId <= 0) throw new InvalidOperationException("Selecione um usuario para editar.");
            _administration.UpdateUser(CurrentUserId, msg.userId, ToRequest(msg));
            PostJson(new { type = "updated", message = L("Usuario atualizado com sucesso.", "ユーザーを更新しました。") });
            SendPayload("rows");
        }

        private void ResetPassword(JsRequest msg)
        {
            if (msg.userId <= 0) throw new InvalidOperationException("Selecione um usuario para redefinir a senha.");
            DemandMatchingPasswords(msg);
            _administration.ResetPassword(CurrentUserId, msg.userId, msg.password);
            PostJson(new { type = "password_reset", message = L("Senha redefinida com sucesso.", "パスワードを再設定しました。") });
        }

        private void SavePermissions(JsRequest msg)
        {
            _permissionMatrix.SaveProfilePermissions(
                CurrentUserId,
                msg.profileCode,
                (msg.permissions ?? []).Select(item => new ModulePermissionSelection(
                    item.moduleCode, item.actionCode, item.isAllowed)).ToArray());
            PostJson(new
            {
                type = "permissions_saved",
                message = L("Matriz de permissoes atualizada.", "権限マトリクスを更新しました。")
            });
            SendPayload("rows");
        }

        private void SendPayload(string type)
        {
            var payload = _administration.GetPayload(CurrentUserId);
            var canManagePermissionMatrix = Program.CurrentSession?.Access.HierarchyRank == 6;
            var permissionPayload = canManagePermissionMatrix
                ? _permissionMatrix.GetPayload(CurrentUserId)
                : null;
            PostJson(new
            {
                type,
                locale = Program.CurrentLocale,
                profiles = payload.Profiles.Select(item => new { code = item.Code, namePt = item.NamePt, nameJp = item.NameJp, rank = item.HierarchyRank, scopeMode = item.ScopeMode }),
                organizationNodes = payload.OrganizationNodes.Select(item => new { id = item.Id, parentId = item.ParentId, nodeType = item.NodeType, code = item.Code, namePt = item.NamePt, nameJp = item.NameJp }),
                sectors = payload.Sectors.Select(item => new { id = item.Id, organizationNodeId = item.OrganizationNodeId, code = item.Code, namePt = item.NamePt, nameJp = item.NameJp }),
                canManagePermissionMatrix,
                permissionModules = ModuleCodes.All,
                permissionActions = PermissionActions.All,
                permissionProfiles = permissionPayload?.Profiles.Select(item => new
                {
                    code = item.Code,
                    namePt = item.NamePt,
                    nameJp = item.NameJp,
                    rank = item.HierarchyRank
                }) ?? [],
                permissionRows = permissionPayload?.Permissions.Select(item => new
                {
                    profileCode = item.ProfileCode,
                    moduleCode = item.ModuleCode,
                    actionCode = item.ActionCode,
                    isAllowed = item.IsAllowed
                }) ?? [],
                rows = payload.Users.Select(item => new
                {
                    id = item.Id, login = item.Login, codigoFJ = item.CodigoFJ, name = item.Name,
                    profileCode = item.ProfileCode, profileNamePt = item.ProfileNamePt, profileNameJp = item.ProfileNameJp,
                    referenceNodeId = item.ReferenceNodeId, referenceCode = item.ReferenceCode, isActive = item.IsActive,
                    sectorIds = item.SectorIds, createdAt = item.CreatedAt
                })
            });
        }

        private static UserAdministrationSaveRequest ToRequest(JsRequest msg) => new(
            (msg.login ?? string.Empty).Trim(), (msg.codigoFJ ?? string.Empty).Trim(), (msg.name ?? string.Empty).Trim(),
            (msg.profileCode ?? string.Empty).Trim(), msg.referenceNodeId, msg.sectorIds ?? [], msg.isActive);

        private static void DemandMatchingPasswords(JsRequest msg)
        {
            if (!string.Equals(msg.password, msg.confirmPassword, StringComparison.Ordinal))
                throw new InvalidOperationException("A confirmacao da senha nao confere.");
        }

        private static string L(string pt, string jp) =>
            string.Equals(Program.CurrentLocale, "ja-JP", StringComparison.OrdinalIgnoreCase) ? jp : pt;

        private void PostJson(object payload) => webViewAccessControl.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(payload));
    }
}
