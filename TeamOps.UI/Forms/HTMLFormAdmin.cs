using Dapper;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using TeamOps.Data.Db;
using TeamOps.Data.Repositories;
using TeamOps.Data.Security;
using TeamOps.Core.Security;
using TeamOps.Config;

namespace TeamOps.UI.Forms
{
    public partial class HTMLFormAdmin : Form
    {
        private readonly SqliteConnectionFactory _factory;
        private readonly Dictionary<string, AdminEntityDefinition> _entities;
        private readonly AdministrativeScopeService _scopeService;
        private readonly OrganizationAdministrationService _organizationAdministration;
        private AdministrativeScope _scope;

        public HTMLFormAdmin()
        {
            InitializeComponent();
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Text = L("Administracao", "\u7ba1\u7406");

            _factory = Program.ConnectionFactory;
            _scopeService = new AdministrativeScopeService(_factory);
            _organizationAdministration = new OrganizationAdministrationService(_factory);
            _scope = _scopeService.GetScope(CurrentUserId);
            _entities = BuildEntities();

            InitializeWebView();
        }

        private static int CurrentUserId => Program.CurrentUser?.Id
            ?? throw new UnauthorizedAccessException("Sessao de usuario indisponivel.");

        private async void InitializeWebView()
        {
            await webViewAdmin.EnsureCoreWebView2Async(null);

            var core = webViewAdmin.CoreWebView2;
            core.Settings.IsWebMessageEnabled = true;
            core.Settings.AreDefaultScriptDialogsEnabled = true;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDevToolsEnabled = false;

            core.WebMessageReceived += WebMessageReceived;

            core.SetVirtualHostNameToFolderMapping(
                "app",
                Path.Combine(Application.StartupPath, "ui", "admin"),
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

                    case "load_entity":
                        SendEntityRows(ReadString(root, "entity"), root);
                        break;

                    case "save":
                        SaveEntity(ReadString(root, "entity"), root);
                        break;

                    case "update":
                        UpdateEntity(ReadString(root, "entity"), ReadInt(root, "id"), root);
                        break;

                    case "delete":
                        DeleteEntity(ReadString(root, "entity"), ReadInt(root, "id"));
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
            _scope = _scopeService.GetScope(CurrentUserId);
            PostJson(new
            {
                type = "init",
                data = new
                {
                    locale = Program.CurrentLocale,
                    activeEntity = "machine",
                    entities = _entities.Values.Select(entity => new
                    {
                        key = entity.Key,
                        group = entity.Group,
                        readOnly = entity.ReadOnly,
                        titlePt = entity.TitlePt,
                        titleJp = entity.TitleJp,
                        descriptionPt = entity.DescriptionPt,
                        descriptionJp = entity.DescriptionJp,
                        fields = entity.Fields.Select(field => new
                        {
                            key = field.Key,
                            labelPt = field.LabelPt,
                            labelJp = field.LabelJp,
                            type = field.Type,
                            required = field.Required,
                            lookupKey = field.LookupKey
                        }),
                        columns = entity.Columns.Select(column => new
                        {
                            key = column.Key,
                            jpKey = column.JpKey,
                            labelPt = column.LabelPt,
                            labelJp = column.LabelJp
                        })
                    }),
                    lookups = BuildLookups()
                }
            });

            SendEntityRows("machine");
        }

        private object BuildLookups()
        {
            using var conn = _factory.CreateOpenConnection();
            EnsureAdminSchema(conn);
            var authorizedSectorIds = _scope.AuthorizedSectorIds.ToArray();

            return new
            {
                sectors = conn.Query(
                    @"
                        SELECT
                            Id AS id,
                            COALESCE(NamePt, '') AS namePt,
                            COALESCE(NULLIF(NameJp, ''), NamePt, '') AS nameJp,
                            COALESCE(NamePt, '') AS labelPt,
                            COALESCE(NULLIF(NameJp, ''), NamePt, '') AS labelJp
                        FROM Sectors
                        WHERE Id IN @authorizedSectorIds
                        ORDER BY NamePt;"
                    , new { authorizedSectorIds }).ToList(),
                locals = conn.Query(
                    @"
                        SELECT
                            l.Id AS id,
                            COALESCE(l.NamePt, '') AS namePt,
                            COALESCE(NULLIF(l.NameJp, ''), l.NamePt, '') AS nameJp,
                            COALESCE(l.SectorId, 0) AS sectorId,
                            COALESCE(s.NamePt, '') AS sectorNamePt,
                            COALESCE(NULLIF(s.NameJp, ''), s.NamePt, '') AS sectorNameJp,
                            COALESCE(NULLIF(l.ShortCode, ''), NULLIF(l.NamePt, ''), NULLIF(l.NameJp, ''), 'L' || l.Id) AS shortCode,
                            COALESCE(s.NamePt, '') || ' - ' || COALESCE(NULLIF(l.ShortCode, ''), NULLIF(l.NamePt, ''), NULLIF(l.NameJp, ''), 'L' || l.Id) || ' - ' || COALESCE(l.NamePt, '') AS labelPt,
                            COALESCE(NULLIF(s.NameJp, ''), s.NamePt, '') || ' - ' || COALESCE(NULLIF(l.ShortCode, ''), NULLIF(l.NamePt, ''), NULLIF(l.NameJp, ''), 'L' || l.Id) || ' - ' || COALESCE(NULLIF(l.NameJp, ''), l.NamePt, '') AS labelJp
                        FROM Locals l
                        LEFT JOIN Sectors s ON s.Id = l.SectorId
                        WHERE l.SectorId IN @authorizedSectorIds
                        ORDER BY l.SectorId, l.NamePt, l.Id;"
                    , new { authorizedSectorIds }).ToList(),
                organizationUnits = conn.Query(
                    @"
                        SELECT Id AS id, Code AS code, NamePt AS namePt,
                               COALESCE(NULLIF(NameJp, ''), NamePt) AS nameJp,
                               Code || ' - ' || NamePt AS labelPt,
                               Code || ' - ' || COALESCE(NULLIF(NameJp, ''), NamePt) AS labelJp
                        FROM OrganizationNodes
                        WHERE NodeType = 'Unit' AND IsActive = 1 AND @canManageGlobal = 1
                        ORDER BY Code COLLATE NOCASE;",
                    new { canManageGlobal = _scope.CanManageGlobalCatalogs ? 1 : 0 }
                ).ToList(),
                machineActive = new[]
                {
                    new { id = 1, namePt = "Ativa", nameJp = "稼働", labelPt = "Ativa", labelJp = "稼働" },
                    new { id = 0, namePt = "Inativa", nameJp = "停止", labelPt = "Inativa", labelJp = "停止" }
                },
                booleanChoices = new[]
                {
                    new { id = 1, namePt = "Sim", nameJp = "はい", labelPt = "Sim", labelJp = "はい" },
                    new { id = 0, namePt = "Nao", nameJp = "いいえ", labelPt = "Nao", labelJp = "いいえ" }
                },
                statusClasses = new[]
                {
                    new { id = 0, namePt = "Rodando", nameJp = "稼動中" },
                    new { id = 1, namePt = "Inativo", nameJp = "非稼働" },
                    new { id = 3, namePt = "Parado", nameJp = "停止" },
                    new { id = 4, namePt = "Erro", nameJp = "エラー" }
                }
            };
        }

        private static void EnsureAdminSchema(IDbConnection conn)
        {
            ProductionSchemaMigrator.Ensure(conn);
        }

        private void SendEntityRows(string entityKey, JsonElement? root = null)
        {
            _scope = _scopeService.GetScope(CurrentUserId, PermissionActions.View);
            if (!_entities.TryGetValue(entityKey, out var entity))
                throw new InvalidOperationException(L("Item administrativo invalido.", "\u7121\u52b9\u306a\u7ba1\u7406\u9805\u76ee\u3067\u3059\u3002"));

            using var conn = _factory.CreateOpenConnection();
            EnsureAdminSchema(conn);
            var rows = entityKey == "system_log"
                ? LoadSystemLogRows(conn, root).ToList()
                : FilterRows(entity, entity.QueryRows(conn)).ToList();

            PostJson(new
            {
                type = "entity_rows",
                data = new
                {
                    entity = entityKey,
                    rows,
                    filters = entityKey == "system_log" ? (root.HasValue ? ReadSystemLogFilters(root.Value) : GetDefaultSystemLogFilters()) : null,
                    lookups = BuildLookups()
                }
            });
        }

        private IEnumerable<object> FilterRows(AdminEntityDefinition entity, IEnumerable<object> rows)
        {
            if (entity.Scope == AdminEntityScope.Global)
                return _scope.CanManageGlobalCatalogs ? rows : [];

            return rows.Where(row =>
            {
                var sectorId = entity.Key == "sector"
                    ? ReadRowInt(row, "id")
                    : ReadRowInt(row, "sectorId");
                return sectorId > 0
                    ? _scope.ContainsSector(sectorId)
                    : entity.Scope == AdminEntityScope.SectorOrGlobal && _scope.CanManageGlobalCatalogs;
            });
        }

        private static int ReadRowInt(object row, string key)
        {
            if (row is IDictionary<string, object> dictionary
                && dictionary.TryGetValue(key, out var value)
                && value != null)
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);

            var property = row.GetType().GetProperty(key,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
            var propertyValue = property?.GetValue(row);
            return propertyValue == null ? 0 : Convert.ToInt32(propertyValue, CultureInfo.InvariantCulture);
        }

        private static IEnumerable<object> LoadSystemLogRows(IDbConnection conn, JsonElement? root)
        {
            var filters = root.HasValue ? ReadSystemLogFilters(root.Value) : GetDefaultSystemLogFilters();
            var startDate = ParseDateOrDefault(filters.DateStart, DateTime.Today.AddDays(-30));
            var endDate = ParseDateOrDefault(filters.DateEnd, DateTime.Today);

            if (endDate < startDate)
            {
                (startDate, endDate) = (endDate, startDate);
            }

            var user = (filters.UserFJ ?? string.Empty).Trim();
            var module = (filters.Module ?? string.Empty).Trim();
            var action = (filters.Action ?? string.Empty).Trim();

            return conn.Query(
                @"
                    SELECT
                        Id AS id,
                        COALESCE(Timestamp, '') AS timestamp,
                        COALESCE(UserFJ, '') AS userFJ,
                        COALESCE(Module, '') AS module,
                        COALESCE(Action, '') AS action,
                        COALESCE(CAST(TargetId AS TEXT), '') AS targetId,
                        COALESCE(Details, '') AS details
                    FROM SystemLog
                    WHERE date(Timestamp) BETWEEN date(@startDate) AND date(@endDate)
                      AND (@user = '' OR UserFJ LIKE '%' || @user || '%')
                      AND (@module = '' OR Module LIKE '%' || @module || '%')
                      AND (@action = '' OR Action LIKE '%' || @action || '%')
                    ORDER BY Timestamp DESC, Id DESC
                    LIMIT 1000;",
                new
                {
                    startDate = startDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    endDate = endDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    user,
                    module,
                    action
                });
        }

        private void SaveEntity(string entityKey, JsonElement root)
        {
            _scope = _scopeService.GetScope(CurrentUserId, PermissionActions.Create);
            if (!_entities.TryGetValue(entityKey, out var entity))
                throw new InvalidOperationException(L("Item administrativo invalido.", "\u7121\u52b9\u306a\u7ba1\u7406\u9805\u76ee\u3067\u3059\u3002"));

            if (entity.ReadOnly)
                throw new InvalidOperationException(L("Este item e somente leitura.", "\u3053\u306e\u9805\u76ee\u306f\u95b2\u89a7\u306e\u307f\u3067\u3059\u3002"));

            using var conn = _factory.CreateOpenConnection();
            EnsureAdminSchema(conn);
            var values = root.GetProperty("values");
            if (!SaveOrganizationEntity(entityKey, values))
            {
                DemandRequestedScope(conn, entity, values, PermissionActions.Create);
                var parameters = entity.BuildParameters(conn, values, null);
                using var transaction = conn.BeginTransaction();
                conn.Execute(entity.InsertSql, parameters, transaction);
                var id = conn.ExecuteScalar<long>("SELECT last_insert_rowid();", transaction: transaction);
                WriteAdminAudit(conn, transaction, "AdminCreated", entityKey, id);
                transaction.Commit();
            }

            PostJson(new
            {
                type = "saved",
                message = L("Cadastro salvo com sucesso.", "\u767b\u9332\u3092\u4fdd\u5b58\u3057\u307e\u3057\u305f\u3002")
            });

            SendEntityRows(entityKey);
        }

        private void UpdateEntity(string entityKey, int id, JsonElement root)
        {
            _scope = _scopeService.GetScope(CurrentUserId, PermissionActions.Edit);
            if (id <= 0)
                throw new InvalidOperationException(L("Selecione um registro valido para editar.", "\u7de8\u96c6\u5bfe\u8c61\u306e\u30ec\u30b3\u30fc\u30c9\u3092\u9078\u629e\u3057\u3066\u304f\u3060\u3055\u3044\u3002"));

            if (!_entities.TryGetValue(entityKey, out var entity))
                throw new InvalidOperationException(L("Item administrativo invalido.", "\u7121\u52b9\u306a\u7ba1\u7406\u9805\u76ee\u3067\u3059\u3002"));

            if (entity.ReadOnly)
                throw new InvalidOperationException(L("Este item e somente leitura.", "\u3053\u306e\u9805\u76ee\u306f\u95b2\u89a7\u306e\u307f\u3067\u3059\u3002"));

            using var conn = _factory.CreateOpenConnection();
            EnsureAdminSchema(conn);
            var values = root.GetProperty("values");
            if (!UpdateOrganizationEntity(entityKey, id, values))
            {
                DemandExistingScope(conn, entity, id, PermissionActions.Edit);
                DemandRequestedScope(conn, entity, values, PermissionActions.Edit);
                var parameters = entity.BuildParameters(conn, values, id);
                using var transaction = conn.BeginTransaction();
                if (conn.Execute(entity.UpdateSql, parameters, transaction) != 1)
                    throw new InvalidOperationException(L("Registro nao encontrado para atualizacao.", "更新対象が見つかりません。"));
                WriteAdminAudit(conn, transaction, "AdminUpdated", entityKey, id);
                transaction.Commit();
            }

            PostJson(new
            {
                type = "updated",
                message = L("Cadastro atualizado com sucesso.", "\u767b\u9332\u3092\u66f4\u65b0\u3057\u307e\u3057\u305f\u3002")
            });

            SendEntityRows(entityKey);
        }

        private void DeleteEntity(string entityKey, int id)
        {
            _scope = _scopeService.GetScope(CurrentUserId, PermissionActions.Delete);
            if (id <= 0)
                throw new InvalidOperationException(L("Selecione um registro valido para excluir.", "\u524a\u9664\u5bfe\u8c61\u306e\u30ec\u30b3\u30fc\u30c9\u3092\u9078\u629e\u3057\u3066\u304f\u3060\u3055\u3044\u3002"));

            if (!_entities.TryGetValue(entityKey, out var entity))
                throw new InvalidOperationException(L("Item administrativo invalido.", "\u7121\u52b9\u306a\u7ba1\u7406\u9805\u76ee\u3067\u3059\u3002"));

            if (entity.ReadOnly)
                throw new InvalidOperationException(L("Este item e somente leitura.", "\u3053\u306e\u9805\u76ee\u306f\u95b2\u89a7\u306e\u307f\u3067\u3059\u3002"));

            using var conn = _factory.CreateOpenConnection();
            EnsureAdminSchema(conn);
            if (!DeleteOrganizationEntity(entityKey, id))
            {
                DemandExistingScope(conn, entity, id, PermissionActions.Delete);
                using var transaction = conn.BeginTransaction();
                if (conn.Execute(entity.DeleteSql, new { id }, transaction) != 1)
                    throw new InvalidOperationException(L("Registro nao encontrado para exclusao.", "削除対象が見つかりません。"));
                WriteAdminAudit(conn, transaction, "AdminDeleted", entityKey, id);
                transaction.Commit();
            }

            PostJson(new
            {
                type = "deleted",
                message = L("Cadastro excluido com sucesso.", "\u767b\u9332\u3092\u524a\u9664\u3057\u307e\u3057\u305f\u3002")
            });

            SendEntityRows(entityKey);
        }

        private void DemandRequestedScope(
            IDbConnection conn,
            AdminEntityDefinition entity,
            JsonElement values,
            string action)
        {
            if (entity.Scope == AdminEntityScope.Global)
            {
                _scopeService.DemandGlobal(CurrentUserId, action);
                return;
            }

            var sectorId = ReadInt(values, "sectorId");
            var localId = ReadInt(values, "localId");
            if (localId > 0)
                sectorId = conn.ExecuteScalar<int?>("SELECT SectorId FROM Locals WHERE Id = @localId;", new { localId }) ?? 0;

            if (sectorId <= 0 && entity.Scope == AdminEntityScope.SectorOrGlobal)
            {
                _scopeService.DemandGlobal(CurrentUserId, action);
                return;
            }

            _scopeService.DemandSector(CurrentUserId, sectorId, action);
        }

        private bool SaveOrganizationEntity(string entityKey, JsonElement values)
        {
            if (entityKey == "organization_unit")
            {
                _organizationAdministration.AddUnit(CurrentUserId, ReadUnitRequest(values));
                return true;
            }
            if (entityKey == "organization_sector")
            {
                _organizationAdministration.AddSector(CurrentUserId, ReadSectorRequest(values));
                return true;
            }
            return false;
        }

        private bool UpdateOrganizationEntity(string entityKey, int id, JsonElement values)
        {
            if (entityKey == "organization_unit")
            {
                _organizationAdministration.UpdateUnit(CurrentUserId, id, ReadUnitRequest(values));
                return true;
            }
            if (entityKey == "organization_sector")
            {
                _organizationAdministration.UpdateSector(CurrentUserId, id, ReadSectorRequest(values));
                return true;
            }
            return false;
        }

        private bool DeleteOrganizationEntity(string entityKey, int id)
        {
            if (entityKey == "organization_unit")
            {
                _organizationAdministration.DeactivateUnit(CurrentUserId, id);
                return true;
            }
            if (entityKey == "organization_sector")
            {
                _organizationAdministration.DeactivateSector(CurrentUserId, id);
                return true;
            }
            return false;
        }

        private static OrganizationUnitSaveRequest ReadUnitRequest(JsonElement values) => new(
            ReadInt(values, "parentId") is var parentId && parentId > 0 ? parentId : null,
            ReadString(values, "code"),
            ReadString(values, "namePt"),
            ReadString(values, "nameJp"),
            ReadBool(values, "isActive", true));

        private static OrganizationSectorSaveRequest ReadSectorRequest(JsonElement values) => new(
            ReadInt(values, "parentUnitId"),
            ReadString(values, "code"),
            ReadString(values, "namePt"),
            ReadString(values, "nameJp"),
            ReadBool(values, "isActive", true));

        private void DemandExistingScope(
            IDbConnection conn,
            AdminEntityDefinition entity,
            int id,
            string action)
        {
            if (entity.Scope == AdminEntityScope.Global)
            {
                _scopeService.DemandGlobal(CurrentUserId, action);
                return;
            }

            var sectorId = entity.Key switch
            {
                "sector" => conn.ExecuteScalar<int?>("SELECT Id FROM Sectors WHERE Id = @id;", new { id }),
                "local" => conn.ExecuteScalar<int?>("SELECT SectorId FROM Locals WHERE Id = @id;", new { id }),
                "machine" => conn.ExecuteScalar<int?>("SELECT SectorId FROM Machines WHERE Id = @id;", new { id }),
                "machine_status" => conn.ExecuteScalar<int?>("SELECT SectorId FROM MachineStatuses WHERE Id = @id;", new { id }),
                "production_procedure_time" => conn.ExecuteScalar<int?>("SELECT SectorId FROM ProductionProcedureTimes WHERE Id = @id;", new { id }),
                _ => null
            };

            if (!sectorId.HasValue)
            {
                if (entity.Scope == AdminEntityScope.SectorOrGlobal
                    && conn.ExecuteScalar<int>("SELECT COUNT(1) FROM MachineStatuses WHERE Id = @id AND SectorId IS NULL;", new { id }) == 1)
                {
                    _scopeService.DemandGlobal(CurrentUserId, action);
                    return;
                }

                throw new InvalidOperationException(L("Registro administrativo nao encontrado.", "管理レコードが見つかりません。"));
            }

            _scopeService.DemandSector(CurrentUserId, sectorId.Value, action);
        }

        private static void WriteAdminAudit(
            IDbConnection conn,
            IDbTransaction transaction,
            string eventType,
            string entityKey,
            long id)
        {
            conn.Execute(@"
                INSERT INTO SecurityAudit (ActorUserId, EventType, TargetType, TargetId, Details)
                VALUES (@actorUserId, @eventType, @entityKey, @targetId, 'Administrative catalog change');",
                new
                {
                    actorUserId = CurrentUserId,
                    eventType,
                    entityKey,
                    targetId = id.ToString(CultureInfo.InvariantCulture)
                }, transaction);
        }

        private Dictionary<string, AdminEntityDefinition> BuildEntities()
        {
            return new[]
            {
                CreateOrganizationUnitEntity(),
                CreateOrganizationSectorEntity(),
                CreateNameEntity(
                    "shift", "core",
                    "Turnos", "\u30b7\u30d5\u30c8",
                    "Base dos turnos usados no dashboard, tasks e filtros.",
                    "\u30c0\u30c3\u30b7\u30e5\u30dc\u30fc\u30c9\u3001tasks\u3001\u30d5\u30a3\u30eb\u30bf\u3067\u4f7f\u3046\u30b7\u30d5\u30c8\u30de\u30b9\u30bf\u3067\u3059\u3002",
                    "Shifts"),
                CreateNameEntity(
                    "group", "core",
                    "Grupos", "\u30b0\u30eb\u30fc\u30d7",
                    "Agrupamentos de lideranca e leitura de area.",
                    "\u30ea\u30fc\u30c0\u30fc\u30b0\u30eb\u30fc\u30d7\u3068\u30a8\u30ea\u30a2\u8aad\u307f\u53d6\u308a\u306e\u57fa\u790e\u30de\u30b9\u30bf\u3067\u3059\u3002",
                    "Groups"),
                ConfigureScope(CreateNameEntity(
                    "sector", "core",
                    "Setores", "\u30bb\u30af\u30bf\u30fc",
                    "Setores exibidos nas leituras, filtros e presencas.",
                    "\u8aad\u307f\u53d6\u308a\u3001\u30d5\u30a3\u30eb\u30bf\u3001\u51fa\u5e2d\u7ba1\u7406\u3067\u4f7f\u3046\u30bb\u30af\u30bf\u30fc\u3067\u3059\u3002",
                    "Sectors"), AdminEntityScope.Sector, readOnly: true),
                ConfigureScope(CreateLocalEntity(), AdminEntityScope.Sector),
                CreateNameEntity(
                    "equipment", "production",
                    "Equipamentos", "\u8a2d\u5099",
                    "Equipamentos vinculados aos cadastros operacionais.",
                    "\u904b\u7528\u7cfb\u30d5\u30a9\u30fc\u30e0\u306b\u7d10\u4ed8\u304f\u8a2d\u5099\u30de\u30b9\u30bf\u3067\u3059\u3002",
                    "Equipments"),
                ConfigureScope(CreateMachineEntity(), AdminEntityScope.Sector),
                ConfigureScope(CreateMachineAuditEntity(), AdminEntityScope.SectorOrGlobal, readOnly: true),
                ConfigureScope(CreateMachineStatusEntity(), AdminEntityScope.SectorOrGlobal),
                CreatePartCodeStyleEntity(),
                ConfigureScope(CreateProductionProcedureTimeEntity(), AdminEntityScope.Sector),
                CreateNameEntity(
                    "category", "production",
                    "Categorias", "\u30ab\u30c6\u30b4\u30ea",
                    "Categorias usadas no Hikitsugui e em outros registros.",
                    "Hikitsugui \u3068\u305d\u306e\u4ed6\u306e\u767b\u9332\u3067\u4f7f\u3046\u30ab\u30c6\u30b4\u30ea\u3067\u3059\u3002",
                    "Categories"),
                CreateNameEntity(
                    "follow_reason", "followup",
                    "Motivos Follow", "\u30d5\u30a9\u30ed\u30fc\u7406\u7531",
                    "Motivos padrao para os acompanhamentos.",
                    "\u30d5\u30a9\u30ed\u30fc\u767b\u9332\u3067\u4f7f\u3046\u7406\u7531\u30de\u30b9\u30bf\u3067\u3059\u3002",
                    "FollowUpReasons"),
                CreateNameEntity(
                    "follow_type", "followup",
                    "Tipos Follow", "\u30d5\u30a9\u30ed\u30fc\u7a2e\u5225",
                    "Tipos de acompanhamento para os formularios de follow.",
                    "\u30d5\u30a9\u30ed\u30fc\u30d5\u30a9\u30fc\u30e0\u3067\u4f7f\u3046\u7a2e\u5225\u30de\u30b9\u30bf\u3067\u3059\u3002",
                    "FollowUpTypes"),
                CreateShainEntity(),
                CreateSecurityAuditEntity(),
                CreateStorageDiagnosticsEntity(),
                CreateSystemLogEntity()
            }
            .Where(item => _scope.CanManageGlobalCatalogs || item.Scope != AdminEntityScope.Global)
            .ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);
        }

