using Dapper;
using Microsoft.Web.WebView2.Core;
using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using TeamOps.Data.Db;
using TeamOps.Data.Repositories;
using TeamOps.Core.Entities;
using TeamOps.Core.Security;
using TeamOps.Data.Security;

namespace TeamOps.UI.Forms
{
    public sealed class HTMLFormPrClReport : Form
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly SqliteConnectionFactory _factory;
        private readonly bool _isPr;
        private readonly User _currentUser;
        private readonly AuthorizationService _authorization;
        private readonly Microsoft.Web.WebView2.WinForms.WebView2 _webView;

        public HTMLFormPrClReport(SqliteConnectionFactory factory, User currentUser, bool isPr)
        {
            _factory = factory;
            _currentUser = currentUser;
            _isPr = isPr;
            _authorization = new AuthorizationService(factory);

            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Text = _isPr ? L("Relatorio PR", "PR Report") : L("Relatorio CL", "CL Report");
            Width = 1480;
            Height = 900;
            StartPosition = FormStartPosition.CenterParent;

            _webView = new Microsoft.Web.WebView2.WinForms.WebView2 { Dock = DockStyle.Fill };
            Controls.Add(_webView);
            InitializeWebView();
        }

        private async void InitializeWebView()
        {
            await _webView.EnsureCoreWebView2Async(null);

            var core = _webView.CoreWebView2;
            core.Settings.IsWebMessageEnabled = true;
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.Settings.AreDefaultScriptDialogsEnabled = true;
            core.Settings.AreDevToolsEnabled = true;
            core.WebMessageReceived += WebMessageReceived;

            core.SetVirtualHostNameToFolderMapping(
                "app",
                Path.Combine(Application.StartupPath, "ui", "pr-cl-report"),
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

                    case "filter":
                        SendRows(ReadFilter(root));
                        break;

                    case "open_file":
                        OpenFile(ReadString(root, "fileName"));
                        break;
                }
            }
            catch (Exception ex)
            {
                PostJson(new { type = "error", message = ex.Message });
            }
        }

        private void SendInit()
        {
            _authorization.Demand(_currentUser.Id, ModuleCodes.Reports, PermissionActions.View);
            var authorizedSectorIds = _authorization.GetContext(_currentUser.Id).AuthorizedSectorIds;
            var today = DateTime.Today;
            PostJson(new
            {
                type = "init",
                data = new
                {
                    locale = Program.CurrentLocale,
                    kind = _isPr ? "PR" : "CL",
                    defaults = new
                    {
                        start = today.AddMonths(-1).ToString("yyyy-MM-dd"),
                        end = today.ToString("yyyy-MM-dd")
                    },
                    sectors = new SectorRepository(_factory).GetAll()
                        .Where(item => authorizedSectorIds.Contains(item.Id))
                        .Select(item => new { id = item.Id, namePt = item.NamePt, nameJp = item.NameJp }),
                    categories = GetCategories().Select(item => new { id = item.Id, namePt = item.NamePt, nameJp = item.NameJp }),
                    priorities = GetPriorities().Select(item => new { id = item.Id, namePt = item.NamePt, nameJp = item.NameJp })
                }
            });
        }

        private void SendRows(ReportFilter filter)
        {
            _authorization.DemandSectorOrAggregate(
                _currentUser.Id,
                ModuleCodes.PrCl,
                PermissionActions.View,
                filter.SectorId);
            var authorizedSectorIds = _authorization.GetContext(_currentUser.Id).AuthorizedSectorIds.ToArray();
            using var conn = _factory.CreateOpenConnection();
            var table = _isPr ? "PR" : "CL";
            var categoryTable = _isPr ? "PRCategorias" : "CLCategorias";
            var priorityTable = _isPr ? "PRPrioridades" : "CLPrioridades";

            var rows = conn.Query<ReportRow>(
                $@"
                    SELECT
                        d.Id,
                        COALESCE(d.Titulo, '') AS Title,
                        COALESCE(d.NomeArquivo, '') AS FileName,
                        substr(d.DataEmissao, 1, 10) AS EmissionDate,
                        COALESCE(s.NamePt, '') AS SectorNamePt,
                        COALESCE(NULLIF(s.NameJp, ''), s.NamePt, '') AS SectorNameJp,
                        COALESCE(c.NamePt, '') AS CategoryNamePt,
                        COALESCE(NULLIF(c.NameJp, ''), c.NamePt, '') AS CategoryNameJp,
                        COALESCE(p.NamePt, '') AS PriorityNamePt,
                        COALESCE(NULLIF(p.NameJp, ''), p.NamePt, '') AS PriorityNameJp,
                        COALESCE(op.NameRomanji, d.AutorCodigoFJ, '') AS AuthorNamePt,
                        COALESCE(NULLIF(op.NameNihongo, ''), op.NameRomanji, d.AutorCodigoFJ, '') AS AuthorNameJp
                    FROM {table} d
                    LEFT JOIN Sectors s ON s.Id = d.SetorId
                    LEFT JOIN {categoryTable} c ON c.Id = d.CategoriaId
                    LEFT JOIN {priorityTable} p ON p.Id = d.PrioridadeId
                    LEFT JOIN Operators op ON op.CodigoFJ = d.AutorCodigoFJ
                    WHERE date(d.DataEmissao) BETWEEN date(@Start) AND date(@End)
                      AND d.SetorId IN @AuthorizedSectorIds
                      AND (@SectorId <= 0 OR d.SetorId = @SectorId)
                      AND (@CategoryId <= 0 OR d.CategoriaId = @CategoryId)
                      AND (@PriorityId <= 0 OR d.PrioridadeId = @PriorityId)
                      AND (
                            @Search = ''
                         OR d.Titulo LIKE '%' || @Search || '%'
                         OR d.NomeArquivo LIKE '%' || @Search || '%'
                         OR COALESCE(op.NameRomanji, '') LIKE '%' || @Search || '%'
                         OR COALESCE(op.NameNihongo, '') LIKE '%' || @Search || '%'
                      )
                    ORDER BY date(d.DataEmissao) DESC, d.Id DESC;",
                new
                {
                    filter.Start,
                    filter.End,
                    filter.SectorId,
                    filter.CategoryId,
                    filter.PriorityId,
                    filter.Search,
                    AuthorizedSectorIds = authorizedSectorIds
                })
                .ToList();

            PostJson(new
            {
                type = "rows",
                data = new
                {
                    rows,
                    totals = new
                    {
                        count = rows.Count,
                        sectors = rows.Select(item => item.SectorNamePt).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                        authors = rows.Select(item => item.AuthorNamePt).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).Count()
                    }
                }
            });
        }

        private void OpenFile(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName)
                throw new UnauthorizedAccessException(L("Nome de arquivo invalido.", "Invalid file name."));

            var authorizedSectorIds = _authorization.GetContext(_currentUser.Id).AuthorizedSectorIds.ToArray();
            using (var conn = _factory.CreateOpenConnection())
            {
                var table = _isPr ? "PR" : "CL";
                var allowed = conn.ExecuteScalar<long>(
                    $@"SELECT EXISTS (
                           SELECT 1 FROM {table}
                           WHERE NomeArquivo = @fileName
                             AND SetorId IN @authorizedSectorIds
                       );",
                    new { fileName, authorizedSectorIds }) == 1;
                if (!allowed)
                    throw new UnauthorizedAccessException(L("Arquivo fora do escopo autorizado.", "File outside the authorized scope."));
            }

            var directory = ConfigurationManager.AppSettings[_isPr ? "PRDirectory" : "CLDirectory"] ?? string.Empty;
            var path = Path.Combine(directory, fileName);
            if (!File.Exists(path))
            {
                throw new InvalidOperationException(L("Arquivo nao encontrado.", "File not found."));
            }

            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }

        private LookupItem[] GetCategories()
        {
            return _isPr
                ? new PRCategoriaRepository(_factory).GetAll().ToArray()
                : new CLCategoriaRepository(_factory).GetAll().ToArray();
        }

        private LookupItem[] GetPriorities()
        {
            return _isPr
                ? new PRPrioridadeRepository(_factory).GetAll().ToArray()
                : new CLPrioridadeRepository(_factory).GetAll().ToArray();
        }

        private static ReportFilter ReadFilter(JsonElement root)
        {
            var start = ReadString(root, "start");
            var end = ReadString(root, "end");
            if (string.IsNullOrWhiteSpace(start))
                start = DateTime.Today.AddMonths(-1).ToString("yyyy-MM-dd");
            if (string.IsNullOrWhiteSpace(end))
                end = DateTime.Today.ToString("yyyy-MM-dd");

            return new ReportFilter(
                start,
                end,
                ReadInt(root, "sectorId"),
                ReadInt(root, "categoryId"),
                ReadInt(root, "priorityId"),
                ReadString(root, "search").Trim());
        }

        private void PostJson(object payload)
        {
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => _webView.CoreWebView2?.PostWebMessageAsJson(json)));
                return;
            }

            _webView.CoreWebView2?.PostWebMessageAsJson(json);
        }

        private static int ReadInt(JsonElement root, string propertyName)
        {
            return root.TryGetProperty(propertyName, out var property) && property.TryGetInt32(out var value) ? value : 0;
        }

        private static string ReadString(JsonElement root, string propertyName)
        {
            return root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
                ? property.GetString() ?? string.Empty
                : string.Empty;
        }

        private static string L(string pt, string en)
        {
            return string.Equals(Program.CurrentLocale, "ja-JP", StringComparison.OrdinalIgnoreCase) ? en : pt;
        }

        private sealed record ReportFilter(string Start, string End, int SectorId, int CategoryId, int PriorityId, string Search);

        private sealed class ReportRow
        {
            public int Id { get; set; }
            public string Title { get; set; } = string.Empty;
            public string FileName { get; set; } = string.Empty;
            public string EmissionDate { get; set; } = string.Empty;
            public string SectorNamePt { get; set; } = string.Empty;
            public string SectorNameJp { get; set; } = string.Empty;
            public string CategoryNamePt { get; set; } = string.Empty;
            public string CategoryNameJp { get; set; } = string.Empty;
            public string PriorityNamePt { get; set; } = string.Empty;
            public string PriorityNameJp { get; set; } = string.Empty;
            public string AuthorNamePt { get; set; } = string.Empty;
            public string AuthorNameJp { get; set; } = string.Empty;
        }
    }
}
