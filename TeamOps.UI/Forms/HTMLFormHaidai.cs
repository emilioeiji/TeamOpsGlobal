using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using TeamOps.Core.Entities;
using TeamOps.Core.Security;
using TeamOps.Data.Db;
using TeamOps.Data.Security;
using TeamOps.UI.Services;

namespace TeamOps.UI.Forms
{
    public sealed class HTMLFormHaidai : Form
    {
        private readonly User _currentUser;
        private readonly Operator _currentOperator;
        private readonly HaidaiModuleService _service;
        private readonly AuthorizationService _authorization;
        private readonly Microsoft.Web.WebView2.WinForms.WebView2 _webView;

        public HTMLFormHaidai(
            SqliteConnectionFactory factory,
            User currentUser,
            Operator currentOperator)
        {
            _currentUser = currentUser;
            _currentOperator = currentOperator;
            _service = new HaidaiModuleService(factory);
            _authorization = new AuthorizationService(factory);

            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Text = L("Haidai", "Haidai");
            Width = 1480;
            Height = 940;
            StartPosition = FormStartPosition.CenterParent;

            _webView = new Microsoft.Web.WebView2.WinForms.WebView2
            {
                Dock = DockStyle.Fill
            };

            Controls.Add(_webView);
            InitializeWebView();
        }

