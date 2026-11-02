using Dapper;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using TeamOps.Core.Common;
using TeamOps.Core.Entities;
using TeamOps.Data.Db;
using TeamOps.Data.Security;
using TeamOps.UI.Forms.Models;

namespace TeamOps.UI.Forms
{
    public partial class HTMLFormHikitsuguiReader : Form
    {
        private readonly SqliteConnectionFactory _factory;
        private readonly User _currentUser;
        private readonly Operator _currentOperator;
        private readonly HikitsuguiAudienceService _audienceService;
        private int? _authorizedPreviewId;

        public HTMLFormHikitsuguiReader(
            SqliteConnectionFactory factory,
            User currentUser,
            Operator currentOperator)
        {
            InitializeComponent();
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Text = L("Leitura de Hikitsugui", "\u5f15\u7d99\u304e\u8aad\u307f\u53d6\u308a");

            _factory = factory;
            _currentUser = currentUser;
            _currentOperator = currentOperator;
            _audienceService = new HikitsuguiAudienceService(factory);

            InitializeWebView();
        }

        private async void InitializeWebView()
        {
            await webViewReader.EnsureCoreWebView2Async(null);

            var core = webViewReader.CoreWebView2;

            core.Settings.IsWebMessageEnabled = true;
            core.Settings.AreDefaultScriptDialogsEnabled = true;
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.Settings.AreDevToolsEnabled = true;

            core.WebMessageReceived += WebMessageReceived;

            core.SetVirtualHostNameToFolderMapping(
                "app",
                Path.Combine(Application.StartupPath, "ui", "hikitsugui-reader"),
                CoreWebView2HostResourceAccessKind.Allow
            );

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

                case "filter":
                    SendMatrix(
                        ParseDateOrFallback(msg.dtInicial, DateTime.Today.AddMonths(-1)),
                        ParseDateOrFallback(msg.dtFinal, DateTime.Today),
                        NormalizeAudience(msg.publico),
                        msg.shiftId,
                        msg.sectorId
                    );
                    break;

                case "preview":
                    SendPreview(msg.id);
                    break;

                case "open_attachment":
                    OpenAttachment(msg.id, msg.path);
                    break;
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
            ).ToList();

            var authorizedSectorIds = Program.CurrentSession?.Access.AuthorizedSectorIds
                ?? throw new InvalidOperationException("Sessao de acesso nao inicializada.");

            var sectors = conn.Query(
                @"SELECT
                      Id,
                      COALESCE(NamePt, '') AS NamePt,
                      COALESCE(NULLIF(NameJp, ''), NamePt, '') AS NameJp
                  FROM Sectors
                  WHERE IsActive = 1
                  ORDER BY Id;"
            ).Where(item => authorizedSectorIds.Contains((int)item.Id)).ToList();

            var start = DateTime.Today.AddMonths(-1);
            var end = DateTime.Today;

            PostJson(new
            {
                type = "init",
                locale = Program.CurrentLocale,
                operatorNamePt = _currentOperator.NameRomanji,
                operatorNameJp = string.IsNullOrWhiteSpace(_currentOperator.NameNihongo)
                    ? _currentOperator.NameRomanji
                    : _currentOperator.NameNihongo,
                startDate = start.ToString("yyyy-MM-dd"),
                endDate = end.ToString("yyyy-MM-dd"),
                shifts,
                sectors
            });

            SendMatrix(start, end, "operators", 0, 0);
        }

        private void SendMatrix(DateTime start, DateTime end, string audience, int shiftId, int sectorId)
        {
            using var conn = _factory.CreateOpenConnection();

            var members = QueryMembers(conn, audience, shiftId, sectorId).ToList();
            var hikitsuguis = QueryHikitsuguis(conn, start, end, audience, sectorId).ToList();
            var reads = QueryReads(conn, start, end)
                .GroupBy(item => item.HikitsuguiId)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(item => item.ReaderCodigoFJ).ToHashSet(StringComparer.OrdinalIgnoreCase)
                );

