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
        private readonly OperatorAccessService _operatorAccessService;
        private readonly object _sessionGate = new();
        private int? _authenticatedUserId;
        private string _authenticatedCodigoFJ = string.Empty;
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
            _operatorAccessService = new OperatorAccessService(Program.ConnectionFactory);

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

                        RunBackground(() =>
                        {
                            var op = _operatorRepo.GetByCodigoFJ(fj);
                            if (op == null || !op.Status)
                            {
                                ClearSession();
                                SendJson("operator_not_found", null);
                                return;
                            }

                            var assignedSectors = _operatorAccessService.GetSectors(op.CodigoFJ);
                            var assignedSectorIds = assignedSectors.Select(item => item.Id).ToArray();
                            var userId = GetActiveOperatorUserId(op.CodigoFJ)
                                ?? _operatorAccessService.ProvisionOrUpdate(
                                    op.CodigoFJ,
                                    op.NameRomanji,
                                    op.SectorId,
                                    assignedSectorIds.Length == 0 ? [op.SectorId] : assignedSectorIds,
                                    op.OperatorType,
                                    null,
                                    true);

                            SetSession(userId, op.CodigoFJ);
                            var sectors = GetAllowedSectors(userId, op.CodigoFJ);
                            var sectorNames = sectors.ToDictionary(item => item.Id);
                            var locals = GetAllowedLocals(userId, op.CodigoFJ)
                                .Select(local => new
                                {
                                    local.Id,
                                    local.NamePt,
                                    local.NameJp,
                                    local.SectorId,
                                    SectorNamePt = sectorNames.TryGetValue(local.SectorId, out var sector) ? sector.NamePt : string.Empty,
                                    SectorNameJp = sectorNames.TryGetValue(local.SectorId, out var sectorJp) ? sectorJp.NameJp : string.Empty
                                });

                            SendJson("operator_loaded", new
                            {
                                op.CodigoFJ,
                                op.NameRomanji,
                                op.SectorId,
                                op.ShiftId,
                                sectors,
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
                        RunBackground(() =>
                        {
                            var session = DemandAuthenticatedSession();
                            int localId = msg["localId"].GetInt32();

                            var op = _operatorRepo.GetByCodigoFJ(session.CodigoFJ);
                            if (op == null || !op.Status) throw new UnauthorizedAccessException("Sessao do operador invalida.");

                            var local = ResolveAllowedLocal(session.UserId, session.CodigoFJ, localId);
                            if (local == null) throw new UnauthorizedAccessException("Local fora do escopo autorizado.");

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
                        RunBackground(() =>
                        {
                            var session = DemandAuthenticatedSession();

                            DateTime dtIni = DateTime.Parse(msg["dtInicial"].GetString() ?? string.Empty);
                            DateTime dtFim = DateTime.Parse(msg["dtFinal"].GetString() ?? string.Empty).AddDays(1);

                            int localId = msg["localId"].GetInt32();
                            var local = ResolveAllowedLocal(session.UserId, session.CodigoFJ, localId);
                            if (local == null)
                                throw new UnauthorizedAccessException("Local fora do escopo autorizado.");

                            var lista = _hikRepo.GetForOperator(
                                dtIni, dtFim, session.UserId, session.CodigoFJ, local.SectorId, localId);
                            ApplyReadStatus(lista, session.CodigoFJ);

                            SendJson("hikitsugui_list", lista);
                        });
                        break;
                    }

                // ============================================
                // PREVIEW
                // ============================================
                case "preview":
                    {
                        RunBackground(() =>
                        {
                            int id = msg["id"].GetInt32();
                            var session = DemandAuthenticatedSession();

                            _audienceService.DemandCanViewForOperator(session.UserId, session.CodigoFJ, id);

                            var h = _hikRepo.GetById(id);
                            if (h == null) return;

                            _authorizedPreviewId = id;

                            EnsureRead(id, session.CodigoFJ);
                            SendJson("read_status", new { id, lido = true });

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
                        try
                        {
                            var session = DemandAuthenticatedSession();
                            string path = msg["path"].GetString() ?? string.Empty;
                            if (!_authorizedPreviewId.HasValue || !IsRegisteredAttachment(_authorizedPreviewId.Value, path))
                                return;
                            _audienceService.DemandCanViewForOperator(
                                session.UserId, session.CodigoFJ, _authorizedPreviewId.Value);

                            if (File.Exists(path))
                            {
                                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = path,
                                    UseShellExecute = true
                                });
                            }
                        }
                        catch (Exception ex) { SendJson("error", new { message = ex.Message }); }
                        break;
                    }

                case "logout":
                    ClearSession();
                    break;

                case "has_read":
                    {
                        RunBackground(() =>
                        {
                            int id = msg["id"].GetInt32();
                            var session = DemandAuthenticatedSession();
                            _audienceService.DemandCanViewForOperator(session.UserId, session.CodigoFJ, id);

                            bool lido = _readRepo.HasRead(id, session.CodigoFJ);

                            SendJson("read_status", new { id, lido });
                        });
                        break;
                    }

                case "mark_read":
                    {
                        RunBackground(() =>
                        {
                            int id = msg["id"].GetInt32();
                            var session = DemandAuthenticatedSession();
                            _audienceService.DemandCanViewForOperator(session.UserId, session.CodigoFJ, id);

                            EnsureRead(id, session.CodigoFJ);

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

        private List<TeamOps.Core.Entities.Local> GetAllowedLocals(int userId, string codigoFJ)
        {
            var locals = _localRepo.GetAll();
            var authorizedSectorIds = new AuthorizationService(Program.ConnectionFactory)
                .GetContext(userId).AuthorizedSectorIds;
            var operatorSectorIds = _operatorAccessService.GetSectors(codigoFJ).Select(item => item.Id).ToHashSet();

            return locals
                .Where(x => authorizedSectorIds.Contains(x.SectorId) && operatorSectorIds.Contains(x.SectorId))
                .OrderBy(x => x.NamePt)
                .ToList();
        }

        private TeamOps.Core.Entities.Local? ResolveAllowedLocal(int userId, string codigoFJ, int localId)
        {
            return GetAllowedLocals(userId, codigoFJ).FirstOrDefault(x => x.Id == localId);
        }

        private IReadOnlyList<OperatorSectorAccess> GetAllowedSectors(int userId, string codigoFJ)
        {
            var authorized = new AuthorizationService(Program.ConnectionFactory).GetContext(userId).AuthorizedSectorIds;
            return _operatorAccessService.GetSectors(codigoFJ).Where(item => authorized.Contains(item.Id)).ToArray();
        }

        private void SetSession(int userId, string codigoFJ)
        {
            lock (_sessionGate)
            {
                _authenticatedUserId = userId;
                _authenticatedCodigoFJ = codigoFJ;
                _authorizedPreviewId = null;
            }
        }

        private void ClearSession()
        {
            lock (_sessionGate)
            {
                _authenticatedUserId = null;
                _authenticatedCodigoFJ = string.Empty;
                _authorizedPreviewId = null;
            }
        }

        private (int UserId, string CodigoFJ) DemandAuthenticatedSession()
        {
            int? userId;
            string codigoFJ;
            lock (_sessionGate)
            {
                userId = _authenticatedUserId;
                codigoFJ = _authenticatedCodigoFJ;
            }
            if (!userId.HasValue || string.IsNullOrWhiteSpace(codigoFJ))
                throw new UnauthorizedAccessException("Autenticacao necessaria.");

            using var conn = Program.ConnectionFactory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT EXISTS(
                    SELECT 1 FROM Users u
                    JOIN Operators o ON o.CodigoFJ = u.CodigoFJ COLLATE NOCASE
                    WHERE u.Id = @userId AND u.IsActive = 1 AND o.Status = 1
                      AND o.CodigoFJ = @fj COLLATE NOCASE
                );";
            cmd.Parameters.AddWithValue("@userId", userId.Value);
            cmd.Parameters.AddWithValue("@fj", codigoFJ);
            if (Convert.ToInt64(cmd.ExecuteScalar()) != 1)
            {
                ClearSession();
                throw new UnauthorizedAccessException("Sessao expirada ou acesso revogado.");
            }
            return (userId.Value, codigoFJ);
        }

        private void RunBackground(Action action)
        {
            Task.Run(() =>
            {
                try { action(); }
                catch (Exception ex) { SendJson("error", new { message = ex.Message }); }
            });
        }

        private static int? GetActiveOperatorUserId(string codigoFJ)
        {
            using var conn = Program.ConnectionFactory.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT Id FROM Users
                WHERE CodigoFJ = @fj COLLATE NOCASE AND IsActive = 1
                LIMIT 1;";
            cmd.Parameters.AddWithValue("@fj", (codigoFJ ?? string.Empty).Trim());
            var value = cmd.ExecuteScalar();
            return value is null or DBNull ? null : Convert.ToInt32(value);
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
