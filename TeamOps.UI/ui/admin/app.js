const state = {
    locale: "pt-BR",
    entities: {},
    entityOrder: [],
    lookups: {},
    currentEntity: "",
    rows: [],
    filteredRows: [],
    modalMode: "create",
    editingId: 0,
    shortCodeTouched: false,
    systemLogFilters: null
};

const I18N = {
    "pt-BR": {
        title: "Administracao",
        headerTitle: "Administracao",
        headerSubtitle: "Painel central para ajustar bases do sistema, vinculos operacionais e cadastros usados em producao, Haidai e relatorios.",
        metaEntity: "Cadastro ativo",
        metaTotal: "Registros",
        sidebarTitle: "Itens",
        sidebarSubtitle: "Priorize Locais e Maquinas para manter o sistema consistente com o banco.",
        badge: "Configuracao do sistema",
        searchLabel: "Buscar",
        searchPlaceholder: "Buscar por codigo, nome, setor, local ou validacao...",
        logFilterStart: "Data inicial",
        logFilterEnd: "Data final",
        logFilterUser: "Usuario",
        logFilterModule: "Modulo",
        logFilterAction: "Acao",
        logFilterApply: "Atualizar",
        new: "Novo",
        tableTitle: "Registros",
        tableSubtitle: "Ajuste os dados direto na lista e use os indicadores para corrigir pendencias antes que afetem outras telas.",
        tableReadonlySubtitle: "Consulta simples do historico ou das pendencias, sem permitir inclusao, edicao ou exclusao.",
        actions: "Acoes",
        actionEdit: "Editar",
        actionDelete: "Excluir",
        loading: "Carregando...",
        empty: "Nenhum registro encontrado.",
        modalCreateTitle: entity => `Novo ${entity}`,
        modalEditTitle: entity => `Editar ${entity}`,
        modalCreateSubtitle: "Preencha os campos e salve.",
        modalEditSubtitle: "Ajuste os dados e salve as alteracoes.",
        cancel: "Cancelar",
        save: "Salvar",
        saveChanges: "Salvar alteracoes",
        createRecord: "Cadastrar",
        readonlyBadge: "Somente leitura",
        selectPlaceholder: "Selecione...",
        confirmDelete: entity => `Deseja excluir este registro de ${entity}?`,
        metricsTotal: "Total",
        metricsFiltered: "Visiveis",
        metricsMode: "Modo",
        metricsEditable: "Editavel",
        metricsReadonly: "Leitura",
        metricsMachinesLinked: "Com local",
        metricsMachinesActive: "Ativas",
        metricsMachinesPending: "Pendencias",
        metricsLocalsShortCode: "Com codigo",
        metricsLocalsLinked: "Com maquinas",
        metricsLocalsSectors: "Setores",
        metricsAuditMissingLocal: "Sem local",
        metricsAuditMismatch: "Divergencia",
        metricsAuditMissingSector: "Sem setor",
        yes: "Sim",
        no: "Nao",
        validationOk: "OK",
        fieldRequired: "Obrigatorio"
    },
    "ja-JP": {
        title: "管理",
        headerTitle: "管理",
        headerSubtitle: "生産、ハイダイ、レポートで使う基礎マスタと運用紐付けをまとめて調整する画面です。",
        metaEntity: "選択中",
        metaTotal: "件数",
        sidebarTitle: "項目",
        sidebarSubtitle: "特に場所と設備の紐付けを優先して整備してください。",
        badge: "システム設定",
        searchLabel: "検索",
        searchPlaceholder: "コード、名称、セクター、場所、検証結果で検索...",
        new: "新規",
        tableTitle: "レコード",
        tableSubtitle: "一覧から直接修正し、他画面へ影響する前に不整合を直せます。",
        tableReadonlySubtitle: "履歴または要確認一覧の参照用です。追加・編集・削除はできません。",
        actions: "操作",
        actionEdit: "編集",
        actionDelete: "削除",
        loading: "読み込み中...",
        empty: "レコードがありません。",
        modalCreateTitle: entity => `${entity} を新規登録`,
        modalEditTitle: entity => `${entity} を編集`,
        modalCreateSubtitle: "必要項目を入力して保存してください。",
        modalEditSubtitle: "内容を修正して保存してください。",
        cancel: "キャンセル",
        save: "保存",
        saveChanges: "変更を保存",
        createRecord: "登録",
        readonlyBadge: "閲覧のみ",
        selectPlaceholder: "選択してください",
        confirmDelete: entity => `${entity} のこのレコードを削除しますか。`,
        metricsTotal: "総数",
        metricsFiltered: "表示中",
        metricsMode: "モード",
        metricsEditable: "編集可",
        metricsReadonly: "参照",
        metricsMachinesLinked: "場所あり",
        metricsMachinesActive: "稼働",
        metricsMachinesPending: "要確認",
        metricsLocalsShortCode: "略称あり",
        metricsLocalsLinked: "設備あり",
        metricsLocalsSectors: "セクター数",
        metricsAuditMissingLocal: "場所未設定",
        metricsAuditMismatch: "不一致",
        metricsAuditMissingSector: "セクター未設定",
        yes: "はい",
        no: "いいえ",
        validationOk: "OK",
        fieldRequired: "必須"
    }
};

