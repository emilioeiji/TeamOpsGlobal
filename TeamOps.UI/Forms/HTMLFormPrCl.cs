using ClosedXML.Excel;
using Microsoft.Web.WebView2.Core;
using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using TeamOps.Core.Entities;
using TeamOps.Core.Security;
using TeamOps.Data.Db;
using TeamOps.Data.Repositories;
using TeamOps.Data.Security;

namespace TeamOps.UI.Forms
{
    public sealed class HTMLFormPrCl : Form
    {
        private readonly SqliteConnectionFactory _factory;
        private readonly Operator _currentOperator;
        private readonly User _currentUser;
        private readonly AuthorizationService _authorization;
        private readonly bool _isPr;
        private readonly Microsoft.Web.WebView2.WinForms.WebView2 _webView;

        public HTMLFormPrCl(SqliteConnectionFactory factory, User currentUser, Operator currentOperator, bool isPr)
        {
            _factory = factory;
            _currentUser = currentUser;
            _currentOperator = currentOperator;
            _isPr = isPr;
            _authorization = new AuthorizationService(factory);

            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Text = _isPr ? L("PR", "PR") : L("CL", "CL");
            Width = 1280;
            Height = 820;
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
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.Settings.AreDefaultScriptDialogsEnabled = true;
            core.Settings.AreDevToolsEnabled = true;
            core.WebMessageReceived += WebMessageReceived;

            core.SetVirtualHostNameToFolderMapping(
                "app",
                Path.Combine(Application.StartupPath, "ui", "pr-cl"),
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

                    case "save":
                        SaveDocument(root);
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
            _authorization.Demand(_currentUser.Id, ModuleCodes.PrCl, PermissionActions.Create);
            var authorizedSectorIds = _authorization.GetContext(_currentUser.Id).AuthorizedSectorIds;
            var sectors = new SectorRepository(_factory).GetAll()
                .Where(item => authorizedSectorIds.Contains(item.Id))
                .ToList();
            var categories = _isPr
                ? new PRCategoriaRepository(_factory).GetAll()
                : new CLCategoriaRepository(_factory).GetAll();
            var priorities = _isPr
                ? new PRPrioridadeRepository(_factory).GetAll()
                : new CLPrioridadeRepository(_factory).GetAll();

            PostJson(new
            {
                type = "init",
                data = new
                {
                    locale = Program.CurrentLocale,
                    kind = _isPr ? "PR" : "CL",
                    author = string.IsNullOrWhiteSpace(_currentOperator.NameNihongo)
                        ? _currentOperator.NameRomanji
                        : _currentOperator.NameNihongo,
                    emissionDate = DateTime.Now.ToString("yyyy-MM-dd"),
                    nextId = GetNextId(),
                    sectors = sectors.Select(item => new { id = item.Id, namePt = item.NamePt, nameJp = item.NameJp }),
                    categories = categories.Select(item => new { id = item.Id, namePt = item.NamePt, nameJp = item.NameJp }),
                    priorities = priorities.Select(item => new { id = item.Id, namePt = item.NamePt, nameJp = item.NameJp })
                }
            });
        }

        private void SaveDocument(JsonElement root)
        {
            var input = new DocumentInput(
                ReadInt(root, "sectorId"),
                ReadInt(root, "categoryId"),
                ReadInt(root, "priorityId"),
                ReadString(root, "title").Trim(),
                ReadString(root, "fileName").Trim());

            ValidateInput(input);
            _authorization.Demand(
                _currentUser.Id,
                ModuleCodes.PrCl,
                PermissionActions.Create,
                input.SectorId);

            var id = _isPr
                ? new PRRepository(_factory).Add(new PR
                {
                    SetorId = input.SectorId,
                    CategoriaId = input.CategoryId,
                    PrioridadeId = input.PriorityId,
                    Titulo = input.Title,
                    NomeArquivo = input.FileName,
                    DataEmissao = DateTime.Now,
                    AutorCodigoFJ = _currentOperator.CodigoFJ
                })
                : new CLRepository(_factory).Add(new CL
                {
                    SetorId = input.SectorId,
                    CategoriaId = input.CategoryId,
                    PrioridadeId = input.PriorityId,
                    Titulo = input.Title,
                    NomeArquivo = input.FileName,
                    DataEmissao = DateTime.Now,
                    AutorCodigoFJ = _currentOperator.CodigoFJ
                });

            var finalPath = GenerateExcel(id, input);
            Process.Start(new ProcessStartInfo
            {
                FileName = finalPath,
                UseShellExecute = true
            });

            PostJson(new
            {
                type = "saved",
                data = new
                {
                    message = L("Documento salvo e arquivo gerado com sucesso.", "Document saved and file generated."),
                    path = finalPath,
                    nextId = GetNextId()
                }
            });
        }

        private string GenerateExcel(int documentId, DocumentInput input)
        {
            var template = ConfigurationManager.AppSettings[_isPr ? "PRTemplate" : "CLTemplate"] ?? string.Empty;
            var directory = ConfigurationManager.AppSettings[_isPr ? "PRDirectory" : "CLDirectory"] ?? string.Empty;

            if (string.IsNullOrWhiteSpace(template) || !File.Exists(template))
            {
                throw new InvalidOperationException(L("Arquivo modelo nao encontrado.", "Template file not found."));
            }

            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException(L("Pasta de destino nao configurada.", "Output directory is not configured."));
            }

            Directory.CreateDirectory(directory);
            var finalPath = Path.Combine(directory, input.FileName);
            File.Copy(template, finalPath, overwrite: true);

            using var wb = new XLWorkbook(finalPath);
            var ws = wb.Worksheet("PR\u6587\u66f8");
            var priorities = _isPr
                ? new PRPrioridadeRepository(_factory).GetAll()
                : new CLPrioridadeRepository(_factory).GetAll();
            var priority = priorities.FirstOrDefault(item => item.Id == input.PriorityId);

            ws.Cell("D4").Value = documentId;
            ws.Cell("F5").Value = input.Title;
            ws.Cell("D1").Value = priority?.NamePt ?? string.Empty;
            ws.Cell("T2").Value = DateTime.Now.ToString("yyyy-MM-dd");
            ws.Cell("T4").Value = string.IsNullOrWhiteSpace(_currentOperator.NameNihongo)
                ? _currentOperator.NameRomanji
                : _currentOperator.NameNihongo;

            switch (input.CategoryId)
            {
                case 1:
                    ws.Cell("D7").Value = "\u2714";
                    break;
                case 2:
                    ws.Cell("D9").Value = "\u2714";
                    break;
                case 3:
                    ws.Cell("N7").Value = "\u2714";
                    break;
                case 4:
                    ws.Cell("N9").Value = "\u2714";
                    break;
            }

            ExportOperators(wb, input.SectorId);
            wb.Save();
            return finalPath;
        }

