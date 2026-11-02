const ACCESS_LABELS = {
    "pt-BR": {
        1: "Basic",
        2: "KL",
        3: "GL",
        4: "Manager",
        5: "Admin"
    },
    "ja-JP": {
        1: "Basic",
        2: "KL",
        3: "GL",
        4: "Manager",
        5: "Admin"
    }
};

const PERMISSION_RULES = {
    btnRelatorios: 3,
    btnPresenca: 3,
    btnHaidai: 3,
    btnFollowUp: 2,
    btnTasks: 3,
    btnMasterCard: 2,
    btnProduction: 3,
    btnHikitsugui: 2,
    btnHikitsuguiLeaderRead: 2,
    btnPR: 2,
    btnCL: 2,
    btnSobraDePeca: 2,
    btnAdmin: 5,
    btnAccessControl: 5
};

const I18N = {
    "pt-BR": {
        htmlLang: "pt-BR",
        pageTitle: "TeamOps - Dashboard",
        headerTitle: "TeamOps Dashboard",
        headerSubtitle: "Central de operacoes e acompanhamento da equipe",
        metaUser: "Usuario",
        metaOperator: "Operador",
        metaShift: "Turno",
        metaSector: "Setor",
        allSectors: "Todos os setores autorizados",
        heroTitle: "Atalhos principais do time",
        heroSubtitle: "",
        statDate: "Data e Hora",
        statProfile: "Perfil Atual",
        statTasks: "Tasks em Aberto",
        statMasterInProgress: "MasterCard em Andamento",
        statMasterFollow: "MasterCard em Follow",
        pendingBadge: "Pendencias do turno",
        pendingTitle: "Antes de iniciar",
        pendingSubtitle: "Existem pendencias abertas no sistema para acompanhamento.",
        pendingTasksLabel: "Tasks em aberto",
        pendingMasterLabel: "MasterCard pendente",
        pendingTasksTitle: "Tasks",
        pendingMasterTitle: "MasterCard",
        pendingOpen: "Abrir",
        pendingAck: "Estou ciente",
        pendingEmpty: "Nenhum item pendente.",
        pendingDue: "Prazo",
        pendingFollow: "Follow",
        statusPending: "Pendente",
        statusInProgress: "Em andamento",
        statusFollow: "Follow",
        groupOpsTitle: "Operacao",
        groupOpsSubtitle: "Cadastros, acompanhamento e consulta de registros.",
        groupDocsTitle: "Hikitsugui e Documentos",
        groupDocsSubtitle: "Fluxos de comunicacao e documentos de processo.",
        groupPresenceTitle: "Presenca",
        groupPresenceSubtitle: "Acesso rapido aos paineis por setor.",
        groupAdminTitle: "Administracao",
        groupAdminSubtitle: "Configuracoes e controle de acesso.",
        tileOperatorsTitle: "Operadores",
        tileOperatorsSubtitle: "Cadastro e consulta",
        tileFollowTitle: "Acompanhamento",
        tileFollowSubtitle: "Registro e orientacao",
        tileTasksTitle: "Tasks",
        tileTasksSubtitle: "Planejamento do turno",
        tileProductionTitle: "Producao",
        tileProductionSubtitle: "Monitor de maquinas",
        tileMasterCardTitle: "MasterCard",
        tileMasterCardSubtitle: "Treinamento, follow e fechamento",
        tileScrapTitle: "Sobra de Peca",
        tileScrapSubtitle: "Lancamento de perdas",
        tileReportsTitle: "Relatorios",
        tileReportsSubtitle: "Consultas e impressao",
        tileHikiCreateTitle: "Hikitsugui",
        tileHikiCreateSubtitle: "Cadastro rapido",
        tileHikiReadTitle: "Leitura Hikitsugui",
        tileHikiReadSubtitle: "Consulta e resposta",
        tilePrTitle: "PR",
        tilePrSubtitle: "Documento de processo",
        tileClTitle: "CL",
        tileClSubtitle: "Controle de linha",
        tileTodokeTitle: "Todoke",
        tileTodokeSubtitle: "Solicitacoes e folhas",
        tilePresenceTitle: "Presenca",
        tilePresenceSubtitle: "Painel dos setores",
        tileHaidaiTitle: "Haidai",
        tileHaidaiSubtitle: "Escala diaria por grupo",
        tileAdminTitle: "Admin",
        tileAdminSubtitle: "Configuracoes do sistema",
        tileAccessTitle: "Acesso",
        tileAccessSubtitle: "Permissoes e usuarios",
        profilePrefix: "Perfil",
        toggleAria: "Alternar idioma para japones"
    },
    "ja-JP": {
        htmlLang: "ja-JP",
        pageTitle: "\u30c1\u30fc\u30e0\u30aa\u30d7\u30b9 - \u30c0\u30c3\u30b7\u30e5\u30dc\u30fc\u30c9",
        headerTitle: "\u30c1\u30fc\u30e0\u904b\u7528\u30c0\u30c3\u30b7\u30e5\u30dc\u30fc\u30c9",
        headerSubtitle: "\u30c1\u30fc\u30e0\u306e\u904b\u7528\u3068\u30d5\u30a9\u30ed\u30fc\u3092\u307e\u3068\u3081\u3066\u78ba\u8a8d\u3067\u304d\u307e\u3059\u3002",
        metaUser: "\u30e6\u30fc\u30b6\u30fc",
        metaOperator: "\u4f5c\u696d\u8005",
        metaShift: "\u30b7\u30d5\u30c8",
        metaSector: "\u30bb\u30af\u30bf\u30fc",
        allSectors: "\u8a31\u53ef\u3055\u308c\u305f\u3059\u3079\u3066\u306e\u30bb\u30af\u30bf\u30fc",
        heroTitle: "\u4e3b\u8981\u30b7\u30e7\u30fc\u30c8\u30ab\u30c3\u30c8",
        heroSubtitle: "",
        statDate: "\u65e5\u6642",
        statProfile: "\u73fe\u5728\u306e\u6a29\u9650",
        statTasks: "\u672a\u5b8c\u4e86\u30bf\u30b9\u30af",
        statMasterInProgress: "MasterCard \u9032\u884c\u4e2d",
        statMasterFollow: "MasterCard Follow",
        pendingBadge: "\u30b7\u30d5\u30c8\u306e\u672a\u5b8c\u4e86",
        pendingTitle: "\u958b\u59cb\u524d\u306e\u78ba\u8a8d",
        pendingSubtitle: "\u30b7\u30b9\u30c6\u30e0\u306b\u78ba\u8a8d\u304c\u5fc5\u8981\u306a\u672a\u5b8c\u4e86\u9805\u76ee\u304c\u3042\u308a\u307e\u3059\u3002",
        pendingTasksLabel: "\u672a\u5b8c\u4e86\u30bf\u30b9\u30af",
        pendingMasterLabel: "MasterCard \u672a\u5b8c\u4e86",
        pendingTasksTitle: "\u30bf\u30b9\u30af",
        pendingMasterTitle: "MasterCard",
        pendingOpen: "\u958b\u304f",
        pendingAck: "\u78ba\u8a8d\u3057\u307e\u3057\u305f",
        pendingEmpty: "\u672a\u5b8c\u4e86\u9805\u76ee\u306f\u3042\u308a\u307e\u305b\u3093\u3002",
        pendingDue: "\u671f\u9650",
        pendingFollow: "Follow",
        statusPending: "\u672a\u7740\u624b",
        statusInProgress: "\u9032\u884c\u4e2d",
        statusFollow: "Follow",
        groupOpsTitle: "\u904b\u7528",
        groupOpsSubtitle: "\u767b\u9332\u3001\u30d5\u30a9\u30ed\u30fc\u3001\u8a18\u9332\u78ba\u8a8d\u3002",
        groupDocsTitle: "\u5f15\u7d99\u304e\u3068\u6587\u66f8",
        groupDocsSubtitle: "\u9023\u7d61\u30d5\u30ed\u30fc\u3068\u5de5\u7a0b\u95a2\u4fc2\u306e\u66f8\u985e\u3002",
        groupPresenceTitle: "\u51fa\u52e4",
        groupPresenceSubtitle: "\u30bb\u30af\u30bf\u30fc\u5225\u30d1\u30cd\u30eb\u3078\u7d20\u65e9\u304f\u79fb\u52d5\u3002",
        groupAdminTitle: "\u7ba1\u7406",
        groupAdminSubtitle: "\u8a2d\u5b9a\u3068\u30a2\u30af\u30bb\u30b9\u7ba1\u7406\u3002",
        tileOperatorsTitle: "\u4f5c\u696d\u8005",
        tileOperatorsSubtitle: "\u767b\u9332\u3068\u691c\u7d22",
        tileFollowTitle: "\u30d5\u30a9\u30ed\u30fc",
        tileFollowSubtitle: "\u8a18\u9332\u3068\u6307\u5c0e",
        tileTasksTitle: "\u30bf\u30b9\u30af",
        tileTasksSubtitle: "\u30b7\u30d5\u30c8\u8a08\u753b",
        tileProductionTitle: "\u751f\u7523",
        tileProductionSubtitle: "\u8a2d\u5099\u30e2\u30cb\u30bf\u30fc",
        tileMasterCardTitle: "MasterCard",
        tileMasterCardSubtitle: "\u6559\u80b2\u30d5\u30ed\u30fc\u3068 Follow \u7ba1\u7406",
        tileScrapTitle: "\u88fd\u54c1\u6b8b\u308a",
        tileScrapSubtitle: "\u30ed\u30b9\u5165\u529b",
        tileReportsTitle: "\u30ec\u30dd\u30fc\u30c8",
        tileReportsSubtitle: "\u691c\u7d22\u3068\u5370\u5237",
        tileHikiCreateTitle: "\u5f15\u7d99\u304e",
        tileHikiCreateSubtitle: "\u65b0\u898f\u767b\u9332",
        tileHikiReadTitle: "\u5f15\u7d99\u304e\u95b2\u89a7",
        tileHikiReadSubtitle: "\u78ba\u8a8d\u3068\u8fd4\u4fe1",
        tilePrTitle: "PR",
        tilePrSubtitle: "\u5de5\u7a0b\u66f8\u985e",
        tileClTitle: "CL",
        tileClSubtitle: "\u30e9\u30a4\u30f3\u7ba1\u7406",
        tileTodokeTitle: "\u5c4a\u51fa",
        tileTodokeSubtitle: "\u7533\u8acb\u3068\u66f8\u985e",
        tilePresenceTitle: "\u51fa\u52e4",
        tilePresenceSubtitle: "\u30bb\u30af\u30bf\u30fc\u30d1\u30cd\u30eb",
        tileHaidaiTitle: "Haidai",
        tileHaidaiSubtitle: "\u65e5\u6b21\u5ec3\u53f0\u30b0\u30eb\u30fc\u30d7\u914d\u7f6e",
        tileAdminTitle: "\u7ba1\u7406",
        tileAdminSubtitle: "\u30b7\u30b9\u30c6\u30e0\u8a2d\u5b9a",
        tileAccessTitle: "\u30a2\u30af\u30bb\u30b9",
        tileAccessSubtitle: "\u6a29\u9650\u3068\u30e6\u30fc\u30b6\u30fc",
        profilePrefix: "\u6a29\u9650",
        toggleAria: "\u8a00\u8a9e\u3092\u30dd\u30eb\u30c8\u30ac\u30eb\u8a9e\u306b\u5207\u308a\u66ff\u3048"
    }
};