window.addEventListener("DOMContentLoaded", () => {
    bindEvents();
    send("load");
});

function bindEvents() {
    document.getElementById("searchInput").addEventListener("input", applySearch);
    document.getElementById("btnNew").addEventListener("click", () => openModal("create"));
    document.getElementById("btnApplyLogFilters").addEventListener("click", applyLogFilters);
    document.getElementById("btnCancelModal").addEventListener("click", closeModal);
    document.getElementById("btnCloseModalX").addEventListener("click", closeModal);
    document.getElementById("modalBackdrop").addEventListener("click", closeModal);
    document.getElementById("btnSaveModal").addEventListener("click", saveModal);
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
        case "entity_rows":
            hydrateRows(msg.data || {});
            break;
        case "saved":
        case "updated":
        case "deleted":
            alert(msg.message || "");
            closeModal();
            break;
        case "error":
            alert(msg.message || "Erro");
            break;
    }
});

function hydrateInit(data) {
    state.locale = data.locale === "ja-JP" ? "ja-JP" : "pt-BR";
    state.lookups = normalizeLookups(data.lookups || {});
    state.entities = {};
    state.entityOrder = [];

    (data.entities || []).forEach(entity => {
        state.entities[entity.key] = normalizeEntity(entity);
        state.entityOrder.push(entity.key);
    });

    state.currentEntity = data.activeEntity || state.entityOrder[0] || "";

    applyLocale();
    renderEntityList();

    if (state.currentEntity) {
        send("load_entity", { entity: state.currentEntity });
    }
}

function hydrateRows(data) {
    if (data.lookups) {
        state.lookups = normalizeLookups(data.lookups);
    }

    if (data.filters) {
        state.systemLogFilters = normalizeSystemLogFilters(data.filters);
    }

    state.currentEntity = data.entity || state.currentEntity;
    state.rows = Array.isArray(data.rows) ? data.rows : [];
    applyLocale();
    renderEntityList();
    renderEntityControls();
    applySearch();
}

function normalizeEntity(entity) {
    return {
        key: entity.key,
        group: entity.group || "misc",
        readOnly: !!entity.readOnly,
        titlePt: entity.titlePt || entity.key,
        titleJp: entity.titleJp || entity.titlePt || entity.key,
        descriptionPt: entity.descriptionPt || "",
        descriptionJp: entity.descriptionJp || entity.descriptionPt || "",
        fields: entity.fields || [],
        columns: entity.columns || []
    };
}

function normalizeLookups(lookups) {
    const normalized = {};

    Object.keys(lookups || {}).forEach(key => {
        normalized[key] = (lookups[key] || []).map(item => ({
            ...item,
            id: normalizeLookupId(item.id),
            namePt: item.namePt || "",
            nameJp: item.nameJp || item.namePt || "",
            labelPt: item.labelPt || item.namePt || "",
            labelJp: item.labelJp || item.nameJp || item.namePt || "",
            sectorId: Number(item.sectorId || 0)
        }));
    });

    return normalized;
}

