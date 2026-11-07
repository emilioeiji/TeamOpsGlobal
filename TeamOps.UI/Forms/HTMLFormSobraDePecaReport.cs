using Dapper;
using Microsoft.Web.WebView2.Core;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using TeamOps.Core.Entities;
using TeamOps.Core.Security;
using TeamOps.Data.Db;
using TeamOps.Data.Security;
using TeamOps.Services;

namespace TeamOps.UI.Forms
{
    public partial class HTMLFormSobraDePecaReport : Form
    {
        private readonly SqliteConnectionFactory _factory;
        private readonly User _currentUser;
        private readonly AuthorizationService _authorization;

        public HTMLFormSobraDePecaReport(SqliteConnectionFactory factory, User currentUser)
        {
            InitializeComponent();
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Text = L("Relatorio de Sobra de Peca", "Scrap Parts Report");

            _factory = factory;
            _currentUser = currentUser;
            _authorization = new AuthorizationService(factory);

            InitializeWebView();
        }

        private async void InitializeWebView()
        {
            await webViewSobraReport.EnsureCoreWebView2Async(null);

            var core = webViewSobraReport.CoreWebView2;
            core.Settings.IsWebMessageEnabled = true;
            core.Settings.AreDefaultScriptDialogsEnabled = true;
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.Settings.AreDevToolsEnabled = true;

            core.WebMessageReceived += WebMessageReceived;

            core.SetVirtualHostNameToFolderMapping(
                "app",
                Path.Combine(Application.StartupPath, "ui", "sobra-de-peca-report"),
                CoreWebView2HostResourceAccessKind.Allow
            );

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
                        LoadInitial();
                        break;
                    case "apply":
                        SendRows(ReadFilter(root));
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

        private void LoadInitial()
        {
            using var conn = _factory.CreateOpenConnection();
            var sectorId = ResolveSobraDePecaSectorId();
            DemandView(sectorId);
            var filter = CreateDefaultFilter();

            PostJson(new
            {
                type = "init",
                data = new
                {
                    locale = Program.CurrentLocale,
                    filters = new
                    {
                        shifts = conn.Query(
                            @"SELECT Id AS id, COALESCE(NamePt, '') AS namePt, COALESCE(NULLIF(NameJp, ''), NamePt, '') AS nameJp FROM Shifts ORDER BY Id;").ToList(),
                        machines = conn.Query(
                            @"SELECT Id AS id, COALESCE(NamePt, '') AS namePt, COALESCE(NULLIF(NameJp, ''), NamePt, '') AS nameJp FROM Machines WHERE SectorId = @sectorId AND COALESCE(IsActive, 1) = 1 ORDER BY NamePt;",
                            new { sectorId }).ToList()
                    },
                    defaults = new
                    {
                        dtInicial = filter.Start.ToString("yyyy-MM-dd"),
                        dtFinal = filter.End.ToString("yyyy-MM-dd"),
                        shiftId = 0,
                        machineId = 0,
                        item = "",
                        search = ""
                    }
                }
            });

            SendRows(filter);
        }

        private void SendRows(SobraReportFilter filter)
        {
            using var conn = _factory.CreateOpenConnection();
            var sectorId = ResolveSobraDePecaSectorId();
            DemandView(sectorId);
            var rows = conn.Query<SobraReportRow>(
                @"
                    SELECT
                        s.Id,
                        substr(s.Data, 1, 10) AS Data,
                        s.TurnoId AS ShiftId,
                        COALESCE(sh.NamePt, '') AS ShiftNamePt,
                        COALESCE(NULLIF(sh.NameJp, ''), sh.NamePt, '') AS ShiftNameJp,
                        s.Lote,
                        s.OperadorId,
                        COALESCE(o.NameRomanji, s.OperadorId) AS OperatorNamePt,
                        COALESCE(NULLIF(o.NameNihongo, ''), o.NameRomanji, s.OperadorId) AS OperatorNameJp,
                        s.Tanjuu,
                        s.PesoGramas,
                        s.Quantidade,
                        s.MachineId,
                        COALESCE(m.NamePt, '') AS MachineNamePt,
                        COALESCE(NULLIF(m.NameJp, ''), m.NamePt, '') AS MachineNameJp,
                        s.ShainId,
                        COALESCE(sa.NameRomanji, '') AS ShainNamePt,
                        COALESCE(NULLIF(sa.NameNihongo, ''), sa.NameRomanji, '') AS ShainNameJp,
                        COALESCE(s.Observacao, '') AS Observacao,
                        COALESCE(s.Lider, '') AS Lider,
                        substr(s.CreatedAt, 1, 16) AS CreatedAt,
                        COALESCE(s.Item, '') AS Item
                    FROM SobraDePeca s
                    LEFT JOIN Shifts sh ON sh.Id = s.TurnoId
                    LEFT JOIN Operators o ON o.CodigoFJ = s.OperadorId
                    LEFT JOIN Machines m ON m.Id = s.MachineId
                    LEFT JOIN Shain sa ON sa.Id = s.ShainId
                    WHERE date(s.Data) >= date(@start)
                      AND date(s.Data) <= date(@end)
                      AND (@shiftId <= 0 OR s.TurnoId = @shiftId)
                      AND (@machineId <= 0 OR s.MachineId = @machineId)
                      AND COALESCE(m.SectorId, 0) = @sectorId
                      AND (@item = '' OR upper(trim(COALESCE(s.Item, ''))) = @item)
                      AND (
                            @search = ''
                            OR upper(COALESCE(s.Lote, '')) LIKE @searchLike
                            OR upper(COALESCE(s.OperadorId, '')) LIKE @searchLike
                            OR upper(COALESCE(o.NameRomanji, '')) LIKE @searchLike
                            OR upper(COALESCE(m.NamePt, '')) LIKE @searchLike
                            OR upper(COALESCE(sa.NameRomanji, '')) LIKE @searchLike
                            OR upper(COALESCE(s.Item, '')) LIKE @searchLike
                            OR upper(COALESCE(s.Observacao, '')) LIKE @searchLike
                            OR upper(COALESCE(s.Lider, '')) LIKE @searchLike
                          )
                    ORDER BY date(s.Data) DESC, s.Id DESC;",
                new
                {
                    start = filter.Start.ToString("yyyy-MM-dd"),
                    end = filter.End.ToString("yyyy-MM-dd"),
                    shiftId = filter.ShiftId,
                    machineId = filter.MachineId,
                    item = filter.Item.ToUpperInvariant(),
                    search = filter.Search.ToUpperInvariant(),
                    searchLike = $"%{filter.Search.ToUpperInvariant()}%",
                    sectorId
                }).ToList();

            PostJson(new
            {
                type = "rows",
                data = new
                {
                    totals = new
                    {
                        total = rows.Count,
                        quantidade = rows.Sum(item => item.Quantidade),
                        pesoGramas = rows.Sum(item => item.PesoGramas),
                        itens = rows.Select(item => item.Item).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).Count()
                    },
                    rows
                }
            });
        }

