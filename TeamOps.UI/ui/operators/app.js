const state = {
    locale: "pt-BR"
};

let rows = [];
let shifts = [];
let groups = [];
let sectors = [];
let editingCodigoFJ = "";
let currentModalMode = "create";

const I18N = {
    "pt-BR": {
        documentTitle: "Operadores",
        headerBar: "Cadastro de Operadores",
        pageTitle: "Operadores",
        pageSubtitle: "Consulta rapida, busca e manutencao dos operadores.",
        showInactive: "Mostrar inativos",
        searchLabel: "Buscar",
        searchPlaceholder: "Buscar por FJ, romanji, nihongo, turno, grupo, setor ou telefone...",
        newOperator: "Novo Operador",
        loading: "Carregando...",
        empty: "Nenhum operador encontrado.",
        thCodigoFJ: "Codigo FJ",
        thRomanji: "Romanji",
        thNihongo: "Nihongo",
        thShift: "Turno",
        thGroup: "Grupo",
        thSector: "Setor",
        thType: "Tipo",
        thStart: "Inicio",
        thEnd: "Termino",
        thStatus: "Status",
        thLeader: "Lider",
        thPhone: "Telefone",
        thActions: "Acoes",
        active: "Ativo",
        inactive: "Inativo",
        yes: "Sim",
        no: "Nao",
        edit: "Editar",
        delete: "Excluir",
        deleteMissing: "Selecione um operador para excluir.",
        deleteConfirm: codigo => `Deseja realmente excluir o operador ${codigo}?`,
        modalCreateTitle: "Novo Operador",
        modalEditTitle: "Editar Operador",
        modalCreateSubtitle: "Preencha os dados para cadastrar um novo operador.",
        modalEditSubtitle: codigo => `Ajuste os dados do operador ${codigo}.`,
        saveChanges: "Salvar Alteracoes",
        register: "Cadastrar",
        closeModalAria: "Fechar modal",
        labelCodigoFJ: "Codigo FJ",
        labelRomanji: "Romanji",
        labelNihongo: "Nihongo",
        labelShift: "Turno",
        labelGroup: "Grupo",
        labelSector: "Setor",
        labelOperatorType: "Tipo de operador",
        operatorRegular: "Regular",
        operatorRelief: "Relief (リリーフ)",
        labelAdditionalSectors: "Setores adicionais",
        additionalSectorsHint: "Use Ctrl para selecionar mais de um setor.",
        labelOperatorPassword: "Senha do OperatorApp",
        operatorPasswordCreateHint: "Mínimo de 6 caracteres, com maiúscula, minúscula e número.",
        operatorPasswordEditHint: "Deixe em branco para manter a senha atual. Se o acesso ainda não existir, informe uma senha.",
        labelStart: "Data de Inicio",
        labelEnd: "Data de Termino",
        labelBirth: "Nascimento",
        labelPhone: "Telefone",
        labelAddress: "Endereco",
        hasEndDate: "Tem data de termino",
        trainer: "Trainer",
        status: "Ativo",
        leader: "Lider",
        cancel: "Cancelar",
        save: "Salvar",
        selectPlaceholder: "Selecione...",
        savedFallback: "Operador cadastrado com sucesso.",
        updatedFallback: "Operador atualizado com sucesso.",
        deletedFallback: "Operador excluido com sucesso.",
        errorFallback: "Ocorreu um erro ao processar a solicitacao."
    },
    "ja-JP": {
        documentTitle: "\u4f5c\u696d\u8005",
        headerBar: "\u4f5c\u696d\u8005\u767b\u9332",
        pageTitle: "\u4f5c\u696d\u8005",
        pageSubtitle: "\u4f5c\u696d\u8005\u306e\u691c\u7d22\u3001\u7167\u4f1a\u3001\u4fdd\u5b88\u3092\u7d20\u65e9\u304f\u884c\u3048\u307e\u3059\u3002",
        showInactive: "\u975e\u7a3c\u50cd\u3082\u8868\u793a",
        searchLabel: "\u691c\u7d22",
        searchPlaceholder: "FJ\u3001\u30ed\u30fc\u30de\u5b57\u3001\u65e5\u672c\u8a9e\u3001\u30b7\u30d5\u30c8\u3001\u30b0\u30eb\u30fc\u30d7\u3001\u30bb\u30af\u30bf\u30fc\u3001\u96fb\u8a71\u3067\u691c\u7d22...",
        newOperator: "\u65b0\u898f\u4f5c\u696d\u8005",
        loading: "\u8aad\u307f\u8fbc\u307f\u4e2d...",
        empty: "\u4f5c\u696d\u8005\u304c\u3042\u308a\u307e\u305b\u3093\u3002",
        thCodigoFJ: "FJ \u30b3\u30fc\u30c9",
        thRomanji: "\u30ed\u30fc\u30de\u5b57",
        thNihongo: "\u65e5\u672c\u8a9e",
        thShift: "\u30b7\u30d5\u30c8",
        thGroup: "\u30b0\u30eb\u30fc\u30d7",
        thSector: "\u30bb\u30af\u30bf\u30fc",
        thType: "\u7a2e\u985e",
        thStart: "\u958b\u59cb",
        thEnd: "\u7d42\u4e86",
        thStatus: "\u72b6\u614b",
        thLeader: "\u30ea\u30fc\u30c0\u30fc",
        thPhone: "\u96fb\u8a71",
        thActions: "\u64cd\u4f5c",
        active: "\u7a3c\u50cd",
        inactive: "\u975e\u7a3c\u50cd",
        yes: "\u306f\u3044",
        no: "\u3044\u3044\u3048",
        edit: "\u7de8\u96c6",
        delete: "\u524a\u9664",
        deleteMissing: "\u524a\u9664\u3059\u308b\u4f5c\u696d\u8005\u3092\u9078\u629e\u3057\u3066\u304f\u3060\u3055\u3044\u3002",
        deleteConfirm: codigo => `\u4f5c\u696d\u8005 ${codigo} \u3092\u672c\u5f53\u306b\u524a\u9664\u3057\u307e\u3059\u304b\u3002`,
        modalCreateTitle: "\u65b0\u898f\u4f5c\u696d\u8005",
        modalEditTitle: "\u4f5c\u696d\u8005\u7de8\u96c6",
        modalCreateSubtitle: "\u65b0\u3057\u3044\u4f5c\u696d\u8005\u306e\u60c5\u5831\u3092\u5165\u529b\u3057\u307e\u3059\u3002",
        modalEditSubtitle: codigo => `\u4f5c\u696d\u8005 ${codigo} \u306e\u60c5\u5831\u3092\u66f4\u65b0\u3057\u307e\u3059\u3002`,
        saveChanges: "\u5909\u66f4\u3092\u4fdd\u5b58",
        register: "\u767b\u9332",
        closeModalAria: "\u30e2\u30fc\u30c0\u30eb\u3092\u9589\u3058\u308b",
        labelCodigoFJ: "FJ \u30b3\u30fc\u30c9",
        labelRomanji: "\u30ed\u30fc\u30de\u5b57",
        labelNihongo: "\u65e5\u672c\u8a9e",
        labelShift: "\u30b7\u30d5\u30c8",
        labelGroup: "\u30b0\u30eb\u30fc\u30d7",
        labelSector: "\u30bb\u30af\u30bf\u30fc",
        labelOperatorType: "\u4f5c\u696d\u8005\u7a2e\u5225",
        operatorRegular: "\u901a\u5e38",
        operatorRelief: "Relief (\u30ea\u30ea\u30fc\u30d5)",
        labelAdditionalSectors: "\u8ffd\u52a0\u30bb\u30af\u30bf\u30fc",
        additionalSectorsHint: "Ctrl \u30ad\u30fc\u3067\u8907\u6570\u9078\u629e\u3067\u304d\u307e\u3059\u3002",
        labelOperatorPassword: "OperatorApp \u30d1\u30b9\u30ef\u30fc\u30c9",
        operatorPasswordCreateHint: "6\u6587\u5b57\u4ee5\u4e0a\u3067\u5927\u6587\u5b57\u3001\u5c0f\u6587\u5b57\u3001\u6570\u5b57\u3092\u542b\u3081\u3066\u304f\u3060\u3055\u3044\u3002",
        operatorPasswordEditHint: "\u73fe\u5728\u306e\u30d1\u30b9\u30ef\u30fc\u30c9\u3092\u4fdd\u6301\u3059\u308b\u5834\u5408\u306f\u7a7a\u6b04\u3002\u30a2\u30af\u30bb\u30b9\u672a\u4f5c\u6210\u306e\u5834\u5408\u306f\u5fc5\u9808\u3067\u3059\u3002",
        labelStart: "\u958b\u59cb\u65e5",
        labelEnd: "\u7d42\u4e86\u65e5",
        labelBirth: "\u751f\u5e74\u6708\u65e5",
        labelPhone: "\u96fb\u8a71",
        labelAddress: "\u4f4f\u6240",
        hasEndDate: "\u7d42\u4e86\u65e5\u3092\u8a2d\u5b9a",
        trainer: "\u30c8\u30ec\u30fc\u30ca\u30fc",
        status: "\u7a3c\u50cd",
        leader: "\u30ea\u30fc\u30c0\u30fc",
        cancel: "\u30ad\u30e3\u30f3\u30bb\u30eb",
        save: "\u4fdd\u5b58",
        selectPlaceholder: "\u9078\u629e\u3057\u3066\u304f\u3060\u3055\u3044",
        savedFallback: "\u4f5c\u696d\u8005\u3092\u767b\u9332\u3057\u307e\u3057\u305f\u3002",
        updatedFallback: "\u4f5c\u696d\u8005\u3092\u66f4\u65b0\u3057\u307e\u3057\u305f\u3002",
        deletedFallback: "\u4f5c\u696d\u8005\u3092\u524a\u9664\u3057\u307e\u3057\u305f\u3002",
        errorFallback: "\u51e6\u7406\u4e2d\u306b\u30a8\u30e9\u30fc\u304c\u767a\u751f\u3057\u307e\u3057\u305f\u3002"
    }
};