function normalizeLookupId(value) {
    const text = String(value ?? "").trim();
    return /^\d+$/.test(text) ? Number(text) : text;
}

function applyLocale() {
    document.documentElement.lang = state.locale;
    document.title = t("title");

    setText("txtHeaderTitle", t("headerTitle"));
    setText("txtHeaderSubtitle", t("headerSubtitle"));
    setText("txtMetaEntity", t("metaEntity"));
    setText("txtMetaTotal", t("metaTotal"));
    setText("txtSidebarTitle", t("sidebarTitle"));
    setText("txtSidebarSubtitle", t("sidebarSubtitle"));
    setText("txtBadge", t("badge"));
    setText("txtSearchLabel", t("searchLabel"));
    document.getElementById("searchInput").placeholder = t("searchPlaceholder");
    setText("lblLogDateStart", t("logFilterStart"));
    setText("lblLogDateEnd", t("logFilterEnd"));
    setText("lblLogUser", t("logFilterUser"));
    setText("lblLogModule", t("logFilterModule"));
    setText("lblLogAction", t("logFilterAction"));
    setText("btnNew", t("new"));
    setText("btnApplyLogFilters", t("logFilterApply"));
    setText("txtTableTitle", t("tableTitle"));
    setText("btnCancelModal", t("cancel"));

    const entity = currentEntity();
    setText("entityTitle", entity ? entityTitle(entity) : "-");
    setText("entityDescription", entity ? entityDescription(entity) : "-");
    setText("lblActiveEntity", entity ? entityTitle(entity) : "-");
    setText("lblTotalRows", state.rows.length);
    setText("btnSaveModal", state.modalMode === "edit" ? t("saveChanges") : t("createRecord"));

    const tableSubtitle = entity?.readOnly ? t("tableReadonlySubtitle") : t("tableSubtitle");
    setText("txtTableSubtitle", tableSubtitle);

    const btnNew = document.getElementById("btnNew");
    btnNew.classList.toggle("hidden", !!entity?.readOnly);
    btnNew.disabled = !!entity?.readOnly;

    renderEntityControls();
    renderMetrics();
}

function renderEntityList() {
    const container = document.getElementById("entityList");
    const groups = {};

    state.entityOrder.forEach(key => {
        const entity = state.entities[key];
        if (!entity) return;
        if (!groups[entity.group]) groups[entity.group] = [];
        groups[entity.group].push(entity);
    });

    const order = ["organization", "core", "production", "followup", "people", "misc"];

    container.innerHTML = order
        .filter(group => groups[group]?.length)
        .map(group => `
            <section class="entity-group">
                <h3>${escapeHtml(groupLabel(group))}</h3>
                <div class="entity-group-list">
                    ${groups[group].map(entity => `
                        <button
                            type="button"
                            class="entity-card ${entity.key === state.currentEntity ? "is-active" : ""}"
                            data-entity="${entity.key}">
                            <strong>${escapeHtml(entityTitle(entity))}</strong>
                            <small>${escapeHtml(entityDescription(entity))}</small>
                        </button>
                    `).join("")}
                </div>
            </section>
        `).join("");

    container.querySelectorAll("[data-entity]").forEach(button => {
        button.addEventListener("click", () => {
            const key = button.dataset.entity;
            if (!key || key === state.currentEntity) return;
            state.currentEntity = key;
            state.rows = [];
            state.filteredRows = [];
            state.systemLogFilters = key === "system_log" ? getDefaultSystemLogFilters() : null;
            applyLocale();
            renderEntityList();
            renderEntityControls();
            renderTable();
            sendLoadEntity(key);
        });
    });
}

function renderMetrics() {
    const container = document.getElementById("entityMetrics");
    const entity = currentEntity();

    if (!container || !entity) {
        return;
    }

    const metrics = buildMetrics(entity);
    container.innerHTML = metrics.map(metric => `
        <article class="metric-card ${metric.variant ? `is-${metric.variant}` : ""}">
            <span>${escapeHtml(metric.label)}</span>
            <strong>${escapeHtml(metric.value)}</strong>
        </article>
    `).join("");
}

