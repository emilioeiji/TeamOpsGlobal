const state = {
    locale: "pt-BR"
};

let rows = [];
let profiles = [];
let organizationNodes = [];
let sectors = [];
let permissionProfiles = [];
let permissionModules = [];
let permissionActions = [];
let permissionRows = [];
let currentModalMode = "create";
let editingLogin = "";
let passwordTargetLogin = "";

const I18N = {
    "pt-BR": {
        documentTitle: "Controle de Acesso",
        headerBar: "Controle de Acesso",
        eyebrow: "Administracao de Usuarios",
        heroTitle: "Permissoes e senhas em um fluxo mais simples",
        heroText: "Busque rapidamente, ajuste nivel de acesso com seguranca e cadastre novos usuarios sem sair da tela.",
        metricTotal: "Total",
        metricAdmins: "Admins",
        metricManagers: "Managers+",
        usersTitle: "Usuarios cadastrados",
        usersSubtitle: "Pesquise por login, nome, FJ ou nivel de acesso.",
        searchLabel: "Buscar",
        searchPlaceholder: "Digite o login, nome, FJ ou nivel...",
        newUser: "Novo Usuario",
        permissionMatrix: "Matriz de Permissoes",
        permissionMatrixTitle: "Matriz de Permissoes",
        permissionMatrixSubtitle: "Defina as acoes permitidas para cada perfil. O acesso continua limitado aos setores do usuario.",
        permissionProfile: "Perfil",
        permissionScopeNote: "As permissoes funcionais abaixo sao combinadas com a arvore e os setores atribuidos a cada usuario.",
        savePermissionMatrix: "Salvar Permissoes",
        permissionSavedFallback: "Matriz de permissoes atualizada.",
        moduleColumn: "Modulo",
        loading: "Carregando...",
        empty: "Nenhum usuario encontrado.",
        thLogin: "Login",
        thName: "Nome",
        thCodigoFJ: "Codigo FJ",
        thLevel: "Nivel",
        thCreatedAt: "Criado em",
        thActions: "Acoes",
        edit: "Editar",
        password: "Senha",
        userCreateTitle: "Novo Usuario",
        userEditTitle: "Editar Usuario",
        userCreateSubtitle: "Cadastre um novo usuario com o nivel de acesso correto.",
        userEditSubtitle: login => `Ajuste os dados e o nivel de acesso de ${login}.`,
        saveChanges: "Salvar Alteracoes",
        register: "Cadastrar",
        closeModalAria: "Fechar modal",
        loginLabel: "Login",
        accessLevelLabel: "Nivel de Acesso",
        selectPlaceholder: "Selecione...",
        nameLabel: "Nome",
        namePlaceholder: "Opcional, mas ajuda a localizar mais rapido",
        initialPassword: "Senha inicial",
        passwordLabel: "Senha",
        confirmPasswordLabel: "Confirmar Senha",
        cancel: "Cancelar",
        resetPasswordTitle: "Redefinir Senha",
        resetPasswordSubtitle: login => `Defina uma nova senha para ${login}.`,
        resetPasswordDefaultSubtitle: "Defina uma nova senha para o usuario selecionado.",
        newPasswordLabel: "Nova Senha",
        confirmNewPasswordLabel: "Confirmar Nova Senha",
        saveNewPassword: "Salvar Nova Senha",
        createdFallback: "Usuario cadastrado com sucesso.",
        updatedFallback: "Usuario atualizado com sucesso.",
        passwordResetFallback: "Senha redefinida com sucesso.",
        errorFallback: "Ocorreu um erro ao processar a solicitacao.",
        selectPasswordUser: "Selecione um usuario para redefinir a senha."
    },
    "ja-JP": {
        documentTitle: "\u30a2\u30af\u30bb\u30b9\u7ba1\u7406",
        headerBar: "\u30a2\u30af\u30bb\u30b9\u7ba1\u7406",
        eyebrow: "\u30e6\u30fc\u30b6\u30fc\u7ba1\u7406",
        heroTitle: "\u6a29\u9650\u3068\u30d1\u30b9\u30ef\u30fc\u30c9\u3092\u5206\u304b\u308a\u3084\u3059\u304f\u7ba1\u7406",
        heroText: "\u753b\u9762\u3092\u79fb\u52d5\u305b\u305a\u306b\u3001\u30e6\u30fc\u30b6\u30fc\u691c\u7d22\u3001\u6a29\u9650\u5909\u66f4\u3001\u65b0\u898f\u767b\u9332\u3092\u884c\u3048\u307e\u3059\u3002",
        metricTotal: "\u5408\u8a08",
        metricAdmins: "\u7ba1\u7406\u8005",
        metricManagers: "Manager+",
        usersTitle: "\u767b\u9332\u6e08\u307f\u30e6\u30fc\u30b6\u30fc",
        usersSubtitle: "\u30ed\u30b0\u30a4\u30f3\u3001\u540d\u524d\u3001FJ\u3001\u6a29\u9650\u3067\u691c\u7d22\u3067\u304d\u307e\u3059\u3002",
        searchLabel: "\u691c\u7d22",
        searchPlaceholder: "\u30ed\u30b0\u30a4\u30f3\u3001\u540d\u524d\u3001FJ\u3001\u6a29\u9650\u3067\u691c\u7d22...",
        newUser: "\u65b0\u898f\u30e6\u30fc\u30b6\u30fc",
        permissionMatrix: "\u6a29\u9650\u30de\u30c8\u30ea\u30af\u30b9",
        permissionMatrixTitle: "\u6a29\u9650\u30de\u30c8\u30ea\u30af\u30b9",
        permissionMatrixSubtitle: "\u30d7\u30ed\u30d5\u30a1\u30a4\u30eb\u3054\u3068\u306b\u8a31\u53ef\u3059\u308b\u64cd\u4f5c\u3092\u8a2d\u5b9a\u3057\u307e\u3059\u3002\u30e6\u30fc\u30b6\u30fc\u306e\u30bb\u30af\u30bf\u30fc\u7bc4\u56f2\u306f\u5f15\u304d\u7d9a\u304d\u9069\u7528\u3055\u308c\u307e\u3059\u3002",
        permissionProfile: "\u30d7\u30ed\u30d5\u30a1\u30a4\u30eb",
        permissionScopeNote: "\u6a5f\u80fd\u6a29\u9650\u306f\u3001\u5404\u30e6\u30fc\u30b6\u30fc\u306e\u7d44\u7e54\u30c4\u30ea\u30fc\u3068\u5272\u308a\u5f53\u3066\u30bb\u30af\u30bf\u30fc\u3068\u7d44\u307f\u5408\u308f\u305b\u3066\u9069\u7528\u3055\u308c\u307e\u3059\u3002",
        savePermissionMatrix: "\u6a29\u9650\u3092\u4fdd\u5b58",
        permissionSavedFallback: "\u6a29\u9650\u30de\u30c8\u30ea\u30af\u30b9\u3092\u66f4\u65b0\u3057\u307e\u3057\u305f\u3002",
        moduleColumn: "\u30e2\u30b8\u30e5\u30fc\u30eb",
        loading: "\u8aad\u307f\u8fbc\u307f\u4e2d...",
        empty: "\u30e6\u30fc\u30b6\u30fc\u304c\u3042\u308a\u307e\u305b\u3093\u3002",
        thLogin: "\u30ed\u30b0\u30a4\u30f3",
        thName: "\u540d\u524d",
        thCodigoFJ: "FJ \u30b3\u30fc\u30c9",
        thLevel: "\u6a29\u9650",
        thCreatedAt: "\u767b\u9332\u65e5\u6642",
        thActions: "\u64cd\u4f5c",
        edit: "\u7de8\u96c6",
        password: "\u30d1\u30b9\u30ef\u30fc\u30c9",
        userCreateTitle: "\u65b0\u898f\u30e6\u30fc\u30b6\u30fc",
        userEditTitle: "\u30e6\u30fc\u30b6\u30fc\u7de8\u96c6",
        userCreateSubtitle: "\u9069\u5207\u306a\u6a29\u9650\u3067\u65b0\u3057\u3044\u30e6\u30fc\u30b6\u30fc\u3092\u767b\u9332\u3057\u307e\u3059\u3002",
        userEditSubtitle: login => `${login} \u306e\u60c5\u5831\u3068\u6a29\u9650\u3092\u66f4\u65b0\u3057\u307e\u3059\u3002`,
        saveChanges: "\u5909\u66f4\u3092\u4fdd\u5b58",
        register: "\u767b\u9332",
        closeModalAria: "\u30e2\u30fc\u30c0\u30eb\u3092\u9589\u3058\u308b",
        loginLabel: "\u30ed\u30b0\u30a4\u30f3",
        accessLevelLabel: "\u30a2\u30af\u30bb\u30b9\u6a29\u9650",
        selectPlaceholder: "\u9078\u629e\u3057\u3066\u304f\u3060\u3055\u3044",
        nameLabel: "\u540d\u524d",
        namePlaceholder: "\u4efb\u610f\u3067\u3059\u304c\u3001\u691c\u7d22\u3057\u3084\u3059\u304f\u306a\u308a\u307e\u3059",
        initialPassword: "\u521d\u671f\u30d1\u30b9\u30ef\u30fc\u30c9",
        passwordLabel: "\u30d1\u30b9\u30ef\u30fc\u30c9",
        confirmPasswordLabel: "\u30d1\u30b9\u30ef\u30fc\u30c9\u78ba\u8a8d",
        cancel: "\u30ad\u30e3\u30f3\u30bb\u30eb",
        resetPasswordTitle: "\u30d1\u30b9\u30ef\u30fc\u30c9\u518d\u8a2d\u5b9a",
        resetPasswordSubtitle: login => `${login} \u306e\u65b0\u3057\u3044\u30d1\u30b9\u30ef\u30fc\u30c9\u3092\u8a2d\u5b9a\u3057\u307e\u3059\u3002`,
        resetPasswordDefaultSubtitle: "\u9078\u629e\u3057\u305f\u30e6\u30fc\u30b6\u30fc\u306e\u65b0\u3057\u3044\u30d1\u30b9\u30ef\u30fc\u30c9\u3092\u8a2d\u5b9a\u3057\u307e\u3059\u3002",
        newPasswordLabel: "\u65b0\u3057\u3044\u30d1\u30b9\u30ef\u30fc\u30c9",
        confirmNewPasswordLabel: "\u65b0\u3057\u3044\u30d1\u30b9\u30ef\u30fc\u30c9\u78ba\u8a8d",
        saveNewPassword: "\u65b0\u3057\u3044\u30d1\u30b9\u30ef\u30fc\u30c9\u3092\u4fdd\u5b58",
        createdFallback: "\u30e6\u30fc\u30b6\u30fc\u3092\u767b\u9332\u3057\u307e\u3057\u305f\u3002",
        updatedFallback: "\u30e6\u30fc\u30b6\u30fc\u3092\u66f4\u65b0\u3057\u307e\u3057\u305f\u3002",
        passwordResetFallback: "\u30d1\u30b9\u30ef\u30fc\u30c9\u3092\u518d\u8a2d\u5b9a\u3057\u307e\u3057\u305f\u3002",
        errorFallback: "\u51e6\u7406\u4e2d\u306b\u30a8\u30e9\u30fc\u304c\u767a\u751f\u3057\u307e\u3057\u305f\u3002",
        selectPasswordUser: "\u30d1\u30b9\u30ef\u30fc\u30c9\u518d\u8a2d\u5b9a\u5bfe\u8c61\u306e\u30e6\u30fc\u30b6\u30fc\u3092\u9078\u629e\u3057\u3066\u304f\u3060\u3055\u3044\u3002"
    }
};

