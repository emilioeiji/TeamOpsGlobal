const state = {
    locale: "pt-BR",
    rows: [],
    shifts: [],
    sectors: [],
    statuses: [],
    allowAggregate: false,
    defaultSectorId: 0
};

const I18N = {
    "pt-BR": {
        title: "Relatorio de Tasks",
        headerTitle: "Relatorio de Tasks",
        headerSubtitle: "Leitura PDCA das tasks cadastradas, com foco em prazo, andamento e fechamento.",
        metricTotal: "Total",
        metricOpen: "Abertas",
        metricCompleted: "Concluidas",
        metricOverdue: "Atrasadas",
        dateFrom: "Data Inicial",
        dateTo: "Data Final",
        shift: "Turno",
        sector: "Setor",
        status: "Status",
        search: "Buscar",
        searchPlaceholder: "Task, lider, turno ou criador...",
        apply: "Buscar",
        clear: "Limpar",
        tableTitle: "Painel PDCA",
        tableSubtitle: "Plan, Do, Check e Act resumidos para leitura rapida.",
        colTask: "Task",
        colPlan: "Plan",
        colDo: "Do",
        colCheck: "Check",
        colAct: "Act",
        colStatus: "Status",
        colActions: "Acoes",
        empty: "Nenhuma task encontrada para o filtro atual.",
        loading: "Carregando...",
        allOption: "Todos",
        actionView: "Visualizar",
        noLeader: "Sem lider",
        noDate: "Nao iniciado",
        noClosure: "Em aberto",
        historyTitle: "Historico",
        historySubtitle: "Mudancas registradas no andamento da task.",
        historyEmpty: "Nenhuma mudanca registrada ainda.",
        modalSubtitle: "Leitura completa da task em PDCA.",
        plan: "Plan",
        do: "Do",
        check: "Check",
        act: "Act",
        labelDescription: "Descricao",
        labelDueDate: "Prazo",
        labelShift: "Turno",
        labelSector: "Setor",
        labelLeader: "Responsavel",
        labelCreatedBy: "Criada por",
        labelCreatedAt: "Criada em",
        labelStartedAt: "Iniciada em",
        labelUpdatedAt: "Atualizada em",
        labelHistoryCount: "Mudancas",
        labelCompletedAt: "Finalizada em",
        labelCancelledAt: "Cancelada em",
        close: "Fechar",
        fromTo: (fromLabel, toLabel) => `De ${fromLabel} para ${toLabel}`,
        initialStatus: toLabel => `Status inicial: ${toLabel}`
    },
    "ja-JP": {
        title: "Tasks Report",
        headerTitle: "Tasks Report",
        headerSubtitle: "Task \u306e\u671f\u9650\u3001\u9032\u6357\u3001\u30af\u30ed\u30fc\u30ba\u3092 PDCA \u3067\u898b\u3084\u3059\u304f\u78ba\u8a8d\u3057\u307e\u3059\u3002",
        metricTotal: "\u5408\u8a08",
        metricOpen: "\u672a\u5b8c\u4e86",
        metricCompleted: "\u5b8c\u4e86",
        metricOverdue: "\u671f\u9650\u8d85\u904e",
        dateFrom: "\u958b\u59cb\u65e5",
        dateTo: "\u7d42\u4e86\u65e5",
        shift: "\u30b7\u30d5\u30c8",
        sector: "\u30bb\u30af\u30bf\u30fc",
        status: "\u30b9\u30c6\u30fc\u30bf\u30b9",
        search: "\u691c\u7d22",
        searchPlaceholder: "Task\u3001\u30ea\u30fc\u30c0\u30fc\u3001\u30b7\u30d5\u30c8\u3001\u4f5c\u6210\u8005...",
        apply: "\u691c\u7d22",
        clear: "\u30af\u30ea\u30a2",
        tableTitle: "PDCA \u30d1\u30cd\u30eb",
        tableSubtitle: "Plan\u3001Do\u3001Check\u3001Act \u3092\u7c21\u6f54\u306b\u78ba\u8a8d\u3067\u304d\u307e\u3059\u3002",
        colTask: "Task",
        colPlan: "Plan",
        colDo: "Do",
        colCheck: "Check",
        colAct: "Act",
        colStatus: "\u30b9\u30c6\u30fc\u30bf\u30b9",
        colActions: "\u64cd\u4f5c",
        empty: "\u73fe\u5728\u306e\u6761\u4ef6\u3067\u306f Task \u304c\u3042\u308a\u307e\u305b\u3093\u3002",
        loading: "\u8aad\u307f\u8fbc\u307f\u4e2d...",
        allOption: "\u3059\u3079\u3066",
        actionView: "\u8868\u793a",
        noLeader: "\u62c5\u5f53\u8005\u306a\u3057",
        noDate: "\u672a\u958b\u59cb",
        noClosure: "\u672a\u30af\u30ed\u30fc\u30ba",
        historyTitle: "\u5c65\u6b74",
        historySubtitle: "Task \u306e\u9032\u6357\u5909\u66f4\u3092\u6642\u7cfb\u5217\u3067\u78ba\u8a8d\u3067\u304d\u307e\u3059\u3002",
        historyEmpty: "\u307e\u3060\u5909\u66f4\u5c65\u6b74\u306f\u3042\u308a\u307e\u305b\u3093\u3002",
        modalSubtitle: "Task \u306e\u5168\u4f53\u50cf\u3092 PDCA \u3067\u78ba\u8a8d\u3057\u307e\u3059\u3002",
        plan: "Plan",
        do: "Do",
        check: "Check",
        act: "Act",
        labelDescription: "\u5185\u5bb9",
        labelDueDate: "\u671f\u9650",
        labelShift: "\u30b7\u30d5\u30c8",
        labelSector: "\u30bb\u30af\u30bf\u30fc",
        labelLeader: "\u62c5\u5f53\u8005",
        labelCreatedBy: "\u4f5c\u6210\u8005",
        labelCreatedAt: "\u4f5c\u6210\u65e5\u6642",
        labelStartedAt: "\u958b\u59cb\u65e5\u6642",
        labelUpdatedAt: "\u66f4\u65b0\u65e5\u6642",
        labelHistoryCount: "\u5909\u66f4\u6570",
        labelCompletedAt: "\u5b8c\u4e86\u65e5\u6642",
        labelCancelledAt: "\u53d6\u6d88\u65e5\u6642",
        close: "\u9589\u3058\u308b",
        fromTo: (fromLabel, toLabel) => `${fromLabel} \u304b\u3089 ${toLabel} \u3078\u5909\u66f4`,
        initialStatus: toLabel => `\u521d\u671f\u30b9\u30c6\u30fc\u30bf\u30b9: ${toLabel}`
    }
};