function buildMetrics(entity) {
    const total = state.rows.length;
    const visible = state.filteredRows.length;
    const metrics = [
        { label: t("metricsTotal"), value: total },
        { label: t("metricsFiltered"), value: visible },
        { label: t("metricsMode"), value: entity.readOnly ? t("metricsReadonly") : t("metricsEditable"), variant: entity.readOnly ? "muted" : "ok" }
    ];

    if (entity.key === "machine") {
        const linked = state.rows.filter(row => Number(row.localId || 0) > 0).length;
        const active = state.rows.filter(row => Number(row.isActive || 0) === 1).length;
        const pending = state.rows.filter(row => String(row.validationPt || "").trim().toUpperCase() !== "OK").length;
        return [
            { label: t("metricsTotal"), value: total },
            { label: t("metricsMachinesLinked"), value: linked, variant: "info" },
            { label: t("metricsMachinesActive"), value: active, variant: "ok" },
            { label: t("metricsMachinesPending"), value: pending, variant: pending > 0 ? "warning" : "ok" }
        ];
    }

    if (entity.key === "local") {
        const withShortCode = state.rows.filter(row => String(row.shortCode || "").trim() !== "").length;
        const linked = state.rows.filter(row => Number(row.machineCount || 0) > 0).length;
        const sectors = new Set(state.rows.map(row => Number(row.sectorId || 0)).filter(id => id > 0)).size;
        return [
            { label: t("metricsTotal"), value: total },
            { label: t("metricsLocalsShortCode"), value: withShortCode, variant: "info" },
            { label: t("metricsLocalsLinked"), value: linked, variant: "ok" },
            { label: t("metricsLocalsSectors"), value: sectors, variant: "muted" }
        ];
    }

    if (entity.key === "machine_audit") {
        const missingLocal = state.rows.filter(row => String(row.issuePt || "").includes("LocalId") || String(row.issuePt || "").includes("local")).length;
        const missingSector = state.rows.filter(row => String(row.issuePt || "").includes("SectorId") || String(row.issuePt || "").includes("setor")).length;
        const mismatch = state.rows.filter(row => String(row.issuePt || "").includes("diferente") || String(row.issuePt || "").includes("inconsistente")).length;
        return [
            { label: t("metricsTotal"), value: total, variant: total > 0 ? "warning" : "ok" },
            { label: t("metricsAuditMissingLocal"), value: missingLocal, variant: missingLocal > 0 ? "warning" : "ok" },
            { label: t("metricsAuditMissingSector"), value: missingSector, variant: missingSector > 0 ? "warning" : "ok" },
            { label: t("metricsAuditMismatch"), value: mismatch, variant: mismatch > 0 ? "warning" : "ok" }
        ];
    }

    return metrics;
}

function renderTable() {
    const entity = currentEntity();
    const head = document.getElementById("tableHeadRow");
    const body = document.getElementById("tableBody");

    if (!entity) {
        head.innerHTML = "";
        body.innerHTML = `<tr><td class="empty-cell" colspan="4">${escapeHtml(t("empty"))}</td></tr>`;
        return;
    }

    head.innerHTML = `
        ${entity.columns.map(column => `<th>${escapeHtml(columnLabel(column))}</th>`).join("")}
        ${entity.readOnly ? "" : `<th class="actions-col">${escapeHtml(t("actions"))}</th>`}
    `;

    if (!state.filteredRows.length) {
        body.innerHTML = `<tr><td class="empty-cell" colspan="${entity.columns.length + (entity.readOnly ? 0 : 1)}">${escapeHtml(state.rows.length ? t("empty") : t("loading"))}</td></tr>`;
        return;
    }

    body.innerHTML = state.filteredRows.map(row => `
        <tr class="${resolveRowClass(entity, row)}">
            ${entity.columns.map(column => `<td>${formatCellValue(row, column)}</td>`).join("")}
            ${entity.readOnly ? "" : `
                <td class="actions-col">
                    <div class="action-buttons">
                        <button class="icon-btn icon-btn-edit" type="button" data-edit="${row.id}" title="${escapeHtmlAttr(t("actionEdit"))}" aria-label="${escapeHtmlAttr(t("actionEdit"))}">
                            ${iconEdit()}
                        </button>
                        <button class="icon-btn icon-btn-delete" type="button" data-delete="${row.id}" title="${escapeHtmlAttr(t("actionDelete"))}" aria-label="${escapeHtmlAttr(t("actionDelete"))}">
                            ${iconDelete()}
                        </button>
                    </div>
                </td>
            `}
        </tr>
    `).join("");

    if (entity.readOnly) {
        return;
    }

    body.querySelectorAll("[data-edit]").forEach(button => {
        button.addEventListener("click", () => openModal("edit", Number(button.dataset.edit)));
    });

    body.querySelectorAll("[data-delete]").forEach(button => {
        button.addEventListener("click", () => deleteRow(Number(button.dataset.delete)));
    });
}