        private async void InitializeWebView()
        {
            _service.EnsureSchema();

            await _webView.EnsureCoreWebView2Async(null);

            var core = _webView.CoreWebView2;
            core.Settings.IsWebMessageEnabled = true;
            core.Settings.AreDefaultScriptDialogsEnabled = true;
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.Settings.AreDevToolsEnabled = true;
            core.WebMessageReceived += WebMessageReceived;

            core.SetVirtualHostNameToFolderMapping(
                "app",
                Path.Combine(Application.StartupPath, "ui", "haidai"),
                CoreWebView2HostResourceAccessKind.Allow);

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
                        SendInit();
                        break;

                    case "load_month_plan":
                        SendMonthlyPlan(
                            ReadInt(root, "year", DateTime.Today.Year),
                            ReadInt(root, "month", DateTime.Today.Month),
                            ReadInt(root, "shiftId"),
                            ReadInt(root, "sectorId"));
                        break;

                    case "refresh":
                        SendBoard(ReadDate(root, "date"), ReadInt(root, "shiftId"), ReadInt(root, "sectorId"));
                        break;

                    case "save_month_plan":
                        SaveMonthlyPlan(root);
                        break;

                    case "save_assignment":
                        SaveAssignment(root);
                        break;

                    case "mark_exception":
                        MarkException(root);
                        break;

                    case "clear_exception":
                        var clearDate = ReadDate(root, "date");
                        var clearShiftId = ReadInt(root, "shiftId");
                        var clearSectorId = ReadInt(root, "sectorId");
                        Demand(PermissionActions.Edit, clearSectorId);
                        _service.ClearException(clearDate, ReadString(root, "operatorCodigoFJ"));
                        SendBoard(clearDate, clearShiftId, clearSectorId);
                        SendMonthlyPlan(clearDate.Year, clearDate.Month, clearShiftId, clearSectorId);
                        PostNotify(L("Excecao removida.", "Exception cleared."));
                        break;

                    case "register_movement":
                        RegisterMovement(root);
                        break;

                    case "delete_movement":
                        DeleteMovement(root);
                        break;

                    case "restore_lineup":
                        var restoreDate = ReadDate(root, "date");
                        var restoreShiftId = ReadInt(root, "shiftId");
                        var restoreSectorId = ReadInt(root, "sectorId");
                        Demand(PermissionActions.Edit, restoreSectorId);
                        _service.RestoreLineup(
                            restoreDate,
                            restoreShiftId,
                            restoreSectorId,
                            ReadString(root, "operatorCodigoFJ"));
                        SendBoard(restoreDate, restoreShiftId, restoreSectorId);
                        SendMonthlyPlan(restoreDate.Year, restoreDate.Month, restoreShiftId, restoreSectorId);
                        PostNotify(L("Operador recolocado na linha.", "Operator restored to lineup."));
                        break;

                    case "export_html":
                        ExportHtml(root);
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

        private void SendInit()
        {
            _authorization.Demand(_currentUser.Id, ModuleCodes.Haidai, PermissionActions.View);
            var authorizedSectorIds = _authorization.GetContext(_currentUser.Id).AuthorizedSectorIds;
            var defaultSectorId = Program.CurrentSession?.SelectedSectorId
                ?? (authorizedSectorIds.Contains(_currentOperator.SectorId)
                    ? _currentOperator.SectorId
                    : authorizedSectorIds.FirstOrDefault());
            var init = _service.GetInitialPayload(_currentOperator.ShiftId, defaultSectorId);

            PostJson(new
            {
                type = "init",
                data = new
                {
                    locale = Program.CurrentLocale,
                    currentUser = _currentUser.Name,
                    currentOperatorName = _currentOperator.NameRomanji,
                    defaults = new
                    {
                        dateIso = init.DateIso,
                        monthIso = init.DateIso.Length >= 7 ? init.DateIso[..7] : DateTime.Today.ToString("yyyy-MM"),
                        shiftId = init.ShiftId,
                        sectorId = init.SectorId
                    },
                    shifts = init.Shifts.Select(item => new { id = item.Id, name = item.Name }),
                    sectors = init.Sectors
                        .Where(item => authorizedSectorIds.Contains(item.Id))
                        .Select(item => new { id = item.Id, name = item.Name }),
                    locals = init.Locals
                        .Where(item => authorizedSectorIds.Contains(item.SectorId))
                        .Select(item => new
                    {
                        id = item.Id,
                        sectorId = item.SectorId,
                        name = item.Name,
                        shortCode = item.ShortCode
                    }),
                    operators = init.Operators
                        .Where(item => authorizedSectorIds.Contains(item.SectorId))
                        .Select(item => new
                    {
                        codigoFJ = item.CodigoFJ,
                        name = item.Name,
                        sectorId = item.SectorId,
                        shiftId = item.ShiftId,
                        groupId = item.GroupId,
                        groupName = item.GroupName,
                        trainer = item.Trainer,
                        isLeader = item.IsLeader
                    })
                }
            });
        }

        private void SendMonthlyPlan(int year, int month, int shiftId, int sectorId)
        {
            if (shiftId <= 0)
            {
                throw new InvalidOperationException(L("Selecione um turno valido.", "Valid shift required."));
            }

            if (sectorId <= 0)
            {
                throw new InvalidOperationException(L("Selecione um setor valido.", "Valid sector required."));
            }

            Demand(PermissionActions.View, sectorId);

            var plan = _service.GetMonthlyPlan(year, month, shiftId, sectorId);

            PostJson(new
            {
                type = "month_plan",
                data = new
                {
                    year = plan.Year,
                    month = plan.Month,
                    monthIso = $"{plan.Year:0000}-{plan.Month:00}",
                    shiftId = plan.ShiftId,
                    sectorId = plan.SectorId,
                    days = plan.Days,
                    groups = plan.Groups.Select(group => new
                    {
                        groupId = group.GroupId,
                        groupName = group.GroupName,
                        operatorCount = group.Operators.Count,
                        operators = group.Operators.Select(op => new
                        {
                            codigoFJ = op.CodigoFJ,
                            name = op.Name,
                            nameJp = op.NameJp,
                            groupId = op.GroupId,
                            groupName = op.GroupName,
                            cells = op.Cells.Select(cell => new
                            {
                                day = cell.Day,
                                assignmentCode = cell.AssignmentCode,
                                localId = cell.LocalId,
                                isTrainee = cell.IsTrainee,
                                isLineupActive = cell.IsLineupActive,
                                isHolidayWork = cell.IsHolidayWork,
                                status = cell.Status
                            })
                        })
                    })
                }
            });
        }

        private void SendBoard(DateTime date, int shiftId, int sectorId)
        {
            if (shiftId <= 0)
            {
                throw new InvalidOperationException(L("Selecione um turno valido.", "Valid shift required."));
            }

            if (sectorId <= 0)
            {
                throw new InvalidOperationException(L("Selecione um setor valido.", "Valid sector required."));
            }

            Demand(PermissionActions.View, sectorId);

            var board = _service.GetBoard(date, shiftId, sectorId);

            PostJson(new
            {
                type = "board",
                data = new
                {
                    dateIso = board.DateIso,
                    shiftId = board.ShiftId,
                    sectorId = board.SectorId,
                    summary = new
                    {
                        operatorCount = board.Summary.OperatorCount,
                        assignedCount = board.Summary.AssignedCount,
                        yukyuCount = board.Summary.YukyuCount,
                        faltaCount = board.Summary.FaltaCount,
                        lateCount = board.Summary.LateCount,
                        earlyLeaveCount = board.Summary.EarlyLeaveCount,
                        traineeCount = board.Summary.TraineeCount,
                        pairCount = board.Summary.PairCount
                    },
                    areaTotals = board.AreaTotals.Select(item => new
                    {
                        area = item.Area,
                        operatorCount = item.OperatorCount
                    }),
                    groups = board.Groups.Select(group => new
                    {
                        groupId = group.GroupId,
                        groupName = group.GroupName,
                        operatorCount = group.OperatorCount,
                        rows = group.Rows.Select(row => new
                        {
                            codigoFJ = row.CodigoFJ,
                            name = row.Name,
                            nameJp = row.NameJp,
                            groupId = row.GroupId,
                            groupName = row.GroupName,
                            trainer = row.Trainer,
                            isLeader = row.IsLeader,
                            localId = row.LocalId,
                            localName = row.LocalName,
                            localShortCode = row.LocalShortCode,
                            assignmentCode = row.AssignmentCode,
                            storedAssignmentCode = row.StoredAssignmentCode,
                            pairKey = row.PairKey,
                            isTrainee = row.IsTrainee,
                            trainerCodigoFJ = row.TrainerCodigoFJ,
                            countsTowardKousu = row.CountsTowardKousu,
                            isLineupActive = row.IsLineupActive,
                            isHolidayWork = row.IsHolidayWork,
                            notes = row.Notes,
                            exceptionMotiveId = row.ExceptionMotiveId,
                            exceptionMotiveName = row.ExceptionMotiveName,
                            exceptionNotes = row.ExceptionNotes,
                            status = row.Status,
                            movementSummary = row.MovementSummary,
                            movementCount = row.MovementCount,
                            movements = row.Movements.Select(movement => new
                            {
                                id = movement.Id,
                                movementType = movement.MovementType,
                                eventTime = movement.EventTime,
                                eventDateTime = movement.EventDateTime,
                                assignmentCode = movement.AssignmentCode,
                                pairKey = movement.PairKey,
                                replacementOperatorCodigoFJ = movement.ReplacementOperatorCodigoFJ,
                                reason = movement.Reason,
                                createdAt = movement.CreatedAt
                            })
                        })
                    })
                }
            });
        }

        private void SaveMonthlyPlan(JsonElement root)
        {
            var year = ReadInt(root, "year", DateTime.Today.Year);
            var month = ReadInt(root, "month", DateTime.Today.Month);
            var shiftId = ReadInt(root, "shiftId");
            var sectorId = ReadInt(root, "sectorId");
            Demand(PermissionActions.Edit, sectorId);

            var cells = new List<HaidaiMonthlySaveCell>();
            if (root.TryGetProperty("cells", out var cellArray) && cellArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in cellArray.EnumerateArray())
                {
                    var operatorCodigoFJ = ReadString(item, "operatorCodigoFJ");
                    var day = ReadInt(item, "day");
                    if (string.IsNullOrWhiteSpace(operatorCodigoFJ) || day <= 0)
                    {
                        continue;
                    }

                    cells.Add(new HaidaiMonthlySaveCell(
                        operatorCodigoFJ,
                        day,
                        ReadString(item, "assignmentCode"),
                        ReadBool(item, "isHolidayWork")));
                }
            }

            _service.SaveMonthlyPlan(new HaidaiMonthlySaveRequest(year, month, shiftId, sectorId, cells));
            SendMonthlyPlan(year, month, shiftId, sectorId);
            SendBoard(ReadDate(root, "selectedDate"), shiftId, sectorId);
            PostNotify(L("Plano mensal salvo.", "Monthly plan saved."));
        }