const MODULE_LABELS = {
    "pt-BR": {
        dashboard: "Painel", hikitsugui: "Hikitsugui", haidai: "Haidai", presence: "Presenca",
        production: "Producao", sobradepeca: "Sobra de Peca", prcl: "PR/CL", followup: "Follow-up",
        tasks: "Tarefas", mastercard: "Master Card", paidleave: "Yukyu", reports: "Relatorios",
        organization: "Organizacao", accesscontrol: "Controle de Acesso"
    },
    "ja-JP": {
        dashboard: "\u30c0\u30c3\u30b7\u30e5\u30dc\u30fc\u30c9", hikitsugui: "Hikitsugui", haidai: "Haidai", presence: "\u5728\u5e2d",
        production: "\u751f\u7523", sobradepeca: "\u4f59\u5269\u54c1", prcl: "PR/CL", followup: "\u30d5\u30a9\u30ed\u30fc\u30a2\u30c3\u30d7",
        tasks: "\u30bf\u30b9\u30af", mastercard: "\u30de\u30b9\u30bf\u30fc\u30ab\u30fc\u30c9", paidleave: "\u6709\u4f11", reports: "\u30ec\u30dd\u30fc\u30c8",
        organization: "\u7d44\u7e54", accesscontrol: "\u30a2\u30af\u30bb\u30b9\u7ba1\u7406"
    }
};

