const state = {
    locale: "pt-BR",
    currentUser: "",
    currentOperatorName: "",
    shifts: [],
    sectors: [],
    locals: [],
    operators: [],
    sharedSectorTopology: {
        isEnabled: false,
        sharedSectorId: 0,
        memberSectorIds: []
    },
    monthPlan: null,
    board: null,
    selectedOperatorCodigoFJ: "",
    selectedDay: 1,
    movementDraft: null
};

document.addEventListener("DOMContentLoaded", () => {
    bindEvents();
    post({ action: "load" });
});

window.chrome?.webview?.addEventListener("message", event => {
    const payload = event.data;
    if (!payload?.type) {
        return;
    }

    switch (payload.type) {
        case "init":
            hydrateInit(payload.data || {});
            refreshContext(false);
            break;
        case "month_plan":
            hydrateMonthPlan(payload.data || {});
            break;
        case "board":
            hydrateBoard(payload.data || {});
            break;
        case "notify":
            showNotice(payload.data?.message || "", "success");
            break;
        case "export_result":
            hideLoading();
            showNotice(`${payload.data?.message || "Exportado."} ${payload.data?.directory || ""}`.trim(), "success");
            break;
        case "error":
            hideLoading();
            showNotice(payload.message || "Erro inesperado.", "error");
            break;
    }
});

function bindEvents() {
    document.getElementById("btnRefresh").addEventListener("click", () => refreshContext(true));
    document.getElementById("btnSaveMonth").addEventListener("click", saveMonthPlan);
    document.getElementById("btnExport").addEventListener("click", exportHtml);
    document.getElementById("monthPicker").addEventListener("change", () => refreshContext(false));
    document.getElementById("dayPicker").addEventListener("change", onDayChanged);
    document.getElementById("shiftPicker").addEventListener("change", () => refreshContext(false));
    document.getElementById("sectorPicker").addEventListener("change", () => refreshContext(false));

    document.getElementById("plannerTable").addEventListener("focusin", event => {
        const input = event.target.closest(".plan-cell");
        if (!input) {
            return;
        }

        selectPlannerCell(input.dataset.op, Number(input.dataset.day || 1), true);
    });

    document.getElementById("plannerTable").addEventListener("click", event => {
        const input = event.target.closest(".plan-cell");
        if (!input) {
            return;
        }

        selectPlannerCell(input.dataset.op, Number(input.dataset.day || 1), true);
    });

    document.getElementById("plannerTable").addEventListener("input", event => {
        const input = event.target.closest(".plan-cell");
        if (!input) {
            return;
        }

        normalizeCellValue(input);
    });

    document.getElementById("plannerTable").addEventListener("paste", handlePlannerPaste);

    document.getElementById("selectedDetailHost").addEventListener("click", event => {
        const movementButton = event.target.closest("[data-movement-action]");
        if (movementButton) {
            const container = event.currentTarget;
            const movementId = Number(movementButton.dataset.movementId || 0);
            const movementAction = movementButton.dataset.movementAction;
            if (movementAction === "edit") {
                editMovement(container, movementId);
                return;
            }

            if (movementAction === "delete") {
                deleteMovement(container, movementId);
                return;
            }
        }

        const button = event.target.closest("[data-row-action]");
        if (!button) {
            return;
        }

        const container = event.currentTarget;
        const action = button.dataset.rowAction;

        if (action === "save") {
            saveDetail(container);
            return;
        }

        if (action === "yukyu") {
            markException(container, 1);
            return;
        }

        if (action === "falta") {
            markException(container, 2);
            return;
        }

        if (action === "off") {
            markOffDay(container);
            return;
        }

        if (action === "clear") {
            clearException(container);
            return;
        }

        if (action === "late") {
            registerMovement(container, "late");
            return;
        }

        if (action === "early_leave") {
            registerMovement(container, "early_leave");
            return;
        }

        if (action === "restore") {
            restoreLineup(container);
        }
    });

    document.getElementById("selectedDetailHost").addEventListener("change", event => {
        const field = event.target.closest("[data-field]");
        if (!field) {
            return;
        }

        const container = event.currentTarget;
        if (field.dataset.field === "localId") {
            syncAssignmentCodeFromLocal(container);
            return;
        }

        if (field.dataset.field === "isTrainee") {
            syncAssignmentCodeFromLocal(container);
        }
    });

    document.getElementById("dayOverviewHost").addEventListener("click", event => {
        const row = event.target.closest("[data-select-op]");
        if (!row) {
            return;
        }

        state.selectedOperatorCodigoFJ = row.dataset.selectOp || "";
        renderDetail();
        highlightPlannerSelection();
    });

    document.getElementById("movementBackdrop").addEventListener("click", closeMovementModal);
    document.getElementById("btnCloseMovementModal").addEventListener("click", closeMovementModal);
    document.getElementById("btnCancelMovementModal").addEventListener("click", closeMovementModal);
    document.getElementById("btnConfirmMovementModal").addEventListener("click", submitMovementModal);
    document.getElementById("movementReplacementSearch").addEventListener("input", renderReplacementCandidates);
    document.getElementById("movementTime").addEventListener("input", refreshMovementDateHint);
}

function hydrateInit(data) {
    state.locale = data.locale === "ja-JP" ? "ja-JP" : "pt-BR";
    state.currentUser = data.currentUser || "-";
    state.currentOperatorName = data.currentOperatorName || "-";
    state.shifts = data.shifts || [];
    state.sectors = data.sectors || [];
    state.locals = data.locals || [];
    state.operators = data.operators || [];
    state.sharedSectorTopology = {
        isEnabled: data.sharedSectorTopology?.isEnabled === true,
        sharedSectorId: Number(data.sharedSectorTopology?.sharedSectorId || 0),
        memberSectorIds: (data.sharedSectorTopology?.memberSectorIds || []).map(Number)
    };

    document.getElementById("lblUser").textContent = state.currentUser;
    document.getElementById("lblOperator").textContent = state.currentOperatorName;

    fillSelect("shiftPicker", state.shifts, data.defaults?.shiftId);
    fillSelect("sectorPicker", state.sectors, data.defaults?.sectorId);
    document.getElementById("monthPicker").value = data.defaults?.monthIso || "";
    state.selectedDay = parseDateIso(data.defaults?.dateIso).day;
}