            var rows = hikitsuguis.Select(item =>
            {
                reads.TryGetValue(item.Id, out var readSet);

                return new
                {
                    id = item.Id,
                    date = item.Date.ToString("yyyy-MM-dd"),
                    dateLabel = item.Date.ToString("yyyy/MM/dd"),
                    categoryPt = item.CategoryPt,
                    categoryJp = item.CategoryJp,
                    creatorNamePt = item.CreatorNamePt,
                    creatorNameJp = item.CreatorNameJp,
                    sectorPt = item.SectorPt,
                    sectorJp = item.SectorJp,
                    descriptionHtml = item.Description ?? string.Empty,
                    readers = readSet?.ToArray() ?? Array.Empty<string>()
                };
            }).ToList();

            PostJson(new
            {
                type = "matrix",
                data = new
                {
                    audience,
                    members,
                    rows
                }
            });
        }

        private IEnumerable<object> QueryMembers(System.Data.IDbConnection conn, string audience, int shiftId, int sectorId)
        {
            const string sql = @"
                SELECT
                    o.CodigoFJ,
                    COALESCE(o.NameRomanji, o.CodigoFJ) AS NamePt,
                    COALESCE(NULLIF(o.NameNihongo, ''), o.NameRomanji, o.CodigoFJ) AS NameJp,
                    o.ShiftId,
                    o.SectorId,
                    COALESCE(o.GroupId, 0) AS GroupId,
                    o.IsLeader,
                    COALESCE(u.AccessLevel, 0) AS AccessLevel
                FROM Operators o
                LEFT JOIN Users u ON u.CodigoFJ = o.CodigoFJ
                WHERE o.Status = 1
                ORDER BY NameRomanji;";

            var rows = conn.Query<MemberRow>(sql).ToList();
            var authorizedSectorIds = Program.CurrentSession?.Access.AuthorizedSectorIds
                ?? throw new InvalidOperationException("Sessao de acesso nao inicializada.");
            rows = rows.Where(row => row.SectorId.HasValue && authorizedSectorIds.Contains(row.SectorId.Value)).ToList();

            rows = audience switch
            {
                "leaders" => rows.Where(row => row.IsLeader).ToList(),
                "masv" => rows.Where(row => row.AccessLevel >= (int)AccessLevel.GL).ToList(),
                _ => rows.Where(row => !row.IsLeader).ToList()
            };

            if (shiftId > 0)
            {
                rows = rows.Where(row => row.ShiftId == shiftId).ToList();
            }

            if (sectorId > 0)
            {
                rows = rows.Where(row => row.SectorId == sectorId).ToList();
            }

            rows = rows
                .OrderBy(row => audience == "operators" ? row.SectorId : row.GroupId)
                .ThenBy(row => row.GroupId)
                .ThenBy(row => row.NamePt)
                .ToList();

            return rows.Select(row => new
            {
                codigoFJ = row.CodigoFJ,
                namePt = row.NamePt,
                nameJp = row.NameJp
            });
        }

        private IEnumerable<HikitsuguiRow> QueryHikitsuguis(System.Data.IDbConnection conn, DateTime start, DateTime end, string audience, int sectorId)
        {
            const string sql = @"
                SELECT
                    h.Id,
                    h.Date,
                    h.ShiftId,
                    COALESCE(h.SectorId, 0) AS SectorId,
                    h.ForLeaders,
                    h.ForOperators,
                    h.ForMaSv,
                    h.Description,
                    COALESCE(c.NamePt, '') AS CategoryPt,
                    COALESCE(NULLIF(c.NameJp, ''), c.NamePt, '') AS CategoryJp,
                    COALESCE(s.NamePt, '') AS SectorPt,
                    COALESCE(NULLIF(s.NameJp, ''), s.NamePt, '') AS SectorJp,
                    COALESCE(o.NameRomanji, h.CreatorCodigoFJ) AS CreatorNamePt,
                    COALESCE(NULLIF(o.NameNihongo, ''), o.NameRomanji, h.CreatorCodigoFJ) AS CreatorNameJp
                FROM Hikitsugui h
                LEFT JOIN Categories c ON c.Id = h.CategoryId
                LEFT JOIN Sectors s ON s.Id = h.SectorId
                LEFT JOIN Operators o ON o.CodigoFJ = h.CreatorCodigoFJ
                WHERE h.Date >= @start
                  AND h.Date < @end
                ORDER BY h.Date DESC, h.Id DESC;";

            var rows = conn.Query<HikitsuguiRow>(sql, new
            {
                start = start.Date,
                end = end.Date.AddDays(1)
            }).ToList();

            var visibleIds = _audienceService.GetVisibleMessageIds(
                _currentUser.Id,
                start.Date,
                end.Date.AddDays(1));

            rows = rows
                .Where(row => visibleIds.Contains(row.Id))
                .Where(row => MatchesAudience(row, audience))
                .Where(row => sectorId == 0 || _audienceService.IsTargetedToSector(row.Id, sectorId))
                .ToList();

            return rows;
        }

        private IEnumerable<HikitsuguiReadMini> QueryReads(System.Data.IDbConnection conn, DateTime start, DateTime end)
        {
            const string sql = @"
                SELECT
                    HikitsuguiId,
                    ReaderCodigoFJ
                FROM HikitsuguiReads
                WHERE ReadAt >= @start
                  AND ReadAt < @end;";

            return conn.Query<HikitsuguiReadMini>(sql, new
            {
                start = start.Date,
                end = end.Date.AddDays(1)
            });
        }

        private void SendPreview(int id)
        {
            _audienceService.DemandCanView(_currentUser.Id, id);
            using var conn = _factory.CreateOpenConnection();

            const string sql = @"
                SELECT
                    h.Id,
                    h.Date,
                    COALESCE(c.NamePt, '') AS CategoryPt,
                    COALESCE(NULLIF(c.NameJp, ''), c.NamePt, '') AS CategoryJp,
                    COALESCE(s.NamePt, '') AS SectorPt,
                    COALESCE(NULLIF(s.NameJp, ''), s.NamePt, '') AS SectorJp,
                    COALESCE(o.NameRomanji, h.CreatorCodigoFJ) AS CreatorNamePt,
                    COALESCE(NULLIF(o.NameNihongo, ''), o.NameRomanji, h.CreatorCodigoFJ) AS CreatorNameJp,
                    h.Description
                FROM Hikitsugui h
                LEFT JOIN Categories c ON c.Id = h.CategoryId
                LEFT JOIN Sectors s ON s.Id = h.SectorId
                LEFT JOIN Operators o ON o.CodigoFJ = h.CreatorCodigoFJ
                WHERE h.Id = @id;";

            var row = conn.QueryFirstOrDefault(sql, new { id });
            if (row == null)
                return;

            _authorizedPreviewId = id;

            var attachments = conn.Query(
                @"SELECT
                      FileName,
                      FilePath
                  FROM HikitsuguiAttachments
                  WHERE HikitsuguiId = @id
                  ORDER BY Id;",
                new { id }
            ).ToList();

            PostJson(new
            {
                type = "preview",
                data = new
                {
                    id = row.Id,
                    date = ((DateTime)row.Date).ToString("yyyy/MM/dd"),
                    categoryPt = row.CategoryPt,
                    categoryJp = row.CategoryJp,
                    sectorPt = row.SectorPt,
                    sectorJp = row.SectorJp,
                    creatorNamePt = row.CreatorNamePt,
                    creatorNameJp = row.CreatorNameJp,
                    descriptionHtml = row.Description ?? string.Empty,
                    attachments
                }
            });
        }

        private void OpenAttachment(int hikitsuguiId, string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                SendError(L("Caminho do anexo invalido.", "添付ファイルのパスが無効です。"));
                return;
            }

            try
            {
                if (_authorizedPreviewId != hikitsuguiId)
                    throw new UnauthorizedAccessException(L("Abra a visualizacao da mensagem antes do anexo.", "添付ファイルを開く前にメッセージを表示してください。"));

                _audienceService.DemandCanView(_currentUser.Id, hikitsuguiId);

                using var conn = _factory.CreateOpenConnection();
                var isRegistered = conn.ExecuteScalar<long>(
                    @"SELECT EXISTS (
                          SELECT 1 FROM HikitsuguiAttachments
                          WHERE HikitsuguiId = @hikitsuguiId AND FilePath = @path
                      );",
                    new { hikitsuguiId, path }) == 1;
                if (!isRegistered)
                    throw new UnauthorizedAccessException(L("Anexo nao pertence a esta mensagem.", "添付ファイルはこのメッセージに属していません。"));

                var ext = Path.GetExtension(path).ToLowerInvariant();
                var blockedExtensions = new[] { ".dll", ".exe", ".bin", ".sys" };

                if (blockedExtensions.Contains(ext))
                {
                    SendError(L("Este tipo de arquivo nao pode ser aberto diretamente.", "この種類のファイルは直接開けません。"));
                    return;
                }

                if (!File.Exists(path))
                {
                    SendError(L("Arquivo nao encontrado.", "ファイルが見つかりません。"));
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                SendError(ex.Message);
            }
        }

        private void SendError(string message)
        {
            PostJson(new
            {
                type = "error",
                message
            });
        }

        private void PostJson(object payload)
        {
            var json = JsonSerializer.Serialize(payload);

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() =>
                {
                    if (webViewReader.CoreWebView2 != null)
                        webViewReader.CoreWebView2.PostWebMessageAsJson(json);
                }));

                return;
            }

            if (webViewReader.CoreWebView2 != null)
                webViewReader.CoreWebView2.PostWebMessageAsJson(json);
        }

        private static DateTime ParseDateOrFallback(string? value, DateTime fallback)
        {
            return DateTime.TryParse(value, out var parsed)
                ? parsed.Date
                : fallback.Date;
        }

        private static string NormalizeAudience(string? value)
        {
            var normalized = (value ?? "operators").Trim().ToLowerInvariant();
            return normalized switch
            {
                "leaders" => "leaders",
                "masv" => "masv",
                _ => "operators"
            };
        }

        private static bool MatchesAudience(HikitsuguiRow row, string audience)
        {
            return audience switch
            {
                "leaders" => row.ForLeaders || row.ForOperators,
                "masv" => row.ForMaSv,
                _ => row.ForOperators
            };
        }

        private static string L(string pt, string jp)
        {
            return string.Equals(Program.CurrentLocale, "ja-JP", StringComparison.OrdinalIgnoreCase)
                ? jp
                : pt;
        }

        private sealed class MemberRow
        {
            public string CodigoFJ { get; set; } = string.Empty;
            public string NamePt { get; set; } = string.Empty;
            public string NameJp { get; set; } = string.Empty;
            public int ShiftId { get; set; }
            public int? SectorId { get; set; }
            public int GroupId { get; set; }
            public bool IsLeader { get; set; }
            public int AccessLevel { get; set; }
        }

        private sealed class HikitsuguiRow
        {
            public int Id { get; set; }
            public DateTime Date { get; set; }
            public int ShiftId { get; set; }
            public int? SectorId { get; set; }
            public bool ForLeaders { get; set; }
            public bool ForOperators { get; set; }
            public bool ForMaSv { get; set; }
            public string Description { get; set; } = string.Empty;
            public string CategoryPt { get; set; } = string.Empty;
            public string CategoryJp { get; set; } = string.Empty;
            public string SectorPt { get; set; } = string.Empty;
            public string SectorJp { get; set; } = string.Empty;
            public string CreatorNamePt { get; set; } = string.Empty;
            public string CreatorNameJp { get; set; } = string.Empty;
        }

        private sealed class HikitsuguiReadMini
        {
            public int HikitsuguiId { get; set; }
            public string ReaderCodigoFJ { get; set; } = string.Empty;
        }
    }
}