function resolveRowClass(entity, row) {
    if (entity.key === "machine" && String(row.validationPt || "").trim().toUpperCase() !== "OK") {
        return "row-warning";
    }

    if (entity.key === "machine_audit") {
        return "row-warning";
    }

    return "";
}

function formatCellValue(row, column) {
    const value = displayValue(row, column);

    if (column.key === "colorHex" || column.key === "textColorHex") {
        const rawColor = String(value ?? "").trim();
        if (/^#[0-9a-fA-F]{6}$/.test(rawColor)) {
            return `<span class="color-swatch" style="--swatch:${escapeHtmlAttr(rawColor)}" title="${escapeHtmlAttr(rawColor)}" aria-label="${escapeHtmlAttr(rawColor)}"></span>`;
        }

        return "-";
    }

    if (typeof value === "string" && value.trim().toUpperCase() === "OK") {
        return `<span class="cell-badge is-ok">${escapeHtml(value)}</span>`;
    }

    if (column.key === "validationPt" || column.key === "issuePt") {
        return `<span class="cell-badge ${String(value).trim().toUpperCase() === "OK" ? "is-ok" : "is-warning"}">${escapeHtml(value || "-")}</span>`;
    }

    if (column.key === "activeLabelPt") {
        const normalized = String(value).toLowerCase();
        return `<span class="cell-badge ${normalized.includes("ativa") || normalized.includes("ativo") || String(value).includes("稼働") ? "is-ok" : "is-muted"}">${escapeHtml(value || "-")}</span>`;
    }

    return escapeHtml(value);
}

function applySearch() {
    const term = document.getElementById("searchInput").value.trim().toLowerCase();
    const entity = currentEntity();

    if (!entity) {
        state.filteredRows = [];
        renderTable();
        renderMetrics();
        return;
    }

    if (!term) {
        state.filteredRows = [...state.rows];
        renderTable();
        renderMetrics();
        return;
    }

    state.filteredRows = state.rows.filter(row => {
        return entity.columns.some(column => {
            const value = String(displayValue(row, column) || "").toLowerCase();
            return value.includes(term);
        });
    });

    renderTable();
    renderMetrics();
}

function renderEntityControls() {
    const entity = currentEntity();
    const container = document.getElementById("systemLogFilters");
    if (!container) {
        return;
    }

    const visible = entity?.key === "system_log";
    container.classList.toggle("hidden", !visible);

    if (!visible) {
        return;
    }

    if (!state.systemLogFilters) {
        state.systemLogFilters = getDefaultSystemLogFilters();
    }

    const filters = state.systemLogFilters;
    document.getElementById("logDateStart").value = filters.dateStart || "";
    document.getElementById("logDateEnd").value = filters.dateEnd || "";
    document.getElementById("logUserFJ").value = filters.userFJ || "";
    document.getElementById("logModule").value = filters.module || "";
    document.getElementById("logAction").value = filters.action || "";
}