function hydrateMonthPlan(data) {
    state.monthPlan = data;
    syncDayPicker(data.days || []);
    renderPlanner(data);
}

function hydrateBoard(data) {
    state.board = data;

    setText("sumOperators", data.summary?.operatorCount ?? 0);
    setText("sumAssigned", data.summary?.assignedCount ?? 0);
    setText("sumYukyu", data.summary?.yukyuCount ?? 0);
    setText("sumFalta", data.summary?.faltaCount ?? 0);
    setText("sumLate", data.summary?.lateCount ?? 0);
    setText("sumEarlyLeave", data.summary?.earlyLeaveCount ?? 0);
    setText("sumTrainee", data.summary?.traineeCount ?? 0);
    setText("sumPairs", data.summary?.pairCount ?? 0);

    const rows = flattenBoardRows(data);
    if (!rows.length) {
        document.getElementById("selectedDetailHost").innerHTML = `<div class="detail-empty">Nenhum operador encontrado para este dia.</div>`;
        document.getElementById("dayOverviewHost").innerHTML = "";
        document.getElementById("areaTotalsHost").innerHTML = "";
        showNotice("Nenhum operador ativo encontrado para este setor e turno.", "warning");
        return;
    }

    if (!state.selectedOperatorCodigoFJ || !rows.some(row => row.codigoFJ === state.selectedOperatorCodigoFJ)) {
        state.selectedOperatorCodigoFJ = rows[0].codigoFJ;
    }

    renderDetail();
    renderDayOverview();
    renderAreaTotals(data.areaTotals || []);
    highlightPlannerSelection();
    hideNotice();
}

function refreshContext(forceBoardRefresh) {
    const month = getSelectedMonth();
    const shiftId = Number(document.getElementById("shiftPicker").value || 0);
    const sectorId = Number(document.getElementById("sectorPicker").value || 0);
    const selectedDate = buildSelectedDateIso(month.year, month.month, state.selectedDay);

    post({
        action: "load_month_plan",
        year: month.year,
        month: month.month,
        shiftId,
        sectorId
    });

    if (forceBoardRefresh || shiftId > 0 || sectorId > 0) {
        post({
            action: "refresh",
            date: selectedDate,
            shiftId,
            sectorId
        });
    }
}

function onDayChanged() {
    state.selectedDay = Number(document.getElementById("dayPicker").value || 1);
    refreshBoard();
}

function refreshBoard() {
    const month = getSelectedMonth();
    post({
        action: "refresh",
        date: buildSelectedDateIso(month.year, month.month, state.selectedDay),
        shiftId: Number(document.getElementById("shiftPicker").value || 0),
        sectorId: Number(document.getElementById("sectorPicker").value || 0)
    });
}

function saveMonthPlan() {
    const month = getSelectedMonth();
    const cells = Array.from(document.querySelectorAll(".plan-cell")).map(input => ({
        operatorCodigoFJ: input.dataset.op,
        day: Number(input.dataset.day || 0),
        assignmentCode: String(input.value || "").trim().toUpperCase(),
        isHolidayWork: input.dataset.isHolidayWork === "true"
    }));

    post({
        action: "save_month_plan",
        year: month.year,
        month: month.month,
        shiftId: Number(document.getElementById("shiftPicker").value || 0),
        sectorId: Number(document.getElementById("sectorPicker").value || 0),
        selectedDate: buildSelectedDateIso(month.year, month.month, state.selectedDay),
        cells
    });
}

function exportHtml() {
    const month = getSelectedMonth();
    showLoading();
    window.setTimeout(() => {
        post({
            action: "export_html",
            date: buildSelectedDateIso(month.year, month.month, state.selectedDay),
            sectorId: Number(document.getElementById("sectorPicker").value || 0)
        });
    }, 40);
}

function renderPlanner(plan) {
    const table = document.getElementById("plannerTable");
    const days = plan.days || [];
    const groups = plan.groups || [];

    document.getElementById("plannerMeta").textContent = `${groups.length} grupo(s), ${days.length} dia(s) no mes. Clique em uma celula para editar o detalhe diario.`;

    const headDays = days.map(day => `<th>${day}</th>`).join("");
    const body = groups.map(group => {
        const operators = sortMonthOperatorsForSelectedDay(group.operators || []);
        const rows = operators.map(operator => {
            const cells = (operator.cells || []).map(cell => {
                const value = escapeAttr(cell.assignmentCode || "");
                const classes = buildPlanCellClasses(cell.assignmentCode || "", cell.status || "", cell.isHolidayWork);
                return `<td class="day-cell"><input class="plan-cell ${classes}" data-op="${escapeAttr(operator.codigoFJ)}" data-day="${cell.day}" data-status="${escapeAttr(cell.status || "")}" data-is-holiday-work="${cell.isHolidayWork ? "true" : "false"}" value="${value}" spellcheck="false"></td>`;
            }).join("");

            return `
                <tr>
                    <td class="sticky-col group-sticky">${escapeHtml(group.groupName)}</td>
                    <td class="sticky-col name-col operator-cell">
                        <div class="operator-name">${escapeHtml(operator.name)}</div>
                        <div class="operator-meta">${escapeHtml(operator.codigoFJ)}${operator.nameJp ? ` | ${escapeHtml(operator.nameJp)}` : ""}</div>
                    </td>
                    ${cells}
                </tr>
            `;
        }).join("");

        return `
            <tr class="group-row">
                <td colspan="${days.length + 2}">Grupo ${escapeHtml(group.groupName)} · ${group.operatorCount || (group.operators || []).length} operador(es)</td>
            </tr>
            ${rows}
        `;
    }).join("");

    table.innerHTML = `
        <thead>
            <tr>
                <th class="sticky-col group-sticky">Grupo</th>
                <th class="sticky-col name-col">Nome</th>
                ${headDays}
            </tr>
        </thead>
        <tbody>${body}</tbody>
    `;

    highlightPlannerSelection();
}