const state = {
    locale: "pt-BR",
    payload: null,
    pendingModalShown: false
};

document.addEventListener("DOMContentLoaded", () => {
    bindActions();
});

function bindActions() {
    document.querySelectorAll("[data-action]").forEach(button => {
        button.addEventListener("click", () => {
            window.chrome.webview.postMessage(button.dataset.action);
        });
    });

    document.getElementById("btnLocaleToggle").addEventListener("click", () => {
        const nextLocale = state.locale === "ja-JP" ? "pt-BR" : "ja-JP";

        setLocale(nextLocale);

        window.chrome.webview.postMessage({
            action: "set_locale",
            locale: nextLocale
        });
    });

    document.getElementById("sectorContext").addEventListener("change", event => {
        window.chrome.webview.postMessage({
            action: "select_sector",
            sectorId: Number(event.target.value || 0)
        });
    });

    document.getElementById("btnClosePendingModal").addEventListener("click", closePendingModal);
    document.getElementById("btnAcknowledgePending").addEventListener("click", closePendingModal);
    ["btnOpenTasksFromModal", "btnOpenMasterFromModal"].forEach(id => {
        document.getElementById(id).addEventListener("click", closePendingModal);
    });
}

window.chrome?.webview?.addEventListener("message", event => {
    const payload = event.data;

    if (!payload?.type) {
        return;
    }

    if (payload.type === "init") {
        hydrateDashboard(payload);
        return;
    }

    if (payload.type === "locale_changed" && payload.locale) {
        setLocale(payload.locale);
        return;
    }

    if (payload.type === "sector_context_changed") {
        state.payload.selectedSectorId = payload.selectedSectorId;
        renderSectorContext();
    }
});