        private static AdminEntityDefinition ConfigureScope(
            AdminEntityDefinition entity,
            AdminEntityScope scope,
            bool readOnly = false)
        {
            entity.Scope = scope;
            entity.ReadOnly = entity.ReadOnly || readOnly;
            return entity;
        }

        private static AdminEntityDefinition CreateOrganizationUnitEntity()
        {
            return new AdminEntityDefinition
            {
                Key = "organization_unit",
                Group = "organization",
                TitlePt = "Arvore de Unidades",
                TitleJp = "組織ユニットツリー",
                DescriptionPt = "Unidades hierarquicas como 5500, 5552 e futuras ramificacoes.",
                DescriptionJp = "5500、5552、および将来の組織ブランチを管理します。",
                Fields =
                {
                    new AdminFieldDefinition("parentId", "Unidade superior", "Parent unit", "select", false, "organizationUnits"),
                    new AdminFieldDefinition("code", "Codigo", "Code", "text", true),
                    new AdminFieldDefinition("namePt", "Nome PT", "Name PT", "text", true),
                    new AdminFieldDefinition("nameJp", "Nome JP", "Name JP", "text", false),
                    new AdminFieldDefinition("isActive", "Ativa", "Active", "checkbox", false)
                },
                Columns =
                {
                    new AdminColumnDefinition("treePath", null, "Caminho", "Path"),
                    new AdminColumnDefinition("code", null, "Codigo", "Code"),
                    new AdminColumnDefinition("namePt", "nameJp", "Nome", "Name"),
                    new AdminColumnDefinition("activeLabelPt", "activeLabelJp", "Status", "Status")
                },
                QueryRows = conn => conn.Query(@"
                    WITH RECURSIVE tree AS (
                        SELECT Id, ParentId, Code, NamePt, NameJp, IsActive, Code AS TreePath
                        FROM OrganizationNodes WHERE NodeType = 'Unit' AND ParentId IS NULL
                        UNION ALL
                        SELECT child.Id, child.ParentId, child.Code, child.NamePt, child.NameJp, child.IsActive,
                               tree.TreePath || ' / ' || child.Code
                        FROM OrganizationNodes child
                        JOIN tree ON child.ParentId = tree.Id
                        WHERE child.NodeType = 'Unit'
                    )
                    SELECT Id AS id, COALESCE(ParentId, 0) AS parentId, Code AS code,
                           NamePt AS namePt, NameJp AS nameJp, IsActive AS isActive, TreePath AS treePath,
                           CASE IsActive WHEN 1 THEN 'Ativa' ELSE 'Inativa' END AS activeLabelPt,
                           CASE IsActive WHEN 1 THEN 'Active' ELSE 'Inactive' END AS activeLabelJp
                    FROM tree ORDER BY TreePath COLLATE NOCASE;"),
                BuildParameters = (_, _, _) => new DynamicParameters()
            };
        }

        private static AdminEntityDefinition CreateOrganizationSectorEntity()
        {
            return new AdminEntityDefinition
            {
                Key = "organization_sector",
                Group = "organization",
                TitlePt = "Setores da Arvore",
                TitleJp = "組織セクター",
                DescriptionPt = "Cria e mantem o setor sincronizado com seu no organizacional.",
                DescriptionJp = "セクターと組織ノードを同期して管理します。",
                Fields =
                {
                    new AdminFieldDefinition("parentUnitId", "Unidade superior", "Parent unit", "select", true, "organizationUnits"),
                    new AdminFieldDefinition("code", "Codigo", "Code", "text", true),
                    new AdminFieldDefinition("namePt", "Nome PT", "Name PT", "text", true),
                    new AdminFieldDefinition("nameJp", "Nome JP", "Name JP", "text", false),
                    new AdminFieldDefinition("isActive", "Ativo", "Active", "checkbox", false)
                },
                Columns =
                {
                    new AdminColumnDefinition("unitCode", null, "Unidade", "Unit"),
                    new AdminColumnDefinition("code", null, "Codigo", "Code"),
                    new AdminColumnDefinition("namePt", "nameJp", "Nome", "Name"),
                    new AdminColumnDefinition("activeLabelPt", "activeLabelJp", "Status", "Status")
                },
                QueryRows = conn => conn.Query(@"
                    SELECT s.Id AS id, node.ParentId AS parentUnitId, parent.Code AS unitCode,
                           s.Code AS code, s.NamePt AS namePt, s.NameJp AS nameJp, s.IsActive AS isActive,
                           CASE s.IsActive WHEN 1 THEN 'Ativo' ELSE 'Inativo' END AS activeLabelPt,
                           CASE s.IsActive WHEN 1 THEN 'Active' ELSE 'Inactive' END AS activeLabelJp
                    FROM Sectors s
                    JOIN OrganizationNodes node ON node.Id = s.OrganizationNodeId AND node.NodeType = 'Sector'
                    LEFT JOIN OrganizationNodes parent ON parent.Id = node.ParentId
                    ORDER BY COALESCE(parent.Code, ''), s.Code COLLATE NOCASE;"),
                BuildParameters = (_, _, _) => new DynamicParameters()
            };
        }

        private static AdminEntityDefinition CreateSecurityAuditEntity()
        {
            return new AdminEntityDefinition
            {
                Key = "security_audit",
                Group = "misc",
                ReadOnly = true,
                TitlePt = "Auditoria de Seguranca",
                TitleJp = "セキュリティ監査",
                DescriptionPt = "Eventos de acesso, autenticacao e alteracoes administrativas.",
                DescriptionJp = "アクセス、認証、管理変更の監査イベントです。",
                Columns =
                {
                    new AdminColumnDefinition("occurredAt", null, "Data/Hora", "Date/Time"),
                    new AdminColumnDefinition("source", null, "Origem", "Source"),
                    new AdminColumnDefinition("actor", null, "Usuario", "User"),
                    new AdminColumnDefinition("eventType", null, "Evento", "Event"),
                    new AdminColumnDefinition("target", null, "Alvo", "Target"),
                    new AdminColumnDefinition("details", null, "Detalhes", "Details")
                },
                QueryRows = conn => conn.Query(@"
                    SELECT * FROM (
                        SELECT sa.OccurredAt AS occurredAt, 'Security' AS source,
                               COALESCE(actor.Login, 'Sistema') AS actor, sa.EventType AS eventType,
                               sa.TargetType || CASE WHEN sa.TargetId IS NULL THEN '' ELSE ':' || sa.TargetId END AS target,
                               COALESCE(sa.Details, '') AS details
                        FROM SecurityAudit sa
                        LEFT JOIN Users actor ON actor.Id = sa.ActorUserId
                        UNION ALL
                        SELECT aa.OccurredAt AS occurredAt, 'Authentication' AS source,
                               aa.Login AS actor, aa.EventType AS eventType,
                               'User:' || COALESCE(CAST(aa.UserId AS TEXT), '-') AS target,
                               trim(COALESCE(aa.Workstation, '') || ' ' || COALESCE(aa.Details, '')) AS details
                        FROM AuthenticationAudit aa
                    ) audit
                    ORDER BY occurredAt DESC
                    LIMIT 1000;"),
                BuildParameters = (_, _, _) => throw new InvalidOperationException("Auditoria somente para leitura.")
            };
        }

        private static AdminEntityDefinition CreateStorageDiagnosticsEntity()
        {
            return new AdminEntityDefinition
            {
                Key = "storage_diagnostics",
                Group = "misc",
                ReadOnly = true,
                TitlePt = "Diagnostico de Armazenamento",
                TitleJp = "Storage Diagnostics",
                DescriptionPt = "Caminhos resolvidos, localizacao local ou UNC e disponibilidade atual.",
                DescriptionJp = "Resolved storage paths, local or UNC location, and current availability.",
                Columns =
                {
                    new AdminColumnDefinition("label", null, "Recurso", "Resource"),
                    new AdminColumnDefinition("path", null, "Caminho resolvido", "Resolved path"),
                    new AdminColumnDefinition("location", null, "Localizacao", "Location"),
                    new AdminColumnDefinition("status", null, "Status", "Status")
                },
                QueryRows = _ => new StorageDiagnosticsService().Inspect().Select(item => new
                {
                    key = item.Key,
                    label = item.Label,
                    path = item.ResolvedPath,
                    location = item.IsUnc ? "UNC" : "Local",
                    status = item.Status
                }),
                BuildParameters = (_, _, _) => throw new InvalidOperationException(
                    "O diagnostico de armazenamento e somente leitura.")
            };
        }

        private static AdminEntityDefinition CreatePartCodeStyleEntity()
        {
            return new AdminEntityDefinition
            {
                Key = "part_code_style",
                Group = "production",
                TitlePt = "Codigos da Producao",
                TitleJp = "Production Codes",
                DescriptionPt = "Legenda visual dos codigos destacados na tela de producao.",
                DescriptionJp = "Visual legend for highlighted production codes.",
                Fields =
                {
                    new AdminFieldDefinition("partCode", "Codigo", "Code", "text", true),
                    new AdminFieldDefinition("colorHex", "Cor", "Color", "color", true),
                    new AdminFieldDefinition("textColorHex", "Cor do Texto", "Text Color", "color", true),
                    new AdminFieldDefinition("description", "Descricao", "Description", "text", false),
                    new AdminFieldDefinition("isActive", "Ativo", "Active", "checkbox", false)
                },
                Columns =
                {
                    new AdminColumnDefinition("partCode", null, "Codigo", "Code"),
                    new AdminColumnDefinition("colorHex", null, "Cor", "Color"),
                    new AdminColumnDefinition("textColorHex", null, "Texto", "Text"),
                    new AdminColumnDefinition("description", null, "Descricao", "Description"),
                    new AdminColumnDefinition("activeLabelPt", "activeLabelJp", "Status", "Status")
                },
                QueryRows = conn => conn.Query(
                    @"
                        SELECT
                            rowid AS id,
                            COALESCE(PartCode, '') AS partCode,
                            COALESCE(ColorHex, '#D93F3F') AS colorHex,
                            COALESCE(TextColorHex, '#FFFFFF') AS textColorHex,
                            COALESCE(Description, '') AS description,
                            COALESCE(IsActive, 1) AS isActive,
                            CASE COALESCE(IsActive, 1)
                                WHEN 1 THEN 'Ativo'
                                ELSE 'Inativo'
                            END AS activeLabelPt,
                            CASE COALESCE(IsActive, 1)
                                WHEN 1 THEN 'Active'
                                ELSE 'Inactive'
                            END AS activeLabelJp
                        FROM ProductionPartCodeStyles
                        ORDER BY COALESCE(IsActive, 1) DESC, PartCode;"
                ),
                InsertSql = @"
                    INSERT INTO ProductionPartCodeStyles
                    (
                        PartCode,
                        ColorHex,
                        TextColorHex,
                        Description,
                        IsActive,
                        UpdatedAt
                    )
                    VALUES
                    (
                        @partCode,
                        @colorHex,
                        @textColorHex,
                        @description,
                        @isActive,
                        CURRENT_TIMESTAMP
                    );",
                UpdateSql = @"
                    UPDATE ProductionPartCodeStyles
                    SET
                        PartCode = @partCode,
                        ColorHex = @colorHex,
                        TextColorHex = @textColorHex,
                        Description = @description,
                        IsActive = @isActive,
                        UpdatedAt = CURRENT_TIMESTAMP
                    WHERE rowid = @id;",
                DeleteSql = "DELETE FROM ProductionPartCodeStyles WHERE rowid = @id;",
                BuildParameters = (conn, values, currentId) =>
                {
                    var partCode = ReadString(values, "partCode").Trim().ToUpperInvariant();
                    var colorHex = NormalizeHexColor(ReadString(values, "colorHex"));
                    var textColorHex = NormalizeHexColor(ReadString(values, "textColorHex"));
                    var description = ReadString(values, "description").Trim();
                    var isActive = ReadBool(values, "isActive", true);

                    if (string.IsNullOrWhiteSpace(partCode))
                        throw new InvalidOperationException(L("Informe o codigo de producao.", "Invalid production code."));

                    var duplicate = conn.ExecuteScalar<int>(
                        @"
                            SELECT COUNT(1)
                            FROM ProductionPartCodeStyles
                            WHERE upper(trim(PartCode)) = upper(trim(@partCode))
                              AND (@currentId IS NULL OR rowid <> @currentId);",
                        new
                        {
                            partCode,
                            currentId
                        }) > 0;

                    if (duplicate)
                        throw new InvalidOperationException(L("Ja existe um codigo de producao com este valor.", "Production code already exists."));

                    var parameters = new DynamicParameters();
                    parameters.Add("@partCode", partCode);
                    parameters.Add("@colorHex", colorHex);
                    parameters.Add("@textColorHex", textColorHex);
                    parameters.Add("@description", string.IsNullOrWhiteSpace(description) ? null : description);
                    parameters.Add("@isActive", isActive ? 1 : 0);
                    if (currentId.HasValue)
                    {
                        parameters.Add("@id", currentId.Value);
                    }

                    return parameters;
                }
            };
        }

        private static AdminEntityDefinition CreateProductionProcedureTimeEntity()
        {
            return new AdminEntityDefinition
            {
                Key = "production_procedure_time",
                Group = "production",
                TitlePt = "Tempos de Procedimento",
                TitleJp = "Procedure Times",
                DescriptionPt = "Tempos padrao usados apenas na previsao de capacidade do G-Bareru. Use KYUKEI para sobrescrever o descanso padrao de 65 min por turno.",
                DescriptionJp = "Standard times used only by G-Bareru capacity forecast. Use KYUKEI to override the default 65 min break per shift.",
                Fields =
                {
                    new AdminFieldDefinition("sectorId", "Setor", "Sector", "select", true, "sectors"),
                    new AdminFieldDefinition("localId", "Area (opcional)", "Area (optional)", "select", false, "locals"),
                    new AdminFieldDefinition("procedureCode", "Procedimento", "Procedure", "text", true),
                    new AdminFieldDefinition("standardMinutes", "Tempo padrao (min)", "Standard minutes", "decimal", true),
                    new AdminFieldDefinition("isActive", "Ativo", "Active", "checkbox", false)
                },
                Columns =
                {
                    new AdminColumnDefinition("id", null, "ID", "ID"),
                    new AdminColumnDefinition("sectorNamePt", "sectorNameJp", "Setor", "Sector"),
                    new AdminColumnDefinition("localDisplayPt", "localDisplayJp", "Area", "Area"),
                    new AdminColumnDefinition("procedureCode", null, "Procedimento", "Procedure"),
                    new AdminColumnDefinition("standardMinutes", null, "Min", "Min"),
                    new AdminColumnDefinition("activeLabelPt", "activeLabelJp", "Status", "Status")
                },
                QueryRows = conn => conn.Query(
                    @"
                        SELECT
                            ppt.Id AS id,
                            ppt.SectorId AS sectorId,
                            COALESCE(ppt.LocalId, 0) AS localId,
                            upper(trim(COALESCE(ppt.ProcedureCode, ''))) AS procedureCode,
                            ppt.StandardMinutes AS standardMinutes,
                            COALESCE(ppt.IsActive, 1) AS isActive,
                            COALESCE(s.NamePt, '') AS sectorNamePt,
                            COALESCE(NULLIF(s.NameJp, ''), s.NamePt, '') AS sectorNameJp,
                            CASE
                                WHEN COALESCE(l.Id, 0) = 0 THEN 'Global do setor'
                                ELSE COALESCE(NULLIF(l.ShortCode, ''), NULLIF(l.NamePt, ''), NULLIF(l.NameJp, ''), 'L' || l.Id) || ' - ' || COALESCE(l.NamePt, '')
                            END AS localDisplayPt,
                            CASE
                                WHEN COALESCE(l.Id, 0) = 0 THEN 'Sector global'
                                ELSE COALESCE(NULLIF(l.ShortCode, ''), NULLIF(l.NamePt, ''), NULLIF(l.NameJp, ''), 'L' || l.Id) || ' - ' || COALESCE(NULLIF(l.NameJp, ''), l.NamePt, '')
                            END AS localDisplayJp,
                            CASE COALESCE(ppt.IsActive, 1)
                                WHEN 1 THEN 'Ativo'
                                ELSE 'Inativo'
                            END AS activeLabelPt,
                            CASE COALESCE(ppt.IsActive, 1)
                                WHEN 1 THEN 'Active'
                                ELSE 'Inactive'
                            END AS activeLabelJp
                        FROM ProductionProcedureTimes ppt
                        LEFT JOIN Sectors s ON s.Id = ppt.SectorId
                        LEFT JOIN Locals l ON l.Id = ppt.LocalId
                        ORDER BY ppt.SectorId, COALESCE(ppt.LocalId, 0), ppt.ProcedureCode;"
                ),
                InsertSql = @"
                    INSERT INTO ProductionProcedureTimes
                    (SectorId, LocalId, ProcedureCode, StandardMinutes, IsActive, UpdatedAt)
                    VALUES
                    (@sectorId, @localId, @procedureCode, @standardMinutes, @isActive, CURRENT_TIMESTAMP);",
                UpdateSql = @"
                    UPDATE ProductionProcedureTimes
                    SET
                        SectorId = @sectorId,
                        LocalId = @localId,
                        ProcedureCode = @procedureCode,
                        StandardMinutes = @standardMinutes,
                        IsActive = @isActive,
                        UpdatedAt = CURRENT_TIMESTAMP
                    WHERE Id = @id;",
                DeleteSql = "DELETE FROM ProductionProcedureTimes WHERE Id = @id;",
                BuildParameters = (conn, values, currentId) =>
                {
                    var sectorId = ReadInt(values, "sectorId");
                    var localId = ReadInt(values, "localId");
                    var procedureCode = NormalizeProcedureCode(ReadString(values, "procedureCode"));
                    var standardMinutes = ReadDouble(values, "standardMinutes");
                    var isActive = ReadBool(values, "isActive", true);

                    if (sectorId <= 0)
                        throw new InvalidOperationException(L("Selecione um setor valido.", "Invalid sector."));

                    if (string.IsNullOrWhiteSpace(procedureCode))
                        throw new InvalidOperationException(L("Informe o procedimento.", "Invalid procedure."));

                    if (standardMinutes <= 0)
                        throw new InvalidOperationException(L("Informe um tempo maior que zero.", "Time must be greater than zero."));

                    var duplicate = conn.ExecuteScalar<int>(
                        @"
                            SELECT COUNT(1)
                            FROM ProductionProcedureTimes
                            WHERE SectorId = @sectorId
                              AND COALESCE(LocalId, 0) = @localId
                              AND upper(trim(ProcedureCode)) = @procedureCode
                              AND (@currentId IS NULL OR Id <> @currentId);",
                        new
                        {
                            sectorId,
                            localId,
                            procedureCode,
                            currentId
                        }) > 0;

                    if (duplicate)
                        throw new InvalidOperationException(L("Ja existe tempo para este procedimento neste setor/area.", "Procedure time already exists."));

                    var parameters = new DynamicParameters();
                    parameters.Add("@sectorId", sectorId);
                    parameters.Add("@localId", localId > 0 ? localId : null);
                    parameters.Add("@procedureCode", procedureCode);
                    parameters.Add("@standardMinutes", standardMinutes);
                    parameters.Add("@isActive", isActive ? 1 : 0);
                    if (currentId.HasValue)
                    {
                        parameters.Add("@id", currentId.Value);
                    }

                    return parameters;
                }
            };
        }

        private static AdminEntityDefinition CreateNameEntity(
            string key,
            string group,
            string titlePt,
            string titleJp,
            string descriptionPt,
            string descriptionJp,
            string tableName)
        {
            return new AdminEntityDefinition
            {
                Key = key,
                Group = group,
                TitlePt = titlePt,
                TitleJp = titleJp,
                DescriptionPt = descriptionPt,
                DescriptionJp = descriptionJp,
                Fields =
                {
                    new AdminFieldDefinition("namePt", "Nome PT", "\u540d\u79f0 PT", "text", true),
                    new AdminFieldDefinition("nameJp", "Nome JP", "\u540d\u79f0 JP", "text", true)
                },
                Columns =
                {
                    new AdminColumnDefinition("id", null, "ID", "ID"),
                    new AdminColumnDefinition("namePt", "nameJp", "Nome", "\u540d\u79f0"),
                    new AdminColumnDefinition("nameJp", null, "Nome JP", "\u540d\u79f0 JP")
                },
                QueryRows = conn => conn.Query(
                    $@"
                        SELECT
                            Id AS id,
                            COALESCE(NamePt, '') AS namePt,
                            COALESCE(NameJp, '') AS nameJp
                        FROM {tableName}
                        ORDER BY Id;"
                ),
                InsertSql = $"INSERT INTO {tableName} (NamePt, NameJp) VALUES (@namePt, @nameJp);",
                UpdateSql = $"UPDATE {tableName} SET NamePt = @namePt, NameJp = @nameJp WHERE Id = @id;",
                DeleteSql = $"DELETE FROM {tableName} WHERE Id = @id;",
                BuildParameters = (_, values, _) =>
                {
                    var namePt = ReadString(values, "namePt").Trim();
                    var nameJp = ReadString(values, "nameJp").Trim();

                    if (string.IsNullOrWhiteSpace(namePt) || string.IsNullOrWhiteSpace(nameJp))
                        throw new InvalidOperationException(L("Preencha os dois campos de nome.", "\u4e21\u65b9\u306e\u540d\u79f0\u3092\u5165\u529b\u3057\u3066\u304f\u3060\u3055\u3044\u3002"));

                    var parameters = new DynamicParameters();
                    parameters.Add("@namePt", namePt);
                    parameters.Add("@nameJp", nameJp);
                    return parameters;
                }
            };
        }

        private static AdminEntityDefinition CreateLocalEntity()
        {
            return new AdminEntityDefinition
            {
                Key = "local",
                Group = "core",
                TitlePt = "Locais",
                TitleJp = "\u5834\u6240",
                DescriptionPt = "Locais, areas e codigos curtos usados em Haidai, producao, presence e exportacoes.",
                DescriptionJp = "\u30cf\u30a4\u30c0\u30a4\u3001\u751f\u7523\u3001\u51fa\u5e2d\u3001\u51fa\u529b\u753b\u9762\u3067\u4f7f\u3046\u5834\u6240\u30fb\u30a8\u30ea\u30a2\u30fb\u7565\u79f0\u30b3\u30fc\u30c9\u3092\u7ba1\u7406\u3057\u307e\u3059\u3002",
                Fields =
                {
                    new AdminFieldDefinition("namePt", "Nome PT", "\u540d\u79f0 PT", "text", true),
                    new AdminFieldDefinition("nameJp", "Nome JP", "\u540d\u79f0 JP", "text", true),
                    new AdminFieldDefinition("shortCode", "Codigo Curto", "\u7565\u79f0\u30b3\u30fc\u30c9", "text", true),
                    new AdminFieldDefinition("sectorId", "Setor", "\u30bb\u30af\u30bf\u30fc", "select", true, "sectors")
                },
                Columns =
                {
                    new AdminColumnDefinition("id", null, "ID", "ID"),
                    new AdminColumnDefinition("shortCode", null, "Codigo", "\u30b3\u30fc\u30c9"),
                    new AdminColumnDefinition("namePt", "nameJp", "Nome", "\u540d\u79f0"),
                    new AdminColumnDefinition("sectorNamePt", "sectorNameJp", "Setor", "\u30bb\u30af\u30bf\u30fc"),
                    new AdminColumnDefinition("machineCount", null, "Maquinas", "\u8a2d\u5099\u6570")
                },
                QueryRows = conn => conn.Query(
                    @"
                        SELECT
                            l.Id AS id,
                            COALESCE(l.NamePt, '') AS namePt,
                            COALESCE(l.NameJp, '') AS nameJp,
                            COALESCE(NULLIF(l.ShortCode, ''), NULLIF(l.NamePt, ''), NULLIF(l.NameJp, ''), 'L' || l.Id) AS shortCode,
                            l.SectorId AS sectorId,
                            COALESCE(s.NamePt, '') AS sectorNamePt,
                            COALESCE(NULLIF(s.NameJp, ''), s.NamePt, '') AS sectorNameJp,
                            COUNT(m.Id) AS machineCount
                        FROM Locals l
                        LEFT JOIN Sectors s ON s.Id = l.SectorId
                        LEFT JOIN Machines m ON m.LocalId = l.Id AND COALESCE(m.IsActive, 1) = 1
                        GROUP BY l.Id, l.NamePt, l.NameJp, l.ShortCode, l.SectorId, s.NamePt, s.NameJp
                        ORDER BY l.SectorId, shortCode, l.Id;"
                ),
                InsertSql = "INSERT INTO Locals (NamePt, NameJp, ShortCode, SectorId) VALUES (@namePt, @nameJp, @shortCode, @sectorId);",
                UpdateSql = "UPDATE Locals SET NamePt = @namePt, NameJp = @nameJp, ShortCode = @shortCode, SectorId = @sectorId WHERE Id = @id;",
                DeleteSql = "DELETE FROM Locals WHERE Id = @id;",
                BuildParameters = (conn, values, currentId) =>
                {
                    var namePt = ReadString(values, "namePt").Trim();
                    var nameJp = ReadString(values, "nameJp").Trim();
                    var shortCode = ReadString(values, "shortCode").Trim();
                    var sectorId = ReadInt(values, "sectorId");

                    if (string.IsNullOrWhiteSpace(namePt) || string.IsNullOrWhiteSpace(nameJp))
                        throw new InvalidOperationException(L("Preencha os dois campos de nome.", "\u4e21\u65b9\u306e\u540d\u79f0\u3092\u5165\u529b\u3057\u3066\u304f\u3060\u3055\u3044\u3002"));

                    if (string.IsNullOrWhiteSpace(shortCode))
                        throw new InvalidOperationException(L("Informe o codigo curto do local.", "\u5834\u6240\u306e\u7565\u79f0\u30b3\u30fc\u30c9\u3092\u5165\u529b\u3057\u3066\u304f\u3060\u3055\u3044\u3002"));

                    if (sectorId <= 0)
                        throw new InvalidOperationException(L("Selecione um setor valido.", "\u6709\u52b9\u306a\u30bb\u30af\u30bf\u30fc\u3092\u9078\u629e\u3057\u3066\u304f\u3060\u3055\u3044\u3002"));

                    var duplicateShortCode = conn.ExecuteScalar<int>(
                        @"
                            SELECT COUNT(1)
                            FROM Locals
                            WHERE upper(trim(COALESCE(ShortCode, ''))) = upper(trim(@shortCode))
                              AND SectorId = @sectorId
                              AND (@currentId IS NULL OR Id <> @currentId);",
                        new
                        {
                            shortCode,
                            sectorId,
                            currentId
                        }
                    ) > 0;

                    if (duplicateShortCode)
                        throw new InvalidOperationException(L("Ja existe um local com este codigo curto neste setor.", "\u3053\u306e\u30bb\u30af\u30bf\u30fc\u306b\u540c\u3058\u7565\u79f0\u30b3\u30fc\u30c9\u306e\u5834\u6240\u304c\u65e2\u306b\u3042\u308a\u307e\u3059\u3002"));

                    var parameters = new DynamicParameters();
                    parameters.Add("@namePt", namePt);
                    parameters.Add("@nameJp", nameJp);
                    parameters.Add("@shortCode", shortCode);
                    parameters.Add("@sectorId", sectorId);
                    if (currentId.HasValue)
                    {
                        parameters.Add("@id", currentId.Value);
                    }
                    return parameters;
                }
            };
        }

        private static AdminEntityDefinition CreateMachineEntity()
        {
            return new AdminEntityDefinition
            {
                Key = "machine",
                Group = "production",
                TitlePt = "Maquinas",
                TitleJp = "\u6a5f\u68b0",
                DescriptionPt = "Cadastro das maquinas com vinculo consistente de codigo, linha, setor e LocalId para producao e relatorios.",
                DescriptionJp = "\u751f\u7523\u30e2\u30cb\u30bf\u30fc\u3068\u30ec\u30dd\u30fc\u30c8\u3067\u4f7f\u3046\u8a2d\u5099\u306e\u30b3\u30fc\u30c9\u3001\u30e9\u30a4\u30f3\u3001\u30bb\u30af\u30bf\u30fc\u3001LocalId \u3092\u6574\u5408\u6027\u3042\u308a\u3067\u7ba1\u7406\u3057\u307e\u3059\u3002",
                Fields =
                {
                    new AdminFieldDefinition("namePt", "Nome PT", "\u540d\u79f0 PT", "text", false),
                    new AdminFieldDefinition("nameJp", "Nome JP", "\u540d\u79f0 JP", "text", false),
                    new AdminFieldDefinition("machineCode", "Codigo da Maquina", "\u8a2d\u5099\u30b3\u30fc\u30c9", "text", true),
                    new AdminFieldDefinition("lineCode", "Linha", "\u30e9\u30a4\u30f3", "text", false),
                    new AdminFieldDefinition("sectorId", "Setor", "\u30bb\u30af\u30bf\u30fc", "select", false, "sectors"),
                    new AdminFieldDefinition("localId", "Local", "\u5834\u6240", "select", false, "locals"),
                    new AdminFieldDefinition("isActive", "Ativa", "\u7a3c\u50cd", "checkbox", false)
                },
                Columns =
                {
                    new AdminColumnDefinition("id", null, "ID", "ID"),
                    new AdminColumnDefinition("machineCode", null, "Codigo", "\u30b3\u30fc\u30c9"),
                    new AdminColumnDefinition("machineKey", null, "Chave", "\u30ad\u30fc"),
                    new AdminColumnDefinition("namePt", "nameJp", "Nome", "\u540d\u79f0"),
                    new AdminColumnDefinition("lineCode", null, "Linha", "\u30e9\u30a4\u30f3"),
                    new AdminColumnDefinition("sectorNamePt", "sectorNameJp", "Setor", "\u30bb\u30af\u30bf\u30fc"),
                    new AdminColumnDefinition("localDisplayPt", "localDisplayJp", "Local", "\u5834\u6240"),
                    new AdminColumnDefinition("activeLabelPt", "activeLabelJp", "Status", "\u72b6\u614b"),
                    new AdminColumnDefinition("validationPt", "validationJp", "Validacao", "\u691c\u8a3c")
                },
                QueryRows = conn => conn.Query(
                    @"
                        SELECT
                            m.Id AS id,
                            COALESCE(m.NamePt, '') AS namePt,
                            COALESCE(m.NameJp, '') AS nameJp,
                            COALESCE(m.MachineCode, '') AS machineCode,
                            COALESCE(m.MachineKey, '') AS machineKey,
                            COALESCE(m.LineCode, '') AS lineCode,
                            COALESCE(m.SectorId, 0) AS sectorId,
                            COALESCE(m.LocalId, 0) AS localId,
                            COALESCE(m.IsActive, 1) AS isActive,
                            COALESCE(s.NamePt, '') AS sectorNamePt,
                            COALESCE(NULLIF(s.NameJp, ''), s.NamePt, '') AS sectorNameJp,
                            COALESCE(l.NamePt, '') AS localNamePt,
                            COALESCE(NULLIF(l.NameJp, ''), l.NamePt, '') AS localNameJp,
                            CASE
                                WHEN COALESCE(l.Id, 0) = 0 THEN ''
                                ELSE COALESCE(NULLIF(l.ShortCode, ''), NULLIF(l.NamePt, ''), NULLIF(l.NameJp, ''), 'L' || l.Id) || ' - ' || COALESCE(l.NamePt, '')
                            END AS localDisplayPt,
                            CASE
                                WHEN COALESCE(l.Id, 0) = 0 THEN ''
                                ELSE COALESCE(NULLIF(l.ShortCode, ''), NULLIF(l.NamePt, ''), NULLIF(l.NameJp, ''), 'L' || l.Id) || ' - ' || COALESCE(NULLIF(l.NameJp, ''), l.NamePt, '')
                            END AS localDisplayJp,
                            CASE COALESCE(m.IsActive, 1)
                                WHEN 1 THEN 'Ativa'
                                ELSE 'Inativa'
                            END AS activeLabelPt,
                            CASE COALESCE(m.IsActive, 1)
                                WHEN 1 THEN '稼働'
                                ELSE '停止'
                            END AS activeLabelJp,
                            CASE
                                WHEN COALESCE(m.LocalId, 0) = 0 THEN 'Sem local vinculado'
                                WHEN COALESCE(m.SectorId, 0) = 0 THEN 'Sem setor vinculado'
                                WHEN COALESCE(l.SectorId, 0) <> COALESCE(m.SectorId, 0) THEN 'Setor da maquina difere do setor do local'
                                ELSE 'OK'
                            END AS validationPt,
                            CASE
                                WHEN COALESCE(m.LocalId, 0) = 0 THEN '場所未設定'
                                WHEN COALESCE(m.SectorId, 0) = 0 THEN 'セクター未設定'
                                WHEN COALESCE(l.SectorId, 0) <> COALESCE(m.SectorId, 0) THEN '設備セクターと場所セクターが一致しません'
                                ELSE 'OK'
                            END AS validationJp
                        FROM Machines m
                        LEFT JOIN Sectors s ON s.Id = m.SectorId
                        LEFT JOIN Locals l ON l.Id = m.LocalId
                        ORDER BY COALESCE(m.MachineCode, ''), m.Id;"
                ),
                InsertSql = @"
                    INSERT INTO Machines
                    (
                        NamePt,
                        NameJp,
                        MachineCode,
                        MachineKey,
                        LineCode,
                        SectorId,
                        LocalId,
                        IsActive
                    )
                    VALUES
                    (
                        @namePt,
                        @nameJp,
                        @machineCode,
                        @machineKey,
                        @lineCode,
                        @sectorId,
                        @localId,
                        @isActive
                    );",
                UpdateSql = @"
                    UPDATE Machines
                    SET
                        NamePt = @namePt,
                        NameJp = @nameJp,
                        MachineCode = @machineCode,
                        MachineKey = @machineKey,
                        LineCode = @lineCode,
                        SectorId = @sectorId,
                        LocalId = @localId,
                        IsActive = @isActive
                    WHERE Id = @id;",
                DeleteSql = "DELETE FROM Machines WHERE Id = @id;",
                BuildParameters = (conn, values, currentId) =>
                {
                    var machineCode = ReadString(values, "machineCode").Trim();
                    var lineCode = ReadString(values, "lineCode").Trim();
                    var sectorId = ReadInt(values, "sectorId");
                    var localId = ReadInt(values, "localId");
                    var isActive = ReadBool(values, "isActive", true);
                    var namePt = ReadString(values, "namePt").Trim();
                    var nameJp = ReadString(values, "nameJp").Trim();

                    if (string.IsNullOrWhiteSpace(machineCode))
                        throw new InvalidOperationException(L("Informe o codigo da maquina.", "\u8a2d\u5099\u30b3\u30fc\u30c9\u3092\u5165\u529b\u3057\u3066\u304f\u3060\u3055\u3044\u3002"));

                    if (string.IsNullOrWhiteSpace(namePt))
                        namePt = machineCode;

                    if (string.IsNullOrWhiteSpace(nameJp))
                        nameJp = machineCode;

                    if (localId > 0)
                    {
                        var localSectorId = conn.ExecuteScalar<int?>(
                            @"SELECT SectorId FROM Locals WHERE Id = @id;",
                            new { id = localId });

                        if (!localSectorId.HasValue || localSectorId.Value <= 0)
                            throw new InvalidOperationException(L("O local selecionado nao foi encontrado.", "\u9078\u629e\u3057\u305f\u5834\u6240\u304c\u898b\u3064\u304b\u308a\u307e\u305b\u3093\u3002"));

                        sectorId = localSectorId.Value;
                    }

                    var machineKey = TeamOps.Data.Repositories.ProductionMachineRepository.BuildMachineKey(machineCode, lineCode);
                    var duplicateMachine = conn.ExecuteScalar<int>(
                        @"
                            SELECT COUNT(1)
                            FROM Machines
                            WHERE upper(trim(COALESCE(MachineKey, ''))) = upper(trim(@machineKey))
                              AND (@currentId IS NULL OR Id <> @currentId);",
                        new
                        {
                            machineKey,
                            currentId
                        }
                    ) > 0;

                    if (duplicateMachine)
                        throw new InvalidOperationException(L("Ja existe uma maquina com a mesma chave codigo + linha.", "\u540c\u3058\u8a2d\u5099\u30ad\u30fc\uff08\u30b3\u30fc\u30c9+\u30e9\u30a4\u30f3\uff09\u306e\u6a5f\u68b0\u304c\u65e2\u306b\u3042\u308a\u307e\u3059\u3002"));

                    var parameters = new DynamicParameters();
                    parameters.Add("@namePt", namePt);
                    parameters.Add("@nameJp", nameJp);
                    parameters.Add("@machineCode", machineCode);
                    parameters.Add("@machineKey", machineKey);
                    parameters.Add("@lineCode", string.IsNullOrWhiteSpace(lineCode) ? null : lineCode);
                    parameters.Add("@sectorId", sectorId > 0 ? sectorId : null);
                    parameters.Add("@localId", localId > 0 ? localId : null);
                    parameters.Add("@isActive", isActive ? 1 : 0);
                    if (currentId.HasValue)
                    {
                        parameters.Add("@id", currentId.Value);
                    }
                    return parameters;
                }
            };
        }

        private static AdminEntityDefinition CreateMachineStatusEntity()
        {
            return new AdminEntityDefinition
            {
                Key = "machine_status",
                Group = "production",
                TitlePt = "Status de Maquina",
                TitleJp = "Machine Status",
                DescriptionPt = "Status brutos importados dos arquivos de producao. Setor vazio significa status global/fallback.",
                DescriptionJp = "Production machine statuses. Empty sector means global fallback.",
                Fields =
                {
                    new AdminFieldDefinition("sectorId", "Setor (vazio = Global)", "Sector (empty = Global)", "select", false, "sectors"),
                    new AdminFieldDefinition("statusCode", "Codigo", "Code", "number", true),
                    new AdminFieldDefinition("displayCode", "Visual", "Visual", "select", true, "statusClasses"),
                    new AdminFieldDefinition("classification", "Regra eficiencia", "Efficiency rule", "text", true),
                    new AdminFieldDefinition("namePt", "Nome PT", "Name PT", "text", true),
                    new AdminFieldDefinition("nameJp", "Nome JP", "Name JP", "text", true),
                    new AdminFieldDefinition("colorHex", "Cor", "Color", "color", true),
                    new AdminFieldDefinition("textColorHex", "Cor do Texto", "Text Color", "color", true)
                },
                Columns =
                {
                    new AdminColumnDefinition("id", null, "ID", "ID"),
                    new AdminColumnDefinition("sectorDisplayPt", "sectorDisplayJp", "Setor", "Sector"),
                    new AdminColumnDefinition("statusCode", null, "Codigo", "Code"),
                    new AdminColumnDefinition("displayNamePt", "displayNameJp", "Visual", "Visual"),
                    new AdminColumnDefinition("classification", null, "Regra eficiencia", "Efficiency rule"),
                    new AdminColumnDefinition("namePt", "nameJp", "Nome", "Name"),
                    new AdminColumnDefinition("colorHex", null, "Cor", "Color")
                },
                QueryRows = conn => conn.Query(
                    @"
                        SELECT
                            ms.Id AS id,
                            COALESCE(ms.SectorId, 0) AS sectorId,
                            COALESCE(s.NamePt, 'Global') AS sectorDisplayPt,
                            COALESCE(NULLIF(s.NameJp, ''), s.NamePt, 'Global') AS sectorDisplayJp,
                            ms.StatusCode AS statusCode,
                            ms.DisplayCode AS displayCode,
                            COALESCE(ms.Classification, '') AS classification,
                            COALESCE(ms.NamePt, '') AS namePt,
                            COALESCE(ms.NameJp, '') AS nameJp,
                            COALESCE(ms.ColorHex, '') AS colorHex,
                            COALESCE(ms.TextColorHex, '') AS textColorHex,
                            CASE ms.DisplayCode
                                WHEN 0 THEN 'Rodando'
                                WHEN 1 THEN 'Inativo'
                                WHEN 3 THEN 'Parado'
                                WHEN 4 THEN 'Erro'
                                ELSE 'Inativo'
                            END AS displayNamePt,
                            CASE ms.DisplayCode
                                WHEN 0 THEN 'Running'
                                WHEN 1 THEN 'Inactive'
                                WHEN 3 THEN 'Stop'
                                WHEN 4 THEN 'Error'
                                ELSE 'Inactive'
                            END AS displayNameJp
                        FROM MachineStatuses ms
                        LEFT JOIN Sectors s ON s.Id = ms.SectorId
                        ORDER BY COALESCE(ms.SectorId, 0), ms.SortOrder, ms.StatusCode, ms.Id;"
                ),
                InsertSql = @"
                    INSERT INTO MachineStatuses
                    (
                        SectorId,
                        StatusCode,
                        DisplayCode,
                        Classification,
                        NamePt,
                        NameJp,
                        ColorHex,
                        TextColorHex,
                        SortOrder,
                        IsActive
                    )
                    VALUES
                    (
                        @sectorId,
                        @statusCode,
                        @displayCode,
                        @classification,
                        @namePt,
                        @nameJp,
                        @colorHex,
                        @textColorHex,
                        @sortOrder,
                        1
                    );",
                UpdateSql = @"
                    UPDATE MachineStatuses
                    SET
                        SectorId = @sectorId,
                        StatusCode = @statusCode,
                        DisplayCode = @displayCode,
                        Classification = @classification,
                        NamePt = @namePt,
                        NameJp = @nameJp,
                        ColorHex = @colorHex,
                        TextColorHex = @textColorHex,
                        SortOrder = @sortOrder
                    WHERE Id = @id;",
                DeleteSql = "DELETE FROM MachineStatuses WHERE Id = @id;",
                BuildParameters = (_, values, currentId) =>
                {
                    var sectorId = ReadInt(values, "sectorId");
                    var statusCode = ReadInt(values, "statusCode");
                    var displayCode = ReadInt(values, "displayCode");
                    var classification = NormalizeMachineStatusClassification(ReadString(values, "classification"));
                    var namePt = ReadString(values, "namePt").Trim();
                    var nameJp = ReadString(values, "nameJp").Trim();
                    var colorHex = NormalizeHexColor(ReadString(values, "colorHex"));
                    var textColorHex = NormalizeHexColor(ReadString(values, "textColorHex"));

                    if (statusCode < 0)
                        throw new InvalidOperationException(L("Informe um codigo de status valido.", "Invalid status code."));

                    if (displayCode != 0 && displayCode != 1 && displayCode != 3 && displayCode != 4)
                        throw new InvalidOperationException(L("Selecione uma classificacao visual valida.", "Invalid visual status."));

                    if (string.IsNullOrWhiteSpace(namePt) || string.IsNullOrWhiteSpace(nameJp))
                        throw new InvalidOperationException(L("Preencha os dois nomes do status.", "Fill both status names."));

                    var parameters = new DynamicParameters();
                    parameters.Add("@sectorId", sectorId > 0 ? sectorId : null);
                    parameters.Add("@statusCode", statusCode);
                    parameters.Add("@displayCode", displayCode);
                    parameters.Add("@classification", classification);
                    parameters.Add("@namePt", namePt);
                    parameters.Add("@nameJp", nameJp);
                    parameters.Add("@colorHex", colorHex);
                    parameters.Add("@textColorHex", textColorHex);
                    parameters.Add("@sortOrder", statusCode);
                    if (currentId.HasValue)
                    {
                        parameters.Add("@id", currentId.Value);
                    }
                    return parameters;
                }
            };
        }

        private static AdminEntityDefinition CreateMachineAuditEntity()
        {
            return new AdminEntityDefinition
            {
                Key = "machine_audit",
                Group = "production",
                ReadOnly = true,
                TitlePt = "Pendencias de Maquina",
                TitleJp = "設備の要確認",
                DescriptionPt = "Lista rapida para achar maquinas sem local, sem setor ou com vinculo inconsistente.",
                DescriptionJp = "場所未設定・セクター未設定・紐付け不整合の設備を確認する一覧です。",
                Columns =
                {
                    new AdminColumnDefinition("machineCode", null, "Codigo", "コード"),
                    new AdminColumnDefinition("lineCode", null, "Linha", "ライン"),
                    new AdminColumnDefinition("sectorNamePt", "sectorNameJp", "Setor da Maquina", "設備セクター"),
                    new AdminColumnDefinition("localDisplayPt", "localDisplayJp", "Local", "場所"),
                    new AdminColumnDefinition("issuePt", "issueJp", "Pendencia", "要確認")
                },
                QueryRows = conn => conn.Query(
                    @"
                        SELECT
                            COALESCE(m.SectorId, 0) AS sectorId,
                            COALESCE(m.MachineCode, '') AS machineCode,
                            COALESCE(m.LineCode, '') AS lineCode,
                            COALESCE(s.NamePt, '') AS sectorNamePt,
                            COALESCE(NULLIF(s.NameJp, ''), s.NamePt, '') AS sectorNameJp,
                            CASE
                                WHEN COALESCE(l.Id, 0) = 0 THEN ''
                                ELSE COALESCE(NULLIF(l.ShortCode, ''), NULLIF(l.NamePt, ''), NULLIF(l.NameJp, ''), 'L' || l.Id) || ' - ' || COALESCE(l.NamePt, '')
                            END AS localDisplayPt,
                            CASE
                                WHEN COALESCE(l.Id, 0) = 0 THEN ''
                                ELSE COALESCE(NULLIF(l.ShortCode, ''), NULLIF(l.NamePt, ''), NULLIF(l.NameJp, ''), 'L' || l.Id) || ' - ' || COALESCE(NULLIF(l.NameJp, ''), l.NamePt, '')
                            END AS localDisplayJp,
                            CASE
                                WHEN COALESCE(m.LocalId, 0) = 0 THEN 'Maquina sem LocalId'
                                WHEN COALESCE(m.SectorId, 0) = 0 THEN 'Maquina sem SectorId'
                                WHEN COALESCE(l.Id, 0) = 0 THEN 'Local vinculado nao existe mais'
                                WHEN COALESCE(l.SectorId, 0) <> COALESCE(m.SectorId, 0) THEN 'SectorId da maquina diferente do setor do local'
                                ELSE ''
                            END AS issuePt,
                            CASE
                                WHEN COALESCE(m.LocalId, 0) = 0 THEN 'LocalId 未設定'
                                WHEN COALESCE(m.SectorId, 0) = 0 THEN 'SectorId 未設定'
                                WHEN COALESCE(l.Id, 0) = 0 THEN '紐付け先の場所が存在しません'
                                WHEN COALESCE(l.SectorId, 0) <> COALESCE(m.SectorId, 0) THEN '設備の SectorId と場所のセクターが一致しません'
                                ELSE ''
                            END AS issueJp
                        FROM Machines m
                        LEFT JOIN Sectors s ON s.Id = m.SectorId
                        LEFT JOIN Locals l ON l.Id = m.LocalId
                        WHERE COALESCE(m.LocalId, 0) = 0
                           OR COALESCE(m.SectorId, 0) = 0
                           OR COALESCE(l.Id, 0) = 0
                           OR COALESCE(l.SectorId, 0) <> COALESCE(m.SectorId, 0)
                        ORDER BY COALESCE(m.MachineCode, ''), COALESCE(m.LineCode, ''), m.Id;"
                ),
                InsertSql = string.Empty,
                UpdateSql = string.Empty,
                DeleteSql = string.Empty,
                BuildParameters = (_, _, _) => throw new InvalidOperationException(L("Esta lista e somente leitura.", "この一覧は閲覧のみです。"))
            };
        }

        private static AdminEntityDefinition CreateShainEntity()
        {
            return new AdminEntityDefinition
            {
                Key = "shain",
                Group = "people",
                TitlePt = "Shain",
                TitleJp = "\u793e\u54e1",
                DescriptionPt = "Base simples de nomes romanji e nihongo para referencias internas.",
                DescriptionJp = "\u30ed\u30fc\u30de\u5b57\u540d\u3068\u65e5\u672c\u8a9e\u540d\u3092\u4fdd\u6301\u3059\u308b\u7c21\u6613\u30de\u30b9\u30bf\u3067\u3059\u3002",
                Fields =
                {
                    new AdminFieldDefinition("nameRomanji", "Nome Romanji", "\u30ed\u30fc\u30de\u5b57\u540d", "text", true),
                    new AdminFieldDefinition("nameNihongo", "Nome Nihongo", "\u65e5\u672c\u8a9e\u540d", "text", true)
                },
                Columns =
                {
                    new AdminColumnDefinition("id", null, "ID", "ID"),
                    new AdminColumnDefinition("nameRomanji", "nameNihongo", "Romanji", "\u30ed\u30fc\u30de\u5b57"),
                    new AdminColumnDefinition("nameNihongo", null, "Nihongo", "\u65e5\u672c\u8a9e")
                },
                QueryRows = conn => conn.Query(
                    @"
                        SELECT
                            Id AS id,
                            COALESCE(NameRomanji, '') AS nameRomanji,
                            COALESCE(NameNihongo, '') AS nameNihongo
                        FROM Shain
                        ORDER BY NameRomanji;"
                ),
                InsertSql = "INSERT INTO Shain (NameRomanji, NameNihongo) VALUES (@nameRomanji, @nameNihongo);",
                UpdateSql = "UPDATE Shain SET NameRomanji = @nameRomanji, NameNihongo = @nameNihongo WHERE Id = @id;",
                DeleteSql = "DELETE FROM Shain WHERE Id = @id;",
                BuildParameters = (_, values, currentId) =>
                {
                    var nameRomanji = ReadString(values, "nameRomanji").Trim();
                    var nameNihongo = ReadString(values, "nameNihongo").Trim();

                    if (string.IsNullOrWhiteSpace(nameRomanji) || string.IsNullOrWhiteSpace(nameNihongo))
                        throw new InvalidOperationException(L("Preencha os dois nomes do shain.", "\u793e\u54e1\u540d\u3092\u4e21\u65b9\u5165\u529b\u3057\u3066\u304f\u3060\u3055\u3044\u3002"));

                    var parameters = new DynamicParameters();
                    parameters.Add("@nameRomanji", nameRomanji);
                    parameters.Add("@nameNihongo", nameNihongo);
                    if (currentId.HasValue)
                    {
                        parameters.Add("@id", currentId.Value);
                    }
                    return parameters;
                }
            };
        }

        private static AdminEntityDefinition CreateSystemLogEntity()
        {
            return new AdminEntityDefinition
            {
                Key = "system_log",
                Group = "misc",
                ReadOnly = true,
                TitlePt = "Log do Sistema",
                TitleJp = "\u30b7\u30b9\u30c6\u30e0\u30ed\u30b0",
                DescriptionPt = "Historico de eventos gravados pelo sistema, somente para consulta.",
                DescriptionJp = "\u30b7\u30b9\u30c6\u30e0\u304c\u8a18\u9332\u3057\u305f\u5c65\u6b74\u3092\u95b2\u89a7\u3059\u308b\u305f\u3081\u306e\u4e00\u89a7\u3067\u3059\u3002",
                Columns =
                {
                    new AdminColumnDefinition("timestamp", null, "Data/Hora", "\u65e5\u6642"),
                    new AdminColumnDefinition("userFJ", null, "FJ", "FJ"),
                    new AdminColumnDefinition("module", null, "Modulo", "\u30e2\u30b8\u30e5\u30fc\u30eb"),
                    new AdminColumnDefinition("action", null, "Acao", "\u64cd\u4f5c"),
                    new AdminColumnDefinition("targetId", null, "Target", "\u5bfe\u8c61"),
                    new AdminColumnDefinition("details", null, "Detalhes", "\u8a73\u7d30")
                },
                QueryRows = conn => conn.ExecuteScalar<int>(
                        @"SELECT COUNT(1)
                          FROM sqlite_master
                          WHERE type = 'table'
                            AND name = 'SystemLog';"
                    ) > 0
                        ? conn.Query(
                            @"
                                SELECT
                                    COALESCE(Timestamp, '') AS timestamp,
                                    COALESCE(UserFJ, '') AS userFJ,
                                    COALESCE(Module, '') AS module,
                                    COALESCE(Action, '') AS action,
                                    COALESCE(CAST(TargetId AS TEXT), '') AS targetId,
                                    COALESCE(Details, '') AS details
                                FROM SystemLog
                                ORDER BY Timestamp DESC, Id DESC
                                LIMIT 500;"
                        )
                        : Array.Empty<object>(),
                InsertSql = string.Empty,
                UpdateSql = string.Empty,
                DeleteSql = string.Empty,
                BuildParameters = (_, _, _) => throw new InvalidOperationException(L("O log do sistema e somente leitura.", "\u30b7\u30b9\u30c6\u30e0\u30ed\u30b0\u306f\u95b2\u89a7\u306e\u307f\u3067\u3059\u3002"))
            };
        }

        private void PostJson(object payload)
        {
            var json = JsonSerializer.Serialize(payload);
            webViewAdmin.CoreWebView2.PostWebMessageAsJson(json);
        }

        private static string ReadString(JsonElement root, string propertyName)
        {
            return root.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
                ? prop.GetString() ?? string.Empty
                : string.Empty;
        }

        private static int ReadInt(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var prop))
                return 0;

            return prop.ValueKind switch
            {
                JsonValueKind.Number => prop.GetInt32(),
                JsonValueKind.String when int.TryParse(prop.GetString(), out var parsed) => parsed,
                _ => 0
            };
        }

        private static bool ReadBool(JsonElement root, string propertyName, bool defaultValue = false)
        {
            if (!root.TryGetProperty(propertyName, out var prop))
                return defaultValue;

            return prop.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number => prop.GetInt32() != 0,
                JsonValueKind.String when bool.TryParse(prop.GetString(), out var parsed) => parsed,
                JsonValueKind.String when int.TryParse(prop.GetString(), out var parsedInt) => parsedInt != 0,
                _ => defaultValue
            };
        }

