using Dapper;
using Microsoft.Web.WebView2.Core;
using System;
using System.Configuration;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using TeamOps.Core.Common;
using TeamOps.Core.Entities;
using TeamOps.Data.Db;
using TeamOps.Data.Repositories;
using TeamOps.Data.Security;
using TeamOps.UI.Forms.Models;

namespace TeamOps.UI.Forms
{
    public partial class HTMLHikitsuguiLeaderRead : Form
    {
        private readonly SqliteConnectionFactory _factory;
        private readonly User _currentUser;          // ← TEM ACCESS LEVEL
        private readonly Operator _currentLeader;    // ← TEM CODIGOFJ
        private readonly HikitsuguiAudienceService _audienceService;
        private int? _authorizedPreviewId;

        public HTMLHikitsuguiLeaderRead(
            SqliteConnectionFactory factory,
            User currentUser,
            Operator currentLeader)
        {
            InitializeComponent();

            _factory = factory;
            _currentUser = currentUser;
            _currentLeader = currentLeader;
            _audienceService = new HikitsuguiAudienceService(factory);

            InitializeWebView();
        }

        private async void InitializeWebView()
        {
            await webViewHikitsugui.EnsureCoreWebView2Async(null);

            var core = webViewHikitsugui.CoreWebView2;

            core.Settings.AreHostObjectsAllowed = true;
            core.Settings.IsWebMessageEnabled = true;
            core.Settings.AreDefaultScriptDialogsEnabled = true;
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.Settings.AreDevToolsEnabled = true;

            core.WebMessageReceived += WebMessageReceived;

            var htmlPath = Path.Combine(
                Application.StartupPath,
                "ui", "hikitsugui-leader-read", "index.html");

            core.SetVirtualHostNameToFolderMapping(
                "app",
                Path.Combine(Application.StartupPath, "ui", "hikitsugui-leader-read"),
                CoreWebView2HostResourceAccessKind.Allow
            );

            core.Navigate("https://app/index.html");

        }

        private void WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            Console.WriteLine("WEBVIEW MSG: " + e.WebMessageAsJson);

            var msg = JsonSerializer.Deserialize<JsRequest>(e.WebMessageAsJson);

            if (msg == null)
            {
                Console.WriteLine("ERRO: msg veio null");
                return;
            }

            Console.WriteLine("ACTION RECEBIDA: " + msg.action);