window.addEventListener("DOMContentLoaded", () => {
    bindEvents();
    send("load");
});

function bindEvents() {
    document.getElementById("searchInput").addEventListener("input", applyFilters);
    document.getElementById("toggleShowInactive").addEventListener("change", applyFilters);
    document.getElementById("btnHeaderNovo").addEventListener("click", openCreateModal);
    document.getElementById("btnSalvarModal").addEventListener("click", submitModal);
    document.getElementById("btnCancelarModal").addEventListener("click", closeModal);
    document.getElementById("btnCloseModal").addEventListener("click", closeModal);
    document.getElementById("modalBackdrop").addEventListener("click", closeModal);
    document.getElementById("chkHasEnd").addEventListener("change", toggleEndDate);
    document.getElementById("cmbOperatorType").addEventListener("change", toggleOperatorType);
    document.getElementById("cmbSector").addEventListener("change", refreshAdditionalSectorOptions);
    document.getElementById("txtCodigoFJ").addEventListener("input", event => {
        event.target.value = event.target.value.toUpperCase();
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
            setLocale(msg.locale);
            hydrateScreen(msg);
            break;
        case "rows":
            rows = normalizeRows(msg.data || []);
            applyFilters();
            syncEditingOperator();
            break;
        case "saved":
            alert(msg.message || t("savedFallback"));
            closeModal();
            break;
        case "updated":
            alert(msg.message || t("updatedFallback"));
            closeModal();
            break;
        case "deleted":
            alert(msg.message || t("deletedFallback"));
            applyFilters();
            break;
        case "error":
            alert(msg.message || t("errorFallback"));
            break;
    }
});

