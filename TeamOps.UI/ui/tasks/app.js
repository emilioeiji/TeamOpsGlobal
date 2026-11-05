const state = {
    locale: "pt-BR",
    currentOperatorNamePt: "",
    currentOperatorNameJp: "",
    currentUser: ""
};

const I18N = {
    "pt-BR": {
        title: "Tasks",
        headerTitle: "Gestao de Tasks",
        eyebrow: "PDCA do Dia a Dia",
        heroTitle: "Solicitacoes claras, com prazo, turno e responsavel",
        heroText: "Gerente e GL podem cadastrar as solicitacoes, e os GLs acompanham o andamento ate a finalizacao sem misturar com hikitsugui.",
        metricOpen: "Abertas",
        metricProgress: "Em Andamento",
        metricOverdue: "Atrasadas",
        tableTitle: "Tasks cadastradas",
        subtitlePrefix: "Responsavel logado",
        subtitleUser: "Usuario",
        showClosed: "Mostrar encerradas",
        statusLabel: "Status",
        searchLabel: "Buscar",
        searchPlaceholder: "Buscar por task, lider, turno ou status...",
        newTask: "Nova Task",
        loading: "Carregando...",
        empty: "Nenhuma task encontrada.",
        tableColWhat: "O que",
        tableColWhen: "Quando",
        tableColShift: "Turno",
        tableColWho: "Quem",
        tableColUpdated: "Atualizada",
        tableColActions: "Acoes",
        createdBy: "Criada por",
        noLeader: "Sem lider",
        overdue: "Atrasada",
        onTime: "No prazo",
        actionView: "Visualizar",
        actionEdit: "Editar",
        actionFinish: "Finalizar",
        confirmFinish: id => `Deseja finalizar a task #${id}?`,
        saveSuccess: "Task cadastrada com sucesso.",
        updateSuccess: "Task atualizada com sucesso.",
        statusSuccess: "Status da task atualizado com sucesso.",
        errorFallback: "Ocorreu um erro ao processar a task.",
        selectAll: "Todos",
        selectPlaceholder: "Selecione...",
        noLeaderOption: "Sem lider definido",
        formCreateTitle: "Nova Task",
        formCreateSubtitle: "Registre o que deve ser feito, para quando e qual lider do turno vai acompanhar.",
        formEditTitle: id => `Editar Task #${id}`,
        formEditSubtitle: id => `Acompanhe a task #${id}, ajuste o status e mantenha o controle do andamento.`,
        metaCreatedBy: "Criada por",
        metaCreatedAt: "Criada em",
        metaUpdatedAt: "Atualizada em",
        metaCompletedAt: "Finalizada em",
        descriptionLabel: "O que deve ser feito",
        descriptionPlaceholder: "Descreva a solicitacao da task com clareza...",
        dueDateLabel: "Quando",
        shiftLabel: "Turno",
        sectorLabel: "Setor",
        leaderLabel: "Quem",
        leaderHint: "Opcional. Lista apenas lideres ativos do turno selecionado.",
        taskStatusLabel: "Status",
        historyTitle: "Historico de Status",
        historySubtitle: "Ultimas alteracoes registradas para esta task.",
        historyEmpty: "Nenhuma mudanca de status registrada ainda.",
        historyFromTo: (fromLabel, toLabel) => `De ${fromLabel} para ${toLabel}`,
        historyInitial: toLabel => `Status inicial: ${toLabel}`,
        historyBy: "Por",
        cancel: "Cancelar",
        save: "Salvar",
        saveChanges: "Salvar Alteracoes",
        createTask: "Cadastrar Task",
        viewTitle: id => `Visualizar Task #${id}`,
        viewSubtitle: "Leitura completa da task, com descricao e historico.",
        viewStatus: "Status",
        viewShift: "Turno",
        viewLeader: "Responsavel",
        viewDueDate: "Prazo",
        viewCreatedBy: "Criada por",
        viewCreatedAt: "Criada em",
        viewUpdatedAt: "Atualizada em",
        viewCompletedAt: "Finalizada em",
        viewDescriptionTitle: "Descricao completa",
        viewDescriptionSubtitle: "Use esta visualizacao quando a task tiver muitos itens, passos ou observacoes.",
        viewHistoryTitle: "Historico de Status",
        viewHistorySubtitle: "Acompanhe como a task evoluiu ao longo do tempo.",
        close: "Fechar",
        editTask: "Editar Task",
        noDescription: "Sem descricao.",
        closeModalAria: "Fechar modal"
    },
    "ja-JP": {
        title: "Tasks",
        headerTitle: "Task Management",
        eyebrow: "Daily PDCA",
        heroTitle: "依頼内容、期限、シフト、担当者を分かりやすく管理",
        heroText: "管理者と GL が依頼を登録し、GL が完了まで進捗を追跡できます。Hikitsugui と混在させません。",
        metricOpen: "未完了",
        metricProgress: "進行中",
        metricOverdue: "期限超過",
        tableTitle: "登録済み Task",
        subtitlePrefix: "担当オペレーター",
        subtitleUser: "ユーザー",
        showClosed: "完了済みも表示",
        statusLabel: "ステータス",
        searchLabel: "検索",
        searchPlaceholder: "Task、担当者、シフト、ステータスで検索...",
        newTask: "新規 Task",
        loading: "読み込み中...",
        empty: "Task が見つかりません。",
        tableColWhat: "内容",
        tableColWhen: "期限",
        tableColShift: "シフト",
        tableColWho: "担当",
        tableColUpdated: "更新日時",
        tableColActions: "操作",
        createdBy: "作成者",
        noLeader: "担当者なし",
        overdue: "期限超過",
        onTime: "期限内",
        actionView: "表示",
        actionEdit: "編集",
        actionFinish: "完了",
        confirmFinish: id => `Task #${id} を完了にしますか。`,
        saveSuccess: "Task を登録しました。",
        updateSuccess: "Task を更新しました。",
        statusSuccess: "Task のステータスを更新しました。",
        errorFallback: "Task の処理中にエラーが発生しました。",
        selectAll: "すべて",
        selectPlaceholder: "選択してください",
        noLeaderOption: "担当者なし",
        formCreateTitle: "新規 Task",
        formCreateSubtitle: "内容、期限、シフト、担当リーダーを登録してください。",
        formEditTitle: id => `Task #${id} を編集`,
        formEditSubtitle: id => `Task #${id} の内容とステータスを更新します。`,
        metaCreatedBy: "作成者",
        metaCreatedAt: "作成日時",
        metaUpdatedAt: "更新日時",
        metaCompletedAt: "完了日時",
        descriptionLabel: "実施内容",
        descriptionPlaceholder: "Task の内容を分かりやすく入力してください...",
        dueDateLabel: "期限",
        shiftLabel: "シフト",
        sectorLabel: "セクター",
        leaderLabel: "担当者",
        leaderHint: "任意です。選択したシフトの有効なリーダーのみ表示します。",
        taskStatusLabel: "ステータス",
        historyTitle: "ステータス履歴",
        historySubtitle: "この Task の最新の変更履歴です。",
        historyEmpty: "まだステータス変更履歴はありません。",
        historyFromTo: (fromLabel, toLabel) => `${fromLabel} から ${toLabel} へ変更`,
        historyInitial: toLabel => `初期ステータス: ${toLabel}`,
        historyBy: "更新者",
        cancel: "キャンセル",
        save: "保存",
        saveChanges: "変更を保存",
        createTask: "Task を登録",
        viewTitle: id => `Task #${id} を表示`,
        viewSubtitle: "Task の内容と履歴をまとめて確認できます。",
        viewStatus: "ステータス",
        viewShift: "シフト",
        viewLeader: "担当者",
        viewDueDate: "期限",
        viewCreatedBy: "作成者",
        viewCreatedAt: "作成日時",
        viewUpdatedAt: "更新日時",
        viewCompletedAt: "完了日時",
        viewDescriptionTitle: "詳細内容",
        viewDescriptionSubtitle: "項目が多い Task もここで全文を読みやすく確認できます。",
        viewHistoryTitle: "ステータス履歴",
        viewHistorySubtitle: "Task の進捗変化を時系列で確認できます。",
        close: "閉じる",
        editTask: "Task を編集",
        noDescription: "説明はありません。",
        closeModalAria: "モーダルを閉じる"
    }
};

