using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using TeamOps.Core.Entities;
using TeamOps.Core.Security;
using TeamOps.Data.Db;
using TeamOps.Data.Repositories;
using TeamOps.Data.Security;
using TeamOps.UI.Forms.Models;

namespace TeamOps.UI.Forms
{
    public partial class HTMLFormOperators : Form
    {
        private readonly SqliteConnectionFactory _factory;
        private readonly OperatorRepository _opRepo;
        private readonly ShiftRepository _shiftRepo;
        private readonly GroupRepository _groupRepo;
        private readonly SectorRepository _sectorRepo;
        private readonly User _currentUser;
        private readonly AuthorizationService _authorization;
        private readonly SessionValidationService _sessionValidation;

        public HTMLFormOperators()
        {
            InitializeComponent();
            Text = L("Cadastro de operadores", "\u4f5c\u696d\u8005\u767b\u9332");

            _factory = Program.ConnectionFactory;
            _currentUser = Program.CurrentUser
                ?? throw new UnauthorizedAccessException("Sessao de usuario indisponivel.");
            _authorization = new AuthorizationService(_factory);
            _sessionValidation = new SessionValidationService(_factory);
            Demand(PermissionActions.View);
            _opRepo = new OperatorRepository(_factory);
            _shiftRepo = new ShiftRepository(_factory);
            _groupRepo = new GroupRepository(_factory);
            _sectorRepo = new SectorRepository(_factory);

            InitializeWebView();
        }

        private async void InitializeWebView()
        {
            await webViewOperators.EnsureCoreWebView2Async(null);

            var core = webViewOperators.CoreWebView2;

            core.Settings.IsWebMessageEnabled = true;
            core.Settings.AreDefaultScriptDialogsEnabled = true;
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.Settings.AreDevToolsEnabled = true;

            core.WebMessageReceived += WebMessageReceived;

            core.SetVirtualHostNameToFolderMapping(
                "app",
                Path.Combine(Application.StartupPath, "ui", "operators"),
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
                        Demand(PermissionActions.View);
                        LoadInitialData();
                        break;

                    case "save":
                        Demand(PermissionActions.Create);
                        SaveOperator(msg);
                        break;

                    case "update":
                        Demand(PermissionActions.Edit);
                        UpdateOperator(msg);
                        break;

                    case "delete":
                        Demand(PermissionActions.Delete);
                        DeleteOperator(msg);
                        break;
                }
            }
            catch (Exception ex)
            {
                PostJson(new { type = "error", message = ex.Message });
            }
        }

        private void Demand(string action)
        {
            _sessionValidation.DemandCurrent(_currentUser);
            _authorization.Demand(_currentUser.Id, ModuleCodes.Organization, action);
        }

        private void LoadInitialData()
        {
            using var conn = _factory.CreateOpenConnection();

            PostJson(new
            {
                type = "init",
                locale = Program.CurrentLocale,
                shifts = _shiftRepo.GetAll(),
                groups = _groupRepo.GetAll(),
                sectors = _sectorRepo.GetAll(),
                rows = QueryOperators(conn).ToList()
            });
        }