            try
            {
            switch (msg.action)
            {
                case "load":
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

                        var authorizedSectorIds = Program.CurrentSession?.Access.AuthorizedSectorIds
                            ?? throw new InvalidOperationException("Sessao de acesso nao inicializada.");

                        var operators = conn.Query(
                            @"SELECT
                                  CodigoFJ,
                                  SectorId,
                                  COALESCE(NameRomanji, CodigoFJ) AS NamePt,
                                  COALESCE(NULLIF(NameNihongo, ''), NameRomanji, CodigoFJ) AS NameJp
                              FROM Operators
                              ORDER BY NameRomanji;"
                        ).Where(item => authorizedSectorIds.Contains((int)item.SectorId));

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

                        var dtInicial = DateTime.Today.AddDays(-31).ToString("yyyy-MM-dd");
                        var dtFinal = DateTime.Today.AddDays(1).ToString("yyyy-MM-dd");

                        var jsonFilters = JsonSerializer.Serialize(new
                        {
                            type = "filters",
                            locale = Program.CurrentLocale,
                            shifts,
                            operators,
                            categories,
                            equipments,
                            locals,
                            sectors,
                            dtInicial,
                            dtFinal,
                            accessLevel = _currentUser.AccessLevel
                        });

                        webViewHikitsugui.CoreWebView2.PostWebMessageAsJson(jsonFilters);

                        SendLeaderRows(dtInicial, dtFinal, "todos", 0, 0, 0, 0, 0, "");

                        break;
                    }

                case "filter":
                    {
                        SendLeaderRows(
                            (string)msg.dtInicial,
                            (string)msg.dtFinal,
                            (string)msg.publico,
                            Convert.ToInt32(msg.shiftId),
                            Convert.ToInt32(msg.operatorId),
                            Convert.ToInt32(msg.reasonId),
                            Convert.ToInt32(msg.equipId),
                            Convert.ToInt32(msg.sectorId),
                            (string)msg.search
                        );
                        break;
                    }

                case "preview":
                    {
                        SendPreview(Convert.ToInt32(msg.id));
                        break;
                    }

                case "preview_and_mark_read":
                    {
                        using var conn = _factory.CreateOpenConnection();

                        var id = Convert.ToInt32(msg.id);
                        _audienceService.DemandCanView(_currentUser.Id, id);
                        EnsureRead(conn, id, _currentLeader.CodigoFJ);

                        SendPreview(id);
                        SendLeaderRows(
                            (string)msg.dtInicial,
                            (string)msg.dtFinal,
                            (string)msg.publico,
                            Convert.ToInt32(msg.shiftId),
                            Convert.ToInt32(msg.operatorId),
                            Convert.ToInt32(msg.reasonId),
                            Convert.ToInt32(msg.equipId),
                            Convert.ToInt32(msg.sectorId),
                            (string)msg.search
                        );
                        break;
                    }

                case "mark_read":
                    {
                        using var conn = _factory.CreateOpenConnection();

                        var id = Convert.ToInt32(msg.id);
                        _audienceService.DemandCanView(_currentUser.Id, id);
                        EnsureRead(conn, id, _currentLeader.CodigoFJ);

                        SendLeaderRows(
                            (string)msg.dtInicial,
                            (string)msg.dtFinal,
                            (string)msg.publico,
                            Convert.ToInt32(msg.shiftId),
                            Convert.ToInt32(msg.operatorId),
                            Convert.ToInt32(msg.reasonId),
                            Convert.ToInt32(msg.equipId),
                            Convert.ToInt32(msg.sectorId),
                            (string)msg.search
                        );
                        break;
                    }

                case "load_for_edit":
                    {
                        using var conn = _factory.CreateOpenConnection();

                        _audienceService.DemandCanEdit(_currentUser.Id, msg.id);

                        var row = conn.QueryFirstOrDefault(
                            "SELECT * FROM Hikitsugui WHERE Id = @id",
                            new { id = msg.id });

                        var attachments = conn.Query(
                            "SELECT FileName, FilePath FROM HikitsuguiAttachments WHERE HikitsuguiId = @id",
                            new { id = msg.id });

                        var json = JsonSerializer.Serialize(new
                        {
                            type = "hikitsugui_edit",
                            data = new[] { row },
                            attachments
                        });

                        webViewHikitsugui.CoreWebView2.PostWebMessageAsJson(json);
                        break;
                    }

                case "select_replies":
                    {
                        SendReplies(Convert.ToInt32(msg.id));
                        break;
                    }

                case "reply":
                    {
                        _audienceService.DemandCanEdit(_currentUser.Id, msg.id);
                        if (string.IsNullOrWhiteSpace(msg.text))
                        {
                            webViewHikitsugui.CoreWebView2.PostWebMessageAsJson(
                                JsonSerializer.Serialize(new
                                {
                                    type = "error",
                                    message = L("Digite uma resposta antes de salvar.", "保存する前に返信を入力してください。")
                                })
                            );
                            break;
                        }

                        ExecuteSql("insert_reply.sql", new
                        {
                            id = Convert.ToInt32(msg.id),
                            codigoFJ = _currentLeader.CodigoFJ,
                            text = (string)msg.text
                        });

                        SendReplies(Convert.ToInt32(msg.id));
                        break;
                    }

                case "select_attachments":
                    {
                        SendAttachments(Convert.ToInt32(msg.id));
                        break;
                    }

                case "open_attachment":
                    {
                        if (string.IsNullOrWhiteSpace(msg.path))
                        {
                            webViewHikitsugui.CoreWebView2.PostWebMessageAsJson(
                                JsonSerializer.Serialize(new
                                {
                                    type = "error",
                                    message = L("Caminho do anexo inválido.", "添付ファイルのパスが無効です。")
                                })
                            );
                            break;
                        }

                        try
                        {
                            if (!_authorizedPreviewId.HasValue)
                                throw new UnauthorizedAccessException(L("Abra a visualizacao da mensagem antes do anexo.", "添付ファイルを開く前にメッセージを表示してください。"));

                            _audienceService.DemandCanView(_currentUser.Id, _authorizedPreviewId.Value);
                            using (var conn = _factory.CreateOpenConnection())
                            {
                                var registered = conn.ExecuteScalar<long>(
                                    @"SELECT EXISTS (
                                          SELECT 1 FROM HikitsuguiAttachments
                                          WHERE HikitsuguiId = @id AND FilePath = @path
                                      );",
                                    new { id = _authorizedPreviewId.Value, path = msg.path }) == 1;
                                if (!registered)
                                    throw new UnauthorizedAccessException(L("Anexo nao pertence a esta mensagem.", "添付ファイルはこのメッセージに属していません。"));
                            }

                            var ext = Path.GetExtension(msg.path).ToLowerInvariant();
                            var blockedExtensions = new[] { ".dll", ".exe", ".bin", ".sys" };

                            if (Array.Exists(blockedExtensions, x => x == ext))
                            {
                                webViewHikitsugui.CoreWebView2.PostWebMessageAsJson(
                                    JsonSerializer.Serialize(new
                                    {
                                        type = "error",
                                        message = L("Este tipo de arquivo não pode ser aberto diretamente.", "この種類のファイルは直接開けません。")
                                    })
                                );
                                break;
                            }

                            if (!File.Exists(msg.path))
                            {
                                webViewHikitsugui.CoreWebView2.PostWebMessageAsJson(
                                    JsonSerializer.Serialize(new
                                    {
                                        type = "error",
                                        message = L("Arquivo não encontrado.", "ファイルが見つかりません。")
                                    })
                                );
                                break;
                            }

                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = msg.path,
                                UseShellExecute = true
                            });
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("OPEN_ATTACHMENT ERROR: " + ex);