function hydrateDashboard(payload) {
    state.payload = payload;

    setLocale(payload.locale || "pt-BR");
    setText("lblUser", payload.user);
    refreshNames();
    setText("lblDate", formatDashboardDate(payload.dateIso, state.locale));
    setText("lblOpenTasks", payload.openTasksForShift ?? 0);
    setText("lblMasterInProgress", payload.masterCardsInProgress ?? 0);
    setText("lblMasterFollow", payload.masterCardsFollow ?? 0);
    renderPendingModal(payload);
    renderSectorContext();

    const level = Number(payload.accessLevel ?? 0);
    const label = ACCESS_LABELS[state.locale]?.[level] || `Nivel ${level}`;
    const profilePrefix = I18N[state.locale]?.profilePrefix || "Perfil";

    setText("lblAccessLevel", label);
    setText("accessBadge", `${profilePrefix} ${label}`);

    applyPermissions(level);
    maybeShowPendingModal(payload);
}

function renderSectorContext() {
    const select = document.getElementById("sectorContext");
    if (!select || !state.payload) return;

    const strings = I18N[state.locale];
    const sectors = state.payload.sectors || [];
    select.innerHTML = "";

    if (state.payload.canUseAggregateScope) {
        const aggregate = document.createElement("option");
        aggregate.value = "0";
        aggregate.textContent = strings.allSectors;
        select.appendChild(aggregate);
    }

    sectors.forEach(sector => {
        const option = document.createElement("option");
        option.value = String(sector.id);
        option.textContent = state.locale === "ja-JP"
            ? (sector.nameJp || sector.namePt || sector.code)
            : (sector.namePt || sector.nameJp || sector.code);
        select.appendChild(option);
    });

    select.value = String(state.payload.selectedSectorId || 0);
    select.disabled = select.options.length <= 1;
}