function renderDetail() {
    const host = document.getElementById("selectedDetailHost");
    const row = getSelectedBoardRow();

    if (!row) {
        host.innerHTML = `<div class="detail-empty">Selecione uma celula para editar o detalhe diario.</div>`;
        return;
    }

    const sectorId = Number(document.getElementById("sectorPicker").value || 0);
    const shiftId = Number(document.getElementById("shiftPicker").value || 0);
    const localOptions = buildLocalOptions(row.codigoFJ, sectorId, row.localId);
    const trainerOptions = buildTrainerOptions(sectorId, shiftId, row.trainerCodigoFJ, row.codigoFJ);
    const pairOptions = buildPairOptions(row);
    const roleTags = [];

    if (row.trainer) {
        roleTags.push(`<span class="tag">Trainer</span>`);
    }

    if (row.isLeader) {
        roleTags.push(`<span class="tag">Lider</span>`);
    }

    if (row.isTrainee) {
        roleTags.push(`<span class="tag">Aprendiz</span>`);
    }

    if (row.pairKey) {
        roleTags.push(`<span class="tag">Par ${escapeHtml(row.pairKey)}</span>`);
    }

    if (row.isHolidayWork) {
        roleTags.push(`<span class="tag tag-shukkin">Shukkin</span>`);
    }

    const detailNotes = [];
    if (row.exceptionNotes) {
        detailNotes.push(`Todoke: ${row.exceptionNotes}`);
    }
    if (row.movementSummary) {
        detailNotes.push(`Historico: ${row.movementSummary}`);
    }
    if (row.notes) {
        detailNotes.push(`Obs.: ${row.notes}`);
    }

    const movementHistory = renderMovementHistory(row);

    host.dataset.op = row.codigoFJ;
    host.innerHTML = `
        <div class="detail-header">
            <h2>${escapeHtml(row.name)}</h2>
            <p>${escapeHtml(buildSelectedDateLabel())} · ${escapeHtml(row.codigoFJ)}${row.nameJp ? ` | ${escapeHtml(row.nameJp)}` : ""}</p>
        </div>

        <div class="status-row">
            <span class="status-chip ${statusClass(row.status)}">${escapeHtml(row.status || "Pendente")}</span>
            ${roleTags.join("")}
        </div>

        ${detailNotes.length ? `<div class="detail-note">${escapeHtml(detailNotes.join(" | "))}</div>` : ""}
        ${movementHistory}

        <div class="detail-grid">
            <label class="detail-field">
                <span>Area base</span>
                <select data-field="localId">${localOptions}</select>
            </label>

            <label class="detail-field">
                <span>Codigo exibido</span>
                <input data-field="assignmentCode" type="text" value="${escapeAttr(row.storedAssignmentCode || row.assignmentCode || "")}" placeholder="A1, A1#, Maisena">
            </label>

            <label class="detail-field">
                <span>Dupla</span>
                <select data-field="pairKey">${pairOptions}</select>
            </label>

            <label class="detail-field">
                <span>Treinador</span>
                <select data-field="trainerCodigoFJ">${trainerOptions}</select>
            </label>

            <label class="detail-field">
                <span>Observacao</span>
                <textarea data-field="notes" placeholder="Observacoes da escala">${escapeHtml(row.notes || "")}</textarea>
            </label>
        </div>

        <div class="check-row">
            <label><input data-field="isTrainee" type="checkbox" ${row.isTrainee ? "checked" : ""}> Aprendiz</label>
            <label><input data-field="countsTowardKousu" type="checkbox" ${row.countsTowardKousu !== false ? "checked" : ""}> Conta no kousu</label>
            <label class="holiday-work-check"><input data-field="isHolidayWork" type="checkbox" ${row.isHolidayWork ? "checked" : ""}> Shukkin</label>
            <label><input data-field="applyPairToMonth" type="checkbox" checked> Replicar dupla para os proximos dias</label>
        </div>

        <div class="action-row">
            <button class="btn btn-inline btn-save" type="button" data-row-action="save">Salvar dia</button>
            <button class="btn btn-inline btn-secondary" type="button" data-row-action="off">Folga</button>
            <button class="btn btn-inline btn-yukyu" type="button" data-row-action="yukyu">Yukyu</button>
            <button class="btn btn-inline btn-falta" type="button" data-row-action="falta">Falta</button>
            <button class="btn btn-inline btn-secondary" type="button" data-row-action="late">Atraso</button>
            <button class="btn btn-inline btn-secondary" type="button" data-row-action="early_leave">Sair cedo</button>
            ${row.isLineupActive === false ? `<button class="btn btn-inline btn-clear" type="button" data-row-action="restore">Voltar para linha</button>` : ""}
            ${row.exceptionMotiveId ? `<button class="btn btn-inline btn-clear" type="button" data-row-action="clear">Voltar normal</button>` : ""}
        </div>
    `;
}

function renderDayOverview() {
    const host = document.getElementById("dayOverviewHost");
    const groups = state.board?.groups || [];
    document.getElementById("dayMeta").textContent = `${buildSelectedDateLabel()} · clique em uma linha para trocar o operador do detalhe.`;

    host.innerHTML = groups.map(group => {
        const rows = (group.rows || []).map(row => `
            <tr class="overview-row ${row.codigoFJ === state.selectedOperatorCodigoFJ ? "is-selected" : ""}" data-select-op="${escapeAttr(row.codigoFJ)}">
                <td>${escapeHtml(row.name)}</td>
                <td>${escapeHtml(row.assignmentCode || "-")}</td>
                <td><span class="status-chip ${statusClass(row.status)}">${escapeHtml(row.status || "Pendente")}</span></td>
            </tr>
        `).join("");

        return `
            <section class="group-overview">
                <div class="group-overview-head">
                    <strong>Grupo ${escapeHtml(group.groupName)}</strong>
                    <span>${group.operatorCount} operador(es)</span>
                </div>
                <table class="overview-table">
                    <thead>
                        <tr>
                            <th>Operador</th>
                            <th>Area</th>
                            <th>Status</th>
                        </tr>
                    </thead>
                    <tbody>${rows}</tbody>
                </table>
            </section>
        `;
    }).join("");
}