function applyLogFilters() {
    const entity = currentEntity();
    if (!entity || entity.key !== "system_log") {
        return;
    }

    state.systemLogFilters = {
        dateStart: document.getElementById("logDateStart").value || "",
        dateEnd: document.getElementById("logDateEnd").value || "",
        userFJ: document.getElementById("logUserFJ").value || "",
        module: document.getElementById("logModule").value || "",
        action: document.getElementById("logAction").value || ""
    };

    sendLoadEntity(entity.key);
}

function sendLoadEntity(entityKey) {
    const payload = { entity: entityKey };
    if (entityKey === "system_log") {
        payload.filters = state.systemLogFilters || getDefaultSystemLogFilters();
    }

    send("load_entity", payload);
}

function getDefaultSystemLogFilters() {
    const end = new Date();
    const start = new Date();
    start.setDate(start.getDate() - 30);

    return {
        dateStart: formatDateForInput(start),
        dateEnd: formatDateForInput(end),
        userFJ: "",
        module: "",
        action: ""
    };
}

function normalizeSystemLogFilters(filters) {
    const defaults = getDefaultSystemLogFilters();
    return {
        dateStart: filters.dateStart || defaults.dateStart,
        dateEnd: filters.dateEnd || defaults.dateEnd,
        userFJ: filters.userFJ || "",
        module: filters.module || "",
        action: filters.action || ""
    };
}

function openModal(mode, id = 0) {
    const entity = currentEntity();
    if (!entity || entity.readOnly) return;

    state.modalMode = mode;
    state.editingId = id;
    state.shortCodeTouched = false;

    const row = mode === "edit"
        ? state.rows.find(item => Number(item.id) === Number(id))
        : null;

    setText("modalTitle", mode === "edit" ? t("modalEditTitle")(entityTitle(entity)) : t("modalCreateTitle")(entityTitle(entity)));
    setText("modalSubtitle", mode === "edit" ? t("modalEditSubtitle") : t("modalCreateSubtitle"));
    setText("btnSaveModal", mode === "edit" ? t("saveChanges") : t("createRecord"));

    document.getElementById("modalFields").innerHTML = entity.fields.map(field => renderField(entity, field, row)).join("");
    document.getElementById("modal").classList.remove("hidden");

    attachModalBehaviors(entity, row);
}

function renderField(entity, field, row) {
    const value = row?.[field.key] ?? "";
    const label = state.locale === "ja-JP" ? field.labelJp : field.labelPt;
    const requiredBadge = field.required ? `<em class="field-required">${escapeHtml(t("fieldRequired"))}</em>` : "";

    if (field.type === "select") {
        const options = getLookupOptionsForField(entity, field, row)
            .map(item => {
                const text = lookupOptionLabel(item);
                const selected = String(value) === String(item.id) ? "selected" : "";
                return `<option value="${item.id}" ${selected}>${escapeHtml(text)}</option>`;
            })
            .join("");

        return `
            <label class="form-field">
                <span>${escapeHtml(label)} ${requiredBadge}</span>
                <select class="input" data-field="${field.key}">
                    <option value="0">${escapeHtml(t("selectPlaceholder"))}</option>
                    ${options}
                </select>
            </label>
        `;
    }

    if (field.type === "number") {
        return `
            <label class="form-field">
                <span>${escapeHtml(label)} ${requiredBadge}</span>
                <input class="input" type="number" step="1" data-field="${field.key}" value="${escapeHtmlAttr(value)}">
            </label>
        `;
    }

    if (field.type === "decimal") {
        return `
            <label class="form-field">
                <span>${escapeHtml(label)} ${requiredBadge}</span>
                <input class="input" type="number" step="0.1" min="0" data-field="${field.key}" value="${escapeHtmlAttr(value)}">
            </label>
        `;
    }

    if (field.type === "color") {
        const normalized = normalizeColorValue(value);
        return `
            <label class="form-field">
                <span>${escapeHtml(label)} ${requiredBadge}</span>
                <input class="input" type="color" data-field="${field.key}" value="${escapeHtmlAttr(normalized)}">
            </label>
        `;
    }

    if (field.type === "checkbox") {
        const checked = Number(value) === 1 || value === true || String(value).toLowerCase() === "true" ? "checked" : "";
        return `
            <label class="form-field form-field-checkbox">
                <span>${escapeHtml(label)}</span>
                <label class="checkbox-inline">
                    <input type="checkbox" data-field="${field.key}" ${checked}>
                    <strong>${checked ? escapeHtml(t("yes")) : escapeHtml(t("no"))}</strong>
                </label>
            </label>
        `;
    }

    return `
        <label class="form-field">
            <span>${escapeHtml(label)} ${requiredBadge}</span>
            <input class="input" type="text" data-field="${field.key}" value="${escapeHtmlAttr(value)}">
        </label>
    `;
}