        private void SaveAssignment(JsonElement root)
        {
            var date = ReadDate(root, "date");
            var shiftId = ReadInt(root, "shiftId");
            var sectorId = ReadInt(root, "sectorId");
            Demand(PermissionActions.Edit, sectorId);

            _service.SaveAssignment(
                new HaidaiSaveAssignmentRequest(
                    date,
                    shiftId,
                    sectorId,
                    ReadString(root, "operatorCodigoFJ"),
                    ReadNullableInt(root, "localId"),
                    ReadString(root, "assignmentCode"),
                    ReadString(root, "pairKey"),
                    ReadBool(root, "isTrainee"),
                    ReadString(root, "trainerCodigoFJ"),
                    ReadBool(root, "countsTowardKousu", true),
                    ReadString(root, "notes"),
                    ReadBool(root, "isHolidayWork"),
                    ReadBool(root, "applyPairToMonth")));

            SendBoard(date, shiftId, sectorId);
            SendMonthlyPlan(date.Year, date.Month, shiftId, sectorId);
            PostNotify(
                ReadBool(root, "applyPairToMonth")
                    ? L("Escala salva e dupla fixada no mes.", "Assignment saved and pair fixed for the month.")
                    : L("Escala do dia salva.", "Daily assignment saved."));
        }