function renderAreaTotals(items) {
    const host = document.getElementById("areaTotalsHost");
    if (!items.length) {
        host.innerHTML = `<div class="detail-empty">Nenhuma area planejada neste setor para o dia selecionado.</div>`;
        return;
    }

    const rows = items.map(item => `
        <tr>
            <td>${escapeHtml(item.area || "-")}</td>
            <td>${Number(item.operatorCount || 0)}</td>
        </tr>
    `).join("");

    host.innerHTML = `
        <div class="area-totals-wrap">
            <table class="area-totals-table">
                <thead>
                    <tr>
                        <th>Area</th>
                        <th>Operadores</th>
                    </tr>
                </thead>
                <tbody>${rows}</tbody>
            </table>
        </div>
    `;
}

function selectPlannerCell(operatorCodigoFJ, day, refresh) {
    state.selectedOperatorCodigoFJ = operatorCodigoFJ || state.selectedOperatorCodigoFJ;
    state.selectedDay = Number.isFinite(day) && day > 0 ? day : state.selectedDay;
    document.getElementById("dayPicker").value = String(state.selectedDay);
    highlightPlannerSelection();

    if (refresh) {
        refreshBoard();
    }
}

function highlightPlannerSelection() {
    document.querySelectorAll(".plan-cell.cell-selected").forEach(input => input.classList.remove("cell-selected"));
    if (!state.selectedOperatorCodigoFJ) {
        return;
    }

    const input = document.querySelector(`.plan-cell[data-op="${cssEscape(state.selectedOperatorCodigoFJ)}"][data-day="${state.selectedDay}"]`);
    if (input) {
        input.classList.add("cell-selected");
    }
}

function handlePlannerPaste(event) {
    const input = event.target.closest(".plan-cell");
    if (!input || !state.monthPlan) {
        return;
    }

    const text = event.clipboardData?.getData("text/plain") || "";
    if (!text.trim()) {
        return;
    }

    event.preventDefault();

    const operatorRows = flattenMonthOperators(state.monthPlan);
    const rowIndex = operatorRows.findIndex(item => item.codigoFJ === input.dataset.op);
    const startDay = Number(input.dataset.day || 1);
    if (rowIndex < 0 || startDay <= 0) {
        return;
    }

    const lines = text.replaceAll("\r", "").split("\n").filter((line, index, all) => {
        return line.length > 0 || index < all.length - 1;
    });

    lines.forEach((line, lineOffset) => {
        const values = line.split("\t");
        values.forEach((value, colOffset) => {
            const targetRow = operatorRows[rowIndex + lineOffset];
            const targetDay = startDay + colOffset;
            if (!targetRow || !targetDay) {
                return;
            }

            const targetInput = document.querySelector(`.plan-cell[data-op="${cssEscape(targetRow.codigoFJ)}"][data-day="${targetDay}"]`);
            if (!targetInput) {
                return;
            }

            targetInput.value = String(value || "").trim().toUpperCase();
            normalizeCellValue(targetInput);
        });
    });
}

function saveDetail(container) {
    post({
        action: "save_assignment",
        date: getSelectedDateIso(),
        shiftId: Number(document.getElementById("shiftPicker").value || 0),
        sectorId: Number(document.getElementById("sectorPicker").value || 0),
        operatorCodigoFJ: container.dataset.op,
        localId: nullableNumber(readField(container, "localId")),
        assignmentCode: readField(container, "assignmentCode"),
        pairKey: readField(container, "pairKey"),
        trainerCodigoFJ: readField(container, "trainerCodigoFJ"),
        notes: readField(container, "notes"),
        isTrainee: readCheckbox(container, "isTrainee"),
        countsTowardKousu: readCheckbox(container, "countsTowardKousu"),
        isHolidayWork: readCheckbox(container, "isHolidayWork"),
        applyPairToMonth: readCheckbox(container, "applyPairToMonth")
    });
}

function markException(container, motiveId) {
    const label = motiveId === 1 ? "Yukyu" : "Falta";
    let notes = "";

    if (motiveId === 2) {
        notes = window.prompt("Informe o motivo da falta:", "") || "";
        if (!notes.trim()) {
            showNotice("O motivo da falta e obrigatorio.", "warning");
            return;
        }
    } else {
        notes = window.prompt("Observacao opcional para o Yukyu:", "") || "";
    }

    post({
        action: "mark_exception",
        date: getSelectedDateIso(),
        shiftId: Number(document.getElementById("shiftPicker").value || 0),
        sectorId: Number(document.getElementById("sectorPicker").value || 0),
        operatorCodigoFJ: container.dataset.op,
        motiveId,
        notes
    });

    showNotice(`${label} enviado para o controle de Todoke.`, "success");
}

function markOffDay(container) {
    if (Number(container.dataset.exceptionMotiveId || 0) > 0) {
        showNotice("Remova Yukyu/Falta antes de marcar folga.", "warning");
        return;
    }

    post({
        action: "save_assignment",
        date: getSelectedDateIso(),
        shiftId: Number(document.getElementById("shiftPicker").value || 0),
        sectorId: Number(document.getElementById("sectorPicker").value || 0),
        operatorCodigoFJ: container.dataset.op,
        localId: null,
        assignmentCode: "休",
        pairKey: "",
        trainerCodigoFJ: "",
        notes: readField(container, "notes"),
        isTrainee: false,
        countsTowardKousu: false,
        isHolidayWork: false,
        applyPairToMonth: false
    });

    showNotice("Folga registrada para o dia selecionado.", "success");
}

