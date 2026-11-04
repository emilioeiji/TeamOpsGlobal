using Microsoft.Web.WebView2.Core;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using TeamOps.Core.Entities;
using TeamOps.Core.Security;
using TeamOps.Data.Repositories;
using TeamOps.Data.Security;

namespace TeamOps.UI.Forms
{
    public partial class HTMLFormFollowOperatorReport : Form
    {
        private readonly string _codigoFJ;
        private readonly FollowUpRepository _followRepo;
        private readonly OperatorRepository _opRepo;
        private readonly User _currentUser;
        private readonly AuthorizationService _authorization;

        public HTMLFormFollowOperatorReport(
            string codigoFJ,
            FollowUpRepository followRepo,
            OperatorRepository opRepo,
            ShiftRepository shiftRepo,
            SectorRepository sectorRepo,
            FollowUpReasonRepository reasonRepo,
            FollowUpTypeRepository typeRepo,
            EquipmentRepository equipRepo,
            LocalRepository localRepo)
        {
            InitializeComponent();
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Text = L("Historico Follow por operador", "\u4f5c\u696d\u8005\u30d5\u30a9\u30ed\u30fc\u5c65\u6b74");

            _codigoFJ = codigoFJ;
            _followRepo = followRepo;
            _opRepo = opRepo;
            _currentUser = Program.CurrentUser ?? throw new InvalidOperationException("Usuario nao autenticado.");
            _authorization = new AuthorizationService(Program.ConnectionFactory);

            InitializeWebView();
        }

        private async void InitializeWebView()
        {
            await webViewFollowOperator.EnsureCoreWebView2Async(null);

            var core = webViewFollowOperator.CoreWebView2;
            core.Settings.IsWebMessageEnabled = true;
            core.Settings.AreDefaultScriptDialogsEnabled = true;
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.Settings.AreDevToolsEnabled = true;

            core.WebMessageReceived += WebMessageReceived;

            core.SetVirtualHostNameToFolderMapping(
                "app",
                Path.Combine(Application.StartupPath, "ui", "follow-operator-report"),
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
            var op = _opRepo.GetByCodigoFJ(_codigoFJ);
            if (op == null)
            {
                SendNotify(L("Nao encontrado", "\u672a\u691c\u51fa"), L("Operador nao encontrado.", "\u4f5c\u696d\u8005\u304c\u898b\u3064\u304b\u308a\u307e\u305b\u3093\u3002"));
                return;
            }

            _authorization.Demand(_currentUser.Id, ModuleCodes.FollowUp, PermissionActions.View);
            var authorizedSectorIds = _authorization.GetContext(_currentUser.Id).AuthorizedSectorIds;
            var list = _followRepo.GetByOperatorWithJoins(_codigoFJ)
                .Where(item => authorizedSectorIds.Contains(item.SectorId))
                .ToList();
            var lastDate = list.Count == 0 ? "-" : list.Max(x => x.Date).ToString("yyyy/MM/dd HH:mm");

            PostJson(new
            {
                type = "init",
                data = new
                {
                    locale = Program.CurrentLocale,
                    generatedAt = DateTime.Now.ToString("yyyy/MM/dd HH:mm"),
                    logoUrl = "https://assets/logo_rodape.png",
                    operatorInfo = new
                    {
                        codigoFJ = op.CodigoFJ,
                        nameRomanji = op.NameRomanji,
                        nameNihongo = op.NameNihongo,
                        startDate = op.StartDate.ToString("yyyy/MM/dd"),
                        total = list.Count,
                        lastDate
                    },
                    records = list.Select(x => new
                    {
                        date = x.Date.ToString("yyyy/MM/dd HH:mm"),
                        shift = Safe(x.ShiftName),
                        executor = JoinNames(x.ExecutorNamePt, x.ExecutorNameJp),
                        witness = JoinNames(x.WitnessNamePt, x.WitnessNameJp),
                        reason = Safe(x.ReasonName),
                        type = Safe(x.TypeName),
                        local = Safe(x.LocalName),
                        equipment = Safe(x.EquipmentName),
                        sector = Safe(x.SectorName),
                        description = Safe(x.Description),
                        guidance = Safe(x.Guidance)
                    }).ToList()
                }
            });
        }

        private async Task PrintAsync()
        {
            if (webViewFollowOperator.CoreWebView2 == null)
                return;

            await webViewFollowOperator.CoreWebView2.ExecuteScriptAsync("window.print();");
        }

        private async Task SavePdfAsync()
        {
            _authorization.Demand(_currentUser.Id, ModuleCodes.FollowUp, PermissionActions.Export);

            if (webViewFollowOperator.CoreWebView2 == null)
                return;

            using var sfd = new SaveFileDialog
            {
                Filter = "PDF (*.pdf)|*.pdf",
                FileName = $"FollowHistory_{_codigoFJ}.pdf"
            };

            if (sfd.ShowDialog(this) != DialogResult.OK)
                return;

            bool ok = await webViewFollowOperator.CoreWebView2.PrintToPdfAsync(sfd.FileName);

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
                    if (webViewFollowOperator.CoreWebView2 != null)
                        webViewFollowOperator.CoreWebView2.PostWebMessageAsJson(json);
                }));
                return;
            }

            if (webViewFollowOperator.CoreWebView2 != null)
                webViewFollowOperator.CoreWebView2.PostWebMessageAsJson(json);
        }

        private static string JoinNames(string pt, string jp)
        {
            var left = string.IsNullOrWhiteSpace(pt) ? "-" : pt;
            var right = string.IsNullOrWhiteSpace(jp) ? "-" : jp;
            return $"{left} / {right}";
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