let rows = [];
let shifts = [];
let sectors = [];
let leaders = [];
let statuses = [];
let defaultSectorId = 0;
let currentModalMode = "create";
let editingTaskId = 0;
let viewingTaskId = 0;

window.addEventListener("DOMContentLoaded", () => {
    bindEvents();
    send("load");
});

function bindEvents() {
    document.getElementById("searchInput").addEventListener("input", applyFilters);
    document.getElementById("statusFilter").addEventListener("change", applyFilters);
    document.getElementById("toggleShowClosed").addEventListener("change", applyFilters);
    document.getElementById("btnHeaderNovo").addEventListener("click", openCreateModal);
    document.getElementById("btnSaveModal").addEventListener("click", submitModal);
    document.getElementById("btnCancelModal").addEventListener("click", closeModal);
    document.getElementById("btnCloseModal").addEventListener("click", closeModal);
    document.getElementById("modalBackdrop").addEventListener("click", closeModal);
    document.getElementById("cmbShift").addEventListener("change", event => {
        updateLeaderOptions(event.target.value, document.getElementById("cmbSector").value, "");
    });
    document.getElementById("cmbSector").addEventListener("change", event => {
        updateLeaderOptions(document.getElementById("cmbShift").value, event.target.value, "");
    });

    document.getElementById("btnCloseViewModal").addEventListener("click", closeViewModal);
    document.getElementById("btnCloseView").addEventListener("click", closeViewModal);
    document.getElementById("viewModalBackdrop").addEventListener("click", closeViewModal);
    document.getElementById("btnEditFromView").addEventListener("click", () => {
        const taskId = viewingTaskId;
        closeViewModal();
        if (taskId) {
            openEditModal(taskId);
        }
    });
}