function clearException(container) {
    const confirmed = window.confirm("Remover Yukyu/Falta deste operador para a data selecionada?");
    if (!confirmed) {
        return;
    }

    post({
        action: "clear_exception",
        date: getSelectedDateIso(),
        shiftId: Number(document.getElementById("shiftPicker").value || 0),
        sectorId: Number(document.getElementById("sectorPicker").value || 0),
        operatorCodigoFJ: container.dataset.op
    });
}

function registerMovement(container, movementType) {
    openMovementModal(container, movementType, null);
}

function openMovementModal(container, movementType, movement) {
    state.movementDraft = {
        operatorCodigoFJ: container.dataset.op,
        movementId: movement ? Number(movement.id || 0) : null,
        movementType,
        localId: nullableNumber(readField(container, "localId")),
        assignmentCode: movement?.assignmentCode || readField(container, "assignmentCode"),
        pairKey: movement?.pairKey || readField(container, "pairKey")
    };

    document.getElementById("movementTitle").textContent = movement
        ? movementType === "late" ? "Editar atraso" : "Editar saida antecipada"
        : movementType === "late" ? "Registrar atraso" : "Registrar saida antecipada";
    document.getElementById("movementSubtitle").textContent = movementType === "late"
        ? "Informe o horario em que o operador entrou e, se houve cobertura, selecione o substituto."
        : "Informe o horario em que o operador saiu e, se houve cobertura, selecione o substituto.";
    document.getElementById("btnConfirmMovementModal").textContent = movement ? "Salvar ajuste" : "Confirmar";
    document.getElementById("movementTime").value = (movement?.eventTime || "").slice(0, 5);
    document.getElementById("movementReason").value = movement?.reason || "";
    document.getElementById("movementReplacementSearch").value = "";
    document.getElementById("movementReplacementCodigoFJ").value = movement?.replacementOperatorCodigoFJ || "";
    if (movement?.replacementOperatorCodigoFJ) {
        const selectedOperator = findOperator(movement.replacementOperatorCodigoFJ);
        document.getElementById("movementReplacementSearch").value = selectedOperator
            ? `${selectedOperator.codigoFJ} - ${selectedOperator.name || selectedOperator.nameJp || ""}`.trim()
            : movement.replacementOperatorCodigoFJ;
    }
    renderReplacementCandidates();
    refreshMovementDateHint();
    document.getElementById("movementModal").classList.remove("hidden");
    window.setTimeout(() => document.getElementById("movementTime").focus(), 0);
}

function closeMovementModal() {
    document.getElementById("movementModal").classList.add("hidden");
    document.getElementById("btnConfirmMovementModal").textContent = "Confirmar";
    state.movementDraft = null;
}

function submitMovementModal() {
    if (!state.movementDraft) {
        return;
    }

    const eventTime = (document.getElementById("movementTime").value || "").trim();
    if (!eventTime) {
        showNotice("Informe o horario do movimento.", "warning");
        return;
    }

    const reason = (document.getElementById("movementReason").value || "").trim();
    if (!reason) {
        showNotice("Informe o motivo do movimento.", "warning");
        return;
    }

    const replacementOperatorCodigoFJ = (document.getElementById("movementReplacementCodigoFJ").value || "").trim();
    const draft = state.movementDraft;
    if (!replacementOperatorCodigoFJ) {
        const confirmedWithoutReplacement = window.confirm("Confirmar que nenhum operador ira substituir este movimento?");
        if (!confirmedWithoutReplacement) {
            return;
        }
    }

    post({
        action: "register_movement",
        date: getSelectedDateIso(),
        shiftId: Number(document.getElementById("shiftPicker").value || 0),
        sectorId: Number(document.getElementById("sectorPicker").value || 0),
        operatorCodigoFJ: draft.operatorCodigoFJ,
        movementId: draft.movementId || 0,
        movementType: draft.movementType,
        eventTime,
        reason,
        replacementOperatorCodigoFJ,
        localId: draft.localId,
        assignmentCode: draft.assignmentCode,
        pairKey: draft.pairKey
    });

    closeMovementModal();
}

function renderReplacementCandidates() {
    const wrap = document.getElementById("movementReplacementList");
    const selectedField = document.getElementById("movementReplacementCodigoFJ");
    const search = (document.getElementById("movementReplacementSearch").value || "").trim().toLowerCase();
    const currentOperatorCodigoFJ = state.movementDraft?.operatorCodigoFJ || "";
    const shiftId = Number(document.getElementById("shiftPicker").value || 0);
    const sectorId = Number(document.getElementById("sectorPicker").value || 0);

    const candidates = state.operators
        .filter(item => item.codigoFJ !== currentOperatorCodigoFJ)
        .filter(item => shiftId <= 0 || Number(item.shiftId || 0) === shiftId)
        .filter(item => {
            return sectorId <= 0 || isOperatorVisibleInSector(item, sectorId);
        })
        .filter(item => {
            if (!search) {
                return true;
            }

            return [item.codigoFJ, item.name, item.nameJp, item.groupName]
                .filter(Boolean)
                .some(value => String(value).toLowerCase().includes(search));
        })
        .slice(0, 18);

    if (!candidates.length) {
        wrap.innerHTML = `<div class="replacement-empty">Nenhum operador encontrado para este filtro.</div>`;
        return;
    }

    wrap.innerHTML = candidates.map(item => {
        const selected = selectedField.value === item.codigoFJ;
        return `
            <button type="button" class="replacement-item ${selected ? "is-selected" : ""}" data-replacement="${escapeAttr(item.codigoFJ)}">
                <div>
                    <strong>${escapeHtml(item.name || item.codigoFJ)}</strong>
                    <small>${escapeHtml(item.codigoFJ)}${item.groupName ? ` · ${escapeHtml(item.groupName)}` : ""}</small>
                </div>
                <small>${escapeHtml(item.nameJp || "")}</small>
            </button>
        `;
    }).join("");

    wrap.querySelectorAll("[data-replacement]").forEach(button => {
        button.addEventListener("click", () => {
            const code = button.dataset.replacement || "";
            const operator = findOperator(code);
            selectedField.value = code;
            document.getElementById("movementReplacementSearch").value = operator
                ? `${operator.codigoFJ} - ${operator.name || operator.nameJp || ""}`.trim()
                : code;
            renderReplacementCandidates();
        });
    });
}