window.addEventListener("DOMContentLoaded", () => {
    bindEvents();
    send("load");
});

function bindEvents() {
    document.getElementById("btnApply").addEventListener("click", applyFilters);
    document.getElementById("btnClear").addEventListener("click", clearFilters);
    document.getElementById("btnCloseModal").addEventListener("click", closeModal);
    document.getElementById("btnCloseModalX").addEventListener("click", closeModal);
    document.getElementById("txtSearchInput").addEventListener("input", debounce(applyFilters, 220));
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
            hydrateInit(msg.data || {});
            break;
        case "rows":
            hydrateRows(msg.data || {});
            break;
        case "details":
            openModal(msg.data || {});
            break;
        case "error":
            alert(msg.message || "Erro");
            break;
    }
});

function hydrateInit(data) {
    state.locale = data.locale === "ja-JP" ? "ja-JP" : "pt-BR";
    state.allowAggregate = data.allowAggregate === true;
    state.defaultSectorId = Number(data.defaults?.sectorId || 0);
    state.shifts = normalizeShifts(data.filters?.shifts || []);
    state.sectors = normalizeShifts(data.filters?.sectors || []);
    state.statuses = normalizeStatuses(data.filters?.statuses || []);

    applyLocale();
    fillSelect("cmbShift", state.shifts, "id", fieldName(), true);
    fillSelect("cmbSector", state.sectors, "id", fieldName(), state.allowAggregate);
    fillSelect("cmbStatus", state.statuses, "value", statusFieldName(), true);

    document.getElementById("dtInicial").value = data.defaults?.dtInicial || "";
    document.getElementById("dtFinal").value = data.defaults?.dtFinal || "";
    document.getElementById("cmbShift").value = String(data.defaults?.shiftId || 0);
    document.getElementById("cmbSector").value = String(state.defaultSectorId);
    document.getElementById("cmbStatus").value = data.defaults?.status || "";
    document.getElementById("txtSearchInput").value = data.defaults?.search || "";
}

