const toast = {
    root: document.getElementById("toast"),
    title: document.getElementById("toastTitle"),
    message: document.getElementById("toastMessage"),
    timer: null
};

const state = {
    locale: "pt-BR"
};

const I18N = {
    "pt-BR": {
        title: "Relatorios - TeamOps",
        headerTitle: "Relatorios",
        headerSubtitle: "Hub rapido para consulta e analise dos principais relatorios do time.",
        metaOperator: "Operador",
        metaShift: "Turno",
        metaDate: "Atualizado",
        heroBadge: "Centro de relatorios",
        heroTitle: "Mesmo atalho do menu atual, agora em HTML",
        heroSubtitle: "Os relatorios ativos abrem imediatamente. Os itens ainda nao migrados aparecem sinalizados para evitar clique sem retorno.",
        availableLabel: "Disponiveis",
        totalLabel: "Total",
        activeTitle: "Relatorios Ativos",
        activeSubtitle: "Entradas que ja possuem fluxo conectado e funcional.",
        tileHikitsuguiTitle: "Hikitsugui",
        tileHikitsuguiSubtitle: "Matriz de leitura e consulta dos registros publicados",
        tileFollowReportTitle: "Relatorio Follow",
        tileFollowReportSubtitle: "Visao analitica completa dos follow-ups",
        tileFollowChartTitle: "Grafico Follow",
        tileFollowChartSubtitle: "Acompanhamento visual por indicadores",
        tileTasksReportTitle: "Relatorio Tasks",
        tileTasksReportSubtitle: "Visao PDCA das tasks cadastradas",
        tileOperatorsSubtitle: "Presenca, producao, follow-up e ocorrencias do Haidai",
        tilePresenceTitle: "Presenca",
        tilePresenceSubtitle: "Resumo de escala, faltas, Yukyu, atrasos e Todoke",
        tileProductionManagementTitle: "Gerencial de Producao",
        tileProductionManagementSubtitle: "Performance historica por operador, setor, turno e maquina",
        tileMasterCardTitle: "MasterCard",
        tileMasterCardSubtitle: "Treinamentos, follow de 30 dias e fechamento",
        devTitle: "Em Desenvolvimento",
        devSubtitle: "Atalhos reservados para manter o mesmo mapa do menu antigo.",
        tileOperatorsTitle: "Operadores",
        tilePrTitle: "PR",
        tilePrSubtitle: "Consulta de PR gerados por periodo e categoria",
        tileClTitle: "CL",
        tileClSubtitle: "Consulta de CL gerados por periodo e categoria",
        tileSobraTitle: "Sobra de Peca",
        soon: "Em breve",
        notifyTitle: "Aviso"
    },
    "ja-JP": {
        title: "Reports - TeamOps",
        headerTitle: "Reports",
        headerSubtitle: "\u4e3b\u8981\u30ec\u30dd\u30fc\u30c8\u3092\u3059\u3070\u3084\u304f\u53c2\u7167\u3067\u304d\u308b TeamOps \u30ec\u30dd\u30fc\u30c8\u30cf\u30d6\u3067\u3059\u3002",
        metaOperator: "\u30aa\u30da\u30ec\u30fc\u30bf\u30fc",
        metaShift: "\u30b7\u30d5\u30c8",
        metaDate: "\u66f4\u65b0\u65e5\u6642",
        heroBadge: "\u30ec\u30dd\u30fc\u30c8\u30bb\u30f3\u30bf\u30fc",
        heroTitle: "\u65e2\u5b58\u30e1\u30cb\u30e5\u30fc\u3068\u540c\u3058\u5c0e\u7dda\u3092 HTML \u3067\u518d\u69cb\u6210",
        heroSubtitle: "\u5229\u7528\u53ef\u80fd\u306a\u30ec\u30dd\u30fc\u30c8\u306f\u3059\u3050\u306b\u958b\u3051\u307e\u3059\u3002\u672a\u79fb\u884c\u306e\u9805\u76ee\u306f\u8aa4\u30af\u30ea\u30c3\u30af\u3092\u9632\u3050\u305f\u3081\u660e\u793a\u3057\u3066\u3044\u307e\u3059\u3002",
        availableLabel: "\u5229\u7528\u53ef\u80fd",
        totalLabel: "\u5408\u8a08",
        activeTitle: "\u6709\u52b9\u306a\u30ec\u30dd\u30fc\u30c8",
        activeSubtitle: "\u3059\u3067\u306b\u63a5\u7d9a\u6e08\u307f\u3067\u5229\u7528\u3067\u304d\u308b\u30ec\u30dd\u30fc\u30c8\u3067\u3059\u3002",
        tileHikitsuguiTitle: "Hikitsugui",
        tileHikitsuguiSubtitle: "\u516c\u958b\u6e08\u307f\u30ec\u30b3\u30fc\u30c9\u306e\u95b2\u89a7\u30de\u30c8\u30ea\u30af\u30b9\u3068\u78ba\u8a8d",
        tileFollowReportTitle: "Follow Report",
        tileFollowReportSubtitle: "\u30d5\u30a9\u30ed\u30fc\u30a2\u30c3\u30d7\u306e\u5206\u6790\u30ec\u30dd\u30fc\u30c8",
        tileFollowChartTitle: "Follow Chart",
        tileFollowChartSubtitle: "\u6307\u6a19\u3054\u3068\u306e\u30d3\u30b8\u30e5\u30a2\u30eb\u63a8\u79fb",
        tileTasksReportTitle: "Tasks Report",
        tileTasksReportSubtitle: "Task \u3092 PDCA \u8996\u70b9\u3067\u78ba\u8a8d",
        tileOperatorsSubtitle: "\u51fa\u52e4\u3001\u751f\u7523\u3001follow-up\u3001Haidai \u5c65\u6b74\u3092\u307e\u3068\u3081\u3066\u78ba\u8a8d",
        tilePresenceTitle: "Attendance",
        tilePresenceSubtitle: "\u30b7\u30d5\u30c8\u3001\u6b20\u52e4\u3001Yukyu\u3001\u9045\u523b\u3001Todoke \u3092\u96c6\u8a08",
        tileProductionManagementTitle: "Production Management",
        tileProductionManagementSubtitle: "\u30aa\u30da\u30ec\u30fc\u30bf\u30fc\u3001\u90e8\u9580\u3001\u30b7\u30d5\u30c8\u3001\u8a2d\u5099\u5225\u306e\u5c65\u6b74\u5206\u6790",
        tileMasterCardTitle: "MasterCard",
        tileMasterCardSubtitle: "\u6559\u80b2\u72b6\u6cc1\u3001Follow 30 \u65e5\u3001\u5b8c\u4e86\u72b6\u6cc1",
        devTitle: "\u958b\u767a\u4e2d",
        devSubtitle: "\u65e7\u30e1\u30cb\u30e5\u30fc\u69cb\u6210\u3092\u7dad\u6301\u3059\u308b\u305f\u3081\u306e\u4e88\u7d04\u67a0\u3067\u3059\u3002",
        tileOperatorsTitle: "\u30aa\u30da\u30ec\u30fc\u30bf\u30fc",
        tilePrTitle: "PR",
        tilePrSubtitle: "PR \u6587\u66f8\u3092\u671f\u9593\u3068\u30ab\u30c6\u30b4\u30ea\u3067\u691c\u7d22",
        tileClTitle: "CL",
        tileClSubtitle: "CL \u6587\u66f8\u3092\u671f\u9593\u3068\u30ab\u30c6\u30b4\u30ea\u3067\u691c\u7d22",
        tileSobraTitle: "Sobra de Peca",
        soon: "\u8fd1\u65e5\u5bfe\u5fdc",
        notifyTitle: "\u901a\u77e5"
    }
};

