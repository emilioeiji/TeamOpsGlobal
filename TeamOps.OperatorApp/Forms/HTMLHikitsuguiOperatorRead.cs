using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using TeamOps.Data.Repositories;
using TeamOps.Data.Security;

namespace TeamOps.OperatorApp.Forms
{
    public partial class HTMLHikitsuguiOperatorRead : Form
    {
        private readonly OperatorRepository _operatorRepo;
        private readonly LocalRepository _localRepo;
        private readonly HikitsuguiRepository _hikRepo;
        private readonly HikitsuguiReadRepository _readRepo;
        private readonly OperatorPresenceRepository _presenceRepo;
        private readonly HikitsuguiAttachmentRepository _attachRepo;
        private readonly HikitsuguiAudienceService _audienceService;
        private int? _authorizedPreviewId;

        public HTMLHikitsuguiOperatorRead()
        {
            InitializeComponent();
            ApplyBranding();

            _operatorRepo = new OperatorRepository(Program.ConnectionFactory);
            _localRepo = new LocalRepository(Program.ConnectionFactory);
            _hikRepo = new HikitsuguiRepository(Program.ConnectionFactory);
            _readRepo = new HikitsuguiReadRepository(Program.ConnectionFactory);
            _presenceRepo = new OperatorPresenceRepository(Program.ConnectionFactory);
            _attachRepo = new HikitsuguiAttachmentRepository(Program.ConnectionFactory);
            _audienceService = new HikitsuguiAudienceService(Program.ConnectionFactory);

            InitializeWebView();
        }

        private async void InitializeWebView()
        {
            await webView.EnsureCoreWebView2Async(null);

            var core = webView.CoreWebView2;

            core.Settings.IsWebMessageEnabled = true;
            core.Settings.AreDefaultScriptDialogsEnabled = true;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDevToolsEnabled = false;

            core.WebMessageReceived += WebMessageReceived;

            core.SetVirtualHostNameToFolderMapping(
                "assets",
                Path.Combine(Application.StartupPath, "Assets"),
                CoreWebView2HostResourceAccessKind.Allow
            );

            core.SetVirtualHostNameToFolderMapping(
                "app",
                Path.Combine(Application.StartupPath, "ui", "hikitsugui-operator-read"),
                CoreWebView2HostResourceAccessKind.Allow
            );

            core.Navigate("https://app/index.html");
        }

        private void ApplyBranding()
        {
            string iconPath = Path.Combine(Application.StartupPath, "Assets", "HikitsuguiOperatorRead.ico");
            if (File.Exists(iconPath))
            {
                Icon = new Icon(iconPath);
            }
        }

        private void WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            // AGORA FUNCIONA: JsonElement
            var msg = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(e.WebMessageAsJson);
            if (msg == null) return;

            string action = msg["action"].GetString() ?? string.Empty;

