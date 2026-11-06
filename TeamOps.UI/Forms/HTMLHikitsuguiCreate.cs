using Dapper;
using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using TeamOps.Core.Entities;
using TeamOps.Config;
using TeamOps.Data.Db;
using TeamOps.Data.Security;
using TeamOps.UI.Forms.Models;

namespace TeamOps.UI.Forms
{
    public partial class HTMLHikitsuguiCreate : Form
    {
        private readonly SqliteConnectionFactory _factory;
        private readonly User _currentUser;
        private readonly Operator _currentOperator;
        private readonly HikitsuguiAudienceService _audienceService;

        public HTMLHikitsuguiCreate(
            SqliteConnectionFactory factory,
            User currentUser,
            Operator currentOperator)
        {
            InitializeComponent();

            _factory = factory;
            _currentUser = currentUser;
            _currentOperator = currentOperator;
            _audienceService = new HikitsuguiAudienceService(factory);

            InitializeWebView();
        }

        private async void InitializeWebView()
        {
            await webViewCreate.EnsureCoreWebView2Async(null);

            var core = webViewCreate.CoreWebView2;

            core.Settings.IsWebMessageEnabled = true;
            core.Settings.AreDefaultScriptDialogsEnabled = true;
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.Settings.AreDevToolsEnabled = true;

            core.WebMessageReceived += WebMessageReceived;

            core.SetVirtualHostNameToFolderMapping(
                "app",
                Path.Combine(Application.StartupPath, "ui", "hikitsugui-create"),
                CoreWebView2HostResourceAccessKind.Allow
            );

            core.Navigate("https://app/index.html");
        }

        private void WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            var msg = JsonSerializer.Deserialize<JsRequest>(e.WebMessageAsJson);
            if (msg == null) return;

            try
            {
                switch (msg.action)
                {
                    case "load":
                        LoadInitialData();
                        break;

                    case "save":
                        Console.WriteLine("QTD ANEXOS RECEBIDOS: " + (msg.attachments?.Count ?? 0));
                        SaveHikitsugui(msg);
                        break;

                    case "cancel":
                        Close();
                        break;
                }
            }
            catch (Exception ex)
            {
                webViewCreate.CoreWebView2.PostWebMessageAsJson(
                    JsonSerializer.Serialize(new { type = "error", message = ex.Message }));
            }
        }

        private void LoadInitialData()
        {
            using var conn = _factory.CreateOpenConnection();

            var shifts = conn.Query(
                @"SELECT
                      Id,
                      COALESCE(NamePt, '') AS NamePt,
                      COALESCE(NULLIF(NameJp, ''), NamePt, '') AS NameJp
                  FROM Shifts
                  ORDER BY Id;"
            );

            var categories = conn.Query(
                @"SELECT
                      Id,
                      COALESCE(NamePt, '') AS NamePt,
                      COALESCE(NULLIF(NameJp, ''), NamePt, '') AS NameJp
                  FROM Categories
                  ORDER BY NamePt;"
            );

            var equipments = conn.Query(
                @"SELECT
                      Id,
                      COALESCE(NamePt, '') AS NamePt,
                      COALESCE(NULLIF(NameJp, ''), NamePt, '') AS NameJp
                  FROM Equipments
                  ORDER BY NamePt;"
            );

            var authorizedSectorIds = Program.CurrentSession?.Access.AuthorizedSectorIds
                ?? throw new InvalidOperationException("Sessao de acesso nao inicializada.");

            var locals = conn.Query(
                @"SELECT
                      Id,
                      SectorId,
                      COALESCE(NamePt, '') AS NamePt,
                      COALESCE(NULLIF(NameJp, ''), NamePt, '') AS NameJp
                  FROM Locals
                  ORDER BY NamePt;"
            ).Where(item => authorizedSectorIds.Contains((int)item.SectorId));

            var sectors = conn.Query(
                @"SELECT
                      Id,
                      COALESCE(NamePt, '') AS NamePt,
                      COALESCE(NULLIF(NameJp, ''), NamePt, '') AS NameJp
                  FROM Sectors
                  WHERE IsActive = 1
                  ORDER BY NamePt;"
            ).Where(item => authorizedSectorIds.Contains((int)item.Id));

            var organizationNodes = conn.Query(
                @"SELECT
                      Id,
                      Code,
                      COALESCE(NamePt, Code) AS NamePt,
                      COALESCE(NULLIF(NameJp, ''), NamePt, Code) AS NameJp
                  FROM OrganizationNodes
                  WHERE NodeType = 'Unit' AND IsActive = 1
                  ORDER BY Code;"
            ).Where(item =>
            {
                var descendantSectorIds = conn.Query<int>(
                    @"WITH RECURSIVE descendants(Id) AS (
                          SELECT Id FROM OrganizationNodes WHERE Id = @nodeId AND IsActive = 1
                          UNION ALL
                          SELECT child.Id FROM OrganizationNodes child
                          JOIN descendants parent ON child.ParentId = parent.Id
                          WHERE child.IsActive = 1
                      )
                      SELECT sector.Id FROM Sectors sector
                      JOIN descendants ON descendants.Id = sector.OrganizationNodeId
                      WHERE sector.IsActive = 1;",
                    new { nodeId = (int)item.Id }).ToArray();

                return descendantSectorIds.Length > 0
                    && descendantSectorIds.All(authorizedSectorIds.Contains);
            }).ToArray();