document.addEventListener("DOMContentLoaded", () => {
    bindActions();
});

function bindActions() {
    document.querySelectorAll("[data-action]").forEach(button => {
        button.addEventListener("click", () => {
            window.chrome.webview.postMessage({
                action: button.dataset.action
            });
        });
    });
}

window.chrome?.webview?.addEventListener("message", event => {
    const payload = event.data;
    if (!payload?.type) {
        return;
    }

    if (payload.type === "init") {
        hydrate(payload.data || {});
        return;
    }

    if (payload.type === "notify") {
        showToast(payload.data?.title || t("notifyTitle"), payload.data?.message || "");
    }
});

function hydrate(data) {
    state.locale = data.locale === "ja-JP" ? "ja-JP" : "pt-BR";
    applyLocale();

    setText("lblOperator", localizedValue(data.operatorNamePt, data.operatorNameJp));
    setText("lblShift", localizedValue(data.shiftNamePt, data.shiftNameJp));
    setText("lblDate", formatDate(data.dateIso));
    setText("lblAvailable", data.availableCount);
    setText("lblTotal", data.totalCount);

    const allowedActions = new Set(data.allowedActions || []);
    document.querySelectorAll("[data-action]").forEach(button => {
        button.hidden = !allowedActions.has(button.dataset.action);
    });
}