function editMovement(container, movementId) {
    const row = getSelectedBoardRow();
    const movement = (row?.movements || []).find(item => Number(item.id || 0) === Number(movementId));
    if (!row || !movement) {
        return;
    }

    openMovementModal(container, movement.movementType, movement);
}

function deleteMovement(container, movementId) {
    const row = getSelectedBoardRow();
    const movement = (row?.movements || []).find(item => Number(item.id || 0) === Number(movementId));
    if (!row || !movement) {
        return;
    }

    const confirmed = window.confirm("Remover este movimento e ajustar a substituicao relacionada?");
    if (!confirmed) {
        return;
    }

    post({
        action: "delete_movement",
        date: getSelectedDateIso(),
        shiftId: Number(document.getElementById("shiftPicker").value || 0),
        sectorId: Number(document.getElementById("sectorPicker").value || 0),
        operatorCodigoFJ: container.dataset.op,
        movementId
    });
}

function renderMovementHistory(row) {
    const movements = row.movements || [];
    if (!movements.length) {
        return "";
    }

    const cards = movements.map(movement => {
        const replacementName = resolveOperatorDisplayName(movement.replacementOperatorCodigoFJ);
        const eventDateLabel = formatMovementEventDateTime(movement.eventDateTime, movement.eventTime);
        return `
            <article class="movement-history-card">
                <div class="movement-history-head">
                    <div class="movement-history-copy">
                        <strong>${escapeHtml(movementTypeLabel(movement.movementType))}</strong>
                        <small>${escapeHtml(eventDateLabel)}</small>
                        <small>${escapeHtml(replacementName ? `Substituto: ${replacementName}` : "Sem substituto")}</small>
                        ${movement.reason ? `<small>${escapeHtml(movement.reason)}</small>` : ""}
                    </div>
                    <div class="movement-history-actions">
                        <button class="btn btn-inline btn-secondary" type="button" data-movement-action="edit" data-movement-id="${Number(movement.id || 0)}">Editar</button>
                        <button class="btn btn-inline btn-clear" type="button" data-movement-action="delete" data-movement-id="${Number(movement.id || 0)}">Remover</button>
                    </div>
                </div>
            </article>
        `;
    }).join("");

    return `<div class="movement-history">${cards}</div>`;
}

function refreshMovementDateHint() {
    const hint = document.getElementById("movementDateHint");
    const time = (document.getElementById("movementTime").value || "").trim();
    hint.textContent = buildMovementActualDateHint(time);
}

function buildMovementActualDateHint(time) {
    if (!time) {
        return "A data real sera ajustada automaticamente conforme o turno.";
    }

    const shiftName = getSelectedShiftName();
    const baseDate = getSelectedDateIso();
    if (isOvernightShiftName(shiftName) && time < "12:00") {
        return `Movimento real: ${formatDateOnly(addDaysIso(baseDate, 1))} ${time} (referente ao dia do Haidai).`;
    }

    return `Movimento real: ${formatDateOnly(baseDate)} ${time}.`;
}

function getSelectedShiftName() {
    const shiftId = Number(document.getElementById("shiftPicker").value || 0);
    return (state.shifts || []).find(item => Number(item.id || 0) === shiftId)?.name || "";
}

function isOvernightShiftName(name) {
    const value = String(name || "").toLowerCase();
    return value.includes("noite") || value.includes("night") || value.includes("yakin") || String(name || "").includes("夜");
}

function addDaysIso(dateIso, days) {
    const [year, month, day] = String(dateIso || "").split("-").map(Number);
    const value = new Date(year || 0, (month || 1) - 1, day || 1);
    value.setDate(value.getDate() + Number(days || 0));
    return `${String(value.getFullYear()).padStart(4, "0")}-${String(value.getMonth() + 1).padStart(2, "0")}-${String(value.getDate()).padStart(2, "0")}`;
}

function resolveOperatorDisplayName(codigoFJ) {
    if (!codigoFJ) {
        return "";
    }

    const operator = findOperator(codigoFJ);
    if (!operator) {
        return codigoFJ;
    }

    return `${operator.codigoFJ} - ${operator.name || operator.nameJp || codigoFJ}`;
}

function movementTypeLabel(movementType) {
    return movementType === "late" ? "Atraso" : movementType === "early_leave" ? "Saida antecipada" : movementType || "-";
}

function formatMovementEventDateTime(eventDateTime, fallbackTime) {
    if (eventDateTime) {
        const [datePart, timePart] = String(eventDateTime).split(" ");
        return `${formatDateOnly(datePart)} ${String(timePart || fallbackTime || "").slice(0, 5)}`.trim();
    }

    return fallbackTime || "-";
}

function formatDateOnly(dateIso) {
    const [year, month, day] = String(dateIso || "").split("-");
    if (!year || !month || !day) {
        return String(dateIso || "-");
    }

    return `${day}/${month}/${year}`;
}

function findOperator(codigoFJ) {
    return findOperatorMeta(codigoFJ);
}

function restoreLineup(container) {
    const confirmed = window.confirm("Reativar este operador na linha atual?");
    if (!confirmed) {
        return;
    }

    post({
        action: "restore_lineup",
        date: getSelectedDateIso(),
        shiftId: Number(document.getElementById("shiftPicker").value || 0),
        sectorId: Number(document.getElementById("sectorPicker").value || 0),
        operatorCodigoFJ: container.dataset.op
    });
}

function syncDayPicker(days) {
    const picker = document.getElementById("dayPicker");
    const current = Number(picker.value || state.selectedDay || 1);
    const maxDay = (days || []).length ? Math.max(...days) : 31;
    state.selectedDay = Math.min(Math.max(current, 1), maxDay);

    picker.innerHTML = (days || []).map(day => `<option value="${day}">Dia ${day}</option>`).join("");
    picker.value = String(state.selectedDay);
}

