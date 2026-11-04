const state = {
    locale: "pt-BR",
    rows: [],
    statuses: [],
    sectors: [],
    equipments: [],
    operators: [],
    trainers: [],
    allowAggregate: false,
    defaultSectorId: 0
};

const I18N = {
    "pt-BR": {
        title: "Relatorio de MasterCard",
        headerTitle: "Relatorio de MasterCard",
        headerSubtitle: "Acompanhe treinamentos em andamento, follow agendado, follow vencido e fechamentos.",
        metricTotal: "Total",
        metricInProgress: "Andamento",
        metricFollow: "Follow",
        metricCompleted: "Finalizados",
        metricOverdueFollow: "Follow Vencido",
        metricDueSoon: "Follow 7 dias",
        dateFrom: "Data Inicial",
        dateTo: "Data Final",
        sector: "Setor",
        equipment: "Equipamento",
        status: "Status",
        operator: "Operador",
        trainer: "Treinador",
        search: "Buscar",
        searchPlaceholder: "Operador, treinador, setor, equipamento, processo...",
        apply: "Buscar",
        clear: "Limpar",
        tableTitle: "Painel de acompanhamento",
        tableSubtitle: "Cada linha mostra quem treinou, quem foi treinado, status atual e proximos follows.",
        colOperator: "Operador",
        colTrainer: "Treinador",
        colProcess: "Processo",
        colStartDate: "Inicio",
        colFollowDate: "Follow",
        colStatus: "Status",
        colActions: "Acoes",
        empty: "Nenhum MasterCard encontrado para o filtro atual.",
        loading: "Carregando...",
        allOption: "Todos",
        actionView: "Visualizar",
        modalSubtitle: "Leitura completa do treinamento e do historico de follow.",
        historyTitle: "Historico",
        historySubtitle: "Mudancas registradas no MasterCard.",
        historyEmpty: "Nenhuma mudanca registrada ainda.",
        cardSection: "Cadastro",
        trainingSection: "Treinamento",
        followSection: "Follow",
        closureSection: "Fechamento",
        labelProcess: "Processo",
        labelOperator: "Operador",
        labelTrainer: "Treinador",
        labelSector: "Setor",
        labelEquipment: "Equipamento",
        labelStartDate: "Inicio",
        labelFollowDate: "Follow",
        labelStatus: "Status",
        labelCreatedBy: "Criado por",
        labelCreatedAt: "Criado em",
        labelUpdatedAt: "Atualizado em",
        labelConcludedAt: "Concluido em",
        labelFinalizedAt: "Finalizado em",
        labelNotes: "Observacoes",
        labelHistoryCount: "Mudancas",
        close: "Fechar",
        fromTo: (fromLabel, toLabel) => `De ${fromLabel} para ${toLabel}`,
        initialStatus: toLabel => `Status inicial: ${toLabel}`,
        noNotes: "Sem observacoes."
    },
    "ja-JP": {
        title: "MasterCard Report",
        headerTitle: "MasterCard Report",
        headerSubtitle: "進行中の教育、予定 Follow、期限超過の Follow、完了状況を追跡します。",
        metricTotal: "合計",
        metricInProgress: "進行中",
        metricFollow: "Follow",
        metricCompleted: "完了",
        metricOverdueFollow: "Follow 遅れ",
        metricDueSoon: "7日以内",
        dateFrom: "開始日",
        dateTo: "終了日",
        sector: "セクター",
        equipment: "設備",
        status: "ステータス",
        operator: "作業者",
        trainer: "指導者",
        search: "検索",
        searchPlaceholder: "作業者、指導者、セクター、設備、工程...",
        apply: "検索",
        clear: "クリア",
        tableTitle: "管理パネル",
        tableSubtitle: "誰が教え、誰が学び、現在どの段階で、次の Follow がいつかを確認できます。",
        colOperator: "作業者",
        colTrainer: "指導者",
        colProcess: "工程",
        colStartDate: "開始",
        colFollowDate: "Follow",
        colStatus: "ステータス",
        colActions: "操作",
        empty: "条件に一致する MasterCard はありません。",
        loading: "読み込み中...",
        allOption: "すべて",
        actionView: "表示",
        modalSubtitle: "教育内容と Follow 履歴をまとめて確認します。",
        historyTitle: "履歴",
        historySubtitle: "MasterCard に記録された変更履歴です。",
        historyEmpty: "まだ履歴はありません。",
        cardSection: "登録",
        trainingSection: "教育",
        followSection: "Follow",
        closureSection: "完了",
        labelProcess: "工程",
        labelOperator: "作業者",
        labelTrainer: "指導者",
        labelSector: "セクター",
        labelEquipment: "設備",
        labelStartDate: "開始",
        labelFollowDate: "Follow",
        labelStatus: "ステータス",
        labelCreatedBy: "作成者",
        labelCreatedAt: "作成日時",
        labelUpdatedAt: "更新日時",
        labelConcludedAt: "完了日時",
        labelFinalizedAt: "最終完了日時",
        labelNotes: "備考",
        labelHistoryCount: "変更数",
        close: "閉じる",
        fromTo: (fromLabel, toLabel) => `${fromLabel} から ${toLabel} へ変更`,
        initialStatus: toLabel => `初期ステータス: ${toLabel}`,
        noNotes: "備考はありません。"
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
    window.chrome.webview.postMessage({ action, ...extra });
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
    state.statuses = normalizeStatuses(data.filters?.statuses || []);
    state.sectors = normalizeLookup(data.filters?.sectors || []);
    state.equipments = normalizeLookup(data.filters?.equipments || []);
    state.operators = normalizePeople(data.filters?.operators || []);
    state.trainers = normalizePeople(data.filters?.trainers || []);

    applyLocale();
    fillSelect("cmbStatus", state.statuses, "value", statusFieldName(), true);
    fillSelect("cmbSector", state.sectors, "id", fieldName(), state.allowAggregate);
    fillSelect("cmbEquipment", state.equipments, "id", fieldName(), true);
    fillSelect("cmbOperator", state.operators, "codigoFJ", fieldName(), true);
    fillSelect("cmbTrainer", state.trainers, "codigoFJ", fieldName(), true);

    document.getElementById("dtInicial").value = data.defaults?.dtInicial || "";
    document.getElementById("dtFinal").value = data.defaults?.dtFinal || "";
    document.getElementById("cmbSector").value = String(state.defaultSectorId);
    document.getElementById("cmbEquipment").value = String(data.defaults?.equipmentId || 0);
    document.getElementById("cmbStatus").value = data.defaults?.status || "";
    document.getElementById("cmbOperator").value = data.defaults?.operatorCodigoFJ || "";
    document.getElementById("cmbTrainer").value = data.defaults?.trainerCodigoFJ || "";
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
    setText("txtMetricInProgress", t("metricInProgress"));
    setText("txtMetricFollow", t("metricFollow"));
    setText("txtMetricCompleted", t("metricCompleted"));
    setText("txtMetricOverdueFollow", t("metricOverdueFollow"));
    setText("txtMetricDueSoon", t("metricDueSoon"));
    setText("txtDateFrom", t("dateFrom"));
    setText("txtDateTo", t("dateTo"));
    setText("txtSector", t("sector"));
    setText("txtEquipment", t("equipment"));
    setText("txtStatus", t("status"));
    setText("txtOperator", t("operator"));
    setText("txtTrainer", t("trainer"));
    setText("txtSearch", t("search"));
    document.getElementById("txtSearchInput").placeholder = t("searchPlaceholder");
    setText("btnApply", t("apply"));
    setText("btnClear", t("clear"));
    setText("txtTableTitle", t("tableTitle"));
    setText("txtTableSubtitle", t("tableSubtitle"));
    setText("txtColOperator", t("colOperator"));
    setText("txtColTrainer", t("colTrainer"));
    setText("txtColProcess", t("colProcess"));
    setText("txtColStartDate", t("colStartDate"));
    setText("txtColFollowDate", t("colFollowDate"));
    setText("txtColStatus", t("colStatus"));
    setText("txtColActions", t("colActions"));
    setText("txtModalPlan", t("cardSection"));
    setText("txtModalDo", t("trainingSection"));
    setText("txtModalCheck", t("followSection"));
    setText("txtModalAct", t("closureSection"));
    setText("txtHistoryTitle", t("historyTitle"));
    setText("txtHistorySubtitle", t("historySubtitle"));
    setText("btnCloseModal", t("close"));
}

function applyFilters() {
    send("apply", currentFilterPayload());
}

function clearFilters() {
    document.getElementById("cmbSector").value = String(state.defaultSectorId);
    document.getElementById("cmbEquipment").value = "0";
    document.getElementById("cmbStatus").value = "";
    document.getElementById("cmbOperator").value = "";
    document.getElementById("cmbTrainer").value = "";
    document.getElementById("txtSearchInput").value = "";
    applyFilters();
}

function currentFilterPayload() {
    return {
        dtInicial: document.getElementById("dtInicial").value,
        dtFinal: document.getElementById("dtFinal").value,
        sectorId: Number(document.getElementById("cmbSector").value || 0),
        equipmentId: Number(document.getElementById("cmbEquipment").value || 0),
        status: document.getElementById("cmbStatus").value || "",
        operatorCodigoFJ: document.getElementById("cmbOperator").value || "",
        trainerCodigoFJ: document.getElementById("cmbTrainer").value || "",
        search: document.getElementById("txtSearchInput").value.trim()
    };
}

function renderMetrics(totals) {
    setText("metricTotal", totals.total ?? 0);
    setText("metricInProgress", totals.inProgress ?? 0);
    setText("metricFollow", totals.follow ?? 0);
    setText("metricCompleted", totals.completed ?? 0);
    setText("metricOverdueFollow", totals.overdueFollow ?? 0);
    setText("metricDueSoon", totals.dueSoon ?? 0);
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
            <td>${renderMiniStack([
                localizedValue(row.operatorNamePt, row.operatorNameJp) || row.operatorCodigoFJ,
                row.operatorCodigoFJ
            ], true)}</td>
            <td>${renderMiniStack([
                localizedValue(row.trainerNamePt, row.trainerNameJp) || row.trainerCodigoFJ,
                localizedValue(row.sectorNamePt, row.sectorNameJp) || "-"
            ])}</td>
            <td>${renderMiniStack([
                truncate(row.description, 90),
                localizedValue(row.equipmentNamePt, row.equipmentNameJp) || "-"
            ])}</td>
            <td>${renderMiniStack([
                `${t("labelStartDate")}: ${row.startDate || "-"}`,
                `${t("labelConcludedAt")}: ${row.concludedAt || "-"}`
            ])}</td>
            <td>${renderMiniStack([
                `${t("labelFollowDate")}: ${row.followDate || "-"}`,
                `${t("labelFinalizedAt")}: ${row.finalizedAt || "-"}`
            ])}</td>
            <td><span class="status-pill ${statusClass(row.masterCardStatus)}">${escapeHtml(statusLabel(row.masterCardStatus))}</span></td>
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
    const row = normalizeRows([data.row || {}])[0];
    if (!row) return;

    setText("modalTitle", `MasterCard #${row.id || "-"}`);
    setText("modalSubtitle", t("modalSubtitle"));
    document.getElementById("modalStatus").textContent = statusLabel(row.masterCardStatus);
    document.getElementById("modalStatus").className = `status-pill ${statusClass(row.masterCardStatus)}`;

    document.getElementById("modalPlan").innerHTML = renderDetailList([
        [t("labelOperator"), `${localizedValue(row.operatorNamePt, row.operatorNameJp) || row.operatorCodigoFJ} (${row.operatorCodigoFJ})`],
        [t("labelTrainer"), `${localizedValue(row.trainerNamePt, row.trainerNameJp) || row.trainerCodigoFJ} (${row.trainerCodigoFJ})`],
        [t("labelCreatedBy"), localizedValue(row.createdByNamePt, row.createdByNameJp) || "-"],
        [t("labelCreatedAt"), row.createdAt || "-"]
    ]);

    document.getElementById("modalDo").innerHTML = renderDetailList([
        [t("labelProcess"), row.description || "-"],
        [t("labelSector"), localizedValue(row.sectorNamePt, row.sectorNameJp) || "-"],
        [t("labelEquipment"), localizedValue(row.equipmentNamePt, row.equipmentNameJp) || "-"],
        [t("labelStartDate"), row.startDate || "-"]
    ]);

    document.getElementById("modalCheck").innerHTML = renderDetailList([
        [t("labelStatus"), statusLabel(row.masterCardStatus)],
        [t("labelConcludedAt"), row.concludedAt || "-"],
        [t("labelFollowDate"), row.followDate || "-"],
        [t("labelHistoryCount"), String(row.historyCount || 0)]
    ]);

    document.getElementById("modalAct").innerHTML = renderDetailList([
        [t("labelUpdatedAt"), row.updatedAt || "-"],
        [t("labelFinalizedAt"), row.finalizedAt || "-"],
        [t("labelNotes"), row.notes || t("noNotes")]
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

function normalizeLookup(items) {
    return items.map(item => ({
        id: Number(item.id || 0),
        namePt: item.namePt || "",
        nameJp: item.nameJp || item.namePt || ""
    }));
}

function normalizePeople(items) {
    return items.map(item => ({
        codigoFJ: item.codigoFJ || "",
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
        operatorCodigoFJ: item.operatorCodigoFJ || "",
        operatorNamePt: item.operatorNamePt || "",
        operatorNameJp: item.operatorNameJp || item.operatorNamePt || "",
        trainerCodigoFJ: item.trainerCodigoFJ || "",
        trainerNamePt: item.trainerNamePt || "",
        trainerNameJp: item.trainerNameJp || item.trainerNamePt || "",
        sectorNamePt: item.sectorNamePt || "",
        sectorNameJp: item.sectorNameJp || item.sectorNamePt || "",
        equipmentNamePt: item.equipmentNamePt || "",
        equipmentNameJp: item.equipmentNameJp || item.equipmentNamePt || "",
        description: item.description || "",
        notes: item.notes || "",
        startDate: item.startDate || "",
        masterCardStatus: item.masterCardStatus || "in_progress",
        concludedAt: item.concludedAt || "",
        followDate: item.followDate || "",
        finalizedAt: item.finalizedAt || "",
        createdAt: item.createdAt || "",
        updatedAt: item.updatedAt || "",
        createdByNamePt: item.createdByNamePt || "",
        createdByNameJp: item.createdByNameJp || item.createdByNamePt || "",
        historyCount: Number(item.historyCount || 0)
    }));
}

function fillSelect(id, items, valueField, textField, withAll) {
    const select = document.getElementById(id);
    const options = [];

    if (withAll) {
        options.push(`<option value="${id === "cmbSector" || id === "cmbEquipment" ? "0" : ""}">${escapeHtml(t("allOption"))}</option>`);
    }

    items.forEach(item => {
        options.push(`<option value="${escapeHtmlAttr(item[valueField])}">${escapeHtml(item[textField])}</option>`);
    });

    select.innerHTML = options.join("");
}

function renderMiniStack(lines, emphasizeFirst = false) {
    return `<div class="mini-stack">${lines.map((line, index) => `<span${emphasizeFirst && index === 0 ? "><strong>" : ">"}${escapeHtml(line)}${emphasizeFirst && index === 0 ? "</strong>" : ""}</span>`).join("")}</div>`;
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
    return match ? (state.locale === "ja-JP" ? match.labelJp : match.labelPt) : value;
}

function statusClass(value) {
    return {
        in_progress: "status-progress",
        follow: "status-blocked",
        completed: "status-completed"
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