function send(action, extra = {}) {
    window.chrome.webview.postMessage({
        action,
        ...extra
    });
}

window.chrome.webview.addEventListener("message", event => {
    const msg = event.data;

    switch (msg.type) {
        case "init":
            hydrateScreen(msg);
            break;

        case "rows":
            rows = normalizeRows(msg.data || []);
            renderMetrics(rows);
            applyFilters();
            syncTaskModals();
            break;

        case "task_details":
            if (Number(msg.id) === Number(editingTaskId)) {
                renderHistoryList("historyList", msg.history || []);
            }

            if (Number(msg.id) === Number(viewingTaskId)) {
                renderHistoryList("viewHistoryList", msg.history || []);
            }
            break;

        case "saved":
            alert(msg.message || t("saveSuccess"));
            closeModal();
            break;

        case "updated":
            alert(msg.message || t("updateSuccess"));
            closeModal();
            break;

        case "status_changed":
            alert(msg.message || t("statusSuccess"));
            break;

        case "error":
            alert(msg.message || t("errorFallback"));
            break;
    }
});

function hydrateScreen(payload) {
    state.locale = payload.locale === "ja-JP" ? "ja-JP" : "pt-BR";
    state.currentOperatorNamePt = payload.currentOperatorNamePt || "";
    state.currentOperatorNameJp = payload.currentOperatorNameJp || payload.currentOperatorNamePt || "";
    state.currentUser = payload.currentUser || "";

    rows = normalizeRows(payload.rows || []);
    shifts = normalizeShifts(payload.shifts || []);
    sectors = normalizeShifts(payload.sectors || []);
    leaders = normalizeLeaders(payload.leaders || []);
    statuses = normalizeStatuses(payload.statuses || []);
    defaultSectorId = Number(payload.defaultSectorId || 0);

    setLocale(state.locale);
    refillLookupControls();
    resetModalForm();
    closeViewModal();
    renderMetrics(rows);
    applyFilters();
}

function normalizeShifts(items) {
    return items.map(item => ({
        id: Number(item.id || 0),
        namePt: item.namePt || "",
        nameJp: item.nameJp || item.namePt || ""
    }));
}

function normalizeLeaders(items) {
    return items.map(item => ({
        codigoFJ: item.codigoFJ || "",
        namePt: item.namePt || "",
        nameJp: item.nameJp || item.namePt || "",
        shiftId: Number(item.shiftId || 0),
        sectorId: Number(item.sectorId || 0)
    }));
}

function normalizeStatuses(items) {
    return items.map(item => ({
        value: item.value || "",
        labelPt: item.labelPt || item.label || "",
        labelJp: item.labelJp || item.labelPt || item.label || ""
    }));
}

function normalizeRows(items) {
    return items.map(item => ({
        id: Number(item.id || 0),
        description: item.description || "",
        dueDate: item.dueDate || "",
        shiftId: Number(item.shiftId || 0),
        sectorId: Number(item.sectorId || 0),
        sectorNamePt: item.sectorNamePt || "",
        sectorNameJp: item.sectorNameJp || item.sectorNamePt || "",
        shiftNamePt: item.shiftNamePt || "",
        shiftNameJp: item.shiftNameJp || item.shiftNamePt || "",
        leaderCodigoFJ: item.leaderCodigoFJ || "",
        leaderNamePt: item.leaderNamePt || "",
        leaderNameJp: item.leaderNameJp || item.leaderNamePt || "",
        taskStatus: item.taskStatus || "pending",
        createdByNamePt: item.createdByNamePt || "",
        createdByNameJp: item.createdByNameJp || item.createdByNamePt || "",
        createdAt: item.createdAt || "",
        updatedAt: item.updatedAt || "",
        startedAt: item.startedAt || "",
        completedAt: item.completedAt || "",
        cancelledAt: item.cancelledAt || ""
    }));
}

