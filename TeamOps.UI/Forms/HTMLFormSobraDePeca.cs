using Dapper;
using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using TeamOps.Core.Entities;
using TeamOps.Core.Security;
using TeamOps.Data.Db;
using TeamOps.Data.Security;
using TeamOps.UI.Forms.Models;

namespace TeamOps.UI.Forms
{
    public partial class HTMLFormSobraDePeca : Form
    {
        private readonly SqliteConnectionFactory _factory;
        private readonly Operator _currentOperator;
        private readonly User _currentUser;
        private readonly AuthorizationService _authorization;

        public HTMLFormSobraDePeca(
            SqliteConnectionFactory factory,
            User currentUser,
            Operator currentOperator)
        {
            InitializeComponent();
            Text = L("Sobra de peca", "\u90e8\u54c1\u4f59\u308a\u767b\u9332");

            _factory = factory;
            _currentUser = currentUser;
            _currentOperator = currentOperator;
            _authorization = new AuthorizationService(factory);

            InitializeWebView();
        }

        private async void InitializeWebView()
        {
            await webViewSobraDePeca.EnsureCoreWebView2Async(null);

            var core = webViewSobraDePeca.CoreWebView2;

            core.Settings.IsWebMessageEnabled = true;
            core.Settings.AreDefaultScriptDialogsEnabled = true;
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.Settings.AreDevToolsEnabled = true;

            core.WebMessageReceived += WebMessageReceived;

            core.SetVirtualHostNameToFolderMapping(
                "app",
                Path.Combine(Application.StartupPath, "ui", "sobra-de-peca"),
                CoreWebView2HostResourceAccessKind.Allow
            );

            core.Navigate("https://app/index.html");
        }

        private void WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            var msg = JsonSerializer.Deserialize<JsRequest>(e.WebMessageAsJson);
            if (msg == null) return;

            switch (msg.action)
            {
                case "load":
                    LoadInitialData();
                    break;

                case "save":
                    SaveSobraDePeca(msg);
                    break;
            }
        }

        private void LoadInitialData()
        {
            using var conn = _factory.CreateOpenConnection();
            var sectorId = ResolveGBareruSectorId(conn);
            _authorization.Demand(
                _currentUser.Id,
                ModuleCodes.SobraDePeca,
                PermissionActions.View,
                sectorId);

            var shiftName = conn.QueryFirstOrDefault<string>(
                string.Equals(Program.CurrentLocale, "ja-JP", StringComparison.OrdinalIgnoreCase)
                    ? "SELECT COALESCE(NULLIF(NameJp, ''), NamePt, '') FROM Shifts WHERE Id = @id"
                    : "SELECT COALESCE(NamePt, '') FROM Shifts WHERE Id = @id",
                new { id = _currentOperator.ShiftId }
            ) ?? "";

            var operators = conn.Query(
                @"SELECT
                      CodigoFJ,
                      COALESCE(NameRomanji, CodigoFJ) AS NamePt,
                      COALESCE(NULLIF(NameNihongo, ''), NameRomanji, CodigoFJ) AS NameJp
                  FROM Operators
                  WHERE Status = 1 AND ShiftId = @shiftId AND SectorId = @sectorId
                  ORDER BY NameRomanji",
                new { shiftId = _currentOperator.ShiftId, sectorId }
            ).ToList();

            var machines = conn.Query(
                @"SELECT
                      Id,
                      COALESCE(NamePt, '') AS NamePt,
                      COALESCE(NULLIF(NameJp, ''), NamePt, '') AS NameJp
                  FROM Machines
                  WHERE SectorId = @sectorId
                    AND COALESCE(IsActive, 1) = 1
                  ORDER BY NamePt",
                new { sectorId }
            ).ToList();

            var shains = conn.Query(
                @"SELECT
                      Id,
                      COALESCE(NameRomanji, '') AS NamePt,
                      COALESCE(NULLIF(NameNihongo, ''), NameRomanji, '') AS NameJp
                  FROM Shain
                  ORDER BY NameRomanji"
            ).ToList();

            var rows = QueryRecentRows(conn, sectorId).ToList();

            PostJson(new
            {
                type = "init",
                locale = Program.CurrentLocale,
                shiftId = _currentOperator.ShiftId,
                shiftName,
                leaderNamePt = string.IsNullOrWhiteSpace(_currentOperator.NameRomanji)
                    ? _currentOperator.CodigoFJ
                    : _currentOperator.NameRomanji,
                leaderNameJp = string.IsNullOrWhiteSpace(_currentOperator.NameNihongo)
                    ? (string.IsNullOrWhiteSpace(_currentOperator.NameRomanji)
                        ? _currentOperator.CodigoFJ
                        : _currentOperator.NameRomanji)
                    : _currentOperator.NameNihongo,
                today = DateTime.Today.ToString("yyyy-MM-dd"),
                operators,
                machines,
                shains,
                rows
            });
        }