function refreshNames() {
    if (!state.payload) {
        return;
    }

    const operatorName = state.locale === "ja-JP"
        ? (state.payload.operatorNameJp || state.payload.operatorNamePt)
        : (state.payload.operatorNamePt || state.payload.operatorNameJp);

    const shiftName = state.locale === "ja-JP"
        ? (state.payload.shiftNameJp || state.payload.shiftNamePt)
        : (state.payload.shiftNamePt || state.payload.shiftNameJp);

    setText("lblOperator", operatorName);
    setText("lblShift", shiftName);
}

function applyPermissions(level) {
    Object.entries(PERMISSION_RULES).forEach(([id, minimumLevel]) => {
        const button = document.getElementById(id);
        if (!button) return;

        button.classList.toggle("hidden", level < minimumLevel);
    });

    document.querySelectorAll("[data-group]").forEach(group => {
        const visibleActions = Array.from(group.querySelectorAll(".action-tile"))
            .filter(button => !button.classList.contains("hidden"));

        group.classList.toggle("hidden", visibleActions.length === 0);
    });
}

function setLocale(locale) {
    state.locale = locale === "ja-JP" ? "ja-JP" : "pt-BR";

    const strings = I18N[state.locale];
    document.documentElement.lang = strings.htmlLang;
    document.title = strings.pageTitle;

    const toggle = document.getElementById("btnLocaleToggle");
    toggle.classList.toggle("locale-ja", state.locale === "ja-JP");
    toggle.classList.toggle("locale-pt", state.locale !== "ja-JP");
    toggle.setAttribute("aria-label", strings.toggleAria);

    setText("txtHeaderTitle", strings.headerTitle);
    setText("txtHeaderSubtitle", strings.headerSubtitle);
    setText("txtMetaUser", strings.metaUser);
    setText("txtMetaOperator", strings.metaOperator);
    setText("txtMetaShift", strings.metaShift);
    setText("txtMetaSector", strings.metaSector);
    setText("txtHeroTitle", strings.heroTitle);
    setText("txtHeroSubtitle", strings.heroSubtitle);
    document.getElementById("txtHeroSubtitle").classList.toggle("hidden", !strings.heroSubtitle);
    setText("txtStatDate", strings.statDate);
    setText("txtStatProfile", strings.statProfile);
    setText("txtStatTasks", strings.statTasks);
    setText("txtStatMasterInProgress", strings.statMasterInProgress);
    setText("txtStatMasterFollow", strings.statMasterFollow);
    setText("txtPendingBadge", strings.pendingBadge);
    setText("pendingModalTitle", strings.pendingTitle);
    setText("txtPendingSubtitle", strings.pendingSubtitle);
    setText("txtPendingTasksLabel", strings.pendingTasksLabel);
    setText("txtPendingMasterLabel", strings.pendingMasterLabel);
    setText("txtPendingTasksTitle", strings.pendingTasksTitle);
    setText("txtPendingMasterTitle", strings.pendingMasterTitle);
    setText("btnOpenTasksFromModal", strings.pendingOpen);
    setText("btnOpenMasterFromModal", strings.pendingOpen);
    setText("btnAcknowledgePending", strings.pendingAck);
    setText("txtGroupOpsTitle", strings.groupOpsTitle);
    setText("txtGroupOpsSubtitle", strings.groupOpsSubtitle);
    setText("txtGroupDocsTitle", strings.groupDocsTitle);
    setText("txtGroupDocsSubtitle", strings.groupDocsSubtitle);
    setText("txtGroupPresenceTitle", strings.groupPresenceTitle);
    setText("txtGroupPresenceSubtitle", strings.groupPresenceSubtitle);
    setText("txtGroupAdminTitle", strings.groupAdminTitle);
    setText("txtGroupAdminSubtitle", strings.groupAdminSubtitle);
    setText("txtTileOperatorsTitle", strings.tileOperatorsTitle);
    setText("txtTileOperatorsSubtitle", strings.tileOperatorsSubtitle);
    setText("txtTileFollowTitle", strings.tileFollowTitle);
    setText("txtTileFollowSubtitle", strings.tileFollowSubtitle);
    setText("txtTileTasksTitle", strings.tileTasksTitle);
    setText("txtTileTasksSubtitle", strings.tileTasksSubtitle);
    setText("txtTileMasterCardTitle", strings.tileMasterCardTitle);
    setText("txtTileMasterCardSubtitle", strings.tileMasterCardSubtitle);
    setText("txtTileProductionTitle", strings.tileProductionTitle);
    setText("txtTileProductionSubtitle", strings.tileProductionSubtitle);
    setText("txtTileScrapTitle", strings.tileScrapTitle);
    setText("txtTileScrapSubtitle", strings.tileScrapSubtitle);
    setText("txtTileReportsTitle", strings.tileReportsTitle);
    setText("txtTileReportsSubtitle", strings.tileReportsSubtitle);
    setText("txtTileHikiCreateTitle", strings.tileHikiCreateTitle);
    setText("txtTileHikiCreateSubtitle", strings.tileHikiCreateSubtitle);
    setText("txtTileHikiReadTitle", strings.tileHikiReadTitle);
    setText("txtTileHikiReadSubtitle", strings.tileHikiReadSubtitle);
    setText("txtTilePrTitle", strings.tilePrTitle);
    setText("txtTilePrSubtitle", strings.tilePrSubtitle);
    setText("txtTileClTitle", strings.tileClTitle);
    setText("txtTileClSubtitle", strings.tileClSubtitle);
    setText("txtTileTodokeTitle", strings.tileTodokeTitle);
    setText("txtTileTodokeSubtitle", strings.tileTodokeSubtitle);
    setText("txtTilePresenceTitle", strings.tilePresenceTitle);
    setText("txtTilePresenceSubtitle", strings.tilePresenceSubtitle);
    setText("txtTileHaidaiTitle", strings.tileHaidaiTitle);
    setText("txtTileHaidaiSubtitle", strings.tileHaidaiSubtitle);
    setText("txtTileAdminTitle", strings.tileAdminTitle);
    setText("txtTileAdminSubtitle", strings.tileAdminSubtitle);
    setText("txtTileAccessTitle", strings.tileAccessTitle);
    setText("txtTileAccessSubtitle", strings.tileAccessSubtitle);

    if (state.payload) {
        refreshNames();
    renderSectorContext();
        setText("lblDate", formatDashboardDate(state.payload.dateIso, state.locale));

        const level = Number(state.payload.accessLevel ?? 0);
        const label = ACCESS_LABELS[state.locale]?.[level] || `Nivel ${level}`;
        setText("lblAccessLevel", label);
        setText("accessBadge", `${strings.profilePrefix} ${label}`);
        renderPendingModal(state.payload);
    }
}