function refillLookupControls() {
    const currentShift = document.getElementById("cmbShift").value;
    const currentSector = document.getElementById("cmbSector").value;
    const currentStatus = document.getElementById("cmbTaskStatus").value;
    const currentFilterStatus = document.getElementById("statusFilter").value;
    const currentLeader = document.getElementById("cmbLeader").value;

    fillSelect("cmbShift", shifts, "id", getLocalizedFieldName(), t("selectPlaceholder"));
    fillSelect("cmbSector", sectors, "id", getLocalizedFieldName(), t("selectPlaceholder"));
    fillSelect("cmbTaskStatus", statuses, "value", getStatusFieldName(), t("selectPlaceholder"));
    fillSelect("statusFilter", statuses, "value", getStatusFieldName(), t("selectAll"));

    document.getElementById("cmbShift").value = currentShift;
    document.getElementById("cmbSector").value = currentSector || String(defaultSectorId || "");
    document.getElementById("cmbTaskStatus").value = currentStatus || "pending";
    document.getElementById("statusFilter").value = currentFilterStatus;
    updateLeaderOptions(
        document.getElementById("cmbShift").value,
        document.getElementById("cmbSector").value,
        currentLeader);
}

function fillSelect(id, items, valueField, textField, firstLabel) {
    const select = document.getElementById(id);
    const options = [`<option value="">${escapeHtml(firstLabel)}</option>`];

    items.forEach(item => {
        options.push(`<option value="${escapeHtmlAttr(item[valueField])}">${escapeHtml(item[textField])}</option>`);
    });

    select.innerHTML = options.join("");
}

function renderMetrics(items) {
    const openCount = items.filter(item => !isClosedStatus(item.taskStatus)).length;
    const progressCount = items.filter(item => item.taskStatus === "in_progress").length;
    const overdueCount = items.filter(item => isOverdue(item)).length;

    document.getElementById("metricOpen").textContent = String(openCount);
    document.getElementById("metricProgress").textContent = String(progressCount);
    document.getElementById("metricOverdue").textContent = String(overdueCount);
}

function applyFilters() {
    const term = document.getElementById("searchInput").value.trim().toLowerCase();
    const statusFilter = document.getElementById("statusFilter").value;
    const showClosed = document.getElementById("toggleShowClosed").checked;

    const filtered = rows.filter(item => {
        if (!showClosed && isClosedStatus(item.taskStatus)) return false;
        if (statusFilter && item.taskStatus !== statusFilter) return false;
        if (!term) return true;

        const haystack = [
            item.description,
            item.shiftNamePt,
            item.shiftNameJp,
            item.leaderNamePt,
            item.leaderNameJp,
            item.createdByNamePt,
            item.createdByNameJp,
            getStatusLabel(item.taskStatus, "pt-BR"),
            getStatusLabel(item.taskStatus, "ja-JP")
        ]
            .filter(Boolean)
            .join(" ")
            .toLowerCase();

        return haystack.includes(term);
    });

    renderTable(filtered);
}