const ACTION_LABELS = {
    "pt-BR": { view: "Ver", create: "Criar", edit: "Editar", delete: "Excluir", export: "Exportar", approve: "Aprovar", administer: "Administrar" },
    "ja-JP": { view: "\u8868\u793a", create: "\u4f5c\u6210", edit: "\u7de8\u96c6", delete: "\u524a\u9664", export: "\u51fa\u529b", approve: "\u627f\u8a8d", administer: "\u7ba1\u7406" }
};

window.addEventListener("DOMContentLoaded", () => {
    bindEvents();
    send("load");
});

function bindEvents() {
    document.getElementById("searchInput").addEventListener("input", applyFilters);
    document.getElementById("btnHeaderNovo").addEventListener("click", openCreateModal);
    document.getElementById("cmbProfile").addEventListener("change", syncScopeMode);
    document.getElementById("btnPermissionMatrix").addEventListener("click", openPermissionModal);
    document.getElementById("cmbPermissionProfile").addEventListener("change", renderPermissionMatrix);
    document.getElementById("btnClosePermissionModal").addEventListener("click", closePermissionModal);
    document.getElementById("btnCancelPermissionModal").addEventListener("click", closePermissionModal);
    document.getElementById("permissionModalBackdrop").addEventListener("click", closePermissionModal);
    document.getElementById("btnSavePermissionMatrix").addEventListener("click", savePermissionMatrix);

    document.getElementById("btnCloseUserModal").addEventListener("click", closeUserModal);
    document.getElementById("btnCancelUserModal").addEventListener("click", closeUserModal);
    document.getElementById("userModalBackdrop").addEventListener("click", closeUserModal);
    document.getElementById("btnSaveUserModal").addEventListener("click", submitUserModal);

    document.getElementById("btnClosePasswordModal").addEventListener("click", closePasswordModal);
    document.getElementById("btnCancelPasswordModal").addEventListener("click", closePasswordModal);
    document.getElementById("passwordModalBackdrop").addEventListener("click", closePasswordModal);
    document.getElementById("btnSavePasswordModal").addEventListener("click", submitPasswordReset);
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
            hydratePermissions(msg);
            rows = normalizeRows(msg.rows || msg.data || []);
            renderMetrics(rows);
            applyFilters();
            syncEditingUser();
            break;
        case "permissions_saved":
            alert(msg.message || t("permissionSavedFallback"));
            closePermissionModal();
            break;
        case "created":
            alert(msg.message || t("createdFallback"));
            closeUserModal();
            break;
        case "updated":
            alert(msg.message || t("updatedFallback"));
            closeUserModal();
            break;
        case "password_reset":
            alert(msg.message || t("passwordResetFallback"));
            closePasswordModal();
            break;
        case "error":
            alert(msg.message || t("errorFallback"));
            break;
    }
});

