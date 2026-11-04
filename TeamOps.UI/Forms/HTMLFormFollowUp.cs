using Microsoft.Web.WebView2.Core;
using System;
using System.Drawing;
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
    public partial class HTMLFormFollowUp : Form
    {
        private readonly SqliteConnectionFactory _factory;
        private readonly User _user;
        private readonly Operator _currentOperator;
        private readonly FollowUpRepository _followUpRepo;
        private readonly ShiftRepository _shiftRepo;
        private readonly OperatorRepository _operatorRepo;
        private readonly FollowUpReasonRepository _reasonRepo;
        private readonly FollowUpTypeRepository _typeRepo;
        private readonly LocalRepository _localRepo;
        private readonly EquipmentRepository _equipmentRepo;
        private readonly SectorRepository _sectorRepo;
        private readonly AuthorizationService _authorization;

        public HTMLFormFollowUp(
            SqliteConnectionFactory factory,
            User user,
            Operator currentOperator)
        {
            InitializeComponent();
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Text = L("Cadastro de acompanhamento", "\u30d5\u30a9\u30ed\u30fc\u767b\u9332");

            _factory = factory;
            _user = user;
            _currentOperator = currentOperator;

            _followUpRepo = new FollowUpRepository(_factory);
            _shiftRepo = new ShiftRepository(_factory);
            _operatorRepo = new OperatorRepository(_factory);
            _reasonRepo = new FollowUpReasonRepository(_factory);
            _typeRepo = new FollowUpTypeRepository(_factory);
            _localRepo = new LocalRepository(_factory);
            _equipmentRepo = new EquipmentRepository(_factory);
            _sectorRepo = new SectorRepository(_factory);
            _authorization = new AuthorizationService(_factory);

            InitializeWebView();
        }

        private async void InitializeWebView()
        {
            await webViewFollowUp.EnsureCoreWebView2Async(null);

            var core = webViewFollowUp.CoreWebView2;
            core.Settings.IsWebMessageEnabled = true;
            core.Settings.AreDefaultScriptDialogsEnabled = true;
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.Settings.AreDevToolsEnabled = true;

            core.WebMessageReceived += WebMessageReceived;

            core.SetVirtualHostNameToFolderMapping(
                "app",
                Path.Combine(Application.StartupPath, "ui", "follow-up"),
                CoreWebView2HostResourceAccessKind.Allow
            );

            core.SetVirtualHostNameToFolderMapping(
                "assets",
                Path.Combine(Application.StartupPath, "Assets"),
                CoreWebView2HostResourceAccessKind.Allow
            );

            core.Navigate("https://app/index.html");
        }

        private void WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var msg = JsonSerializer.Deserialize<JsRequest>(e.WebMessageAsJson);
                if (msg == null)
                    return;

                switch (msg.action)
                {
                    case "load":
                        LoadInitial();
                        break;

                    case "save":
                        SaveFollowUp(msg);
                        break;

                    case "cancel":
                        BeginInvoke(new Action(Close));
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
            _authorization.Demand(_user.Id, ModuleCodes.FollowUp, PermissionActions.View);
            var context = _authorization.GetContext(_user.Id);
            var authorizedSectorIds = context.AuthorizedSectorIds;

            var shifts = _shiftRepo.GetAll()
                .Select(x => new
                {
                    id = x.Id,
                    namePt = x.NamePt,
                    nameJp = string.IsNullOrWhiteSpace(x.NameJp) ? x.NamePt : x.NameJp
                })
                .ToList();

            var sectors = _sectorRepo.GetAll()
                .Where(x => authorizedSectorIds.Contains(x.Id))
                .Select(x => new
                {
                    id = x.Id,
                    namePt = x.NamePt,
                    nameJp = string.IsNullOrWhiteSpace(x.NameJp) ? x.NamePt : x.NameJp
                })
                .ToList();

            var reasons = _reasonRepo.GetAll()
                .Select(x => new
                {
                    id = x.Id,
                    namePt = x.NamePt,
                    nameJp = string.IsNullOrWhiteSpace(x.NameJp) ? x.NamePt : x.NameJp
                })
                .ToList();

            var types = _typeRepo.GetAll()
                .Select(x => new
                {
                    id = x.Id,
                    namePt = x.NamePt,
                    nameJp = string.IsNullOrWhiteSpace(x.NameJp) ? x.NamePt : x.NameJp
                })
                .ToList();

            var locals = _localRepo.GetAll()
                .Where(x => authorizedSectorIds.Contains(x.SectorId))
                .Select(x => new
                {
                    id = x.Id,
                    namePt = x.NamePt,
                    nameJp = string.IsNullOrWhiteSpace(x.NameJp) ? x.NamePt : x.NameJp,
                    sectorId = x.SectorId
                })
                .ToList();

            var equipments = _equipmentRepo.GetAll()
                .Select(x => new
                {
                    id = x.Id,
                    namePt = x.NamePt,
                    nameJp = string.IsNullOrWhiteSpace(x.NameJp) ? x.NamePt : x.NameJp
                })
                .ToList();

            var operators = _operatorRepo.GetAll()
                .Where(x => x.Status && authorizedSectorIds.Contains(x.SectorId))
                .Select(x => new
                {
                    codigoFJ = x.CodigoFJ,
                    namePt = string.IsNullOrWhiteSpace(x.NameRomanji) ? x.CodigoFJ : x.NameRomanji,
                    nameJp = string.IsNullOrWhiteSpace(x.NameNihongo)
                        ? (string.IsNullOrWhiteSpace(x.NameRomanji) ? x.CodigoFJ : x.NameRomanji)
                        : x.NameNihongo,
                    shiftId = x.ShiftId,
                    sectorId = x.SectorId,
                    isLeader = x.IsLeader
                })
                .ToList();

            PostJson(new
            {
                type = "init",
                locale = Program.CurrentLocale,
                data = new
                {
                    header = new
                    {
                        title = L("Cadastro de acompanhamento", "\u30d5\u30a9\u30ed\u30fc\u767b\u9332"),
                        subtitle = L(
                            "Registro rapido de erros, orientacoes e observacoes do operador.",
                            "\u4f5c\u696d\u8005\u306e\u30a8\u30e9\u30fc\u3001\u6307\u5c0e\u3001\u6c17\u4ed8\u304d\u3092\u7c21\u5358\u306b\u8a18\u9332\u3057\u307e\u3059\u3002")
                    },
                    lookups = new
                    {
                        shifts,
                        sectors,
                        reasons,
                        types,
                        locals,
                        equipments,
                        operators
                    },
                    defaults = new
                    {
                        date = DateTime.Today.ToString("yyyy-MM-dd"),
                        shiftId = _currentOperator.ShiftId,
                        sectorId = Program.CurrentSession?.SelectedSectorId
                            ?? (authorizedSectorIds.Contains(_currentOperator.SectorId)
                                ? _currentOperator.SectorId
                                : authorizedSectorIds.FirstOrDefault()),
                        executorCodigoFJ = _currentOperator.CodigoFJ,
                        executorNamePt = string.IsNullOrWhiteSpace(_currentOperator.NameRomanji)
                            ? _currentOperator.CodigoFJ
                            : _currentOperator.NameRomanji,
                        executorNameJp = string.IsNullOrWhiteSpace(_currentOperator.NameNihongo)
                            ? (string.IsNullOrWhiteSpace(_currentOperator.NameRomanji)
                                ? _currentOperator.CodigoFJ
                                : _currentOperator.NameRomanji)
                            : _currentOperator.NameNihongo,
                        creatorNamePt = _user.Name,
                        creatorNameJp = _user.Name,
                        currentOperatorNamePt = string.IsNullOrWhiteSpace(_currentOperator.NameRomanji)
                            ? _currentOperator.CodigoFJ
                            : _currentOperator.NameRomanji,
                        currentOperatorNameJp = string.IsNullOrWhiteSpace(_currentOperator.NameNihongo)
                            ? (string.IsNullOrWhiteSpace(_currentOperator.NameRomanji)
                                ? _currentOperator.CodigoFJ
                                : _currentOperator.NameRomanji)
                            : _currentOperator.NameNihongo,
                        currentOperatorCodigo = _currentOperator.CodigoFJ
                    },
                    logoUrl = "https://assets/logo_rodape.png"
                }
            });
        }

        private void SaveFollowUp(JsRequest msg)
        {
            var validationError = ValidatePayload(msg);
            if (!string.IsNullOrWhiteSpace(validationError))
            {
                PostJson(new
                {
                    type = "error",
                    message = validationError
                });
                return;
            }

            _authorization.Demand(_user.Id, ModuleCodes.FollowUp, PermissionActions.Create, msg.sectorId);
            var selectedOperator = _operatorRepo.GetAll()
                .FirstOrDefault(item => string.Equals(
                    item.CodigoFJ,
                    msg.operatorCodigoFJ.Trim(),
                    StringComparison.OrdinalIgnoreCase));
            if (selectedOperator == null || !selectedOperator.Status || selectedOperator.SectorId != msg.sectorId)
                throw new InvalidOperationException(L(
                    "O operador selecionado nao pertence ao setor informado.",
                    "選択した作業者は指定したセクターに所属していません。"));

            var selectedLocal = _localRepo.GetAll().FirstOrDefault(item => item.Id == msg.localId);
            if (selectedLocal == null || selectedLocal.SectorId != msg.sectorId)
                throw new InvalidOperationException(L(
                    "O local selecionado nao pertence ao setor informado.",
                    "選択した場所は指定したセクターに属していません。"));

            var now = DateTime.Now;
            var date = now;
            if (!string.IsNullOrWhiteSpace(msg.date) &&
                DateTime.TryParse(msg.date, out var parsedDate))
            {
                date = parsedDate.Date.Add(now.TimeOfDay);
            }

            var followUp = new FollowUp
            {
                Date = date,
                ShiftId = msg.shiftId,
                OperatorCodigoFJ = msg.operatorCodigoFJ.Trim(),
                ExecutorCodigoFJ = msg.executorCodigoFJ.Trim(),
                WitnessCodigoFJ = string.IsNullOrWhiteSpace(msg.witnessCodigoFJ)
                    ? null
                    : msg.witnessCodigoFJ.Trim(),
                ReasonId = msg.reasonId,
                TypeId = msg.typeId,
                LocalId = msg.localId,
                EquipmentId = msg.equipmentId,
                SectorId = msg.sectorId,
                Description = msg.description.Trim(),
                Guidance = msg.guidance.Trim()
            };

            var newId = _followUpRepo.Add(followUp);

            PostJson(new
            {
                type = "saved",
                data = new
                {
                    id = newId,
                    message = L(
                        "Acompanhamento salvo com sucesso.",
                        "\u30d5\u30a9\u30ed\u30fc\u3092\u4fdd\u5b58\u3057\u307e\u3057\u305f\u3002")
                }
            });
        }

        private static string ValidatePayload(JsRequest msg)
        {
            if (msg.shiftId <= 0)
                return L("Selecione o turno.", "\u30b7\u30d5\u30c8\u3092\u9078\u629e\u3057\u3066\u304f\u3060\u3055\u3044\u3002");

            if (msg.sectorId <= 0)
                return L("Selecione o setor.", "\u30bb\u30af\u30bf\u30fc\u3092\u9078\u629e\u3057\u3066\u304f\u3060\u3055\u3044\u3002");

            if (string.IsNullOrWhiteSpace(msg.operatorCodigoFJ))
                return L("Selecione o operador.", "\u4f5c\u696d\u8005\u3092\u9078\u629e\u3057\u3066\u304f\u3060\u3055\u3044\u3002");

            if (string.IsNullOrWhiteSpace(msg.executorCodigoFJ))
                return L("Selecione o executor.", "\u5b9f\u65bd\u8005\u3092\u9078\u629e\u3057\u3066\u304f\u3060\u3055\u3044\u3002");

            if (msg.reasonId <= 0)
                return L("Selecione o motivo.", "\u7406\u7531\u3092\u9078\u629e\u3057\u3066\u304f\u3060\u3055\u3044\u3002");

            if (msg.typeId <= 0)
                return L("Selecione o tipo.", "\u7a2e\u5225\u3092\u9078\u629e\u3057\u3066\u304f\u3060\u3055\u3044\u3002");

            if (msg.localId <= 0)
                return L("Selecione o local.", "\u5834\u6240\u3092\u9078\u629e\u3057\u3066\u304f\u3060\u3055\u3044\u3002");

            if (msg.equipmentId <= 0)
                return L("Selecione o equipamento.", "\u8a2d\u5099\u3092\u9078\u629e\u3057\u3066\u304f\u3060\u3055\u3044\u3002");

            if (string.IsNullOrWhiteSpace(msg.description))
                return L(
                    "Digite a descricao.",
                    "\u5185\u5bb9\u3092\u5165\u529b\u3057\u3066\u304f\u3060\u3055\u3044\u3002");

            if (string.IsNullOrWhiteSpace(msg.guidance))
                return L(
                    "Digite a orientacao.",
                    "\u6307\u5c0e\u5185\u5bb9\u3092\u5165\u529b\u3057\u3066\u304f\u3060\u3055\u3044\u3002");

            return "";
        }

        private static string L(string pt, string jp)
        {
            return string.Equals(Program.CurrentLocale, "ja-JP", StringComparison.OrdinalIgnoreCase)
                ? jp
                : pt;
        }

        private void PostJson(object payload)
        {
            var json = JsonSerializer.Serialize(payload);

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() =>
                {
                    if (webViewFollowUp.CoreWebView2 != null)
                        webViewFollowUp.CoreWebView2.PostWebMessageAsJson(json);
                }));
                return;
            }

            if (webViewFollowUp.CoreWebView2 != null)
                webViewFollowUp.CoreWebView2.PostWebMessageAsJson(json);
        }
    }
}