        private void SaveOperator(JsRequest msg)
        {
            try
            {
                var codigoFJ = NormalizeCodigoFJ(msg.codigoFJ);
                ValidatePayload(msg, codigoFJ, false);

                if (_opRepo.GetByCodigoFJ(codigoFJ) != null)
                    throw new InvalidOperationException(L("Ja existe um operador com este Codigo FJ.", "\u3053\u306e FJ \u30b3\u30fc\u30c9\u306e\u4f5c\u696d\u8005\u306f\u65e2\u306b\u5b58\u5728\u3057\u307e\u3059\u3002"));

                _opRepo.Add(BuildOperator(msg, codigoFJ));

                PostJson(new
                {
                    type = "saved",
                    message = L("Operador cadastrado com sucesso.", "\u4f5c\u696d\u8005\u3092\u767b\u9332\u3057\u307e\u3057\u305f\u3002")
                });

                SendRows();
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

        private void UpdateOperator(JsRequest msg)
        {
            try
            {
                var codigoFJ = NormalizeCodigoFJ(msg.codigoFJ);
                ValidatePayload(msg, codigoFJ, true);

                if (_opRepo.GetByCodigoFJ(codigoFJ) == null)
                    throw new InvalidOperationException(L("Operador nao encontrado para atualizacao.", "\u66f4\u65b0\u5bfe\u8c61\u306e\u4f5c\u696d\u8005\u304c\u898b\u3064\u304b\u308a\u307e\u305b\u3093\u3002"));

                _opRepo.Update(BuildOperator(msg, codigoFJ));

                PostJson(new
                {
                    type = "updated",
                    message = L("Operador atualizado com sucesso.", "\u4f5c\u696d\u8005\u3092\u66f4\u65b0\u3057\u307e\u3057\u305f\u3002")
                });

                SendRows();
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

        private void DeleteOperator(JsRequest msg)
        {
            try
            {
                var codigoFJ = NormalizeCodigoFJ(msg.codigoFJ);
                if (string.IsNullOrWhiteSpace(codigoFJ))
                    throw new InvalidOperationException(L("Selecione um operador para excluir.", "\u524a\u9664\u3059\u308b\u4f5c\u696d\u8005\u3092\u9078\u629e\u3057\u3066\u304f\u3060\u3055\u3044\u3002"));

                using var conn = _factory.CreateOpenConnection();

                if (_opRepo.GetByCodigoFJ(codigoFJ) == null)
                    throw new InvalidOperationException(L("Operador nao encontrado para exclusao.", "\u524a\u9664\u5bfe\u8c61\u306e\u4f5c\u696d\u8005\u304c\u898b\u3064\u304b\u308a\u307e\u305b\u3093\u3002"));

                if (HasOperatorDependencies(conn, codigoFJ, out var dependencyTable))
                    throw new InvalidOperationException(
                        L(
                            $"Nao e possivel excluir este operador porque o Codigo FJ ja esta vinculado a registros em {dependencyTable}.",
                            $"\u3053\u306e\u4f5c\u696d\u8005\u306f FJ \u30b3\u30fc\u30c9\u304c {dependencyTable} \u306e\u30ec\u30b3\u30fc\u30c9\u3068\u7d10\u4ed8\u3051\u3089\u308c\u3066\u3044\u308b\u305f\u3081\u524a\u9664\u3067\u304d\u307e\u305b\u3093\u3002")
                    );

                _opRepo.Delete(codigoFJ);

                PostJson(new
                {
                    type = "deleted",
                    message = L("Operador excluido com sucesso.", "\u4f5c\u696d\u8005\u3092\u524a\u9664\u3057\u307e\u3057\u305f\u3002")
                });

                SendRows();
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

        private void SendRows()
        {
            using var conn = _factory.CreateOpenConnection();

            PostJson(new
            {
                type = "rows",
                data = QueryOperators(conn).ToList()
            });
        }

        private static string NormalizeCodigoFJ(string? codigoFJ)
        {
            return (codigoFJ ?? string.Empty).Trim().ToUpperInvariant();
        }

        private void ValidatePayload(JsRequest msg, string codigoFJ, bool isUpdate)
        {
            if (string.IsNullOrWhiteSpace(codigoFJ))
                throw new InvalidOperationException(L("Informe o Codigo FJ.", "FJ \u30b3\u30fc\u30c9\u3092\u5165\u529b\u3057\u3066\u304f\u3060\u3055\u3044\u3002"));

            if (string.IsNullOrWhiteSpace(msg.nameRomanji))
                throw new InvalidOperationException(L("Informe o nome Romanji.", "\u30ed\u30fc\u30de\u5b57\u540d\u3092\u5165\u529b\u3057\u3066\u304f\u3060\u3055\u3044\u3002"));

            if (string.IsNullOrWhiteSpace(msg.nameNihongo))
                throw new InvalidOperationException(L("Informe o nome Nihongo.", "\u65e5\u672c\u8a9e\u540d\u3092\u5165\u529b\u3057\u3066\u304f\u3060\u3055\u3044\u3002"));

            if (msg.shiftId <= 0)
                throw new InvalidOperationException(L("Selecione o turno.", "\u30b7\u30d5\u30c8\u3092\u9078\u629e\u3057\u3066\u304f\u3060\u3055\u3044\u3002"));

            if (msg.groupId <= 0)
                throw new InvalidOperationException(L("Selecione o grupo.", "\u30b0\u30eb\u30fc\u30d7\u3092\u9078\u629e\u3057\u3066\u304f\u3060\u3055\u3044\u3002"));

            if (msg.sectorId <= 0)
                throw new InvalidOperationException(L("Selecione o setor.", "\u30bb\u30af\u30bf\u30fc\u3092\u9078\u629e\u3057\u3066\u304f\u3060\u3055\u3044\u3002"));

            if (!DateTime.TryParse(msg.startDate, out var startDate))
                throw new InvalidOperationException(L("Informe uma data de inicio valida.", "\u6709\u52b9\u306a\u958b\u59cb\u65e5\u3092\u5165\u529b\u3057\u3066\u304f\u3060\u3055\u3044\u3002"));

            if (msg.hasEndDate)
            {
                if (!DateTime.TryParse(msg.endDate, out var endDate))
                    throw new InvalidOperationException(L("Informe uma data de termino valida.", "\u6709\u52b9\u306a\u7d42\u4e86\u65e5\u3092\u5165\u529b\u3057\u3066\u304f\u3060\u3055\u3044\u3002"));

                if (endDate < startDate)
                    throw new InvalidOperationException(L("A data de termino nao pode ser anterior a data de inicio.", "\u7d42\u4e86\u65e5\u306f\u958b\u59cb\u65e5\u3088\u308a\u524d\u306b\u3067\u304d\u307e\u305b\u3093\u3002"));
            }

            if (!string.IsNullOrWhiteSpace(msg.birthDate) && !DateTime.TryParse(msg.birthDate, out _))
                throw new InvalidOperationException(L("Informe uma data de nascimento valida.", "\u6709\u52b9\u306a\u751f\u5e74\u6708\u65e5\u3092\u5165\u529b\u3057\u3066\u304f\u3060\u3055\u3044\u3002"));

            if (isUpdate && string.IsNullOrWhiteSpace(codigoFJ))
                throw new InvalidOperationException(L("Codigo FJ invalido para atualizacao.", "\u66f4\u65b0\u7528\u306e FJ \u30b3\u30fc\u30c9\u304c\u7121\u52b9\u3067\u3059\u3002"));
        }

        private static Operator BuildOperator(JsRequest msg, string codigoFJ)
        {
            DateTime? endDate = null;
            if (msg.hasEndDate && DateTime.TryParse(msg.endDate, out var parsedEndDate))
                endDate = parsedEndDate;

            DateTime? birthDate = null;
            if (!string.IsNullOrWhiteSpace(msg.birthDate) && DateTime.TryParse(msg.birthDate, out var parsedBirthDate))
                birthDate = parsedBirthDate;

            return new Operator
            {
                CodigoFJ = codigoFJ,
                NameRomanji = msg.nameRomanji.Trim(),
                NameNihongo = msg.nameNihongo.Trim(),
                ShiftId = msg.shiftId,
                GroupId = msg.groupId,
                SectorId = msg.sectorId,
                StartDate = DateTime.Parse(msg.startDate),
                EndDate = endDate,
                Trainer = msg.trainer,
                Status = msg.status,
                IsLeader = msg.isLeader,
                Telefone = string.IsNullOrWhiteSpace(msg.phone) ? null : msg.phone.Trim(),
                Endereco = string.IsNullOrWhiteSpace(msg.address) ? null : msg.address.Trim(),
                Nascimento = birthDate
            };
        }

        private static System.Collections.Generic.IEnumerable<dynamic> QueryOperators(System.Data.IDbConnection conn)
        {
            const string sql = @"
                SELECT
                    o.CodigoFJ AS codigoFJ,
                    o.NameRomanji AS nameRomanji,
                    o.NameNihongo AS nameNihongo,
                    o.ShiftId AS shiftId,
                    o.GroupId AS groupId,
                    o.SectorId AS sectorId,
                    substr(o.StartDate, 1, 10) AS startDate,
                    CASE
                        WHEN o.EndDate IS NULL THEN ''
                        ELSE substr(o.EndDate, 1, 10)
                    END AS endDate,
                    CASE
                        WHEN o.Nascimento IS NULL THEN ''
                        ELSE substr(o.Nascimento, 1, 10)
                    END AS birthDate,
                    o.Trainer AS trainer,
                    o.Status AS status,
                    o.IsLeader AS isLeader,
                    COALESCE(o.Telefone, '') AS phone,
                    COALESCE(o.Endereco, '') AS address,
                    COALESCE(sh.NamePt, '') AS shiftNamePt,
                    COALESCE(NULLIF(sh.NameJp, ''), sh.NamePt, '') AS shiftNameJp,
                    COALESCE(g.NamePt, '') AS groupNamePt,
                    COALESCE(NULLIF(g.NameJp, ''), g.NamePt, '') AS groupNameJp,
                    COALESCE(sc.NamePt, '') AS sectorNamePt,
                    COALESCE(NULLIF(sc.NameJp, ''), sc.NamePt, '') AS sectorNameJp
                FROM Operators o
                LEFT JOIN Shifts sh ON sh.Id = o.ShiftId
                LEFT JOIN Groups g ON g.Id = o.GroupId
                LEFT JOIN Sectors sc ON sc.Id = o.SectorId
                ORDER BY o.Status DESC, o.NameRomanji;";

            return conn.Query(sql);
        }

        private static string L(string pt, string jp)
        {
            return string.Equals(Program.CurrentLocale, "ja-JP", StringComparison.OrdinalIgnoreCase)
                ? jp
                : pt;
        }

        private static bool HasOperatorDependencies(System.Data.IDbConnection conn, string codigoFJ, out string dependencyTable)
        {
            dependencyTable = string.Empty;

            if (conn is not SqliteConnection sqliteConn)
                return false;

            using var tablesCmd = sqliteConn.CreateCommand();
            tablesCmd.CommandText = @"
                SELECT name
                FROM sqlite_master
                WHERE type = 'table'
                  AND name NOT LIKE 'sqlite_%'
                  AND name <> 'Operators'
                ORDER BY name;";

            using var tablesReader = tablesCmd.ExecuteReader();
            while (tablesReader.Read())
            {
                var tableName = tablesReader.GetString(0);

                using var fkCmd = sqliteConn.CreateCommand();
                fkCmd.CommandText = $"PRAGMA foreign_key_list(\"{tableName.Replace("\"", "\"\"")}\");";

                using var fkReader = fkCmd.ExecuteReader();
                while (fkReader.Read())
                {
                    var referencedTable = fkReader["table"]?.ToString();
                    var fromColumn = fkReader["from"]?.ToString();
                    var toColumn = fkReader["to"]?.ToString();

                    if (!string.Equals(referencedTable, "Operators", StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(toColumn, "CodigoFJ", StringComparison.OrdinalIgnoreCase) ||
                        string.IsNullOrWhiteSpace(fromColumn))
                    {
                        continue;
                    }

                    using var refCmd = sqliteConn.CreateCommand();
                    refCmd.CommandText = $@"
                        SELECT 1
                        FROM ""{tableName.Replace("\"", "\"\"")}""
                        WHERE ""{fromColumn.Replace("\"", "\"\"")}"" = @codigoFJ
                        LIMIT 1;";
                    refCmd.Parameters.AddWithValue("@codigoFJ", codigoFJ);

                    if (refCmd.ExecuteScalar() != null)
                    {
                        dependencyTable = tableName;
                        return true;
                    }
                }
            }

            return false;
        }

        private void PostJson(object payload)
        {
            webViewOperators.CoreWebView2.PostWebMessageAsJson(
                JsonSerializer.Serialize(payload)
            );
        }
    }
}