        private void SaveSobraDePeca(JsRequest msg)
        {
            try
            {
                ValidatePayloadLocalized(msg);

                using var conn = _factory.CreateOpenConnection();
                var sectorId = ResolveGBareruSectorId(conn);
                _authorization.Demand(
                    _currentUser.Id,
                    ModuleCodes.SobraDePeca,
                    PermissionActions.Create,
                    sectorId);
                var machineIsAllowed = conn.ExecuteScalar<long>(
                    @"SELECT EXISTS (
                          SELECT 1 FROM Machines
                          WHERE Id = @machineId AND SectorId = @sectorId AND COALESCE(IsActive, 1) = 1
                      );",
                    new { msg.machineId, sectorId }) == 1;
                if (!machineIsAllowed)
                    throw new UnauthorizedAccessException(L("Maquina fora do setor autorizado.", "許可されたセクター外の設備です。"));

                const string sql = @"
                    INSERT INTO SobraDePeca
                    (Data, TurnoId, Lote, OperadorId, Tanjuu, PesoGramas, Quantidade, MachineId, ShainId, Observacao, Lider, CreatedAt, Item)
                    VALUES
                    (@Data, @TurnoId, @Lote, @OperadorId, @Tanjuu, @PesoGramas, @Quantidade, @MachineId, @ShainId, @Observacao, @Lider, @CreatedAt, @Item);
                    SELECT last_insert_rowid();";

                var data = DateTime.Parse(msg.date);
                var createdAt = DateTime.Now;

                var newId = conn.ExecuteScalar<int>(sql, new
                {
                    Data = data.ToString("yyyy-MM-dd"),
                    TurnoId = _currentOperator.ShiftId,
                    Lote = msg.lote.Trim(),
                    OperadorId = msg.opCodigoFJ,
                    Tanjuu = msg.tanjuu,
                    PesoGramas = msg.pesoGramas,
                    Quantidade = msg.quantidade,
                    MachineId = msg.machineId,
                    ShainId = msg.shainId,
                    Observacao = string.IsNullOrWhiteSpace(msg.observacao) ? null : msg.observacao.Trim(),
                    Lider = _currentOperator.NameRomanji,
                    CreatedAt = createdAt.ToString("yyyy-MM-dd HH:mm:ss"),
                    Item = msg.item.Trim().ToUpperInvariant()
                });

                PostJson(new
                {
                    type = "saved",
                    id = newId,
                    message = "Sobra de peça registrada com sucesso."
                });

                SendRows(conn, sectorId);
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

        private void ValidatePayload(JsRequest msg)
        {
            if (string.IsNullOrWhiteSpace(msg.date))
                throw new InvalidOperationException("Informe a data.");

            if (!DateTime.TryParse(msg.date, out _))
                throw new InvalidOperationException("Informe uma data válida.");

            if (string.IsNullOrWhiteSpace(msg.lote))
                throw new InvalidOperationException("Informe o lote.");

            if (string.IsNullOrWhiteSpace(msg.opCodigoFJ))
                throw new InvalidOperationException("Selecione um operador.");

            if (msg.tanjuu <= 0)
                throw new InvalidOperationException("Informe um tanjuu válido.");

            if (msg.pesoGramas <= 0)
                throw new InvalidOperationException("Informe um peso válido.");

            if (msg.quantidade <= 0)
                throw new InvalidOperationException("Quantidade inválida.");

            if (msg.machineId <= 0)
                throw new InvalidOperationException("Selecione uma máquina.");

            if (msg.shainId <= 0)
                throw new InvalidOperationException("Selecione um shain.");

            if (string.IsNullOrWhiteSpace(msg.item))
                throw new InvalidOperationException("Informe o item.");
        }

        private void SendRows(System.Data.IDbConnection conn, int sectorId)
        {
            PostJson(new
            {
                type = "rows",
                data = QueryRecentRows(conn, sectorId).ToList()
            });
        }

        private static System.Collections.Generic.IEnumerable<dynamic> QueryRecentRows(System.Data.IDbConnection conn, int sectorId)
        {
            const string sql = @"
                SELECT
                    s.Id AS id,
                    substr(s.Data, 1, 10) AS data,
                    COALESCE(sh.NamePt, '') AS shiftNamePt,
                    COALESCE(NULLIF(sh.NameJp, ''), sh.NamePt, '') AS shiftNameJp,
                    s.Lote AS lote,
                    COALESCE(o.NameRomanji, s.OperadorId) AS operatorNamePt,
                    COALESCE(NULLIF(o.NameNihongo, ''), o.NameRomanji, s.OperadorId) AS operatorNameJp,
                    s.Tanjuu AS tanjuu,
                    s.PesoGramas AS pesoGramas,
                    s.Quantidade AS quantidade,
                    COALESCE(m.NamePt, '') AS machineNamePt,
                    COALESCE(NULLIF(m.NameJp, ''), m.NamePt, '') AS machineNameJp,
                    COALESCE(sa.NameRomanji, '') AS shainNamePt,
                    COALESCE(NULLIF(sa.NameNihongo, ''), sa.NameRomanji, '') AS shainNameJp,
                    COALESCE(s.Observacao, '') AS observacao,
                    s.Lider AS lider,
                    substr(s.CreatedAt, 1, 16) AS createdAt,
                    s.Item AS item
                FROM SobraDePeca s
                LEFT JOIN Shifts sh ON sh.Id = s.TurnoId
                LEFT JOIN Operators o ON o.CodigoFJ = s.OperadorId
                LEFT JOIN Machines m ON m.Id = s.MachineId
                LEFT JOIN Shain sa ON sa.Id = s.ShainId
                WHERE m.SectorId = @sectorId
                ORDER BY s.Id DESC
                LIMIT 100;";

            return conn.Query(sql, new { sectorId });
        }

        private static int ResolveGBareruSectorId(System.Data.IDbConnection conn)
        {
            return conn.ExecuteScalar<int?>(
                "SELECT Id FROM Sectors WHERE Code = 'G-BARERU' COLLATE NOCASE AND IsActive = 1;")
                ?? throw new InvalidOperationException("Setor G-Bareru nao configurado.");
        }

        private void PostJson(object payload)
        {
            webViewSobraDePeca.CoreWebView2.PostWebMessageAsJson(
                JsonSerializer.Serialize(payload)
            );
        }

        private void ValidatePayloadLocalized(JsRequest msg)
        {
            if (string.IsNullOrWhiteSpace(msg.date))
                throw new InvalidOperationException(L("Informe a data.", "\u65e5\u4ed8\u3092\u5165\u529b\u3057\u3066\u304f\u3060\u3055\u3044\u3002"));

            if (!DateTime.TryParse(msg.date, out _))
                throw new InvalidOperationException(L("Informe uma data valida.", "\u6709\u52b9\u306a\u65e5\u4ed8\u3092\u5165\u529b\u3057\u3066\u304f\u3060\u3055\u3044\u3002"));

            if (string.IsNullOrWhiteSpace(msg.lote))
                throw new InvalidOperationException(L("Informe o lote.", "\u30ed\u30c3\u30c8\u3092\u5165\u529b\u3057\u3066\u304f\u3060\u3055\u3044\u3002"));

            if (string.IsNullOrWhiteSpace(msg.opCodigoFJ))
                throw new InvalidOperationException(L("Selecione um operador.", "\u4f5c\u696d\u8005\u3092\u9078\u629e\u3057\u3066\u304f\u3060\u3055\u3044\u3002"));

            if (msg.tanjuu <= 0)
                throw new InvalidOperationException(L("Informe um tanjuu valido.", "\u6709\u52b9\u306a\u5358\u91cd\u3092\u5165\u529b\u3057\u3066\u304f\u3060\u3055\u3044\u3002"));

            if (msg.pesoGramas <= 0)
                throw new InvalidOperationException(L("Informe um peso valido.", "\u6709\u52b9\u306a\u91cd\u91cf\u3092\u5165\u529b\u3057\u3066\u304f\u3060\u3055\u3044\u3002"));

            if (msg.quantidade <= 0)
                throw new InvalidOperationException(L("Quantidade invalida.", "\u6570\u91cf\u304c\u7121\u52b9\u3067\u3059\u3002"));

            if (msg.machineId <= 0)
                throw new InvalidOperationException(L("Selecione uma maquina.", "\u8a2d\u5099\u3092\u9078\u629e\u3057\u3066\u304f\u3060\u3055\u3044\u3002"));

            if (msg.shainId <= 0)
                throw new InvalidOperationException(L("Selecione um shain.", "\u793e\u54e1\u3092\u9078\u629e\u3057\u3066\u304f\u3060\u3055\u3044\u3002"));

            if (string.IsNullOrWhiteSpace(msg.item))
                throw new InvalidOperationException(L("Informe o item.", "\u54c1\u76ee\u3092\u5165\u529b\u3057\u3066\u304f\u3060\u3055\u3044\u3002"));
        }

        private static string L(string pt, string jp)
        {
            return string.Equals(Program.CurrentLocale, "ja-JP", StringComparison.OrdinalIgnoreCase)
                ? jp
                : pt;
        }
    }
}
