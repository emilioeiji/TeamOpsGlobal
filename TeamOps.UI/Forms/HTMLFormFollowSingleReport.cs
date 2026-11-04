using Microsoft.Web.WebView2.Core;
using System;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using TeamOps.Core.Entities;
using TeamOps.Core.Security;
using TeamOps.Data.Repositories;
using TeamOps.Data.Security;

namespace TeamOps.UI.Forms
{
    public partial class HTMLFormFollowSingleReport : Form
    {
        private readonly int _followId;
        private readonly FollowUpRepository _followRepo;
        private readonly OperatorRepository _opRepo;
        private readonly User _currentUser;
        private readonly AuthorizationService _authorization;

        public HTMLFormFollowSingleReport(
            int followId,
            FollowUpRepository followRepo,
            OperatorRepository opRepo)
        {
            InitializeComponent();
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Text = L("Follow-up individual", "\u500b\u5225\u30d5\u30a9\u30ed\u30fc");

            _followId = followId;
            _followRepo = followRepo;
            _opRepo = opRepo;
            _currentUser = Program.CurrentUser ?? throw new InvalidOperationException("Usuario nao autenticado.");
            _authorization = new AuthorizationService(Program.ConnectionFactory);

            InitializeWebView();
        }

        private async void InitializeWebView()
        {
            await webViewFollowSingle.EnsureCoreWebView2Async(null);

            var core = webViewFollowSingle.CoreWebView2;
            core.Settings.IsWebMessageEnabled = true;
            core.Settings.AreDefaultScriptDialogsEnabled = true;
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.Settings.AreDevToolsEnabled = true;

            core.WebMessageReceived += WebMessageReceived;

            core.SetVirtualHostNameToFolderMapping(
                "app",
                Path.Combine(Application.StartupPath, "ui", "follow-single-report"),
                CoreWebView2HostResourceAccessKind.Allow
            );

            core.SetVirtualHostNameToFolderMapping(
                "assets",
                Path.Combine(Application.StartupPath, "Assets"),
                CoreWebView2HostResourceAccessKind.Allow
            );

            core.Navigate("https://app/index.html");
        }

        private async void WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                using var json = JsonDocument.Parse(e.WebMessageAsJson);
                var root = json.RootElement;
                var action = root.TryGetProperty("action", out var actionProp)
                    ? actionProp.GetString() ?? ""
                    : "";