            switch (action)
            {
                // ============================================
                // LOAD OPERATOR
                // ============================================
                case "load_operator":
                    {
                        string fj = msg["fj"].GetString() ?? string.Empty;

                        Task.Run(() =>
                        {
                            var op = _operatorRepo.GetByCodigoFJ(fj);
                            var userId = GetActiveUserId(fj);
                            if (op == null || !userId.HasValue)
                            {
                                SendJson("operator_not_found", null);
                                return;
                            }

                            var locals = GetAllowedLocals(userId.Value);

                            SendJson("operator_loaded", new
                            {
                                op.CodigoFJ,
                                op.NameRomanji,
                                op.SectorId,
                                op.ShiftId,
                                locals
                            });
                        });
                        break;
                    }

                // ============================================
                // REGISTER PRESENCE
                // ============================================
                case "register_presence":
                    {
                        Task.Run(() =>
                        {
                            string fj = msg["fj"].GetString() ?? string.Empty;
                            int localId = msg["localId"].GetInt32();

                            var op = _operatorRepo.GetByCodigoFJ(fj);
                            var userId = GetActiveUserId(fj);
                            if (op == null || !userId.HasValue) return;

                            var local = ResolveAllowedLocal(userId.Value, localId);
                            if (local == null) return;

                            DateTime now = DateTime.Now;
                            DateTime dataRegistro = now;

                            if (op.ShiftId == 2)
                            {
                                if (now.TimeOfDay >= TimeSpan.Zero &&
                                    now.TimeOfDay <= new TimeSpan(8, 35, 0))
                                {
                                    dataRegistro = now.AddDays(-1);
                                }
                            }

                            _presenceRepo.RegisterPresence(
                                op.CodigoFJ,
                                local.SectorId,
                                localId,
                                op.ShiftId,
                                dataRegistro
                            );
                        });
                        break;
                    }

                // ============================================
                // FILTER
                // ============================================
                case "filter":
                    {
                        Task.Run(() =>
                        {
                            string fj = msg["fj"].GetString() ?? string.Empty;
                            var op = _operatorRepo.GetByCodigoFJ(fj);
                            var userId = GetActiveUserId(fj);
                            if (op == null || !userId.HasValue) return;

                            DateTime dtIni = DateTime.Parse(msg["dtInicial"].GetString() ?? string.Empty);
                            DateTime dtFim = DateTime.Parse(msg["dtFinal"].GetString() ?? string.Empty).AddDays(1);

                            int localId = msg["localId"].GetInt32();
                            var local = ResolveAllowedLocal(userId.Value, localId);
                            if (local == null) return;

                            var lista = _hikRepo.GetForOperator(dtIni, dtFim, userId.Value, localId);
                            ApplyReadStatus(lista, fj);

                            SendJson("hikitsugui_list", lista);
                        });
                        break;
                    }

                // ============================================
                // PREVIEW
                // ============================================
                case "preview":
                    {
                        Task.Run(() =>
                        {
                            int id = msg["id"].GetInt32();
                            string fj = msg.ContainsKey("fj") ? msg["fj"].GetString() ?? string.Empty : string.Empty;
                            var userId = GetActiveUserId(fj);
                            if (!userId.HasValue) return;

                            _audienceService.DemandCanView(userId.Value, id);

                            var h = _hikRepo.GetById(id);
                            if (h == null) return;

                            _authorizedPreviewId = id;

                            if (!string.IsNullOrEmpty(fj))
                            {
                                EnsureRead(id, fj);
                                SendJson("read_status", new { id, lido = true });
                            }

                            SendJson("hikitsugui_preview", h);

                            var anexos = _attachRepo.GetByHikitsugui(id);
                            SendJson("attachments", anexos);
                        });
                        break;
                    }

                // ============================================
                // OPEN ATTACHMENT
                // ============================================
                case "open_attachment":
                    {
                        string path = msg["path"].GetString() ?? string.Empty;
                        if (!_authorizedPreviewId.HasValue || !IsRegisteredAttachment(_authorizedPreviewId.Value, path))
                            return;

                        if (File.Exists(path))
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = path,
                                UseShellExecute = true
                            });
                        }
                        break;
                    }

                case "has_read":
                    {
                        Task.Run(() =>
                        {
                            int id = msg["id"].GetInt32();
                            string fj = msg["fj"].GetString() ?? string.Empty;
                            var userId = GetActiveUserId(fj);
                            if (!userId.HasValue) return;
                            _audienceService.DemandCanView(userId.Value, id);

                            bool lido = _readRepo.HasRead(id, fj);

                            SendJson("read_status", new { id, lido });
                        });
                        break;
                    }

                case "mark_read":
                    {
                        Task.Run(() =>
                        {
                            int id = msg["id"].GetInt32();
                            string fj = msg["fj"].GetString() ?? string.Empty;
                            var userId = GetActiveUserId(fj);
                            if (!userId.HasValue) return;
                            _audienceService.DemandCanView(userId.Value, id);

                            EnsureRead(id, fj);

                            SendJson("read_status", new { id, lido = true });
                        });
                        break;
                    }
            }
        }

        private void ApplyReadStatus(List<TeamOps.Core.Entities.HikitsuguiListItem> lista, string fj)
        {
            if (lista.Count == 0 || string.IsNullOrWhiteSpace(fj))
                return;

            using var conn = Program.ConnectionFactory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();

            var ids = lista.Select(x => x.Id).Distinct().ToList();
            var placeholders = new List<string>(ids.Count);

            cmd.CommandText = "SELECT HikitsuguiId FROM HikitsuguiReads WHERE ReaderCodigoFJ = @fj AND HikitsuguiId IN (";
            cmd.Parameters.AddWithValue("@fj", fj);

            for (int i = 0; i < ids.Count; i++)
            {
                var name = $"@id{i}";
                placeholders.Add(name);
                cmd.Parameters.AddWithValue(name, ids[i]);
            }

            cmd.CommandText += string.Join(", ", placeholders) + ")";

            var readIds = new HashSet<int>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                readIds.Add(reader.GetInt32(0));
            }

            foreach (var item in lista)
            {
                item.IsRead = readIds.Contains(item.Id);
            }
        }

        private void EnsureRead(int id, string fj)
        {
            _readRepo.MarkAsRead(id, fj);
        }

        private List<TeamOps.Core.Entities.Local> GetAllowedLocals(int userId)
        {
            var locals = _localRepo.GetAll();
            var authorizedSectorIds = new AuthorizationService(Program.ConnectionFactory)
                .GetContext(userId).AuthorizedSectorIds;

            return locals
                .Where(x => authorizedSectorIds.Contains(x.SectorId))
                .OrderBy(x => x.NamePt)
                .ToList();
        }

        private TeamOps.Core.Entities.Local? ResolveAllowedLocal(int userId, int localId)
        {
            return GetAllowedLocals(userId).FirstOrDefault(x => x.Id == localId);
        }

        private static int? GetActiveUserId(string? codigoFJ)
        {
            if (string.IsNullOrWhiteSpace(codigoFJ))
                return null;

            using var conn = Program.ConnectionFactory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT Id FROM Users
                WHERE CodigoFJ = @codigoFJ COLLATE NOCASE
                  AND IsActive = 1;";
            cmd.Parameters.AddWithValue("@codigoFJ", codigoFJ.Trim());
            var value = cmd.ExecuteScalar();
            return value is null or DBNull
                ? null
                : Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static bool IsRegisteredAttachment(int hikitsuguiId, string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            using var conn = Program.ConnectionFactory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT EXISTS (
                    SELECT 1 FROM HikitsuguiAttachments
                    WHERE HikitsuguiId = @id AND FilePath = @path
                );";
            cmd.Parameters.AddWithValue("@id", hikitsuguiId);
            cmd.Parameters.AddWithValue("@path", path);
            return (long)cmd.ExecuteScalar()! == 1;
        }

        private void SendJson(string type, object? data)
        {
            var json = JsonSerializer.Serialize(new { type, data });

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() =>
                {
                    if (webView.CoreWebView2 != null)
                        webView.CoreWebView2.PostWebMessageAsJson(json);
                }));

                return;
            }

            if (webView.CoreWebView2 != null)
                webView.CoreWebView2.PostWebMessageAsJson(json);
        }
    }
}