function hydrateScreen(payload) {
    profiles = payload.profiles || [];
    organizationNodes = payload.organizationNodes || [];
    sectors = payload.sectors || [];
    rows = normalizeRows(payload.rows || []);
    hydratePermissions(payload);

    fillSelect("cmbProfile", profiles.map(item => ({ ...item, label: state.locale === "ja-JP" ? item.nameJp : item.namePt })), "code", "label");
    fillSelect("cmbReferenceNode", organizationNodes.map(item => ({ ...item, label: `${item.code} - ${state.locale === "ja-JP" ? item.nameJp : item.namePt}` })), "id", "label");
    fillSelect("cmbSectorIds", sectors.map(item => ({ ...item, label: `${item.code} - ${state.locale === "ja-JP" ? item.nameJp : item.namePt}` })), "id", "label");
    renderMetrics(rows);
    resetUserForm();
    applyFilters();
}

function hydratePermissions(payload) {
    permissionProfiles = payload.permissionProfiles || permissionProfiles;
    permissionModules = payload.permissionModules || permissionModules;
    permissionActions = payload.permissionActions || permissionActions;
    permissionRows = payload.permissionRows || permissionRows;
    document.getElementById("btnPermissionMatrix").classList.toggle("hidden", payload.canManagePermissionMatrix !== true);

    const selectedProfile = document.getElementById("cmbPermissionProfile").value;
    fillSelect("cmbPermissionProfile", permissionProfiles.map(item => ({
        ...item,
        label: `${item.code} - ${state.locale === "ja-JP" ? item.nameJp : item.namePt}`
    })), "code", "label");
    if (permissionProfiles.some(item => item.code === selectedProfile)) {
        document.getElementById("cmbPermissionProfile").value = selectedProfile;
    }
    renderPermissionMatrix();
}