function renderTable(items) {
    const container = document.getElementById("tableContainer");

    if (!items.length) {
        container.innerHTML = `<div class="empty-state">${escapeHtml(t("empty"))}</div>`;
        return;
    }

    let html = `
        <table class="tasks-table">
            <thead>
                <tr>
                    <th>#</th>
                    <th>${escapeHtml(t("tableColWhat"))}</th>
                    <th>${escapeHtml(t("tableColWhen"))}</th>
                    <th>${escapeHtml(t("tableColShift"))}</th>
                    <th>${escapeHtml(t("tableColWho"))}</th>
                    <th>${escapeHtml(t("statusLabel"))}</th>
                    <th>${escapeHtml(t("tableColUpdated"))}</th>
                    <th>${escapeHtml(t("tableColActions"))}</th>
                </tr>
            </thead>
            <tbody>
    `;

    items.forEach(item => {
        const selectedClass = item.id === editingTaskId || item.id === viewingTaskId ? "is-selected" : "";
        const overdueClass = isOverdue(item) ? "due-overdue" : "";
        const dueLabel = isOverdue(item) ? t("overdue") : t("onTime");
        const leaderName = getLocalizedValue(item.leaderNamePt, item.leaderNameJp) || t("noLeader");
        const createdByName = getLocalizedValue(item.createdByNamePt, item.createdByNameJp) || "-";
        const shiftName = getLocalizedValue(item.shiftNamePt, item.shiftNameJp) || "-";

        html += `
            <tr class="${selectedClass}">
                <td>${item.id}</td>
                <td>
                    <div class="task-main">
                        <strong title="${escapeHtmlAttr(item.description)}">${escapeHtml(truncate(item.description, 110))}</strong>
                        <small>${escapeHtml(t("createdBy"))} ${escapeHtml(createdByName)}</small>
                    </div>
                </td>
                <td>
                    <div class="due-stack">
                        <strong class="${overdueClass}">${escapeHtml(item.dueDate || "-")}</strong>
                        <small class="${overdueClass}">${escapeHtml(dueLabel)}</small>
                    </div>
                </td>
                <td>${escapeHtml(shiftName)}</td>
                <td>${escapeHtml(leaderName)}</td>
                <td><span class="status-pill ${statusClass(item.taskStatus)}">${escapeHtml(getStatusLabel(item.taskStatus))}</span></td>
                <td>${escapeHtml(item.updatedAt || "-")}</td>
                <td class="actions-cell">
                    <div class="actions-stack">
                        <button class="icon-btn icon-btn-view" type="button" data-view="${item.id}" title="${escapeHtmlAttr(t("actionView"))}" aria-label="${escapeHtmlAttr(t("actionView"))}">
                            ${iconEye()}
                        </button>
                        <button class="icon-btn icon-btn-edit" type="button" data-edit="${item.id}" title="${escapeHtmlAttr(t("actionEdit"))}" aria-label="${escapeHtmlAttr(t("actionEdit"))}">
                            ${iconEdit()}
                        </button>
                        ${isClosedStatus(item.taskStatus)
                            ? ""
                            : `<button class="icon-btn icon-btn-finish" type="button" data-finish="${item.id}" title="${escapeHtmlAttr(t("actionFinish"))}" aria-label="${escapeHtmlAttr(t("actionFinish"))}">
                                ${iconCheck()}
                            </button>`}
                    </div>
                </td>
            </tr>
        `;
    });

    html += `
            </tbody>
        </table>
    `;

    container.innerHTML = html;

    container.querySelectorAll("[data-view]").forEach(button => {
        button.addEventListener("click", () => {
            openViewModal(Number(button.dataset.view));
        });
    });

    container.querySelectorAll("[data-edit]").forEach(button => {
        button.addEventListener("click", () => {
            openEditModal(Number(button.dataset.edit));
        });
    });

    container.querySelectorAll("[data-finish]").forEach(button => {
        button.addEventListener("click", () => {
            quickFinishTask(Number(button.dataset.finish));
        });
    });
}

function openCreateModal() {
    currentModalMode = "create";
    resetModalForm();
    syncModeState();
    document.getElementById("taskModal").classList.remove("hidden");
}

function openEditModal(taskId) {
    const task = rows.find(item => item.id === taskId);
    if (!task) return;

    currentModalMode = "edit";
    editingTaskId = task.id;

    fillEditModal(task);
    renderHistoryList("historyList", []);
    syncModeState();
    applyFilters();
    document.getElementById("taskModal").classList.remove("hidden");
    send("load_details", { id: task.id });
}

function fillEditModal(task) {
    document.getElementById("txtDescription").value = task.description || "";
    document.getElementById("dtDueDate").value = task.dueDate || "";
    document.getElementById("cmbShift").value = String(task.shiftId || "");
    document.getElementById("cmbSector").value = String(task.sectorId || "");
    updateLeaderOptions(task.shiftId, task.sectorId, task.leaderCodigoFJ || "");
    document.getElementById("cmbTaskStatus").value = task.taskStatus || "pending";

    document.getElementById("metaCreatedBy").textContent = getLocalizedValue(task.createdByNamePt, task.createdByNameJp) || "-";
    document.getElementById("metaCreatedAt").textContent = task.createdAt || "-";
    document.getElementById("metaUpdatedAt").textContent = task.updatedAt || "-";
    document.getElementById("metaCompletedAt").textContent = task.completedAt || task.cancelledAt || "-";
}

function openViewModal(taskId) {
    const task = rows.find(item => item.id === taskId);
    if (!task) return;

    viewingTaskId = task.id;
    fillViewModal(task);
    renderHistoryList("viewHistoryList", []);
    applyFilters();
    document.getElementById("viewModal").classList.remove("hidden");
    send("load_details", { id: task.id });
}