function setLocale(locale) {
    state.locale = locale === "ja-JP" ? "ja-JP" : "pt-BR";
    document.documentElement.lang = state.locale;
    document.title = t("documentTitle");

    document.querySelectorAll("[data-i18n]").forEach(element => {
        element.textContent = t(element.dataset.i18n);
    });

    document.querySelectorAll("[data-i18n-placeholder]").forEach(element => {
        element.setAttribute("placeholder", t(element.dataset.i18nPlaceholder));
    });

    document.querySelectorAll("[data-i18n-aria-label]").forEach(element => {
        element.setAttribute("aria-label", t(element.dataset.i18nAriaLabel));
    });
}

function hydrateScreen(payload) {
    shifts = payload.shifts || [];
    groups = payload.groups || [];
    sectors = payload.sectors || [];
    rows = normalizeRows(payload.rows || []);

    fillSelect("cmbShift", shifts);
    fillSelect("cmbGroup", groups);
    fillSelect("cmbSector", sectors);
    refreshAdditionalSectorOptions();

    resetModalForm();
    applyFilters();
}

function normalizeRows(items) {
    return items.map(item => ({
        ...item,
        trainer: toBool(item.trainer),
        status: toBool(item.status),
        isLeader: toBool(item.isLeader)
        ,sectorIds: String(item.sectorIdsCsv || item.sectorId || "")
            .split(",").map(Number).filter(Boolean)
        ,hasOperatorAppAccess: toBool(item.hasOperatorAppAccess)
    }));
}