        private static double ReadDouble(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var prop))
                return 0;

            return prop.ValueKind switch
            {
                JsonValueKind.Number => prop.GetDouble(),
                JsonValueKind.String when double.TryParse(prop.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed) => parsed,
                JsonValueKind.String when double.TryParse(prop.GetString(), out var parsedLocal) => parsedLocal,
                _ => 0
            };
        }

        private static SystemLogFilters ReadSystemLogFilters(JsonElement root)
        {
            if (!root.TryGetProperty("filters", out var filters) || filters.ValueKind != JsonValueKind.Object)
            {
                return GetDefaultSystemLogFilters();
            }

            return new SystemLogFilters(
                ReadString(filters, "dateStart").Trim(),
                ReadString(filters, "dateEnd").Trim(),
                ReadString(filters, "userFJ").Trim(),
                ReadString(filters, "module").Trim(),
                ReadString(filters, "action").Trim());
        }

        private static SystemLogFilters GetDefaultSystemLogFilters()
        {
            return new SystemLogFilters(
                (DateTime.Today.AddDays(-30)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                string.Empty,
                string.Empty,
                string.Empty);
        }

        private static DateTime ParseDateOrDefault(string? value, DateTime fallback)
        {
            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                ? parsed.Date
                : fallback.Date;
        }

        private sealed record SystemLogFilters(
            string DateStart,
            string DateEnd,
            string UserFJ,
            string Module,
            string Action);

        private static string L(string pt, string jp)
        {
            return string.Equals(Program.CurrentLocale, "ja-JP", StringComparison.OrdinalIgnoreCase)
                ? jp
                : pt;
        }

        private static string NormalizeHexColor(string value)
        {
            var normalized = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
                return "#FFFFFF";

            if (!normalized.StartsWith("#", StringComparison.Ordinal))
                normalized = "#" + normalized;

            return normalized.Length == 7 ? normalized.ToUpperInvariant() : "#FFFFFF";
        }

        private static string NormalizeMachineStatusClassification(string value)
        {
            var normalized = (value ?? string.Empty).Trim();

            if (normalized.Equals("Running", StringComparison.OrdinalIgnoreCase))
                return "Running";

            if (normalized.Equals("StopCounts", StringComparison.OrdinalIgnoreCase))
                return "StopCounts";

            if (normalized.Equals("StopNoCount", StringComparison.OrdinalIgnoreCase))
                return "StopNoCount";

            if (normalized.Equals("Error", StringComparison.OrdinalIgnoreCase))
                return "Error";

            throw new InvalidOperationException(L(
                "Informe uma regra de eficiencia valida: Running, StopCounts, StopNoCount ou Error.",
                "Invalid efficiency rule: Running, StopCounts, StopNoCount or Error."));
        }

        private static string NormalizeProcedureCode(string value)
        {
            var normalized = (value ?? string.Empty).Trim().ToUpperInvariant();

            if (normalized.Equals("ECII", StringComparison.OrdinalIgnoreCase))
                return "ECII";

            if (normalized.Equals("BUNKATSU", StringComparison.OrdinalIgnoreCase))
                return "BUNKATSU";

            if (normalized.Equals("DCS", StringComparison.OrdinalIgnoreCase))
                return "DCS";

            throw new InvalidOperationException(L(
                "Informe um procedimento valido: ECII, BUNKATSU ou DCS.",
                "Invalid procedure: ECII, BUNKATSU or DCS."));
        }

        private sealed class AdminEntityDefinition
        {
            public string Key { get; set; } = string.Empty;
            public string Group { get; set; } = string.Empty;
            public bool ReadOnly { get; set; }
            public AdminEntityScope Scope { get; set; } = AdminEntityScope.Global;
            public string TitlePt { get; set; } = string.Empty;
            public string TitleJp { get; set; } = string.Empty;
            public string DescriptionPt { get; set; } = string.Empty;
            public string DescriptionJp { get; set; } = string.Empty;
            public List<AdminFieldDefinition> Fields { get; } = new();
            public List<AdminColumnDefinition> Columns { get; } = new();
            public Func<IDbConnection, IEnumerable<object>> QueryRows { get; set; } = _ => Array.Empty<object>();
            public string InsertSql { get; set; } = string.Empty;
            public string UpdateSql { get; set; } = string.Empty;
            public string DeleteSql { get; set; } = string.Empty;
            public Func<IDbConnection, JsonElement, int?, DynamicParameters> BuildParameters { get; set; } = (_, _, _) => new DynamicParameters();
        }

        private enum AdminEntityScope
        {
            Global,
            Sector,
            SectorOrGlobal
        }

        private sealed class AdminFieldDefinition
        {
            public AdminFieldDefinition(string key, string labelPt, string labelJp, string type, bool required, string? lookupKey = null)
            {
                Key = key;
                LabelPt = labelPt;
                LabelJp = labelJp;
                Type = type;
                Required = required;
                LookupKey = lookupKey;
            }

            public string Key { get; }
            public string LabelPt { get; }
            public string LabelJp { get; }
            public string Type { get; }
            public bool Required { get; }
            public string? LookupKey { get; }
        }

        private sealed class AdminColumnDefinition
        {
            public AdminColumnDefinition(string key, string? jpKey, string labelPt, string labelJp)
            {
                Key = key;
                JpKey = jpKey;
                LabelPt = labelPt;
                LabelJp = labelJp;
            }

            public string Key { get; }
            public string? JpKey { get; }
            public string LabelPt { get; }
            public string LabelJp { get; }
        }
    }
}