function fillViewModal(task) {
    document.getElementById("viewTitle").textContent = t("viewTitle")(task.id);
    document.getElementById("viewStatus").textContent = getStatusLabel(task.taskStatus);
    document.getElementById("viewShift").textContent = getLocalizedValue(task.shiftNamePt, task.shiftNameJp) || "-";
    document.getElementById("viewSector").textContent = getLocalizedValue(task.sectorNamePt, task.sectorNameJp) || "-";
    document.getElementById("viewLeader").textContent = getLocalizedValue(task.leaderNamePt, task.leaderNameJp) || t("noLeader");
    document.getElementById("viewDueDate").textContent = task.dueDate || "-";
    document.getElementById("viewCreatedBy").textContent = getLocalizedValue(task.createdByNamePt, task.createdByNameJp) || "-";
    document.getElementById("viewCreatedAt").textContent = task.createdAt || "-";
    document.getElementById("viewUpdatedAt").textContent = task.updatedAt || "-";
    document.getElementById("viewCompletedAt").textContent = task.completedAt || task.cancelledAt || "-";
    document.getElementById("viewDescription").textContent = task.description || t("noDescription");
}

function syncTaskModals() {
    if (editingTaskId && !document.getElementById("taskModal").classList.contains("hidden")) {
        const task = rows.find(item => item.id === editingTaskId);
        if (!task) {
            closeModal();
        } else {
            fillEditModal(task);
            syncModeState();
        }
    }

    if (viewingTaskId && !document.getElementById("viewModal").classList.contains("hidden")) {
        const task = rows.find(item => item.id === viewingTaskId);
        if (!task) {
            closeViewModal();
        } else {
            fillViewModal(task);
        }
    }
}

function quickFinishTask(taskId) {
    const task = rows.find(item => item.id === taskId);
    if (!task) return;

    if (!confirm(t("confirmFinish")(task.id))) {
        return;
    }

    send("change_status", {
        id: task.id,
        taskStatus: "completed"
    });
}

function resetModalForm() {
    editingTaskId = 0;
    document.getElementById("txtDescription").value = "";
    document.getElementById("dtDueDate").value = "";
    document.getElementById("cmbShift").value = "";
    document.getElementById("cmbSector").value = String(defaultSectorId || "");
    document.getElementById("cmbTaskStatus").value = "pending";
    document.getElementById("metaCreatedBy").textContent = "-";
    document.getElementById("metaCreatedAt").textContent = "-";
    document.getElementById("metaUpdatedAt").textContent = "-";
    document.getElementById("metaCompletedAt").textContent = "-";
    updateLeaderOptions("", defaultSectorId, "");
    renderHistoryList("historyList", []);
    syncModeState();
    applyFilters();
}

function closeModal() {
    document.getElementById("taskModal").classList.add("hidden");
    currentModalMode = "create";
    resetModalForm();
}

function closeViewModal() {
    viewingTaskId = 0;
    document.getElementById("viewModal").classList.add("hidden");
    document.getElementById("viewDescription").textContent = "-";
    renderHistoryList("viewHistoryList", []);
    applyFilters();
}

function updateLeaderOptions(shiftId, sectorId, selectedLeaderCodigoFJ) {
    const select = document.getElementById("cmbLeader");
    const normalizedShiftId = Number(shiftId || 0);
    const normalizedSectorId = Number(sectorId || 0);
    const availableLeaders = leaders.filter(item =>
        Number(item.shiftId) === normalizedShiftId && Number(item.sectorId) === normalizedSectorId);
    const labelField = getLocalizedFieldName();
    const options = [`<option value="">${escapeHtml(t("noLeaderOption"))}</option>`];

    availableLeaders.forEach(item => {
        options.push(`<option value="${escapeHtmlAttr(item.codigoFJ)}">${escapeHtml(item[labelField])}</option>`);
    });

    select.innerHTML = options.join("");
    select.value = selectedLeaderCodigoFJ || "";
}

function submitModal() {
    const payload = buildPayload();

    if (currentModalMode === "edit") {
        send("update", {
            id: editingTaskId,
            ...payload
        });
        return;
    }

    send("save", payload);
}

function buildPayload() {
    return {
        description: document.getElementById("txtDescription").value.trim(),
        dueDate: document.getElementById("dtDueDate").value,
        shiftId: Number(document.getElementById("cmbShift").value || 0),
        sectorId: Number(document.getElementById("cmbSector").value || 0),
        leaderCodigoFJ: document.getElementById("cmbLeader").value,
        taskStatus: document.getElementById("cmbTaskStatus").value || "pending"
    };
}