                            webViewHikitsugui.CoreWebView2.PostWebMessageAsJson(
                                JsonSerializer.Serialize(new
                                {
                                    type = "error",
                                    message = ex.Message
                                })
                            );
                        }

                        break;
                    }

                case "delete_hikitsugui":
                    {
                        _audienceService.DemandCanDelete(_currentUser.Id, msg.id);
                        using var conn = _factory.CreateOpenConnection();
                        using var tran = conn.BeginTransaction();

                        try
                        {
                            conn.Execute(
                                "DELETE FROM HikitsuguiAttachments WHERE HikitsuguiId = @id",
                                new { id = msg.id },
                                tran
                            );

                            conn.Execute(
                                "DELETE FROM HikitsuguiReads WHERE HikitsuguiId = @id",
                                new { id = msg.id },
                                tran
                            );

                            conn.Execute(
                                "DELETE FROM HikitsuguiCorrections WHERE HikitsuguiId = @id",
                                new { id = msg.id },
                                tran
                            );

                            conn.Execute(
                                "DELETE FROM HikitsuguiResponses WHERE HikitsuguiId = @id",
                                new { id = msg.id },
                                tran
                            );

                            conn.Execute(
                                "DELETE FROM Hikitsugui WHERE Id = @id",
                                new { id = msg.id },
                                tran
                            );

                            tran.Commit();

                            try
                            {
                                var logRepo = new SystemLogRepository(_factory);
                                logRepo.Log(
                                    _currentLeader.CodigoFJ,
                                    "Hikitsugui",
                                    "Excluiu",
                                    msg.id
                                );
                            }
                            catch (Exception logEx)
                            {
                                Console.WriteLine("DELETE_HIKITSUGUI LOG ERROR: " + logEx);
                            }

                            SendLeaderRows(
                                (string)msg.dtInicial,
                                (string)msg.dtFinal,
                                (string)msg.publico,
                                Convert.ToInt32(msg.shiftId),
                                Convert.ToInt32(msg.operatorId),
                                Convert.ToInt32(msg.reasonId),
                                Convert.ToInt32(msg.equipId),
                                Convert.ToInt32(msg.sectorId),
                                (string)msg.search
                            );

                            break;
                        }
                        catch (Exception ex)
                        {
                            try
                            {
                                tran.Rollback();
                            }
                            catch (Exception rollbackEx)
                            {
                                Console.WriteLine("DELETE_HIKITSUGUI ROLLBACK ERROR: " + rollbackEx);
                            }

                            Console.WriteLine("DELETE_HIKITSUGUI ERROR: " + ex);

                            webViewHikitsugui.CoreWebView2.PostWebMessageAsJson(
                                JsonSerializer.Serialize(new
                                {
                                    type = "error",
                                    message = ex.Message
                                })
                            );

                            break;
                        }
                    }

                case "save_edit":
                    {
                        _audienceService.DemandCanEdit(_currentUser.Id, msg.id);
                        _audienceService.DemandCanCreateForSectors(_currentUser.Id, [msg.sectorId]);
                        using var conn = _factory.CreateOpenConnection();
                        using var tran = conn.BeginTransaction();

                        try
                        {
                            var sqlUpdate = File.ReadAllText(
                                Path.Combine(Application.StartupPath, "Sql", "Hikitsugui", "update_hikitsugui.sql")
                            );

                            var sqlInsertAttachment = File.ReadAllText(
                                Path.Combine(Application.StartupPath, "Sql", "Hikitsugui", "insert_hikitsugui_attachment.sql")
                            );

                            int id = Convert.ToInt32(msg.id);

                            int? equipmentId = msg.equipmentId == 0
                                ? null
                                : msg.equipmentId;

                            int? localId = msg.localId == 0
                                ? null
                                : msg.localId;

                            int? sectorId = msg.sectorId == 0
                                ? null
                                : msg.sectorId;

                            conn.Execute(sqlUpdate, new
                            {
                                id,
                                categoryId = Convert.ToInt32(msg.categoryId),
                                equipmentId,
                                localId,
                                sectorId,
                                description = (string)msg.description
                            }, tran);

                            conn.Execute(
                                "DELETE FROM HikitsuguiAttachments WHERE HikitsuguiId = @id",
                                new { id },
                                tran
                            );

                            if (msg.existingAttachments != null)
                            {
                                foreach (var item in msg.existingAttachments)
                                {
                                    conn.Execute(sqlInsertAttachment, new
                                    {
                                        hikitsuguiId = id,
                                        fileName = item.FileName,
                                        filePath = item.FilePath
                                    }, tran);
                                }
                            }

                            if (msg.newAttachments != null)
                            {
                                foreach (var item in msg.newAttachments)
                                {
                                    var savedPath = SaveAttachment(id, item.fileName, item.base64);

                                    conn.Execute(sqlInsertAttachment, new
                                    {
                                        hikitsuguiId = id,
                                        fileName = item.fileName,
                                        filePath = savedPath
                                    }, tran);
                                }
                            }

                            tran.Commit();
                            _audienceService.SetSectorAudience(_currentUser.Id, id, [msg.sectorId]);

                            try
                            {
                                var logRepo = new SystemLogRepository(_factory);
                                logRepo.Log(
                                    _currentLeader.CodigoFJ,
                                    "Hikitsugui",
                                    "Editou",
                                    id,
                                    $"Categoria={msg.categoryId}"
                                );
                            }
                            catch (Exception logEx)
                            {
                                Console.WriteLine("SAVE_EDIT LOG ERROR: " + logEx);
                            }

                            SendLeaderRows(
                                (string)msg.dtInicial,
                                (string)msg.dtFinal,
                                (string)msg.publico,
                                Convert.ToInt32(msg.shiftId),
                                Convert.ToInt32(msg.operatorId),
                                Convert.ToInt32(msg.reasonId),
                                Convert.ToInt32(msg.equipId),
                                Convert.ToInt32(msg.sectorIdFilter),
                                (string)msg.search
                            );

                            break; // 👈 ESSENCIAL
                        }
                        catch (Exception ex)
                        {
                            try
                            {
                                tran.Rollback();
                            }
                            catch (Exception rollbackEx)
                            {
                                Console.WriteLine("ROLLBACK ERROR: " + rollbackEx);
                            }

                            Console.WriteLine("SAVE_EDIT ERROR: " + ex);

                            webViewHikitsugui.CoreWebView2.PostWebMessageAsJson(
                                JsonSerializer.Serialize(new
                                {
                                    type = "error",
                                    message = ex.Message
                                })
                            );

                            break;
                        }
                    }
            }
            }
            catch (Exception ex)
            {
                webViewHikitsugui.CoreWebView2.PostWebMessageAsJson(
                    JsonSerializer.Serialize(new { type = "error", message = ex.Message }));
            }
        }

        // ============================================================
        // QUERIES BILÍNGUES
        // ============================================================
        private void SendLeaderRows(
            string dtInicial,
            string dtFinal,
            string publico,
            int shiftId,
            int operatorId,
            int reasonId,
            int equipId,
            int sectorId,
            string search)
        {
            const string sql = @"
                SELECT DISTINCT
                    h.Id,
                    h.Date,
                    COALESCE(o.NameRomanji, h.CreatorCodigoFJ) AS OperatorNamePt,
                    COALESCE(NULLIF(o.NameNihongo, ''), o.NameRomanji, h.CreatorCodigoFJ) AS OperatorNameJp,
                    COALESCE(c.NamePt, '') AS CategoryPt,
                    COALESCE(NULLIF(c.NameJp, ''), c.NamePt, '') AS CategoryJp,
                    COALESCE(e.NamePt, '') AS EquipmentPt,
                    COALESCE(NULLIF(e.NameJp, ''), e.NamePt, '') AS EquipmentJp,
                    COALESCE(l.NamePt, '') AS LocalPt,
                    COALESCE(NULLIF(l.NameJp, ''), l.NamePt, '') AS LocalJp,
                    COALESCE(s.NamePt, '') AS SectorPt,
                    COALESCE(NULLIF(s.NameJp, ''), s.NamePt, '') AS SectorJp,
                    (
                        SELECT COUNT(1)
                        FROM HikitsuguiResponses hr
                        WHERE hr.HikitsuguiId = h.Id
                    ) AS ReplyCount,
                    h.Description,
                    h.AttachmentPath,
                    CASE WHEN r.Id IS NULL THEN 0 ELSE 1 END AS IsRead
                FROM Hikitsugui h
                LEFT JOIN Operators o ON o.CodigoFJ = h.CreatorCodigoFJ
                LEFT JOIN Categories c ON c.Id = h.CategoryId
                LEFT JOIN Equipments e ON e.Id = h.EquipmentId
                LEFT JOIN Locals l ON l.Id = h.LocalId
                LEFT JOIN Sectors s ON s.Id = h.SectorId
                LEFT JOIN HikitsuguiReads r
                    ON r.HikitsuguiId = h.Id
                    AND r.ReaderCodigoFJ = @codigoFJ
                WHERE 1=1
                  AND h.Date BETWEEN @dtInicial AND @dtFinal
                  AND (
                        (@publico = 'operador' AND h.ForOperators = 1)
                        OR (@publico = 'lider' AND h.ForLeaders = 1)
                        OR (@publico = 'masv' AND h.ForMaSv = 1)
                        OR (
                            @publico = 'todos'
                            AND (
                                (@accessLevel = 1 AND h.ForOperators = 1)
                                OR (@accessLevel = 2 AND (h.ForOperators = 1 OR h.ForLeaders = 1))
                                OR (@accessLevel >= 3 AND (h.ForOperators = 1 OR h.ForLeaders = 1 OR h.ForMaSv = 1))
                            )
                        )
                    )
                  AND (@shiftId = 0 OR h.ShiftId = @shiftId)
                  AND (@operatorId = 0 OR h.CreatorCodigoFJ = @operatorId)
                  AND (@reasonId = 0 OR h.CategoryId = @reasonId)
                  AND (@equipId = 0 OR h.EquipmentId = @equipId)
                  AND (
                        @search = ''
                        OR h.Description LIKE '%' || @search || '%'
                        OR COALESCE(o.NameRomanji, '') LIKE '%' || @search || '%'
                        OR COALESCE(o.NameNihongo, '') LIKE '%' || @search || '%'
                        OR COALESCE(c.NamePt, '') LIKE '%' || @search || '%'
                        OR COALESCE(c.NameJp, '') LIKE '%' || @search || '%'
                        OR COALESCE(e.NamePt, '') LIKE '%' || @search || '%'
                        OR COALESCE(e.NameJp, '') LIKE '%' || @search || '%'
                        OR COALESCE(l.NamePt, '') LIKE '%' || @search || '%'
                        OR COALESCE(l.NameJp, '') LIKE '%' || @search || '%'
                        OR COALESCE(s.NamePt, '') LIKE '%' || @search || '%'
                        OR COALESCE(s.NameJp, '') LIKE '%' || @search || '%'
                    )
                ORDER BY h.Date DESC, h.Id DESC;";

            using var conn = _factory.CreateOpenConnection();
            var start = DateTime.TryParse(dtInicial, out var parsedStart) ? parsedStart.Date : DateTime.Today.AddDays(-31);
            var end = DateTime.TryParse(dtFinal, out var parsedEnd) ? parsedEnd.Date.AddDays(1) : DateTime.Today.AddDays(1);
            var visibleIds = _audienceService.GetVisibleMessageIds(_currentUser.Id, start, end);

            var rows = conn.Query(sql, new
            {
                dtInicial,
                dtFinal,
                publico,
                shiftId,
                operatorId,
                reasonId,
                equipId,
                sectorId,
                codigoFJ = _currentLeader.CodigoFJ,
                search,
                accessLevel = _currentUser.AccessLevel
            }).Where(row => visibleIds.Contains((int)row.Id))
              .Where(row => sectorId == 0 || _audienceService.IsTargetedToSector((int)row.Id, sectorId))
              .Select(row => new
            {
                row.Id,
                row.Date,
                row.OperatorNamePt,
                row.OperatorNameJp,
                row.CategoryPt,
                row.CategoryJp,
                row.EquipmentPt,
                row.EquipmentJp,
                row.LocalPt,
                row.LocalJp,
                row.SectorPt,
                row.SectorJp,
                row.ReplyCount,
                row.Description,
                DescriptionHtml = row.Description ?? string.Empty,
                row.AttachmentPath,
                row.IsRead
            }).ToList();

            webViewHikitsugui.CoreWebView2.PostWebMessageAsJson(
                JsonSerializer.Serialize(new
                {
                    type = "hikitsugui_for_leader",
                    data = rows
                })
            );
        }

        private void SendPreview(int id)
        {
            _audienceService.DemandCanView(_currentUser.Id, id);
            const string sql = @"
                SELECT
                    h.Id,
                    h.Date,
                    COALESCE(o.NameRomanji, h.CreatorCodigoFJ) AS OperatorNamePt,
                    COALESCE(NULLIF(o.NameNihongo, ''), o.NameRomanji, h.CreatorCodigoFJ) AS OperatorNameJp,
                    COALESCE(c.NamePt, '') AS CategoryPt,
                    COALESCE(NULLIF(c.NameJp, ''), c.NamePt, '') AS CategoryJp,
                    COALESCE(e.NamePt, '') AS EquipmentPt,
                    COALESCE(NULLIF(e.NameJp, ''), e.NamePt, '') AS EquipmentJp,
                    COALESCE(l.NamePt, '') AS LocalPt,
                    COALESCE(NULLIF(l.NameJp, ''), l.NamePt, '') AS LocalJp,
                    COALESCE(s.NamePt, '') AS SectorPt,
                    COALESCE(NULLIF(s.NameJp, ''), s.NamePt, '') AS SectorJp,
                    h.Description,
                    h.AttachmentPath
                FROM Hikitsugui h
                LEFT JOIN Operators o ON o.CodigoFJ = h.CreatorCodigoFJ
                LEFT JOIN Categories c ON c.Id = h.CategoryId
                LEFT JOIN Equipments e ON e.Id = h.EquipmentId
                LEFT JOIN Locals l ON l.Id = h.LocalId
                LEFT JOIN Sectors s ON s.Id = h.SectorId
                WHERE h.Id = @id;";

            try
            {
                using var conn = _factory.CreateOpenConnection();
                var row = conn.QueryFirstOrDefault(sql, new { id });
                if (row == null)
                {
                    webViewHikitsugui.CoreWebView2.PostWebMessageAsJson(
                        JsonSerializer.Serialize(new
                        {
                            type = "error",
                            message = L("Hikitsugui não encontrado ou indisponível.", "Hikitsugui が見つかりません。")
                        })
                    );
                    return;
                }

                _authorizedPreviewId = id;

                var description = Convert.ToString(row.Description) ?? string.Empty;
                Console.WriteLine($"[HikitsuguiLeaderRead][Preview] HikitsuguiId={id} DescriptionLength={description.Length} FieldNameUsed=Description DescriptionFromPayload={description}");

                webViewHikitsugui.CoreWebView2.PostWebMessageAsJson(
                    JsonSerializer.Serialize(new
                    {
                        type = "hikitsugui_by_id",
                        data = new[]
                        {
                            new
                            {
                                row.Id,
                                row.Date,
                                row.OperatorNamePt,
                                row.OperatorNameJp,
                                row.CategoryPt,
                                row.CategoryJp,
                                row.EquipmentPt,
                                row.EquipmentJp,
                                row.LocalPt,
                                row.LocalJp,
                                row.SectorPt,
                                row.SectorJp,
                                Description = description,
                                DescriptionHtml = description,
                                row.AttachmentPath
                            }
                        }
                    })
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine("HIKITSUGUI PREVIEW ERROR: " + ex);
                webViewHikitsugui.CoreWebView2.PostWebMessageAsJson(
                    JsonSerializer.Serialize(new
                    {
                        type = "error",
                        message = ex.Message
                    })
                );
                return;
            }
        }

        private void SendReplies(int id)
        {
            _audienceService.DemandCanView(_currentUser.Id, id);
            const string sql = @"
                SELECT
                    r.Id,
                    r.HikitsuguiId,
                    r.Message,
                    r.Date,
                    COALESCE(o.NameRomanji, r.ResponderCodigoFJ) AS ResponderNamePt,
                    COALESCE(NULLIF(o.NameNihongo, ''), o.NameRomanji, r.ResponderCodigoFJ) AS ResponderNameJp
                FROM HikitsuguiResponses r
                JOIN Operators o ON o.CodigoFJ = r.ResponderCodigoFJ
                WHERE r.HikitsuguiId = @id
                ORDER BY r.Date ASC;";

            using var conn = _factory.CreateOpenConnection();
            var rows = conn.Query(sql, new { id }).ToList();

            webViewHikitsugui.CoreWebView2.PostWebMessageAsJson(
                JsonSerializer.Serialize(new
                {
                    type = "replies",
                    data = rows
                })
            );
        }

        private void SendAttachments(int id)
        {
            _audienceService.DemandCanView(_currentUser.Id, id);
            const string sql = @"
                SELECT
                    Id,
                    HikitsuguiId,
                    FileName,
                    FilePath,
                    CreatedAt
                FROM HikitsuguiAttachments
                WHERE HikitsuguiId = @id
                ORDER BY Id;";

            using var conn = _factory.CreateOpenConnection();
            var rows = conn.Query(sql, new { id }).ToList();

            webViewHikitsugui.CoreWebView2.PostWebMessageAsJson(
                JsonSerializer.Serialize(new
                {
                    type = "attachments",
                    data = rows
                })
            );
        }

        private void ExecuteSql(string sqlFile, object param)
        {
            var sqlPath = Path.Combine(Application.StartupPath, "Sql", "Hikitsugui", sqlFile);
            var sql = File.ReadAllText(sqlPath);

            using var conn = _factory.CreateOpenConnection();
            conn.Execute(sql, param);
        }

        private void EnsureRead(System.Data.IDbConnection conn, int id, string codigoFJ)
        {
            var alreadyRead = conn.ExecuteScalar<int>(
                @"SELECT COUNT(1)
                  FROM HikitsuguiReads
                  WHERE HikitsuguiId = @id
                    AND ReaderCodigoFJ = @codigoFJ;",
                new { id, codigoFJ });

            if (alreadyRead == 0)
            {
                conn.Execute(
                    @"INSERT INTO HikitsuguiReads
                      (HikitsuguiId, ReaderCodigoFJ, ReadAt)
                      VALUES
                      (@id, @codigoFJ, CURRENT_TIMESTAMP);",
                    new { id, codigoFJ });
            }
        }

        private string SaveAttachment(int hikitsuguiId, string fileName, string base64)
        {
            var root = ConfigurationManager.AppSettings["HikitsuguiAttachmentPath"];
            if (string.IsNullOrWhiteSpace(root))
                throw new InvalidOperationException("HikitsuguiAttachmentPath não está configurado no app.config.");

            var dir = Path.Combine(root, hikitsuguiId.ToString());

            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var filePath = Path.Combine(dir, fileName);
            File.WriteAllBytes(filePath, Convert.FromBase64String(base64));

            return filePath;
        }

        private static string L(string pt, string jp)
        {
            return string.Equals(Program.CurrentLocale, "ja-JP", StringComparison.OrdinalIgnoreCase)
                ? jp
                : pt;
        }

    }
}