function toBool(value) {
    return value === true || value === 1 || value === "1";
}

function fillSelect(id, items) {
    const select = document.getElementById(id);
    select.innerHTML = `<option value="">${escapeHtml(t("selectPlaceholder"))}</option>`;

    items.forEach(item => {
        select.innerHTML += `<option value="${item.Id}">${escapeHtml(localizedName(item.NamePt, item.NameJp))}</option>`;
    });
}

function applyFilters() {
    const term = document.getElementById("searchInput").value.trim().toLowerCase();
    const showInactive = document.getElementById("toggleShowInactive").checked;

    const filtered = rows.filter(item => {
        if (!showInactive && !item.status) return false;
        if (!term) return true;

        const haystack = [
            item.codigoFJ,
            item.nameRomanji,
            item.nameNihongo,
            item.shiftNamePt,
            item.shiftNameJp,
            item.groupNamePt,
            item.groupNameJp,
            item.sectorNamePt,
            item.sectorNameJp,
            item.phone,
            item.address
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

    if (!items || items.length === 0) {
        container.innerHTML = `<div class="empty-state">${escapeHtml(t("empty"))}</div>`;
        return;
    }

    let html = `
        <table class="operators-table">
            <thead>
                <tr>
                    <th>${escapeHtml(t("thCodigoFJ"))}</th>
                    <th>${escapeHtml(t("thRomanji"))}</th>
                    <th>${escapeHtml(t("thNihongo"))}</th>
                    <th>${escapeHtml(t("thShift"))}</th>
                    <th>${escapeHtml(t("thGroup"))}</th>
                    <th>${escapeHtml(t("thSector"))}</th>
                    <th>${escapeHtml(t("thType"))}</th>
                    <th>${escapeHtml(t("thStart"))}</th>
                    <th>${escapeHtml(t("thEnd"))}</th>
                    <th>${escapeHtml(t("thStatus"))}</th>
                    <th>${escapeHtml(t("thLeader"))}</th>
                    <th>${escapeHtml(t("thPhone"))}</th>
                    <th>${escapeHtml(t("thActions"))}</th>
                </tr>
            </thead>
            <tbody>
    `;

    items.forEach(item => {
        const selectedClass = item.codigoFJ === editingCodigoFJ ? "is-selected" : "";
        const statusLabel = item.status ? t("active") : t("inactive");
        const leaderLabel = item.isLeader ? t("yes") : t("no");

        html += `
            <tr class="${selectedClass}">
                <td>${escapeHtml(item.codigoFJ)}</td>
                <td>${escapeHtml(item.nameRomanji)}</td>
                <td>${escapeHtml(item.nameNihongo)}</td>
                <td>${escapeHtml(localizedName(item.shiftNamePt, item.shiftNameJp))}</td>
                <td>${escapeHtml(localizedName(item.groupNamePt, item.groupNameJp))}</td>
                <td>${escapeHtml(localizedName(item.sectorNamePt, item.sectorNameJp))}</td>
                <td>${escapeHtml(item.operatorType === "Relief" ? t("operatorRelief") : t("operatorRegular"))}</td>
                <td>${escapeHtml(item.startDate || "")}</td>
                <td>${escapeHtml(item.endDate || "-")}</td>
                <td><span class="status-pill ${item.status ? "status-active" : "status-inactive"}">${statusLabel}</span></td>
                <td>${leaderLabel}</td>
                <td>${escapeHtml(item.phone || "-")}</td>
                <td class="actions-cell">
                    <button class="row-btn row-btn-edit" type="button" data-edit="${escapeHtmlAttr(item.codigoFJ)}">${escapeHtml(t("edit"))}</button>
                    <button class="row-btn row-btn-delete" type="button" data-delete="${escapeHtmlAttr(item.codigoFJ)}">${escapeHtml(t("delete"))}</button>
                </td>
            </tr>
        `;
    });

    html += `
            </tbody>
        </table>
    `;

    container.innerHTML = html;

    container.querySelectorAll("[data-edit]").forEach(button => {
        button.addEventListener("click", () => {
            openEditModal(button.dataset.edit);
        });
    });

    container.querySelectorAll("[data-delete]").forEach(button => {
        button.addEventListener("click", () => {
            deleteOperator(button.dataset.delete);
        });
    });
}

function openCreateModal() {
    currentModalMode = "create";
    resetModalForm();
    syncModeState();
    document.getElementById("operatorModal").classList.remove("hidden");
}

function openEditModal(codigoFJ) {
    const operator = rows.find(item => item.codigoFJ === codigoFJ);
    if (!operator) return;

    currentModalMode = "edit";
    editingCodigoFJ = operator.codigoFJ;

    document.getElementById("txtCodigoFJ").value = operator.codigoFJ || "";
    document.getElementById("txtCodigoFJ").readOnly = true;
    document.getElementById("txtRomanji").value = operator.nameRomanji || "";
    document.getElementById("txtNihongo").value = operator.nameNihongo || "";
    document.getElementById("cmbShift").value = String(operator.shiftId || "");
    document.getElementById("cmbGroup").value = String(operator.groupId || "");
    document.getElementById("cmbSector").value = String(operator.sectorId || "");
    document.getElementById("cmbOperatorType").value = operator.operatorType || "Regular";
    refreshAdditionalSectorOptions();
    Array.from(document.getElementById("cmbAdditionalSectors").options).forEach(option => {
        option.selected = operator.sectorIds.includes(Number(option.value));
    });
    document.getElementById("txtOperatorPassword").value = "";
    document.getElementById("dtStart").value = operator.startDate || "";
    document.getElementById("chkHasEnd").checked = !!operator.endDate;
    document.getElementById("dtEnd").value = operator.endDate || "";
    document.getElementById("chkTrainer").checked = operator.trainer;
    document.getElementById("chkStatus").checked = operator.status;
    document.getElementById("chkIsLeader").checked = operator.isLeader;
    document.getElementById("txtTelefone").value = operator.phone || "";
    document.getElementById("txtEndereco").value = operator.address || "";
    document.getElementById("dtNascimento").value = operator.birthDate || "";

    toggleEndDate();
    syncModeState();
    toggleOperatorType();
    applyFilters();
    document.getElementById("operatorModal").classList.remove("hidden");
}

function syncEditingOperator() {
    if (!editingCodigoFJ || currentModalMode !== "edit") return;

    const refreshed = rows.find(item => item.codigoFJ === editingCodigoFJ);
    if (!refreshed) {
        closeModal();
        return;
    }

    if (!document.getElementById("operatorModal").classList.contains("hidden")) {
        openEditModal(editingCodigoFJ);
    }
}

function resetModalForm() {
    editingCodigoFJ = "";
    document.getElementById("txtCodigoFJ").value = "";
    document.getElementById("txtCodigoFJ").readOnly = false;
    document.getElementById("txtRomanji").value = "";
    document.getElementById("txtNihongo").value = "";
    document.getElementById("cmbShift").value = "";
    document.getElementById("cmbGroup").value = "";
    document.getElementById("cmbSector").value = "";
    document.getElementById("cmbOperatorType").value = "Regular";
    document.getElementById("txtOperatorPassword").value = "";
    refreshAdditionalSectorOptions();
    document.getElementById("dtStart").value = "";
    document.getElementById("chkHasEnd").checked = false;
    document.getElementById("dtEnd").value = "";
    document.getElementById("chkTrainer").checked = false;
    document.getElementById("chkStatus").checked = true;
    document.getElementById("chkIsLeader").checked = false;
    document.getElementById("txtTelefone").value = "";
    document.getElementById("txtEndereco").value = "";
    document.getElementById("dtNascimento").value = "";

    toggleEndDate();
    syncModeState();
    toggleOperatorType();
    applyFilters();
}

function closeModal() {
    document.getElementById("operatorModal").classList.add("hidden");
    currentModalMode = "create";
    resetModalForm();
}

function toggleEndDate() {
    const hasEnd = document.getElementById("chkHasEnd").checked;
    const endInput = document.getElementById("dtEnd");
    endInput.disabled = !hasEnd;

    if (!hasEnd) {
        endInput.value = "";
    }
}

function toggleOperatorType() {
    const relief = document.getElementById("cmbOperatorType").value === "Relief";
    document.getElementById("additionalSectorsGroup").classList.toggle("hidden", !relief);
    document.getElementById("cmbAdditionalSectors").disabled = !relief;
    if (!relief) {
        Array.from(document.getElementById("cmbAdditionalSectors").options).forEach(option => option.selected = false);
    }
}

function refreshAdditionalSectorOptions() {
    const select = document.getElementById("cmbAdditionalSectors");
    const selected = new Set(Array.from(select.selectedOptions).map(option => option.value));
    const primary = document.getElementById("cmbSector").value;
    select.innerHTML = "";
    sectors.filter(item => String(item.Id) !== primary).forEach(item => {
        const option = document.createElement("option");
        option.value = String(item.Id);
        option.textContent = localizedName(item.NamePt, item.NameJp);
        option.selected = selected.has(option.value);
        select.appendChild(option);
    });
}

function submitModal() {
    if (currentModalMode === "edit") {
        send("update", buildPayload());
        return;
    }

    send("save", buildPayload());
}

function deleteOperator(codigoFJ) {
    const targetCodigoFJ = String(codigoFJ || "").trim();
    if (!targetCodigoFJ) {
        alert(t("deleteMissing"));
        return;
    }

    if (!confirm(t("deleteConfirm")(targetCodigoFJ))) {
        return;
    }

    send("delete", { codigoFJ: targetCodigoFJ });
}

function buildPayload() {
    return {
        codigoFJ: document.getElementById("txtCodigoFJ").value.trim().toUpperCase(),
        nameRomanji: document.getElementById("txtRomanji").value.trim(),
        nameNihongo: document.getElementById("txtNihongo").value.trim(),
        shiftId: Number(document.getElementById("cmbShift").value || 0),
        groupId: Number(document.getElementById("cmbGroup").value || 0),
        sectorId: Number(document.getElementById("cmbSector").value || 0),
        operatorType: document.getElementById("cmbOperatorType").value,
        sectorIds: Array.from(document.getElementById("cmbAdditionalSectors").selectedOptions)
            .map(option => Number(option.value)),
        password: document.getElementById("txtOperatorPassword").value,
        startDate: document.getElementById("dtStart").value,
        hasEndDate: document.getElementById("chkHasEnd").checked,
        endDate: document.getElementById("dtEnd").value,
        trainer: document.getElementById("chkTrainer").checked,
        status: document.getElementById("chkStatus").checked,
        isLeader: document.getElementById("chkIsLeader").checked,
        phone: document.getElementById("txtTelefone").value.trim(),
        address: document.getElementById("txtEndereco").value.trim(),
        birthDate: document.getElementById("dtNascimento").value
    };
}

function syncModeState() {
    const isEdit = currentModalMode === "edit" && !!editingCodigoFJ;
    document.getElementById("formTitle").textContent = isEdit ? t("modalEditTitle") : t("modalCreateTitle");
    document.getElementById("formSubtitle").textContent = isEdit
        ? t("modalEditSubtitle")(editingCodigoFJ)
        : t("modalCreateSubtitle");
    document.getElementById("btnSalvarModal").textContent = isEdit ? t("saveChanges") : t("register");
    document.getElementById("operatorPasswordHint").textContent = isEdit
        ? t("operatorPasswordEditHint")
        : t("operatorPasswordCreateHint");
}

function localizedName(pt, jp) {
    return state.locale === "ja-JP"
        ? (jp || pt || "")
        : (pt || jp || "");
}

function t(key) {
    return I18N[state.locale]?.[key] ?? I18N["pt-BR"]?.[key] ?? key;
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