function renderHistoryList(containerId, items) {
    const list = document.getElementById(containerId);

    if (!items.length) {
        list.innerHTML = `<div class="history-empty">${escapeHtml(t("historyEmpty"))}</div>`;
        return;
    }

    list.innerHTML = items.map(item => {
        const previousLabel = getHistoryStatusLabel(item, "previous");
        const newLabel = getHistoryStatusLabel(item, "new");
        const changedBy = getLocalizedValue(item.changedByNamePt, item.changedByNameJp) || "-";
        const description = previousLabel
            ? t("historyFromTo")(previousLabel, newLabel)
            : t("historyInitial")(newLabel);
        const noteSuffix = item.note ? ` | ${escapeHtml(item.note)}` : "";

        return `
            <article class="history-item">
                <div class="history-head">
                    <strong>${escapeHtml(newLabel || "-")}</strong>
                    <span>${escapeHtml(item.changedAt || "-")}</span>
                </div>
                <p>${escapeHtml(description)}</p>
                <small>${escapeHtml(t("historyBy"))} ${escapeHtml(changedBy)}${noteSuffix}</small>
            </article>
        `;
    }).join("");
}

function syncModeState() {
    const isEdit = currentModalMode === "edit" && !!editingTaskId;
    document.getElementById("formTitle").textContent = isEdit ? t("formEditTitle")(editingTaskId) : t("formCreateTitle");
    document.getElementById("formSubtitle").textContent = isEdit
        ? t("formEditSubtitle")(editingTaskId)
        : t("formCreateSubtitle");
    document.getElementById("btnSaveModal").textContent = isEdit ? t("saveChanges") : t("createTask");
    document.getElementById("metaSummary").classList.toggle("hidden", !isEdit);
    document.getElementById("historySection").classList.toggle("hidden", !isEdit);
}

function setLocale(locale) {
    state.locale = locale === "ja-JP" ? "ja-JP" : "pt-BR";
    document.documentElement.lang = state.locale;
    document.title = t("title");

    document.getElementById("txtHeaderTitle").textContent = t("headerTitle");
    document.getElementById("txtEyebrow").textContent = t("eyebrow");
    document.getElementById("txtHeroTitle").textContent = t("heroTitle");
    document.getElementById("txtHeroText").textContent = t("heroText");
    document.getElementById("txtMetricOpen").textContent = t("metricOpen");
    document.getElementById("txtMetricProgress").textContent = t("metricProgress");
    document.getElementById("txtMetricOverdue").textContent = t("metricOverdue");
    document.getElementById("txtTableTitle").textContent = t("tableTitle");
    document.getElementById("txtShowClosed").textContent = t("showClosed");
    document.getElementById("txtStatusLabel").textContent = t("statusLabel");
    document.getElementById("txtSearchLabel").textContent = t("searchLabel");
    document.getElementById("searchInput").placeholder = t("searchPlaceholder");
    document.getElementById("btnHeaderNovo").textContent = t("newTask");

    document.getElementById("txtMetaCreatedBy").textContent = t("metaCreatedBy");
    document.getElementById("txtMetaCreatedAt").textContent = t("metaCreatedAt");
    document.getElementById("txtMetaUpdatedAt").textContent = t("metaUpdatedAt");
    document.getElementById("txtMetaCompletedAt").textContent = t("metaCompletedAt");
    document.getElementById("txtDescriptionLabel").textContent = t("descriptionLabel");
    document.getElementById("txtDescription").placeholder = t("descriptionPlaceholder");
    document.getElementById("txtDueDateLabel").textContent = t("dueDateLabel");
    document.getElementById("txtShiftLabel").textContent = t("shiftLabel");
    document.getElementById("txtSectorLabel").textContent = t("sectorLabel");
    document.getElementById("txtViewSector").textContent = t("sectorLabel");
    document.getElementById("txtLeaderLabel").textContent = t("leaderLabel");
    document.getElementById("txtLeaderHint").textContent = t("leaderHint");
    document.getElementById("txtTaskStatusLabel").textContent = t("taskStatusLabel");
    document.getElementById("txtHistoryTitle").textContent = t("historyTitle");
    document.getElementById("txtHistorySubtitle").textContent = t("historySubtitle");
    document.getElementById("btnCancelModal").textContent = t("cancel");
    document.getElementById("btnCloseModal").setAttribute("aria-label", t("closeModalAria"));

    document.getElementById("txtViewStatus").textContent = t("viewStatus");
    document.getElementById("txtViewShift").textContent = t("viewShift");
    document.getElementById("txtViewLeader").textContent = t("viewLeader");
    document.getElementById("txtViewDueDate").textContent = t("viewDueDate");
    document.getElementById("txtViewCreatedBy").textContent = t("viewCreatedBy");
    document.getElementById("txtViewCreatedAt").textContent = t("viewCreatedAt");
    document.getElementById("txtViewUpdatedAt").textContent = t("viewUpdatedAt");
    document.getElementById("txtViewCompletedAt").textContent = t("viewCompletedAt");
    document.getElementById("txtViewDescriptionTitle").textContent = t("viewDescriptionTitle");
    document.getElementById("txtViewDescriptionSubtitle").textContent = t("viewDescriptionSubtitle");
    document.getElementById("txtViewHistoryTitle").textContent = t("viewHistoryTitle");
    document.getElementById("txtViewHistorySubtitle").textContent = t("viewHistorySubtitle");
    document.getElementById("btnCloseView").textContent = t("close");
    document.getElementById("btnEditFromView").textContent = t("editTask");
    document.getElementById("btnCloseViewModal").setAttribute("aria-label", t("closeModalAria"));

    const currentOperatorName = getLocalizedValue(state.currentOperatorNamePt, state.currentOperatorNameJp) || "-";
    document.getElementById("screenSubtitle").textContent =
        `${t("subtitlePrefix")}: ${currentOperatorName} | ${t("subtitleUser")}: ${state.currentUser || "-"}`;

    refillLookupControls();
    syncModeState();

    if (viewingTaskId) {
        const task = rows.find(item => item.id === viewingTaskId);
        if (task) {
            fillViewModal(task);
        }
    }

    applyFilters();
}