function hydrateRows(data) {
    state.rows = normalizeRows(data.rows || []);
    renderMetrics(data.totals || {});
    renderTable(state.rows);
}

function applyLocale() {
    document.documentElement.lang = state.locale;
    document.title = t("title");

    setText("txtHeaderTitle", t("headerTitle"));
    setText("txtHeaderSubtitle", t("headerSubtitle"));
    setText("txtMetricTotal", t("metricTotal"));
    setText("txtMetricOpen", t("metricOpen"));
    setText("txtMetricCompleted", t("metricCompleted"));
    setText("txtMetricOverdue", t("metricOverdue"));
    setText("txtDateFrom", t("dateFrom"));
    setText("txtDateTo", t("dateTo"));
    setText("txtShift", t("shift"));
    setText("txtSector", t("sector"));
    setText("txtStatus", t("status"));
    setText("txtSearch", t("search"));
    document.getElementById("txtSearchInput").placeholder = t("searchPlaceholder");
    setText("btnApply", t("apply"));
    setText("btnClear", t("clear"));
    setText("txtTableTitle", t("tableTitle"));
    setText("txtTableSubtitle", t("tableSubtitle"));
    setText("txtColTask", t("colTask"));
    setText("txtColPlan", t("colPlan"));
    setText("txtColDo", t("colDo"));
    setText("txtColCheck", t("colCheck"));
    setText("txtColAct", t("colAct"));
    setText("txtColStatus", t("colStatus"));
    setText("txtColActions", t("colActions"));
    setText("txtModalPlan", t("plan"));
    setText("txtModalDo", t("do"));
    setText("txtModalCheck", t("check"));
    setText("txtModalAct", t("act"));
    setText("txtHistoryTitle", t("historyTitle"));
    setText("txtHistorySubtitle", t("historySubtitle"));
    setText("btnCloseModal", t("close"));
}

function applyFilters() {
    send("apply", currentFilterPayload());
}

function clearFilters() {
    document.getElementById("cmbShift").value = "0";
    document.getElementById("cmbSector").value = String(state.defaultSectorId);
    document.getElementById("cmbStatus").value = "";
    document.getElementById("txtSearchInput").value = "";
    applyFilters();
}

function currentFilterPayload() {
    return {
        dtInicial: document.getElementById("dtInicial").value,
        dtFinal: document.getElementById("dtFinal").value,
        shiftId: Number(document.getElementById("cmbShift").value || 0),
        sectorId: Number(document.getElementById("cmbSector").value || 0),
        status: document.getElementById("cmbStatus").value || "",
        search: document.getElementById("txtSearchInput").value.trim()
    };
}

function renderMetrics(totals) {
    setText("metricTotal", totals.total ?? 0);
    setText("metricOpen", totals.open ?? 0);
    setText("metricCompleted", totals.completed ?? 0);
    setText("metricOverdue", totals.overdue ?? 0);
}