                switch (action)
                {
                    case "load":
                        LoadReport();
                        break;

                    case "print":
                        await PrintAsync();
                        break;

                    case "save_pdf":
                        await SavePdfAsync();
                        break;
                }
            }
            catch (Exception ex)
            {
                SendNotify(L("Erro", "\u30a8\u30e9\u30fc"), ex.Message);
            }
        }

        private void LoadReport()
        {
            var follow = _followRepo.GetByIdWithJoins(_followId);
            if (follow == null)
            {
                SendNotify(L("Nao encontrado", "\u672a\u691c\u51fa"), L("FollowUp nao encontrado.", "\u30d5\u30a9\u30ed\u30fc\u304c\u898b\u3064\u304b\u308a\u307e\u305b\u3093\u3002"));
                return;
            }
            _authorization.Demand(_currentUser.Id, ModuleCodes.FollowUp, PermissionActions.View, follow.SectorId);

            var op = _opRepo.GetByCodigoFJ(follow.OperatorCodigoFJ);
            var ex = _opRepo.GetByCodigoFJ(follow.ExecutorCodigoFJ);
            var wi = !string.IsNullOrWhiteSpace(follow.WitnessCodigoFJ)
                ? _opRepo.GetByCodigoFJ(follow.WitnessCodigoFJ)
                : null;

            if (op == null || ex == null)
            {
                SendNotify(L("Erro", "\u30a8\u30e9\u30fc"), L("Nao foi possivel carregar os dados do operador.", "\u4f5c\u696d\u8005\u306e\u60c5\u5831\u3092\u8aad\u307f\u8fbc\u3081\u307e\u305b\u3093\u3067\u3057\u305f\u3002"));
                return;
            }

            PostJson(new
            {
                type = "init",
                data = new
                {
                    locale = Program.CurrentLocale,
                    id = follow.Id,
                    generatedAt = DateTime.Now.ToString("yyyy/MM/dd HH:mm"),
                    logoUrl = "https://assets/logo_rodape.png",
                    header = new
                    {
                        title = L("FOLLOW-UP REPORT", "\u500b\u5225\u30d5\u30a9\u30ed\u30fc\u30ec\u30dd\u30fc\u30c8"),
                        subtitle = L("Formulario individual para impressao", "\u5370\u5237\u7528\u306e\u500b\u5225\u30d5\u30a9\u30ed\u30fc")
                    },
                    operatorInfo = new
                    {
                        codigoFJ = op.CodigoFJ,
                        nameRomanji = op.NameRomanji,
                        nameNihongo = op.NameNihongo,
                        startDate = op.StartDate.ToString("yyyy/MM/dd")
                    },
                    follow = new
                    {
                        date = follow.Date.ToString("yyyy/MM/dd HH:mm"),
                        shift = Safe(follow.ShiftName),
                        executor = $"{ex.NameRomanji} / {ex.NameNihongo}",
                        witness = wi != null ? $"{wi.NameRomanji} / {wi.NameNihongo}" : "-",
                        reason = Safe(follow.ReasonName),
                        type = Safe(follow.TypeName),
                        local = Safe(follow.LocalName),
                        equipment = Safe(follow.EquipmentName),
                        sector = Safe(follow.SectorName),
                        description = Safe(follow.Description),
                        guidance = Safe(follow.Guidance)
                    }
                }
            });
        }

        private async Task PrintAsync()
        {
            if (webViewFollowSingle.CoreWebView2 == null)
                return;

            await webViewFollowSingle.CoreWebView2.ExecuteScriptAsync("window.print();");
        }

        private async Task SavePdfAsync()
        {
            var follow = _followRepo.GetByIdWithJoins(_followId)
                ?? throw new InvalidOperationException(L("FollowUp nao encontrado.", "フォローが見つかりません。"));
            _authorization.Demand(_currentUser.Id, ModuleCodes.FollowUp, PermissionActions.Export, follow.SectorId);

            if (webViewFollowSingle.CoreWebView2 == null)
                return;

            using var sfd = new SaveFileDialog
            {
                Filter = "PDF (*.pdf)|*.pdf",
                FileName = $"FollowUp_{_followId}.pdf"
            };

            if (sfd.ShowDialog(this) != DialogResult.OK)
                return;

            bool ok = await webViewFollowSingle.CoreWebView2.PrintToPdfAsync(sfd.FileName);

            if (ok)
                SendNotify(L("PDF gerado", "PDF \u51fa\u529b"), L("Arquivo PDF salvo com sucesso.", "PDF \u30d5\u30a1\u30a4\u30eb\u3092\u4fdd\u5b58\u3057\u307e\u3057\u305f\u3002"));
            else
                SendNotify(L("Falha", "\u5931\u6557"), L("Nao foi possivel gerar o PDF.", "PDF \u3092\u751f\u6210\u3067\u304d\u307e\u305b\u3093\u3067\u3057\u305f\u3002"));
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
                    if (webViewFollowSingle.CoreWebView2 != null)
                        webViewFollowSingle.CoreWebView2.PostWebMessageAsJson(json);
                }));
                return;
            }

            if (webViewFollowSingle.CoreWebView2 != null)
                webViewFollowSingle.CoreWebView2.PostWebMessageAsJson(json);
        }

        private static string Safe(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value;
        }

        private static string L(string pt, string jp)
        {
            return string.Equals(Program.CurrentLocale, "ja-JP", StringComparison.OrdinalIgnoreCase)
                ? jp
                : pt;
        }
    }
}