function getLocalizedValue(pt, jp) {
    return state.locale === "ja-JP" ? (jp || pt || "") : (pt || jp || "");
}

function getLocalizedFieldName() {
    return state.locale === "ja-JP" ? "nameJp" : "namePt";
}

function getStatusFieldName() {
    return state.locale === "ja-JP" ? "labelJp" : "labelPt";
}

function getStatusLabel(taskStatus, locale = state.locale) {
    const match = statuses.find(item => item.value === taskStatus);
    if (!match) return taskStatus || "";
    return locale === "ja-JP" ? match.labelJp : match.labelPt;
}

function getHistoryStatusLabel(item, prefix) {
    return state.locale === "ja-JP"
        ? item[`${prefix}StatusLabelJp`] || ""
        : item[`${prefix}StatusLabelPt`] || "";
}

function t(key) {
    return I18N[state.locale]?.[key] ?? I18N["pt-BR"][key];
}

function isClosedStatus(taskStatus) {
    return taskStatus === "completed" || taskStatus === "cancelled";
}

function isOverdue(item) {
    if (isClosedStatus(item.taskStatus) || !item.dueDate) return false;
    return item.dueDate < todayIso();
}

function statusClass(taskStatus) {
    return {
        pending: "status-pending",
        in_progress: "status-progress",
        blocked: "status-blocked",
        completed: "status-completed",
        cancelled: "status-cancelled"
    }[taskStatus] || "status-pending";
}

function todayIso() {
    return new Date().toISOString().slice(0, 10);
}

function truncate(value, size) {
    const text = String(value || "");
    return text.length > size ? `${text.slice(0, size - 1)}...` : text;
}

function iconEye() {
    return `
        <svg viewBox="0 0 24 24" aria-hidden="true">
            <path d="M1.5 12s3.8-6 10.5-6 10.5 6 10.5 6-3.8 6-10.5 6S1.5 12 1.5 12Z"></path>
            <circle cx="12" cy="12" r="3.2"></circle>
        </svg>
    `;
}

function iconEdit() {
    return `
        <svg viewBox="0 0 24 24" aria-hidden="true">
            <path d="M4 16.7V20h3.3L18.7 8.6l-3.3-3.3L4 16.7Z"></path>
            <path d="M13.9 4.7l3.3 3.3"></path>
        </svg>
    `;
}

function iconCheck() {
    return `
        <svg viewBox="0 0 24 24" aria-hidden="true">
            <path d="m4 12 5 5 11-11"></path>
        </svg>
    `;
}

function escapeHtml(value) {
    return String(value ?? "")
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/\"/g, "&quot;")
        .replace(/'/g, "&#39;");
}

function escapeHtmlAttr(value) {
    return escapeHtml(value);
}