function renderTable(rows) {
    const tbody = document.getElementById("tblBody");

    if (!rows.length) {
        tbody.innerHTML = `<tr><td colspan="8" class="empty-cell">${escapeHtml(t("empty"))}</td></tr>`;
        return;
    }

    tbody.innerHTML = rows.map(row => `
        <tr>
            <td>${row.id}</td>
            <td>
                <div class="task-cell">
                    <strong title="${escapeHtmlAttr(row.description)}">${escapeHtml(truncate(row.description, 130))}</strong>
                </div>
            </td>
            <td>${renderMiniStack([
                `${t("labelDueDate")}: ${row.dueDate || "-"}`,
                `${t("labelShift")}: ${localizedValue(row.shiftNamePt, row.shiftNameJp) || "-"}`,
                `${t("labelSector")}: ${localizedValue(row.sectorNamePt, row.sectorNameJp) || "-"}`
            ])}</td>
            <td>${renderMiniStack([
                `${t("labelLeader")}: ${localizedValue(row.leaderNamePt, row.leaderNameJp) || t("noLeader")}`,
                `${t("labelStartedAt")}: ${row.startedAt || t("noDate")}`
            ])}</td>
            <td>${renderMiniStack([
                `${t("labelUpdatedAt")}: ${row.updatedAt || "-"}`,
                `${t("labelHistoryCount")}: ${row.historyCount || 0}`
            ])}</td>
            <td>${renderMiniStack([
                `${t("labelCompletedAt")}: ${row.completedAt || t("noClosure")}`,
                `${t("labelCancelledAt")}: ${row.cancelledAt || "-"}`
            ])}</td>
            <td><span class="status-pill ${statusClass(row.taskStatus)}">${escapeHtml(statusLabel(row.taskStatus))}</span></td>
            <td class="actions-col">
                <button class="icon-btn" type="button" data-view="${row.id}" title="${escapeHtmlAttr(t("actionView"))}" aria-label="${escapeHtmlAttr(t("actionView"))}">
                    ${iconEye()}
                </button>
            </td>
        </tr>
    `).join("");

    tbody.querySelectorAll("[data-view]").forEach(button => {
        button.addEventListener("click", () => {
            send("details", { id: Number(button.dataset.view) });
        });
    });
}

function openModal(data) {
    setText("modalTitle", `Task #${data.id || "-"}`);
    setText("modalSubtitle", t("modalSubtitle"));
    document.getElementById("modalStatus").textContent = statusLabel(data.taskStatus);
    document.getElementById("modalStatus").className = `status-pill ${statusClass(data.taskStatus)}`;

    document.getElementById("modalPlan").innerHTML = renderDetailList([
        [t("labelDescription"), data.description || "-"],
        [t("labelDueDate"), data.dueDate || "-"],
        [t("labelShift"), localizedValue(data.shiftNamePt, data.shiftNameJp) || "-"],
        [t("labelSector"), localizedValue(data.sectorNamePt, data.sectorNameJp) || "-"],
        [t("labelLeader"), localizedValue(data.leaderNamePt, data.leaderNameJp) || t("noLeader")]
    ]);

    document.getElementById("modalDo").innerHTML = renderDetailList([
        [t("labelCreatedBy"), localizedValue(data.createdByNamePt, data.createdByNameJp) || "-"],
        [t("labelCreatedAt"), data.createdAt || "-"],
        [t("labelStartedAt"), data.startedAt || t("noDate")]
    ]);

    document.getElementById("modalCheck").innerHTML = renderDetailList([
        [t("labelUpdatedAt"), data.updatedAt || "-"],
        [t("labelHistoryCount"), String(data.historyCount || 0)],
        [t("status"), statusLabel(data.taskStatus)]
    ]);

    document.getElementById("modalAct").innerHTML = renderDetailList([
        [t("labelCompletedAt"), data.completedAt || t("noClosure")],
        [t("labelCancelledAt"), data.cancelledAt || "-"]
    ]);

    renderHistory(data.history || []);
    document.getElementById("modal").classList.remove("hidden");
}

function closeModal() {
    document.getElementById("modal").classList.add("hidden");
}

function renderHistory(items) {
    const container = document.getElementById("historyList");

    if (!items.length) {
        container.innerHTML = `<div class="empty-history">${escapeHtml(t("historyEmpty"))}</div>`;
        return;
    }

    container.innerHTML = items.map(item => {
        const previousLabel = state.locale === "ja-JP"
            ? (item.previousStatusLabelJp || "")
            : (item.previousStatusLabelPt || "");
        const newLabel = state.locale === "ja-JP"
            ? (item.newStatusLabelJp || "")
            : (item.newStatusLabelPt || "");
        const changedBy = localizedValue(item.changedByNamePt, item.changedByNameJp) || "-";
        const summary = previousLabel
            ? t("fromTo")(previousLabel, newLabel)
            : t("initialStatus")(newLabel);

        return `
            <article class="history-item">
                <strong>${escapeHtml(summary)}</strong>
                <span>${escapeHtml(item.changedAt || "-")}</span>
                <small>${escapeHtml(changedBy)}${item.note ? ` | ${escapeHtml(item.note)}` : ""}</small>
            </article>
        `;
    }).join("");
}