function getLookupOptionsForField(entity, field, row) {
    const items = [...(state.lookups[field.lookupKey] || [])];

    if (entity.key === "machine" && field.key === "localId") {
        const sectorId = Number(document.querySelector('[data-field="sectorId"]')?.value || row?.sectorId || 0);
        if (sectorId > 0) {
            return items.filter(item => Number(item.sectorId || 0) === sectorId);
        }
    }

    return items;
}

function attachModalBehaviors(entity, row) {
    document.querySelectorAll('[data-field][type="checkbox"]').forEach(input => {
        input.addEventListener("change", () => {
            const label = input.closest(".checkbox-inline")?.querySelector("strong");
            if (label) {
                label.textContent = input.checked ? t("yes") : t("no");
            }
        });
    });

    if (entity.key === "machine") {
        const sectorField = document.querySelector('[data-field="sectorId"]');
        const localField = document.querySelector('[data-field="localId"]');

        if (sectorField && localField) {
            sectorField.addEventListener("change", () => refreshLocalOptions(row));
            localField.addEventListener("change", () => syncSectorFromSelectedLocal());
            refreshLocalOptions(row);
        }
    }

    if (entity.key === "local") {
        const nameField = document.querySelector('[data-field="namePt"]');
        const shortCodeField = document.querySelector('[data-field="shortCode"]');

        if (shortCodeField) {
            shortCodeField.addEventListener("input", () => {
                state.shortCodeTouched = true;
            });
        }

        if (nameField && shortCodeField) {
            nameField.addEventListener("input", () => {
                if (!state.shortCodeTouched && !shortCodeField.value.trim()) {
                    shortCodeField.value = sanitizeShortCode(nameField.value);
                }
            });
        }
    }
}

function refreshLocalOptions(row) {
    const entity = currentEntity();
    if (!entity || entity.key !== "machine") return;

    const localField = document.querySelector('[data-field="localId"]');
    const sectorField = document.querySelector('[data-field="sectorId"]');
    if (!localField || !sectorField) return;

    const selectedLocal = Number(localField.value || row?.localId || 0);
    const sectorId = Number(sectorField.value || row?.sectorId || 0);
    const options = getLookupOptionsForField(entity, { key: "localId", lookupKey: "locals" }, row);

    localField.innerHTML = `
        <option value="0">${escapeHtml(t("selectPlaceholder"))}</option>
        ${options.map(item => `
            <option value="${item.id}" ${Number(item.id) === selectedLocal ? "selected" : ""}>
                ${escapeHtml(lookupOptionLabel(item))}
            </option>
        `).join("")}
    `;

    if (selectedLocal > 0 && !options.some(item => Number(item.id) === selectedLocal)) {
        localField.value = "0";
    }

    if (sectorId <= 0 && selectedLocal > 0) {
        syncSectorFromSelectedLocal();
    }
}

function syncSectorFromSelectedLocal() {
    const localField = document.querySelector('[data-field="localId"]');
    const sectorField = document.querySelector('[data-field="sectorId"]');
    if (!localField || !sectorField) return;

    const selectedLocalId = Number(localField.value || 0);
    if (selectedLocalId <= 0) return;

    const local = (state.lookups.locals || []).find(item => Number(item.id) === selectedLocalId);
    if (!local || Number(local.sectorId || 0) <= 0) return;

    sectorField.value = String(local.sectorId);
    refreshLocalOptions();
}