        private static SobraReportFilter CreateDefaultFilter()
        {
            var end = DateTime.Today;
            return new SobraReportFilter
            {
                Start = end.AddDays(-29),
                End = end
            };
        }

        private void DemandView(int sectorId)
        {
            _authorization.Demand(_currentUser.Id, ModuleCodes.Reports, PermissionActions.View);
            _authorization.Demand(
                _currentUser.Id,
                ModuleCodes.SobraDePeca,
                PermissionActions.View,
                sectorId);
        }

        private int ResolveSobraDePecaSectorId() =>
            new SectorCapabilityService(_factory).ResolveRequired(SectorFeatureCodes.SobraDePeca);

        private static SobraReportFilter ReadFilter(JsonElement root)
        {
            var fallback = CreateDefaultFilter();
            return new SobraReportFilter
            {
                Start = ReadDate(root, "dtInicial", fallback.Start),
                End = ReadDate(root, "dtFinal", fallback.End),
                ShiftId = ReadInt(root, "shiftId"),
                MachineId = ReadInt(root, "machineId"),
                Item = ReadString(root, "item").Trim(),
                Search = ReadString(root, "search").Trim()
            };
        }

        private static DateTime ReadDate(JsonElement root, string propertyName, DateTime fallback)
        {
            var value = ReadString(root, propertyName);
            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed)
                ? parsed.Date
                : fallback;
        }

        private static int ReadInt(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var prop))
                return 0;

            return prop.ValueKind switch
            {
                JsonValueKind.Number when prop.TryGetInt32(out var value) => value,
                JsonValueKind.String when int.TryParse(prop.GetString(), out var value) => value,
                _ => 0
            };
        }

        private static string ReadString(JsonElement root, string propertyName)
        {
            return root.TryGetProperty(propertyName, out var prop) && prop.ValueKind != JsonValueKind.Null
                ? prop.ToString() ?? string.Empty
                : string.Empty;
        }

        private void PostJson(object payload)
        {
            var json = JsonSerializer.Serialize(payload);

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() =>
                {
                    if (webViewSobraReport.CoreWebView2 != null)
                        webViewSobraReport.CoreWebView2.PostWebMessageAsJson(json);
                }));

                return;
            }

            if (webViewSobraReport.CoreWebView2 != null)
                webViewSobraReport.CoreWebView2.PostWebMessageAsJson(json);
        }

        private static string L(string pt, string jp)
        {
            return string.Equals(Program.CurrentLocale, "ja-JP", StringComparison.OrdinalIgnoreCase)
                ? jp
                : pt;
        }

        private sealed class SobraReportFilter
        {
            public DateTime Start { get; set; }
            public DateTime End { get; set; }
            public int ShiftId { get; set; }
            public int MachineId { get; set; }
            public string Item { get; set; } = string.Empty;
            public string Search { get; set; } = string.Empty;
        }

        private sealed class SobraReportRow
        {
            public int Id { get; set; }
            public string Data { get; set; } = string.Empty;
            public int ShiftId { get; set; }
            public string ShiftNamePt { get; set; } = string.Empty;
            public string ShiftNameJp { get; set; } = string.Empty;
            public string Lote { get; set; } = string.Empty;
            public string OperadorId { get; set; } = string.Empty;
            public string OperatorNamePt { get; set; } = string.Empty;
            public string OperatorNameJp { get; set; } = string.Empty;
            public double Tanjuu { get; set; }
            public double PesoGramas { get; set; }
            public double Quantidade { get; set; }
            public int MachineId { get; set; }
            public string MachineNamePt { get; set; } = string.Empty;
            public string MachineNameJp { get; set; } = string.Empty;
            public int ShainId { get; set; }
            public string ShainNamePt { get; set; } = string.Empty;
            public string ShainNameJp { get; set; } = string.Empty;
            public string Observacao { get; set; } = string.Empty;
            public string Lider { get; set; } = string.Empty;
            public string CreatedAt { get; set; } = string.Empty;
            public string Item { get; set; } = string.Empty;
        }
    }
}