function normalizeShifts(items) {
    return items.map(item => ({
        id: Number(item.id || 0),
        namePt: item.namePt || "",
        nameJp: item.nameJp || item.namePt || ""
    }));
}

function normalizeStatuses(items) {
    return items.map(item => ({
        value: item.value || "",
        labelPt: item.labelPt || "",
        labelJp: item.labelJp || item.labelPt || ""
    }));
}

function normalizeRows(items) {
    return items.map(item => ({
        id: Number(item.id || 0),
        description: item.description || "",
        dueDate: item.dueDate || "",
        shiftNamePt: item.shiftNamePt || "",
        shiftNameJp: item.shiftNameJp || item.shiftNamePt || "",
        sectorNamePt: item.sectorNamePt || "",
        sectorNameJp: item.sectorNameJp || item.sectorNamePt || "",
        leaderNamePt: item.leaderNamePt || "",
        leaderNameJp: item.leaderNameJp || item.leaderNamePt || "",
        createdByNamePt: item.createdByNamePt || "",
        createdByNameJp: item.createdByNameJp || item.createdByNamePt || "",
        taskStatus: item.taskStatus || "pending",
        createdAt: item.createdAt || "",
        updatedAt: item.updatedAt || "",
        startedAt: item.startedAt || "",
        completedAt: item.completedAt || "",
        cancelledAt: item.cancelledAt || "",
        historyCount: Number(item.historyCount || 0)
    }));
}

function fillSelect(id, items, valueField, textField, withAll) {
    const select = document.getElementById(id);
    const options = [];

    if (withAll) {
        options.push(`<option value="${id === "cmbShift" || id === "cmbSector" ? "0" : ""}">${escapeHtml(t("allOption"))}</option>`);
    }

    items.forEach(item => {
        options.push(`<option value="${escapeHtmlAttr(item[valueField])}">${escapeHtml(item[textField])}</option>`);
    });

    select.innerHTML = options.join("");
}

function renderMiniStack(lines) {
    return `<div class="mini-stack">${lines.map(line => `<span>${escapeHtml(line)}</span>`).join("")}</div>`;
}

function renderDetailList(entries) {
    return entries.map(([label, value]) => `
        <div class="detail-row">
            <span>${escapeHtml(label)}</span>
            <strong>${escapeHtml(value)}</strong>
        </div>
    `).join("");
}

function localizedValue(pt, jp) {
    return state.locale === "ja-JP"
        ? (jp || pt || "")
        : (pt || jp || "");
}

function fieldName() {
    return state.locale === "ja-JP" ? "nameJp" : "namePt";
}

function statusFieldName() {
    return state.locale === "ja-JP" ? "labelJp" : "labelPt";
}

function statusLabel(value) {
    const match = state.statuses.find(item => item.value === value);
    return match
        ? (state.locale === "ja-JP" ? match.labelJp : match.labelPt)
        : value;
}

function statusClass(value) {
    return {
        pending: "status-pending",
        in_progress: "status-progress",
        blocked: "status-blocked",
        completed: "status-completed",
        cancelled: "status-cancelled"
    }[value] || "status-pending";
}

function debounce(fn, wait) {
    let timer = null;
    return (...args) => {
        clearTimeout(timer);
        timer = setTimeout(() => fn(...args), wait);
    };
}

function setText(id, value) {
    const element = document.getElementById(id);
    if (element) {
        element.textContent = value ?? "-";
    }
}

function t(key) {
    return I18N[state.locale]?.[key] ?? I18N["pt-BR"][key];
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

function escapeHtml(value) {
    return String(value ?? "")
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#39;");
}

function escapeHtmlAttr(value) {
    return escapeHtml(value);
}
