const state = {
    locale: "pt-BR",
    operatorNamePt: "",
    operatorNameJp: "",
    members: [],
    rows: []
};

const I18N = {
    "pt-BR": {
        title: "Leitura de Hikitsugui",
        headerTitle: "Leitura de Hikitsugui",
        headerSubtitle: "Matriz de leitura por publico, turno e setor, no padrao TeamOps HTML.",
        metaOperator: "Operador",
        startDate: "Data Inicial",
        endDate: "Data Final",
        shift: "Turno",
        sector: "Setor",
        audienceOperators: "Operadores",
        audienceLeaders: "Lideres",
        audienceMasv: "MA/SV",
        filterButton: "Atualizar Matriz",
        matrixTitle: "Matriz de leitura",
        matrixSubtitle: "Cada coluna representa um operador ou lider. Clique no card do hikitsugui para abrir o preview.",
        summaryMembers: "Colunas",
        summaryRows: "Registros",
        allOption: "Todos",
        previewButton: "Ver detalhes",
        readYes: "\u25cb",
        readNo: "\u00d7",
        infoCreator: "Criador",
        previewTitle: "Visualizar Hikitsugui",
        previewSubtitle: "Descricao completa e anexos do registro selecionado.",
        previewDate: "Data",
        previewCategory: "Categoria",
        previewSector: "Setor",
        previewCreator: "Criador",
        descriptionTitle: "Descricao",
        attachmentsTitle: "Anexos",
        close: "Fechar",
        attachmentOpen: "Abrir",
        noAttachments: "Nenhum anexo encontrado.",
        noRows: "Nenhum hikitsugui encontrado para o filtro atual.",
        errorTitle: "Erro"
    },
    "ja-JP": {
        title: "Hikitsugui Read Matrix",
        headerTitle: "Hikitsugui Read Matrix",
        headerSubtitle: "\u516c\u958b\u5148\u3001\u30b7\u30d5\u30c8\u3001\u30bb\u30af\u30bf\u30fc\u5225\u306e\u95b2\u89a7\u30de\u30c8\u30ea\u30af\u30b9\u3092 TeamOps HTML \u6a19\u6e96\u3067\u8868\u793a\u3057\u307e\u3059\u3002",
        metaOperator: "\u30aa\u30da\u30ec\u30fc\u30bf\u30fc",
        startDate: "\u958b\u59cb\u65e5",
        endDate: "\u7d42\u4e86\u65e5",
        shift: "\u30b7\u30d5\u30c8",
        sector: "\u30bb\u30af\u30bf\u30fc",
        audienceOperators: "\u30aa\u30da\u30ec\u30fc\u30bf\u30fc",
        audienceLeaders: "\u30ea\u30fc\u30c0\u30fc",
        audienceMasv: "MA/SV",
        filterButton: "\u30de\u30c8\u30ea\u30af\u30b9\u66f4\u65b0",
        matrixTitle: "\u95b2\u89a7\u30de\u30c8\u30ea\u30af\u30b9",
        matrixSubtitle: "\u5404\u5217\u306f\u30aa\u30da\u30ec\u30fc\u30bf\u30fc\u307e\u305f\u306f\u30ea\u30fc\u30c0\u30fc\u3067\u3059\u3002Hikitsugui \u30ab\u30fc\u30c9\u3092\u30af\u30ea\u30c3\u30af\u3059\u308b\u3068\u8a73\u7d30\u3092\u8868\u793a\u3057\u307e\u3059\u3002",
        summaryMembers: "\u5217\u6570",
        summaryRows: "\u4ef6\u6570",
        allOption: "\u3059\u3079\u3066",
        previewButton: "\u8a73\u7d30\u8868\u793a",
        readYes: "\u25cb",
        readNo: "\u00d7",
        infoCreator: "\u4f5c\u6210\u8005",
        previewTitle: "Hikitsugui \u3092\u8868\u793a",
        previewSubtitle: "\u9078\u629e\u3057\u305f\u767b\u9332\u306e\u5168\u6587\u3068\u6dfb\u4ed8\u3092\u78ba\u8a8d\u3067\u304d\u307e\u3059\u3002",
        previewDate: "\u65e5\u4ed8",
        previewCategory: "\u30ab\u30c6\u30b4\u30ea",
        previewSector: "\u30bb\u30af\u30bf\u30fc",
        previewCreator: "\u4f5c\u6210\u8005",
        descriptionTitle: "\u5185\u5bb9",
        attachmentsTitle: "\u6dfb\u4ed8",
        close: "\u9589\u3058\u308b",
        attachmentOpen: "\u958b\u304f",
        noAttachments: "\u6dfb\u4ed8\u306f\u3042\u308a\u307e\u305b\u3093\u3002",
        noRows: "\u73fe\u5728\u306e\u6761\u4ef6\u3067\u306f Hikitsugui \u304c\u898b\u3064\u304b\u308a\u307e\u305b\u3093\u3002",
        errorTitle: "\u30a8\u30e9\u30fc"
    }
};