function getSelectedMonth() {
    const raw = document.getElementById("monthPicker").value || new Date().toISOString().slice(0, 7);
    const [yearText, monthText] = raw.split("-");
    return {
        year: Number(yearText || new Date().getFullYear()),
        month: Number(monthText || (new Date().getMonth() + 1))
    };
}

function getSelectedDateIso() {
    const month = getSelectedMonth();
    return buildSelectedDateIso(month.year, month.month, state.selectedDay);
}

function buildSelectedDateIso(year, month, day) {
    const lastDay = new Date(year, month, 0).getDate();
    const clampedDay = Math.min(Math.max(Number(day || 1), 1), lastDay);
    state.selectedDay = clampedDay;
    return `${String(year).padStart(4, "0")}-${String(month).padStart(2, "0")}-${String(clampedDay).padStart(2, "0")}`;
}

function buildSelectedDateLabel() {
    const [year, month, day] = getSelectedDateIso().split("-");
    return `${day}/${month}/${year}`;
}

function parseDateIso(value) {
    const parts = String(value || "").split("-");
    return {
        year: Number(parts[0] || new Date().getFullYear()),
        month: Number(parts[1] || (new Date().getMonth() + 1)),
        day: Number(parts[2] || new Date().getDate())
    };
}

function getSelectedBoardRow() {
    const rows = flattenBoardRows(state.board);
    return rows.find(row => row.codigoFJ === state.selectedOperatorCodigoFJ) || rows[0] || null;
}

function flattenBoardRows(board) {
    return (board?.groups || []).flatMap(group => group.rows || []);
}

function flattenMonthOperators(plan) {
    return (plan?.groups || []).flatMap(group => group.operators || []);
}

function sortMonthOperatorsForSelectedDay(operators) {
    const boardRows = flattenBoardRows(state.board);
    const selectedPairs = new Map(boardRows.map(row => [row.codigoFJ, String(row.pairKey || "").trim()]));
    return [...operators].sort((left, right) => {
        const leftPair = selectedPairs.get(left.codigoFJ) || "";
        const rightPair = selectedPairs.get(right.codigoFJ) || "";

        if (leftPair || rightPair) {
            const pairCompare = (leftPair || `ZZZ-${left.codigoFJ}`).localeCompare(
                rightPair || `ZZZ-${right.codigoFJ}`,
                undefined,
                { numeric: true, sensitivity: "base" }
            );
            if (pairCompare !== 0) {
                return pairCompare;
            }
        }

        return String(left.name || "").localeCompare(String(right.name || ""), undefined, {
            numeric: true,
            sensitivity: "base"
        });
    });
}

function buildLocalOptions(operatorCodigoFJ, sectorId, selectedId) {
    const options = [`<option value="">Sem area definida</option>`];
    const allowedSectorIds = getAllowedLocalSectorIdsForOperator(operatorCodigoFJ, sectorId);
    const locals = state.locals.filter(item => allowedSectorIds.includes(Number(item.sectorId)));

    locals.forEach(local => {
        options.push(
            `<option value="${local.id}" ${Number(selectedId) === Number(local.id) ? "selected" : ""}>${escapeHtml(local.shortCode || local.name)}</option>`
        );
    });

    return options.join("");
}

function findLocalMeta(localId) {
    return state.locals.find(item => Number(item.id) === Number(localId)) || null;
}

function syncAssignmentCodeFromLocal(container) {
    const localId = nullableNumber(readField(container, "localId"));
    const assignmentInput = container.querySelector('[data-field="assignmentCode"]');
    if (!assignmentInput) {
        return;
    }

    if (!localId) {
        assignmentInput.value = "";
        return;
    }

    const local = findLocalMeta(localId);
    if (!local) {
        return;
    }

    const baseCode = String(local.shortCode || local.name || "").trim();
    if (!baseCode) {
        return;
    }

    const currentCode = String(assignmentInput.value || "").trim();
    const shouldAppendTrainee = readCheckbox(container, "isTrainee") || currentCode.endsWith("#");
    assignmentInput.value = `${baseCode}${shouldAppendTrainee ? "#" : ""}`;
}

function buildTrainerOptions(sectorId, shiftId, selectedCodigoFJ, currentOperatorCodigoFJ) {
    const options = [`<option value="">Sem treinador</option>`];
    const trainers = state.operators.filter(item => {
        return isOperatorVisibleInSector(item, sectorId)
            && Number(item.shiftId) === Number(shiftId)
            && item.codigoFJ !== currentOperatorCodigoFJ;
    });

    trainers.forEach(operator => {
        const tags = [];
        if (operator.trainer) {
            tags.push("Trainer");
        }
        if (operator.isLeader) {
            tags.push("Lider");
        }

        const suffix = tags.length ? ` - ${tags.join("/")}` : "";
        options.push(
            `<option value="${escapeAttr(operator.codigoFJ)}" ${operator.codigoFJ === selectedCodigoFJ ? "selected" : ""}>${escapeHtml(operator.name)}${escapeHtml(suffix)}</option>`
        );
    });

    return options.join("");
}

function buildPairOptions(row) {
    const selectedPair = String(row?.pairKey || "").trim();
    const groupId = Number(row?.groupId || 0);
    const groupName = String(row?.groupName || groupId || "G").trim();
    const prefix = groupId > 0
        ? `G${groupId}`
        : sanitizePairToken(groupName || "G");
    const usedPairs = flattenBoardRows(state.board)
        .filter(item => Number(item.groupId || 0) === groupId && item.pairKey)
        .map(item => String(item.pairKey).trim())
        .filter(Boolean);
    const defaultPairs = Array.from({ length: 12 }, (_, index) => `${prefix}-D${String(index + 1).padStart(2, "0")}`);
    const pairs = Array.from(new Set([selectedPair, ...usedPairs, ...defaultPairs].filter(Boolean)));

    return [
        `<option value="" ${selectedPair ? "" : "selected"}>Sem dupla</option>`,
        ...pairs.map((pair, index) => {
            const pairNumber = pair.match(/D(\d+)$/i)?.[1] || String(index + 1).padStart(2, "0");
            const label = pair.startsWith(prefix)
                ? `Dupla ${pairNumber}`
                : `Dupla ${pair}`;
            return `<option value="${escapeAttr(pair)}" ${pair === selectedPair ? "selected" : ""}>${escapeHtml(label)}</option>`;
        })
    ].join("");
}