function openPermissionModal() {
    if (!permissionProfiles.length) return;
    if (!document.getElementById("cmbPermissionProfile").value) {
        document.getElementById("cmbPermissionProfile").value = permissionProfiles[0].code;
    }
    renderPermissionMatrix();
    document.getElementById("permissionModal").classList.remove("hidden");
}

function closePermissionModal() {
    document.getElementById("permissionModal").classList.add("hidden");
}

function renderPermissionMatrix() {
    const container = document.getElementById("permissionMatrixContainer");
    const profileCode = document.getElementById("cmbPermissionProfile").value;
    if (!profileCode || !permissionModules.length) {
        container.innerHTML = "";
        return;
    }

    const allowed = new Set(permissionRows
        .filter(item => item.profileCode === profileCode && item.isAllowed)
        .map(item => `${item.moduleCode}:${item.actionCode}`));
    const headers = permissionActions.map(action => `<th>${escapeHtml(ACTION_LABELS[state.locale]?.[action] || action)}</th>`).join("");
    const body = permissionModules.map(moduleCode => {
        const cells = permissionActions.map(actionCode => {
            const key = `${moduleCode}:${actionCode}`;
            return `<td><input class="permission-check" type="checkbox" data-module="${escapeHtmlAttr(moduleCode)}" data-action="${escapeHtmlAttr(actionCode)}" ${allowed.has(key) ? "checked" : ""}></td>`;
        }).join("");
        return `<tr><th>${escapeHtml(MODULE_LABELS[state.locale]?.[moduleCode] || moduleCode)}</th>${cells}</tr>`;
    }).join("");

    container.innerHTML = `<table class="permission-table"><thead><tr><th>${escapeHtml(t("moduleColumn"))}</th>${headers}</tr></thead><tbody>${body}</tbody></table>`;
}

function savePermissionMatrix() {
    const profileCode = document.getElementById("cmbPermissionProfile").value;
    const permissions = Array.from(document.querySelectorAll(".permission-check:checked")).map(item => ({
        moduleCode: item.dataset.module,
        actionCode: item.dataset.action,
        isAllowed: true
    }));
    send("save_permissions", { profileCode, permissions });
}

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

function normalizeRows(items) {
    return items.map(item => ({
        ...item,
        profileLabel: state.locale === "ja-JP" ? item.profileNameJp : item.profileNamePt,
        sectorIds: (item.sectorIds || []).map(Number),
        codigoFJ: item.codigoFJ || "",
        name: item.name || ""
    }));
}

function fillSelect(id, items, valueField, textField) {
    const select = document.getElementById(id);
    select.innerHTML = select.multiple ? "" : `<option value="">${escapeHtml(t("selectPlaceholder"))}</option>`;

    items.forEach(item => {
        select.innerHTML += `<option value="${item[valueField]}">${escapeHtml(item[textField])}</option>`;
    });
}