document.addEventListener("DOMContentLoaded", () => {
    bindEvents();
    send("load");
});

function bindEvents() {
    document.getElementById("btnFilter").addEventListener("click", applyFilters);
    document.getElementById("btnCloseModal").addEventListener("click", closePreview);
    document.getElementById("btnCloseModalX").addEventListener("click", closePreview);
    document.getElementById("previewBackdrop").addEventListener("click", closePreview);
}

function send(action, extra = {}) {
    window.chrome.webview.postMessage({
        action,
        ...extra
    });
}

window.chrome?.webview?.addEventListener("message", event => {
    const payload = event.data;
    if (!payload?.type) {
        return;
    }

    switch (payload.type) {
        case "init":
            hydrateInit(payload);
            break;

        case "matrix":
            hydrateMatrix(payload.data || {});
            break;

        case "preview":
            openPreview(payload.data || {});
            break;

        case "error":
            alert(`${t("errorTitle")}: ${payload.message || ""}`);
            break;
    }
});

function hydrateInit(payload) {
    state.locale = payload.locale === "ja-JP" ? "ja-JP" : "pt-BR";
    state.operatorNamePt = payload.operatorNamePt || "";
    state.operatorNameJp = payload.operatorNameJp || payload.operatorNamePt || "";

    applyLocale();
    fillSelect("shiftId", payload.shifts || [], "Id", currentNameField(), true);
    fillSelect("sectorId", payload.sectors || [], "Id", currentNameField(), true);

    document.getElementById("startDate").value = payload.startDate || "";
    document.getElementById("endDate").value = payload.endDate || "";
}

function hydrateMatrix(data) {
    state.members = data.members || [];
    state.rows = data.rows || [];

    document.getElementById("lblMemberCount").textContent = String(state.members.length);
    document.getElementById("lblRowCount").textContent = String(state.rows.length);

    renderMatrix();
}

function applyLocale() {
    document.documentElement.lang = state.locale;
    document.title = t("title");

    setText("txtHeaderTitle", t("headerTitle"));
    setText("txtHeaderSubtitle", t("headerSubtitle"));
    setText("txtMetaOperator", t("metaOperator"));
    setText("txtStartDate", t("startDate"));
    setText("txtEndDate", t("endDate"));
    setText("txtShift", t("shift"));
    setText("txtSector", t("sector"));
    setText("txtAudienceOperators", t("audienceOperators"));
    setText("txtAudienceLeaders", t("audienceLeaders"));
    setText("txtAudienceMasv", t("audienceMasv"));
    setText("btnFilter", t("filterButton"));
    setText("txtMatrixTitle", t("matrixTitle"));
    setText("txtMatrixSubtitle", t("matrixSubtitle"));
    setText("txtSummaryMembers", t("summaryMembers"));
    setText("txtSummaryRows", t("summaryRows"));
    setText("txtPreviewTitle", t("previewTitle"));
    setText("txtPreviewSubtitle", t("previewSubtitle"));
    setText("txtPreviewDate", t("previewDate"));
    setText("txtPreviewCategory", t("previewCategory"));
    setText("txtPreviewSector", t("previewSector"));
    setText("txtPreviewCreator", t("previewCreator"));
    setText("txtDescriptionTitle", t("descriptionTitle"));
    setText("txtAttachmentsTitle", t("attachmentsTitle"));
    setText("btnCloseModal", t("close"));

    const operatorName = state.locale === "ja-JP"
        ? (state.operatorNameJp || state.operatorNamePt || "-")
        : (state.operatorNamePt || state.operatorNameJp || "-");

    setText("lblOperator", operatorName);
}

function currentNameField() {
    return state.locale === "ja-JP" ? "NameJp" : "NamePt";
}

function fillSelect(id, items, valueField, textField, withAll) {
    const select = document.getElementById(id);
    const options = [];

    if (withAll) {
        options.push(`<option value="0">${escapeHtml(t("allOption"))}</option>`);
    }

    items.forEach(item => {
        options.push(`<option value="${escapeHtmlAttr(item[valueField])}">${escapeHtml(item[textField])}</option>`);
    });

    select.innerHTML = options.join("");
}