function sanitizePairToken(value) {
    return String(value || "G")
        .normalize("NFD")
        .replace(/[\u0300-\u036f]/g, "")
        .replace(/[^A-Za-z0-9]+/g, "")
        .toUpperCase()
        .slice(0, 8) || "G";
}

function buildReplacementHint(currentOperatorCodigoFJ) {
    const sectorId = Number(document.getElementById("sectorPicker").value || 0);
    const shiftId = Number(document.getElementById("shiftPicker").value || 0);
    return state.operators
        .filter(item =>
            isOperatorVisibleInSector(item, sectorId) &&
            Number(item.shiftId) === shiftId &&
            item.codigoFJ !== currentOperatorCodigoFJ)
        .slice(0, 5)
        .map(item => `${item.codigoFJ} ${item.name}`)
        .join(", ");
}

function findOperatorMeta(codigoFJ) {
    return state.operators.find(item => item.codigoFJ === codigoFJ) || null;
}

function isSharedSupportSector(sectorId) {
    if (!state.sharedSectorTopology.isEnabled) {
        return false;
    }

    const normalizedSectorId = Number(sectorId);
    return state.sharedSectorTopology.memberSectorIds.includes(normalizedSectorId);
}

function isOperatorVisibleInSector(operator, sectorId) {
    const normalizedSectorId = Number(sectorId);
    const operatorSectorId = Number(operator?.sectorId || 0);
    return operatorSectorId === normalizedSectorId
        || (isSharedSupportSector(normalizedSectorId)
            && operatorSectorId === state.sharedSectorTopology.sharedSectorId);
}

function getAllowedLocalSectorIdsForOperator(operatorCodigoFJ, sectorId) {
    const normalizedSectorId = Number(sectorId);
    const operatorMeta = findOperatorMeta(operatorCodigoFJ);
    if (state.sharedSectorTopology.isEnabled
        && (Number(operatorMeta?.sectorId || 0) === state.sharedSectorTopology.sharedSectorId
            || normalizedSectorId === state.sharedSectorTopology.sharedSectorId)) {
        return [...state.sharedSectorTopology.memberSectorIds];
    }

    return [normalizedSectorId];
}

function buildPlanCellClasses(value, status, isHolidayWork = false) {
    if (isHolidayWork) {
        return "cell-shukkin";
    }

    const normalizedStatus = String(status || "").trim().toLowerCase();
    if (normalizedStatus === "falta") {
        return "cell-absence";
    }

    if (normalizedStatus === "yukyu") {
        return "cell-yukyu";
    }

    const raw = String(value || "").trim().toUpperCase();
    if (raw === "休" || raw === "FOLGA" || raw === "OFF" || raw === "-") {
        return "cell-off";
    }

    if (raw.endsWith("#")) {
        return "cell-trainee";
    }

    return "";
}

function normalizeCellValue(input) {
    input.value = String(input.value || "").trim().toUpperCase();
    input.dataset.status = "";
    input.classList.remove("cell-off", "cell-trainee", "cell-absence", "cell-yukyu", "cell-shukkin");

    const classes = buildPlanCellClasses(input.value, "");
    if (classes) {
        input.classList.add(...classes.split(" "));
    }
}

function fillSelect(id, items, selectedId) {
    const element = document.getElementById(id);
    element.innerHTML = (items || [])
        .map(item => `<option value="${item.id}">${escapeHtml(item.name)}</option>`)
        .join("");
    element.value = String(selectedId ?? "");
}

function readField(container, fieldName) {
    const element = container.querySelector(`[data-field="${fieldName}"]`);
    return element ? String(element.value || "").trim() : "";
}

function readCheckbox(container, fieldName) {
    const element = container.querySelector(`[data-field="${fieldName}"]`);
    return Boolean(element?.checked);
}

function nullableNumber(value) {
    const parsed = Number(value || 0);
    return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
}

function statusClass(status) {
    switch ((status || "").toLowerCase()) {
        case "escalado":
            return "status-escalado";
        case "yukyu":
            return "status-yukyu";
        case "falta":
            return "status-falta";
        case "atraso":
            return "status-atraso";
        case "saiu cedo":
            return "status-saiu-cedo";
        case "folga":
            return "status-folga";
        default:
            return "status-pendente";
    }
}

function showNotice(message, kind) {
    const notice = document.getElementById("notice");
    notice.textContent = message;
    notice.className = `notice notice-${kind || "warning"}`;
}

function hideNotice() {
    const notice = document.getElementById("notice");
    notice.className = "notice hidden";
    notice.textContent = "";
}

function showLoading() {
    document.getElementById("loadingOverlay").classList.remove("hidden");
    document.getElementById("btnExport").disabled = true;
}

function hideLoading() {
    document.getElementById("loadingOverlay").classList.add("hidden");
    document.getElementById("btnExport").disabled = false;
}

function post(payload) {
    window.chrome?.webview?.postMessage(payload);
}

function setText(id, value) {
    const element = document.getElementById(id);
    if (element) {
        element.textContent = value ?? "-";
    }
}

function cssEscape(value) {
    return String(value || "").replaceAll("\\", "\\\\").replaceAll("\"", "\\\"");
}

function escapeHtml(value) {
    return String(value ?? "")
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll("\"", "&quot;");
}

function escapeAttr(value) {
    return escapeHtml(value).replaceAll("'", "&#39;");
}