        private void MarkException(JsonElement root)
        {
            var motiveId = ReadInt(root, "motiveId");
            var date = ReadDate(root, "date");
            var shiftId = ReadInt(root, "shiftId");
            var sectorId = ReadInt(root, "sectorId");
            Demand(PermissionActions.Edit, sectorId);
            if (motiveId != 1 && motiveId != 2)
            {
                throw new InvalidOperationException(L("Motivo invalido.", "Invalid motive."));
            }

            _service.UpsertException(
                date,
                ReadString(root, "operatorCodigoFJ"),
                motiveId,
                ReadString(root, "notes"),
                _currentOperator.CodigoFJ);

            SendBoard(date, shiftId, sectorId);
            SendMonthlyPlan(date.Year, date.Month, shiftId, sectorId);
            PostNotify(
                motiveId == 1
                    ? L("Yukyu enviado para o Todoke como pendente.", "Yukyu sent to Todoke as pending.")
                    : L("Falta enviada para o Todoke como pendente.", "Absence sent to Todoke as pending."));
        }

        private void RegisterMovement(JsonElement root)
        {
            var movementType = ReadString(root, "movementType");
            var date = ReadDate(root, "date");
            var shiftId = ReadInt(root, "shiftId");
            var sectorId = ReadInt(root, "sectorId");
            Demand(PermissionActions.Edit, sectorId);
            if (movementType != "late" && movementType != "early_leave")
            {
                throw new InvalidOperationException(L("Movimento invalido.", "Invalid movement."));
            }

            _service.RegisterMovement(
                new HaidaiMovementRequest(
                    date,
                    shiftId,
                    sectorId,
                    ReadString(root, "operatorCodigoFJ"),
                    ReadNullableInt(root, "movementId"),
                    movementType,
                    ReadString(root, "eventTime"),
                    ReadString(root, "replacementOperatorCodigoFJ"),
                    ReadNullableInt(root, "localId"),
                    ReadString(root, "assignmentCode"),
                    ReadString(root, "pairKey"),
                    ReadString(root, "reason"),
                    _currentOperator.CodigoFJ));

            SendBoard(date, shiftId, sectorId);
            SendMonthlyPlan(date.Year, date.Month, shiftId, sectorId);
            PostNotify(
                movementType == "late"
                    ? L("Atraso registrado e enviado ao Todoke como pendente.", "Late arrival registered and sent to Todoke as pending.")
                    : L("Saida antecipada registrada e enviada ao Todoke como pendente.", "Early leave registered and sent to Todoke as pending."));
        }