            var activeSectorIds = conn.Query<int>("SELECT Id FROM Sectors WHERE IsActive = 1;").ToArray();
            var canTargetGeneral = activeSectorIds.Length > 0
                && activeSectorIds.All(authorizedSectorIds.Contains);

            var json = JsonSerializer.Serialize(new
            {
                type = "filters",
                locale = Program.CurrentLocale,
                shifts,
                categories,
                equipments,
                locals,
                sectors,
                organizationNodes,
                canTargetGeneral,
                shiftId = _currentOperator.ShiftId,
                creatorCodigoFJ = _currentOperator.CodigoFJ,
                creatorNamePt = _currentOperator.NameRomanji,
                creatorNameJp = string.IsNullOrWhiteSpace(_currentOperator.NameNihongo)
                    ? _currentOperator.NameRomanji
                    : _currentOperator.NameNihongo,
                now = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
            });

            webViewCreate.CoreWebView2.PostWebMessageAsJson(json);
        }

        private void SaveHikitsugui(JsRequest msg)
        {
            using var conn = _factory.CreateOpenConnection();

            if (msg.sectorId <= 0)
                throw new InvalidOperationException("Selecione um setor.");

            // 1) INSERT PRINCIPAL
            var sqlInsert = File.ReadAllText(
                Path.Combine(Application.StartupPath, "Sql", "Hikitsugui", "insert_hikitsugui.sql")
            );

            int newId = conn.ExecuteScalar<int>(sqlInsert, new
            {
                date = DateTime.Now,
                shiftId = msg.shiftId,
                creator = _currentOperator.CodigoFJ,
                categoryId = msg.reasonId,
                equipmentId = msg.equipId == 0 ? (int?)null : msg.equipId,
                localId = msg.localId == 0 ? (int?)null : msg.localId,
                sectorId = msg.sectorId == 0 ? (int?)null : msg.sectorId,
                description = msg.text
            });

            try
            {
                switch ((msg.audienceType ?? string.Empty).Trim().ToLowerInvariant())
                {
                    case "organization":
                        if (msg.organizationNodeId <= 0)
                            throw new InvalidOperationException("Selecione uma unidade organizacional.");
                        _audienceService.SetOrganizationAudience(_currentUser.Id, newId, msg.organizationNodeId);
                        break;

                    case "general":
                        _audienceService.SetGeneralAudience(_currentUser.Id, newId);
                        break;

                    case "sector":
                    case "":
                        var targetSectorIds = msg.sectorIds?.Count > 0
                            ? msg.sectorIds
                            : [msg.sectorId];
                        _audienceService.SetSectorAudience(_currentUser.Id, newId, targetSectorIds);
                        break;

                    default:
                        throw new InvalidOperationException("Tipo de audiencia invalido.");
                }
            }
            catch
            {
                conn.Execute("DELETE FROM Hikitsugui WHERE Id = @id;", new { id = newId });
                throw;
            }

            // 2) SALVAR ANEXOS
            if (msg.attachments != null)
            {
                var sqlAttach = File.ReadAllText(
                    Path.Combine(Application.StartupPath, "Sql", "Hikitsugui", "insert_hikitsugui_attachment.sql")
                );

                string baseFolder = Path.Combine(
                    GetAttachmentRoot(),
                    newId.ToString()
                );

                Directory.CreateDirectory(baseFolder);

                foreach (var file in msg.attachments)
                {
                    string fileName = file.fileName;
                    string fullPath = Path.Combine(baseFolder, fileName);

                    File.WriteAllBytes(fullPath, Convert.FromBase64String(file.base64));

                    conn.Execute(sqlAttach, new
                    {
                        hikitsuguiId = newId,
                        fileName,
                        filePath = fullPath
                    });
                }
            }

            // 3) RETORNO PARA O JS
            webViewCreate.CoreWebView2.PostWebMessageAsJson(
                JsonSerializer.Serialize(new { type = "saved", id = newId })
            );
        }

        private static string GetAttachmentRoot()
        {
            return StoragePathResolver.GetRequiredPath("HikitsuguiAttachmentPath");
        }
    }
}