function renderPendingModal(payload) {
    if (!payload) {
        return;
    }

    const taskCount = Number(payload.openTasksForShift ?? 0);
    const masterCount = Number(payload.masterCardsInProgress ?? 0) + Number(payload.masterCardsFollow ?? 0);
    const pending = payload.pending || {};

    setText("modalTaskCount", taskCount);
    setText("modalMasterCount", masterCount);
    renderTaskPendingList(pending.tasks || []);
    renderMasterPendingList(pending.masterCards || []);
}

function renderTaskPendingList(items) {
    const root = document.getElementById("pendingTasksList");
    if (!items.length) {
        root.innerHTML = `<div class="pending-empty">${escapeHtml(I18N[state.locale].pendingEmpty)}</div>`;
        return;
    }

    root.innerHTML = items.map(item => `
        <article class="pending-item">
            <div>
                <strong>${escapeHtml(item.description || `Task #${item.id}`)}</strong>
                <span>${escapeHtml(localizedValue(item.assigneeNamePt, item.assigneeNameJp) || "-")}</span>
            </div>
            <small>${escapeHtml(I18N[state.locale].pendingDue)} ${escapeHtml(formatShortDate(item.dueDate))} | ${escapeHtml(statusLabel(item.status))}</small>
        </article>
    `).join("");
}

function renderMasterPendingList(items) {
    const root = document.getElementById("pendingMasterList");
    if (!items.length) {
        root.innerHTML = `<div class="pending-empty">${escapeHtml(I18N[state.locale].pendingEmpty)}</div>`;
        return;
    }

    root.innerHTML = items.map(item => `
        <article class="pending-item">
            <div>
                <strong>${escapeHtml(localizedValue(item.operatorNamePt, item.operatorNameJp) || `MasterCard #${item.id}`)}</strong>
                <span>${escapeHtml(localizedValue(item.equipmentNamePt, item.equipmentNameJp) || "-")}</span>
            </div>
            <small>${escapeHtml(statusLabel(item.status))}${item.followDate ? ` | ${escapeHtml(I18N[state.locale].pendingFollow)} ${escapeHtml(formatShortDate(item.followDate))}` : ""}</small>
        </article>
    `).join("");
}

function maybeShowPendingModal(payload) {
    if (state.pendingModalShown) {
        return;
    }

    const totalPending = Number(payload.openTasksForShift ?? 0)
        + Number(payload.masterCardsInProgress ?? 0)
        + Number(payload.masterCardsFollow ?? 0);

    if (totalPending <= 0) {
        return;
    }

    state.pendingModalShown = true;
    document.body.classList.add("modal-open");
    document.getElementById("pendingModal").classList.remove("hidden");
}

function closePendingModal() {
    document.body.classList.remove("modal-open");
    document.getElementById("pendingModal").classList.add("hidden");
}

function localizedValue(pt, jp) {
    return state.locale === "ja-JP"
        ? (jp || pt || "")
        : (pt || jp || "");
}

function statusLabel(status) {
    const strings = I18N[state.locale];
    return {
        pending: strings.statusPending,
        in_progress: strings.statusInProgress,
        follow: strings.statusFollow
    }[status] || status || "-";
}

function formatShortDate(dateIso) {
    if (!dateIso) {
        return "-";
    }

    const [year, month, day] = String(dateIso).split("-").map(Number);
    if (!year || !month || !day) {
        return dateIso;
    }

    return new Intl.DateTimeFormat(state.locale, {
        month: "2-digit",
        day: "2-digit"
    }).format(new Date(year, month - 1, day));
}

function formatDashboardDate(dateIso, locale) {
    if (!dateIso) {
        return "-";
    }

    const date = new Date(dateIso);
    if (Number.isNaN(date.getTime())) {
        return "-";
    }

    return new Intl.DateTimeFormat(locale, {
        year: "numeric",
        month: "2-digit",
        day: "2-digit",
        hour: "2-digit",
        minute: "2-digit"
    }).format(date);
}

function setText(id, value) {
    const element = document.getElementById(id);
    if (element) {
        element.textContent = value ?? "-";
    }
}

function escapeHtml(value) {
    return String(value ?? "")
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll("\"", "&quot;");
}