        private void DeleteMovement(JsonElement root)
        {
            var date = ReadDate(root, "date");
            var shiftId = ReadInt(root, "shiftId");
            var sectorId = ReadInt(root, "sectorId");
            Demand(PermissionActions.Edit, sectorId);
            var movementId = ReadInt(root, "movementId");
            if (movementId <= 0)
            {
                throw new InvalidOperationException(L("Movimento invalido.", "Invalid movement."));
            }

            _service.DeleteMovement(
                date,
                shiftId,
                sectorId,
                ReadString(root, "operatorCodigoFJ"),
                movementId);

            SendBoard(date, shiftId, sectorId);
            SendMonthlyPlan(date.Year, date.Month, shiftId, sectorId);
            PostNotify(L("Movimento removido com sucesso.", "Movement removed."));
        }

        private void ExportHtml(JsonElement root)
        {
            var sectorId = ReadInt(root, "sectorId");
            Demand(PermissionActions.Export, sectorId);
            var result = _service.ExportSector(
                ReadDate(root, "date"),
                sectorId);

            PostJson(new
            {
                type = "export_result",
                data = new
                {
                    message = L("Arquivos exportados com sucesso.", "Export completed."),
                    directory = result.Directory,
                    files = result.Files
                }
            });
        }

        private void PostNotify(string message)
        {
            PostJson(new
            {
                type = "notify",
                data = new
                {
                    message
                }
            });
        }

        private void Demand(string action, int sectorId)
        {
            _authorization.DemandSectorOrAggregate(
                _currentUser.Id,
                ModuleCodes.Haidai,
                action,
                sectorId);
        }

        private void PostJson(object payload)
        {
            var json = JsonSerializer.Serialize(payload);

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() =>
                {
                    if (_webView.CoreWebView2 != null)
                    {
                        _webView.CoreWebView2.PostWebMessageAsJson(json);
                    }
                }));
                return;
            }

            if (_webView.CoreWebView2 != null)
            {
                _webView.CoreWebView2.PostWebMessageAsJson(json);
            }
        }

        private static string ReadString(JsonElement root, string propertyName)
        {
            return root.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
                ? prop.GetString() ?? string.Empty
                : string.Empty;
        }

        private static int ReadInt(JsonElement root, string propertyName, int defaultValue = 0)
        {
            if (!root.TryGetProperty(propertyName, out var prop))
            {
                return defaultValue;
            }

            return prop.ValueKind switch
            {
                JsonValueKind.Number => prop.GetInt32(),
                JsonValueKind.String when int.TryParse(prop.GetString(), out var parsed) => parsed,
                _ => defaultValue
            };
        }

        private static int? ReadNullableInt(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var prop))
            {
                return null;
            }

            return prop.ValueKind switch
            {
                JsonValueKind.Number => prop.GetInt32(),
                JsonValueKind.String when int.TryParse(prop.GetString(), out var parsed) => parsed,
                _ => null
            };
        }

        private static bool ReadBool(JsonElement root, string propertyName, bool defaultValue = false)
        {
            if (!root.TryGetProperty(propertyName, out var prop))
            {
                return defaultValue;
            }

            return prop.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String when bool.TryParse(prop.GetString(), out var parsed) => parsed,
                JsonValueKind.Number => prop.GetInt32() != 0,
                _ => defaultValue
            };
        }

        private static DateTime ReadDate(JsonElement root, string propertyName)
        {
            var raw = ReadString(root, propertyName);
            if (DateTime.TryParseExact(raw, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var parsed))
            {
                return parsed;
            }

            return DateTime.Today;
        }

        private static string L(string pt, string jp)
        {
            return string.Equals(Program.CurrentLocale, "ja-JP", StringComparison.OrdinalIgnoreCase)
                ? jp
                : pt;
        }
    }
}