        private void ExportOperators(XLWorkbook wb, int sectorId)
        {
            var ws = wb.Worksheet("Operadores");
            var operators = new OperatorRepository(_factory).GetAll()
                .Where(op => op.Status && op.SectorId == sectorId)
                .OrderBy(op => op.NameRomanji)
                .ToList();

            var dayRow = 3;
            var nightRow = 3;
            foreach (var op in operators)
            {
                if (op.ShiftId == 1)
                {
                    ws.Cell(dayRow, 2).Value = op.NameRomanji;
                    dayRow++;
                }
                else if (op.ShiftId == 2)
                {
                    ws.Cell(nightRow, 3).Value = op.NameRomanji;
                    nightRow++;
                }
            }
        }

        private int GetNextId()
        {
            return _isPr
                ? new PRRepository(_factory).GetLastId() + 1
                : new CLRepository(_factory).GetLastId() + 1;
        }

        private static void ValidateInput(DocumentInput input)
        {
            if (input.SectorId <= 0)
                throw new InvalidOperationException(L("Selecione o setor.", "Select the sector."));
            if (input.CategoryId <= 0)
                throw new InvalidOperationException(L("Selecione a categoria.", "Select the category."));
            if (input.PriorityId <= 0)
                throw new InvalidOperationException(L("Selecione a prioridade.", "Select the priority."));
            if (string.IsNullOrWhiteSpace(input.Title))
                throw new InvalidOperationException(L("Digite o titulo.", "Enter the title."));
            if (string.IsNullOrWhiteSpace(input.FileName))
                throw new InvalidOperationException(L("Nome do arquivo invalido.", "Invalid file name."));
        }

        private void PostJson(object payload)
        {
            var json = JsonSerializer.Serialize(payload);
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() =>
                {
                    _webView.CoreWebView2?.PostWebMessageAsJson(json);
                }));
                return;
            }

            _webView.CoreWebView2?.PostWebMessageAsJson(json);
        }

        private static int ReadInt(JsonElement root, string propertyName)
        {
            return root.TryGetProperty(propertyName, out var property) && property.TryGetInt32(out var value)
                ? value
                : 0;
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

        private sealed record DocumentInput(int SectorId, int CategoryId, int PriorityId, string Title, string FileName);
    }
}