function renderMatrix() {
    const container = document.getElementById("matrixContainer");

    if (!state.rows.length) {
        container.innerHTML = `<div class="empty-state">${escapeHtml(t("noRows"))}</div>`;
        return;
    }

    const headerColumns = state.members.map(member => `
        <th class="member-col">
            <span class="member-name">${escapeHtml(localizedMemberName(member))}</span>
        </th>
    `).join("");

    const bodyRows = state.rows.map(row => {
        const cells = state.members.map(member => {
            const read = (row.readers || []).includes(member.codigoFJ);
            return `
                <td class="read-cell ${read ? "read-yes" : "read-no"}">
                    ${read ? t("readYes") : t("readNo")}
                </td>
            `;
        }).join("");

        const category = localizedRowValue(row, "categoryPt", "categoryJp");
        const creator = localizedRowValue(row, "creatorNamePt", "creatorNameJp");
        const sector = localizedRowValue(row, "sectorPt", "sectorJp");
        const plainText = htmlToPlainText(row.descriptionHtml || "");

        return `
            <tr>
                <td class="hik-col">
                    <div class="hik-card">
                        <div class="hik-meta">
                            <span>${escapeHtml(row.dateLabel || row.date || "-")}</span>
                            <span>${escapeHtml(category || "-")}</span>
                            <span>${escapeHtml(sector || "-")}</span>
                        </div>
                        <div class="hik-text">
                            <strong>${escapeHtml(truncate(plainText, 160))}</strong>
                            <small>${escapeHtml(t("infoCreator"))}: ${escapeHtml(creator || "-")}</small>
                        </div>
                        <button class="preview-btn" type="button" onclick="previewRow(${row.id})">${escapeHtml(t("previewButton"))}</button>
                    </div>
                </td>
                ${cells}
            </tr>
        `;
    }).join("");

    container.innerHTML = `
        <table class="matrix-table">
            <thead>
                <tr>
                    <th class="hik-col">${escapeHtml(t("matrixTitle"))}</th>
                    ${headerColumns}
                </tr>
            </thead>
            <tbody>
                ${bodyRows}
            </tbody>
        </table>
    `;
}

function previewRow(id) {
    send("preview", { id });
}

function openPreview(data) {
    state.previewId = Number(data.id || 0);
    setText("previewDate", data.date || "-");
    setText("previewCategory", localizedValue(data.categoryPt, data.categoryJp));
    setText("previewSector", localizedValue(data.sectorPt, data.sectorJp));
    setText("previewCreator", localizedValue(data.creatorNamePt, data.creatorNameJp));

    document.getElementById("previewDescription").innerHTML = data.descriptionHtml || "";
    renderAttachments(data.attachments || []);
    document.getElementById("previewModal").classList.remove("hidden");
}

function renderAttachments(items) {
    const list = document.getElementById("attachmentList");

    if (!items.length) {
        list.innerHTML = `<div class="empty-state">${escapeHtml(t("noAttachments"))}</div>`;
        return;
    }

    list.innerHTML = items.map(item => `
        <div class="attachment-item">
            <span class="attachment-name">${escapeHtml(item.FileName || "-")}</span>
            <button type="button" class="attachment-open" onclick="openAttachment(${JSON.stringify(item.FilePath || "")})">
                ${escapeHtml(t("attachmentOpen"))}
            </button>
        </div>
    `).join("");
}

function openAttachment(path) {
    send("open_attachment", { id: Number(state.previewId || 0), path });
}

function closePreview() {
    state.previewId = 0;
    document.getElementById("previewModal").classList.add("hidden");
}

function applyFilters() {
    send("filter", {
        dtInicial: document.getElementById("startDate").value,
        dtFinal: document.getElementById("endDate").value,
        publico: document.querySelector("input[name='audience']:checked")?.value || "operators",
        shiftId: Number(document.getElementById("shiftId").value || 0),
        sectorId: Number(document.getElementById("sectorId").value || 0)
    });
}

function localizedMemberName(member) {
    return state.locale === "ja-JP"
        ? (member.nameJp || member.namePt || "-")
        : (member.namePt || member.nameJp || "-");
}

function localizedRowValue(row, ptField, jpField) {
    return state.locale === "ja-JP"
        ? (row[jpField] || row[ptField] || "")
        : (row[ptField] || row[jpField] || "");
}

function localizedValue(pt, jp) {
    return state.locale === "ja-JP"
        ? (jp || pt || "-")
        : (pt || jp || "-");
}

function htmlToPlainText(html) {
    const div = document.createElement("div");
    div.innerHTML = html || "";
    return (div.textContent || div.innerText || "").trim();
}

function truncate(value, size) {
    const text = String(value || "");
    return text.length > size ? `${text.slice(0, size - 1)}...` : text;
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