function renderMetrics(items) {
    document.getElementById("metricTotal").textContent = String(items.length);
    document.getElementById("metricAdmins").textContent = String(items.filter(item => item.profileCode === "MANAGER").length);
    document.getElementById("metricManagers").textContent = String(items.filter(item => ["MANAGER", "SV"].includes(item.profileCode)).length);
}

function applyFilters() {
    const term = document.getElementById("searchInput").value.trim().toLowerCase();

    const filtered = rows.filter(item => {
        if (!term) return true;

        const haystack = [
            item.login,
            item.name,
            item.codigoFJ,
            item.profileLabel,
            item.referenceCode
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
        <table class="users-table">
            <thead>
                <tr>
                    <th>${escapeHtml(t("thLogin"))}</th>
                    <th>${escapeHtml(t("thName"))}</th>
                    <th>${escapeHtml(t("thCodigoFJ"))}</th>
                    <th>${escapeHtml(t("thLevel"))}</th>
                    <th>${escapeHtml(t("thCreatedAt"))}</th>
                    <th>${escapeHtml(t("thActions"))}</th>
                </tr>
            </thead>
            <tbody>
    `;

    items.forEach(item => {
        const selectedClass = item.login === editingLogin ? "is-selected" : "";
        const displayName = item.name || "-";
        const displayCodigoFJ = item.codigoFJ || "-";
        const accessClass = `access-${String(item.profileCode || "").toLowerCase()}`;

        html += `
            <tr class="${selectedClass}">
                <td>${escapeHtml(item.login)}</td>
                <td>${escapeHtml(displayName)}</td>
                <td>${escapeHtml(displayCodigoFJ)}</td>
                <td><span class="access-pill ${accessClass}">${escapeHtml(item.profileLabel)} · ${escapeHtml(item.referenceCode)}</span></td>
                <td>${escapeHtml(item.createdAt || "-")}</td>
                <td class="actions-cell">
                    <button class="row-btn row-btn-edit" type="button" data-edit="${escapeHtmlAttr(item.login)}">${escapeHtml(t("edit"))}</button>
                    <button class="row-btn row-btn-password" type="button" data-password="${escapeHtmlAttr(item.login)}">${escapeHtml(t("password"))}</button>
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

    container.querySelectorAll("[data-password]").forEach(button => {
        button.addEventListener("click", () => {
            openPasswordModal(button.dataset.password);
        });
    });
}

function openCreateModal() {
    currentModalMode = "create";
    resetUserForm();
    syncUserModeState();
    document.getElementById("userModal").classList.remove("hidden");
}

function openEditModal(login) {
    const user = rows.find(item => item.login === login);
    if (!user) return;

    currentModalMode = "edit";
    editingLogin = user.login;

    document.getElementById("txtLogin").value = user.login || "";
    document.getElementById("txtLogin").readOnly = true;
    document.getElementById("txtName").value = user.name || "";
    document.getElementById("txtCodigoFJ").value = user.codigoFJ || "";
    document.getElementById("cmbProfile").value = user.profileCode || "";
    document.getElementById("cmbReferenceNode").value = String(user.referenceNodeId || "");
    setSelectedValues("cmbSectorIds", user.sectorIds || []);
    document.getElementById("chkIsActive").checked = user.isActive !== false;
    document.getElementById("txtPassword").value = "";
    document.getElementById("txtConfirmPassword").value = "";

    syncUserModeState();
    syncScopeMode();
    applyFilters();
    document.getElementById("userModal").classList.remove("hidden");
}

function syncEditingUser() {
    if (!editingLogin || currentModalMode !== "edit") return;

    const refreshed = rows.find(item => item.login === editingLogin);
    if (!refreshed) {
        closeUserModal();
        return;
    }

    if (!document.getElementById("userModal").classList.contains("hidden")) {
        openEditModal(editingLogin);
    }
}

function resetUserForm() {
    editingLogin = "";
    document.getElementById("txtLogin").value = "";
    document.getElementById("txtLogin").readOnly = false;
    document.getElementById("txtName").value = "";
    document.getElementById("txtCodigoFJ").value = "";
    document.getElementById("cmbProfile").value = "";
    document.getElementById("cmbReferenceNode").value = "";
    setSelectedValues("cmbSectorIds", []);
    document.getElementById("chkIsActive").checked = true;
    document.getElementById("txtPassword").value = "";
    document.getElementById("txtConfirmPassword").value = "";
    syncUserModeState();
    syncScopeMode();
    applyFilters();
}

function closeUserModal() {
    document.getElementById("userModal").classList.add("hidden");
    currentModalMode = "create";
    resetUserForm();
}

function syncUserModeState() {
    const isEdit = currentModalMode === "edit" && !!editingLogin;
    document.getElementById("userFormTitle").textContent = isEdit ? t("userEditTitle") : t("userCreateTitle");
    document.getElementById("userFormSubtitle").textContent = isEdit
        ? t("userEditSubtitle")(editingLogin)
        : t("userCreateSubtitle");
    document.getElementById("btnSaveUserModal").textContent = isEdit ? t("saveChanges") : t("register");
    document.getElementById("createPasswordBlock").classList.toggle("hidden", isEdit);
}

function submitUserModal() {
    if (currentModalMode === "edit") {
        send("update", buildUserPayload(false));
        return;
    }

    send("create", buildUserPayload(true));
}

function buildUserPayload(includePassword) {
    return {
        userId: currentModalMode === "edit" ? Number(rows.find(item => item.login === editingLogin)?.id || 0) : 0,
        login: document.getElementById("txtLogin").value.trim(),
        codigoFJ: document.getElementById("txtCodigoFJ").value.trim(),
        name: document.getElementById("txtName").value.trim(),
        profileCode: document.getElementById("cmbProfile").value,
        referenceNodeId: Number(document.getElementById("cmbReferenceNode").value || 0),
        sectorIds: getSelectedValues("cmbSectorIds"),
        isActive: document.getElementById("chkIsActive").checked,
        password: includePassword ? document.getElementById("txtPassword").value : "",
        confirmPassword: includePassword ? document.getElementById("txtConfirmPassword").value : ""
    };
}

function openPasswordModal(login) {
    const user = rows.find(item => item.login === login);
    if (!user) return;

    passwordTargetLogin = user.login;
    document.getElementById("txtPasswordLogin").value = user.login;
    document.getElementById("txtNewPassword").value = "";
    document.getElementById("txtConfirmNewPassword").value = "";
    document.getElementById("passwordSubtitle").textContent = t("resetPasswordSubtitle")(user.login);
    document.getElementById("passwordModal").classList.remove("hidden");
}

function closePasswordModal() {
    passwordTargetLogin = "";
    document.getElementById("txtPasswordLogin").value = "";
    document.getElementById("txtNewPassword").value = "";
    document.getElementById("txtConfirmNewPassword").value = "";
    document.getElementById("passwordSubtitle").textContent = t("resetPasswordDefaultSubtitle");
    document.getElementById("passwordModal").classList.add("hidden");
}

function submitPasswordReset() {
    if (!passwordTargetLogin) {
        alert(t("selectPasswordUser"));
        return;
    }

    send("reset_password", {
        userId: Number(rows.find(item => item.login === passwordTargetLogin)?.id || 0),
        login: passwordTargetLogin,
        password: document.getElementById("txtNewPassword").value,
        confirmPassword: document.getElementById("txtConfirmNewPassword").value
    });
}

function syncScopeMode() {
    const profile = profiles.find(item => item.code === document.getElementById("cmbProfile").value);
    const assigned = profile?.scopeMode === "AssignedSectors";
    document.getElementById("sectorAccessGroup").classList.toggle("hidden", !assigned);
    document.getElementById("cmbSectorIds").disabled = !assigned;
    if (!assigned) setSelectedValues("cmbSectorIds", []);
}

function getSelectedValues(id) {
    return Array.from(document.getElementById(id).selectedOptions).map(option => Number(option.value));
}

function setSelectedValues(id, values) {
    const selected = new Set((values || []).map(Number));
    Array.from(document.getElementById(id).options).forEach(option => { option.selected = selected.has(Number(option.value)); });
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