function applyLocale() {
    document.documentElement.lang = state.locale;
    document.title = t("title");

    setText("txtHeaderTitle", t("headerTitle"));
    setText("txtHeaderSubtitle", t("headerSubtitle"));
    setText("txtMetaOperator", t("metaOperator"));
    setText("txtMetaShift", t("metaShift"));
    setText("txtMetaDate", t("metaDate"));
    setText("txtHeroBadge", t("heroBadge"));
    setText("txtHeroTitle", t("heroTitle"));
    setText("txtHeroSubtitle", t("heroSubtitle"));
    setText("txtAvailableLabel", t("availableLabel"));
    setText("txtTotalLabel", t("totalLabel"));
    setText("txtActiveTitle", t("activeTitle"));
    setText("txtActiveSubtitle", t("activeSubtitle"));
    setText("txtTileHikitsuguiTitle", t("tileHikitsuguiTitle"));
    setText("txtTileHikitsuguiSubtitle", t("tileHikitsuguiSubtitle"));
    setText("txtTileFollowReportTitle", t("tileFollowReportTitle"));
    setText("txtTileFollowReportSubtitle", t("tileFollowReportSubtitle"));
    setText("txtTileFollowChartTitle", t("tileFollowChartTitle"));
    setText("txtTileFollowChartSubtitle", t("tileFollowChartSubtitle"));
    setText("txtTileTasksReportTitle", t("tileTasksReportTitle"));
    setText("txtTileTasksReportSubtitle", t("tileTasksReportSubtitle"));
    setText("txtTileOperatorsSubtitle", t("tileOperatorsSubtitle"));
    setText("txtTilePresenceTitle", t("tilePresenceTitle"));
    setText("txtTilePresenceSubtitle", t("tilePresenceSubtitle"));
    setText("txtTileProductionManagementTitle", t("tileProductionManagementTitle"));
    setText("txtTileProductionManagementSubtitle", t("tileProductionManagementSubtitle"));
    setText("txtTileMasterCardTitle", t("tileMasterCardTitle"));
    setText("txtTileMasterCardSubtitle", t("tileMasterCardSubtitle"));
    setText("txtDevTitle", t("devTitle"));
    setText("txtDevSubtitle", t("devSubtitle"));
    setText("txtTileOperatorsTitle", t("tileOperatorsTitle"));
    setText("txtTilePrTitle", t("tilePrTitle"));
    setText("txtTilePrSubtitle", t("tilePrSubtitle"));
    setText("txtTileClTitle", t("tileClTitle"));
    setText("txtTileClSubtitle", t("tileClSubtitle"));
    setText("txtTileSobraTitle", t("tileSobraTitle"));
    setText("txtTileSoon1", t("soon"));
    setText("txtTileSoon3", t("soon"));
}

function localizedValue(pt, jp) {
    return state.locale === "ja-JP"
        ? (jp || pt || "-")
        : (pt || jp || "-");
}

function formatDate(dateIso) {
    if (!dateIso) {
        return "-";
    }

    const date = new Date(dateIso);
    if (Number.isNaN(date.getTime())) {
        return dateIso;
    }

    return state.locale === "ja-JP"
        ? new Intl.DateTimeFormat("ja-JP", { dateStyle: "medium", timeStyle: "short" }).format(date)
        : new Intl.DateTimeFormat("pt-BR", { dateStyle: "short", timeStyle: "short" }).format(date);
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

function showToast(title, message) {
    toast.title.textContent = title;
    toast.message.textContent = message;
    toast.root.classList.remove("hidden");

    if (toast.timer) {
        clearTimeout(toast.timer);
    }

    toast.timer = setTimeout(() => {
        toast.root.classList.add("hidden");
    }, 3200);
}