function saveModal() {
    const entity = currentEntity();
    if (!entity || entity.readOnly) return;

    const values = {};
    document.querySelectorAll("[data-field]").forEach(input => {
        if (input.type === "checkbox") {
            values[input.dataset.field] = input.checked;
            return;
        }

        values[input.dataset.field] = input.value;
    });

    if (state.modalMode === "edit" && state.editingId > 0) {
        send("update", {
            entity: entity.key,
            id: state.editingId,
            values
        });
        return;
    }

    send("save", {
        entity: entity.key,
        values
    });
}

function deleteRow(id) {
    const entity = currentEntity();
    if (!entity || entity.readOnly || id <= 0) return;

    if (!confirm(t("confirmDelete")(entityTitle(entity)))) {
        return;
    }

    send("delete", {
        entity: entity.key,
        id
    });
}

function closeModal() {
    document.getElementById("modal").classList.add("hidden");
    state.modalMode = "create";
    state.editingId = 0;
    state.shortCodeTouched = false;
    setText("btnSaveModal", t("createRecord"));
}

function currentEntity() {
    return state.entities[state.currentEntity] || null;
}

function entityTitle(entity) {
    return state.locale === "ja-JP" ? entity.titleJp : entity.titlePt;
}

function entityDescription(entity) {
    return state.locale === "ja-JP" ? entity.descriptionJp : entity.descriptionPt;
}

function columnLabel(column) {
    return state.locale === "ja-JP" ? column.labelJp : column.labelPt;
}

function displayValue(row, column) {
    if (state.locale === "ja-JP" && column.jpKey && row[column.jpKey] !== undefined && row[column.jpKey] !== "") {
        return row[column.jpKey];
    }
    return row[column.key] ?? "";
}

function lookupOptionLabel(item) {
    return state.locale === "ja-JP"
        ? (item.labelJp || item.nameJp || item.namePt || "")
        : (item.labelPt || item.namePt || item.nameJp || "");
}

function groupLabel(group) {
    const labels = {
        organization: state.locale === "ja-JP" ? "組織" : "Organizacao",
        core: state.locale === "ja-JP" ? "基礎マスタ" : "Base",
        production: state.locale === "ja-JP" ? "生産マスタ" : "Producao",
        followup: state.locale === "ja-JP" ? "フォローマスタ" : "Follow",
        people: state.locale === "ja-JP" ? "人員" : "Pessoas",
        misc: state.locale === "ja-JP" ? "その他" : "Outros"
    };
    return labels[group] || group;
}

function setText(id, value) {
    const element = document.getElementById(id);
    if (element) {
        element.textContent = value ?? "-";
    }
}

function formatDateForInput(date) {
    if (!(date instanceof Date) || Number.isNaN(date.getTime())) {
        return "";
    }

    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, "0");
    const day = String(date.getDate()).padStart(2, "0");
    return `${year}-${month}-${day}`;
}

function t(key) {
    return I18N[state.locale]?.[key] ?? I18N["pt-BR"][key];
}

function iconEdit() {
    return `
        <svg viewBox="0 0 24 24" aria-hidden="true">
            <path d="M4 16.7V20h3.3L18.7 8.6l-3.3-3.3L4 16.7Z"></path>
            <path d="M13.9 4.7l3.3 3.3"></path>
        </svg>
    `;
}

function iconDelete() {
    return `
        <svg viewBox="0 0 24 24" aria-hidden="true">
            <path d="M9 3.75h6l.75 1.5H20v1.5H4V5.25h4.25L9 3.75Z"></path>
            <path d="M6.75 8.25h10.5l-.8 10.1A1.5 1.5 0 0 1 14.95 19.75h-5.9a1.5 1.5 0 0 1-1.49-1.4l-.81-10.1Z"></path>
        </svg>
    `;
}

function sanitizeShortCode(value) {
    return String(value || "")
        .trim()
        .replace(/\s+/g, "")
        .replace(/[^A-Za-z0-9#\-_]/g, "")
        .toUpperCase();
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

function normalizeColorValue(value) {
    const text = String(value ?? "").trim();
    return /^#[0-9a-fA-F]{6}$/.test(text) ? text : "#FFFFFF";
}
