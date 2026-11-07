let editExistingAttachments = [];
let editNewAttachments = [];
let currentEditId = null;
let currentLocale = "pt-BR";
let profileOptions = [];
let editAudienceConfiguration = {};

const I18N = {
    "pt-BR": {
        headerTitle: "Hikitsugui Leader Read",
        dateFromLabel: "Período Inicial",
        dateToLabel: "Período Final",
        shiftLabel: "Turno",
        operatorLabel: "Operador",
        categoryLabel: "Categoria",
        equipmentLabel: "Equipamento",
        localLabel: "Local",
        sectorLabel: "Setor",
        searchPlaceholder: "Buscar texto...",
        searchButton: "Buscar",
        colRead: "Lido",
        colDate: "Data",
        colOperator: "Operador",
        colCategory: "Categoria",
        colEquipment: "Equipamento",
        colLocal: "Local",
        colSector: "Setor",
        colDescription: "Descrição",
        colActions: "Ações",
        actionView: "Visualizar",
        actionEdit: "Editar",
        actionDelete: "Excluir",
        errorPrefix: "Erro: ",
        invalidReply: "Digite uma resposta antes de salvar.",
        readModalTitle: "Detalhes do Hikitsugui",
        labelId: "ID",
        labelDate: "Data",
        labelOperator: "Operador",
        labelCategory: "Categoria",
        labelEquipment: "Equipamento",
        labelLocal: "Local",
        labelSector: "Setor",
        labelDescription: "Descrição",
        repliesTitle: "Respostas",
        attachmentsTitle: "Anexos",
        replyLabel: "Responder",
        replyPlaceholder: "Responder...",
        sendReply: "Enviar Resposta",
        close: "Fechar",
        listButton: "• Lista",
        clearButton: "Limpar",
        noAttachments: "Nenhum anexo",
        noExistingAttachments: "Nenhum anexo existente",
        noReplies: "Nenhuma resposta registrada.",
        openAttachment: "Abrir",
        deleteAttachment: "Excluir",
        confirmDelete: "Tem certeza que deseja excluir?",
        replyBadge: count => `${count} resposta(s)`,
        editModalTitle: "Editar Hikitsugui",
        editCategoryLabel: "Categoria",
        editEquipmentLabel: "Equipamento",
        editLocalLabel: "Local",
        editSectorLabel: "Setor",
        editAudienceProfilesLabel: "Perfis autorizados a visualizar",
        editAudienceTypeLabel: "Audiência territorial",
        editAudienceSectorsLabel: "Setores destinatários",
        editAudienceOrganizationLabel: "Unidade destinatária",
        validationAudienceSectors: "Selecione ao menos um setor destinatário.",
        validationAudienceOrganization: "Selecione uma unidade destinatária.",
        validationAudienceProfiles: "Selecione ao menos um perfil autorizado.",
        editDescriptionLabel: "Descrição",
        editDescriptionPlaceholder: "Descreva aqui...",
        editAttachmentsLabel: "Anexos",
        save: "Salvar",
        cancel: "Cancelar",
        allOption: "Todos",
        selectOption: "Selecionar..."
    },
    "ja-JP": {
        headerTitle: "Hikitsugui Leader Read",
        dateFromLabel: "開始日",
        dateToLabel: "終了日",
        shiftLabel: "シフト",
        operatorLabel: "オペレーター",
        categoryLabel: "カテゴリ",
        equipmentLabel: "設備",
        localLabel: "場所",
        sectorLabel: "セクター",
        searchPlaceholder: "テキスト検索...",
        searchButton: "検索",
        colRead: "確認",
        colDate: "日付",
        colOperator: "オペレーター",
        colCategory: "カテゴリ",
        colEquipment: "設備",
        colLocal: "場所",
        colSector: "セクター",
        colDescription: "内容",
        colActions: "操作",
        actionView: "表示",
        actionEdit: "編集",
        actionDelete: "削除",
        errorPrefix: "エラー: ",
        invalidReply: "保存する前に返信を入力してください。",
        readModalTitle: "Hikitsugui 詳細",
        labelId: "ID",
        labelDate: "日付",
        labelOperator: "オペレーター",
        labelCategory: "カテゴリ",
        labelEquipment: "設備",
        labelLocal: "場所",
        labelSector: "セクター",
        labelDescription: "内容",
        repliesTitle: "返信",
        attachmentsTitle: "添付",
        replyLabel: "返信する",
        replyPlaceholder: "返信を入力...",
        sendReply: "返信を送信",
        close: "閉じる",
        listButton: "• リスト",
        clearButton: "クリア",
        noAttachments: "添付はありません",
        noExistingAttachments: "既存の添付はありません",
        noReplies: "返信はまだありません。",
        openAttachment: "開く",
        deleteAttachment: "削除",
        confirmDelete: "削除してもよろしいですか。",
        replyBadge: count => `返信 ${count} 件`,
        editModalTitle: "Hikitsugui を編集",
        editCategoryLabel: "カテゴリ",
        editEquipmentLabel: "設備",
        editLocalLabel: "場所",
        editSectorLabel: "セクター",
        editAudienceProfilesLabel: "閲覧を許可するプロフィール",
        editAudienceTypeLabel: "公開範囲",
        editAudienceSectorsLabel: "対象セクター",
        editAudienceOrganizationLabel: "対象組織単位",
        validationAudienceSectors: "対象セクターを1つ以上選択してください。",
        validationAudienceOrganization: "対象組織単位を選択してください。",
        validationAudienceProfiles: "閲覧を許可するプロフィールを1つ以上選択してください。",
        editDescriptionLabel: "内容",
        editDescriptionPlaceholder: "ここに入力してください...",
        editAttachmentsLabel: "添付",
        save: "保存",
        cancel: "キャンセル",
        allOption: "すべて",
        selectOption: "選択してください"
    }
};

const ACTION_ICONS = {
    view: `
        <svg viewBox="0 0 24 24" aria-hidden="true" focusable="false" xmlns="http://www.w3.org/2000/svg">
          <circle cx="10" cy="10" r="5.5" fill="none" stroke="currentColor" stroke-width="2"/>
          <path d="M13.8 13.8L19.5 19.5" fill="none" stroke="currentColor" stroke-width="2.6" stroke-linecap="round"/>
          <circle cx="8.3" cy="8.3" r="1.1" fill="currentColor" opacity="0.18"/>
        </svg>
    `,
    edit: `
        <svg viewBox="0 0 24 24" aria-hidden="true" focusable="false">
            <path d="M3 17.25V21h3.75L18.3 9.45l-3.75-3.75L3 17.25Z"></path>
            <path d="m14.55 5.7 3.75 3.75 1.15-1.15a1.32 1.32 0 0 0 0-1.9l-1.85-1.85a1.32 1.32 0 0 0-1.9 0L14.55 5.7Z"></path>
        </svg>
    `,
    delete: `
        <svg viewBox="0 0 24 24" aria-hidden="true" focusable="false">
            <path d="M9 3.75h6l.75 1.5H20v1.5H4V5.25h4.25L9 3.75Z"></path>
            <path d="M6.75 8.25h10.5l-.8 10.1A1.5 1.5 0 0 1 14.95 19.75h-5.9a1.5 1.5 0 0 1-1.49-1.4l-.81-10.1Z"></path>
        </svg>
    `
};

function t(key) {
    return I18N[currentLocale]?.[key] ?? I18N["pt-BR"][key];
}

function getLocaleField(ptField, jpField) {
    return currentLocale === "ja-JP" ? jpField : ptField;
}

function getLocalizedValue(row, ptField, jpField) {
    return row?.[getLocaleField(ptField, jpField)] ?? "";
}

function send(action, extra = {}) {
    window.chrome.webview.postMessage({
        action,
        ...extra
    });
}

window.addEventListener("DOMContentLoaded", () => {
    send("load");
    initReplyEditor();

    document.getElementById("txtSearch").addEventListener("input", () => {
        document.getElementById("btnBuscar").click();
    });

    document.getElementById("btnBuscar").addEventListener("click", () => {
        send("filter", currentFiltersPayload());
    });

    document.getElementById("btnFecharModal")?.addEventListener("click", closeModal);

    document.getElementById("btnEnviarReply")?.addEventListener("click", () => {
        const editor = document.getElementById("replyEditor");
        const text = editor.innerHTML;
        const id = editor.dataset.hikitsuguiId;

        if (!text.trim()) {
            alert(t("invalidReply"));
            return;
        }

        send("reply", { id: Number(id), text });
    });

    const upload = document.getElementById("editFileUpload");
    if (upload) {
        upload.addEventListener("change", async ev => {
            if (!ev.target.files || ev.target.files.length === 0) return;

            for (const file of ev.target.files) {
                const base64 = await toBase64(file);
                editNewAttachments.push({
                    fileName: file.name,
                    base64
                });
            }

            renderEditNewFiles();
        });
    }
    document.getElementById("editAudienceType").addEventListener("change", updateEditAudienceFields);
});

function initReplyEditor() {
    const toolbar = document.querySelector("#modal .editor-toolbar");
    const editor = document.getElementById("replyEditor");

    if (!toolbar || !editor) return;

    toolbar.addEventListener("click", ev => {
        const btn = ev.target.closest("button");
        if (!btn) return;

        const cmd = btn.getAttribute("data-cmd");
        if (!cmd) return;

        editor.focus();
        document.execCommand(cmd, false, null);
    });

    document.getElementById("btnClearReplyFormat")?.addEventListener("click", () => {
        const text = editor.innerText;
        editor.innerHTML = text ? `<p>${text}</p>` : "";
    });
}

window.chrome.webview.addEventListener("message", e => {
    const msg = e.data;

    switch (msg.type) {
        case "filters":
            currentLocale = msg.locale === "ja-JP" ? "ja-JP" : "pt-BR";
            applyLocale();
            fillFilters(msg);
            document.getElementById("dtInicial").value = msg.dtInicial;
            document.getElementById("dtFinal").value = msg.dtFinal;
            fillSelect("editCategoria", msg.categories, "Id", getLocaleField("NamePt", "NameJp"), false);
            fillSelect("editEquipamento", msg.equipments, "Id", getLocaleField("NamePt", "NameJp"), false);
            fillSelect("editLocal", msg.locals, "Id", getLocaleField("NamePt", "NameJp"), false);
            fillSelect("editSector", msg.sectors, "Id", getLocaleField("NamePt", "NameJp"), false);
            fillSelect("editAudienceSectorIds", msg.sectors, "Id", getLocaleField("NamePt", "NameJp"), false);
            document.querySelector('#editAudienceSectorIds option[value="0"]')?.remove();
            fillSelect("editAudienceOrganizationNodeId", msg.organizationNodes || [], "Id", getLocaleField("NamePt", "NameJp"), false);
            document.querySelector('#editAudienceType option[value="organization"]').disabled = !(msg.organizationNodes || []).length;
            document.querySelector('#editAudienceType option[value="general"]').disabled = !msg.canTargetGeneral;
            profileOptions = msg.profileOptions || [];
            renderEditProfileOptions([]);
            break;

        case "hikitsugui_for_leader":
            renderTable(msg.data || []);
            closeEditModal();
            break;

        case "hikitsugui_by_id":
            if (msg.data && msg.data[0]) {
                openModal(msg.data[0]);
            }
            break;

        case "replies":
            renderReplies(msg.data || []);
            document.getElementById("replyEditor").innerHTML = "";
            break;

        case "hikitsugui_edit":
            setTimeout(() => {
                openEditModal(msg.data[0]);
                renderEditProfileOptions(msg.profileCodes || []);
                editAudienceConfiguration = msg;
                renderEditAudience(msg);
                renderEditAttachments(msg.attachments);
                renderEditNewFiles();
            }, 50);
            break;

        case "attachments":
            renderAttachments(msg.data || []);
            break;

        case "error":
            console.error("C# ERROR:", msg.message);
            alert(t("errorPrefix") + msg.message);
            break;
    }
});

function applyLocale() {
    document.documentElement.lang = currentLocale;
    document.title = t("headerTitle");

    document.getElementById("txtHeaderTitle").textContent = t("headerTitle");
    document.getElementById("txtDateFromLabel").textContent = t("dateFromLabel");
    document.getElementById("txtDateToLabel").textContent = t("dateToLabel");
    document.getElementById("txtShiftLabel").textContent = t("shiftLabel");
    document.getElementById("txtOperatorLabel").textContent = t("operatorLabel");
    document.getElementById("txtCategoryLabel").textContent = t("categoryLabel");
    document.getElementById("txtEquipmentLabel").textContent = t("equipmentLabel");
    document.getElementById("txtLocalLabel").textContent = t("localLabel");
    document.getElementById("txtSectorLabel").textContent = t("sectorLabel");
    document.getElementById("txtSearch").placeholder = t("searchPlaceholder");
    document.getElementById("btnBuscar").textContent = t("searchButton");
    document.getElementById("txtColDate").textContent = t("colDate");
    document.getElementById("txtColRead").textContent = t("colRead");
    document.getElementById("txtColOperator").textContent = t("colOperator");
    document.getElementById("txtColCategory").textContent = t("colCategory");
    document.getElementById("txtColEquipment").textContent = t("colEquipment");
    document.getElementById("txtColLocal").textContent = t("colLocal");
    document.getElementById("txtColSector").textContent = t("colSector");
    document.getElementById("txtColDescription").textContent = t("colDescription");
    document.getElementById("txtColActions").textContent = t("colActions");
    document.getElementById("txtReadModalTitle").textContent = t("readModalTitle");
    document.getElementById("txtReplyLabel").textContent = t("replyLabel");
    document.getElementById("replyEditor").dataset.placeholder = t("replyPlaceholder");
    document.getElementById("btnReplyList").textContent = t("listButton");
    document.getElementById("btnClearReplyFormat").textContent = t("clearButton");
    document.getElementById("btnEnviarReply").textContent = t("sendReply");
    document.getElementById("btnFecharModal").textContent = t("close");
    document.getElementById("txtEditModalTitle").textContent = t("editModalTitle");
    document.getElementById("txtEditCategoryLabel").textContent = t("editCategoryLabel");
    document.getElementById("txtEditEquipmentLabel").textContent = t("editEquipmentLabel");
    document.getElementById("txtEditLocalLabel").textContent = t("editLocalLabel");
    document.getElementById("txtEditSectorLabel").textContent = t("editSectorLabel");
    document.getElementById("txtEditAudienceProfilesLabel").textContent = t("editAudienceProfilesLabel");
    document.getElementById("txtEditAudienceTypeLabel").textContent = t("editAudienceTypeLabel");
    document.getElementById("txtEditAudienceSectorsLabel").textContent = t("editAudienceSectorsLabel");
    document.getElementById("txtEditAudienceOrganizationLabel").textContent = t("editAudienceOrganizationLabel");
    document.getElementById("txtEditDescriptionLabel").textContent = t("editDescriptionLabel");
    document.getElementById("editDescricao").dataset.placeholder = t("editDescriptionPlaceholder");
    document.getElementById("btnEditList").textContent = t("listButton");
    document.getElementById("btnClearEditFormat").textContent = t("clearButton");
    document.getElementById("txtEditAttachmentsLabel").textContent = t("editAttachmentsLabel");
    document.getElementById("btnSaveEdit").textContent = t("save");
    document.getElementById("btnCancelEdit").textContent = t("cancel");
}

function fillFilters(msg) {
    const textField = getLocaleField("NamePt", "NameJp");
    fillSelect("shiftId", msg.shifts, "Id", textField, true);
    fillSelect("operatorId", msg.operators, "CodigoFJ", textField, true);
    fillSelect("reasonId", msg.categories, "Id", textField, true);
    fillSelect("equipId", msg.equipments, "Id", textField, true);
    fillSelect("localId", msg.locals, "Id", textField, true);
    fillSelect("sectorId", msg.sectors, "Id", textField, true);
}

function fillSelect(id, list, valueField, textField, withAll) {
    const sel = document.getElementById(id);
    sel.innerHTML = withAll
        ? `<option value="0">${escapeHtml(t("allOption"))}</option>`
        : `<option value="0">${escapeHtml(t("selectOption"))}</option>`;

    list.forEach(item => {
        sel.innerHTML += `<option value="${item[valueField]}">${escapeHtml(item[textField])}</option>`;
    });
}

function renderTable(rows) {
    const tbody = document.getElementById("tblHikitsugui");
    tbody.innerHTML = "";

    rows.forEach(r => {
        const replyCount = Number(r.ReplyCount || 0);
        const icon = Number(r.IsRead) === 1
            ? `<span class="read-indicator-wrap"><span class="maru animate">○</span>${renderReplyBadge(replyCount)}</span>`
            : `<span class="read-indicator-wrap"><span class="batsu animate">×</span>${renderReplyBadge(replyCount)}</span>`;

        const dataFormatada = r.Date?.split(" ")[0] ?? "";
        const operatorName = getLocalizedValue(r, "OperatorNamePt", "OperatorNameJp");
        const categoryName = getLocalizedValue(r, "CategoryPt", "CategoryJp");
        const equipmentName = getLocalizedValue(r, "EquipmentPt", "EquipmentJp");
        const localName = getLocalizedValue(r, "LocalPt", "LocalJp");
        const sectorName = getLocalizedValue(r, "SectorPt", "SectorJp");

        tbody.innerHTML += `
            <tr>
                <td class="border p-2">${icon}</td>
                <td class="border p-2 no-wrap">${r.Id}</td>
                <td class="border p-2 col-data no-wrap">${dataFormatada}</td>
                <td class="border p-2">${escapeHtml(operatorName)}</td>
                <td class="border p-2">${escapeHtml(categoryName)}</td>
                <td class="border p-2">${escapeHtml(equipmentName)}</td>
                <td class="border p-2">${escapeHtml(localName)}</td>
                <td class="border p-2 col-setor no-wrap">${escapeHtml(sectorName)}</td>
                <td class="border p-2">
                    ${truncateHtmlPreservingFormat(r.DescriptionHtml, 120)}
                </td>
                <td class="border p-2 actions-cell">
                    <div class="action-buttons">
                        <button
                            type="button"
                            class="icon-btn icon-btn-view"
                            onclick="previewAndMarkRead(${r.Id})"
                            title="${escapeHtmlAttr(t("actionView"))}"
                            aria-label="${escapeHtmlAttr(`${t("actionView")} hikitsugui ${r.Id}`)}">
                            ${ACTION_ICONS.view}
                        </button>
                        <button
                            type="button"
                            class="icon-btn icon-btn-edit"
                            onclick="openEdit(${r.Id})"
                            title="${escapeHtmlAttr(t("actionEdit"))}"
                            aria-label="${escapeHtmlAttr(`${t("actionEdit")} hikitsugui ${r.Id}`)}">
                            ${ACTION_ICONS.edit}
                        </button>
                        <button
                            type="button"
                            class="icon-btn icon-btn-delete"
                            onclick="deleteHikitsugui(${r.Id})"
                            title="${escapeHtmlAttr(t("actionDelete"))}"
                            aria-label="${escapeHtmlAttr(`${t("actionDelete")} hikitsugui ${r.Id}`)}">
                            ${ACTION_ICONS.delete}
                        </button>
                    </div>
                </td>
            </tr>
        `;
    });
}

function truncateHtmlPreservingFormat(html, maxChars = 120) {
    const div = document.createElement("div");
    div.innerHTML = html || "";

    let count = 0;

    function walk(node) {
        const clone = node.cloneNode(false);

        for (const child of node.childNodes) {
            if (count >= maxChars) break;

            if (child.nodeType === Node.TEXT_NODE) {
                const remaining = maxChars - count;
                const text = child.textContent || "";

                if (text.length <= remaining) {
                    clone.appendChild(document.createTextNode(text));
                    count += text.length;
                } else {
                    clone.appendChild(document.createTextNode(text.substring(0, remaining) + "..."));
                    count = maxChars;
                }
            } else if (child.nodeType === Node.ELEMENT_NODE) {
                const childClone = walk(child);
                if (childClone) clone.appendChild(childClone);
            }
        }

        return clone;
    }

    const truncated = walk(div);
    return truncated.innerHTML;
}

function renderAttachments(rows) {
    const list = document.getElementById("attachmentList");
    list.innerHTML = "";

    if (!rows.length) {
        list.innerHTML = `<i>${escapeHtml(t("noAttachments"))}</i>`;
        return;
    }

    rows.forEach(a => {
        list.innerHTML += `
            <div class="attach-item">
                <div class="attach-info">
                    <span class="attach-icon">📎</span>
                    <span class="attach-name">${escapeHtml(a.FileName)}</span>
                </div>

                <button class="attach-open"
                    onclick="openAttachment('${a.FilePath.replace(/\\/g, "\\\\")}')">
                    ${escapeHtml(t("openAttachment"))}
                </button>
            </div>
        `;
    });
}

function renderReplyBadge(count) {
    if (!count) {
        return "";
    }

    const label = t("replyBadge")(count);
    return `<span class="reply-badge" title="${escapeHtmlAttr(label)}" aria-label="${escapeHtmlAttr(label)}">${count}</span>`;
}

function renderEditAttachments(rows) {
    editExistingAttachments = rows || [];

    const list = document.getElementById("editExistingFiles");
    list.innerHTML = "";

    if (!rows || rows.length === 0) {
        list.innerHTML = `<i>${escapeHtml(t("noExistingAttachments"))}</i>`;
        return;
    }

    rows.forEach((a, index) => {
        list.innerHTML += `
            <li>
                <span>${escapeHtml(a.FileName)}</span>
                <button class="btn-delete-file" onclick="removeExistingAttachment(${index})">${escapeHtml(t("deleteAttachment"))}</button>
            </li>
        `;
    });
}

function removeExistingAttachment(index) {
    editExistingAttachments.splice(index, 1);
    renderEditAttachments(editExistingAttachments);
}

function openAttachment(path) {
    send("open_attachment", { path });
}

function renderEditNewFiles() {
    const list = document.getElementById("editNewFiles");
    list.innerHTML = "";

    editNewAttachments.forEach((file, index) => {
        list.innerHTML += `
            <li>
                <span>${escapeHtml(file.fileName)}</span>
                <button class="btn-delete-file" onclick="removeNewEditAttachment(${index})">${escapeHtml(t("deleteAttachment"))}</button>
            </li>
        `;
    });
}

function removeNewEditAttachment(index) {
    editNewAttachments.splice(index, 1);
    renderEditNewFiles();
}

function currentFiltersPayload(idOverride = null) {
    return {
        ...(idOverride !== null ? { id: idOverride } : {}),
        dtInicial: document.getElementById("dtInicial").value,
        dtFinal: document.getElementById("dtFinal").value,
        shiftId: Number(document.getElementById("shiftId").value),
        operatorId: Number(document.getElementById("operatorId").value),
        reasonId: Number(document.getElementById("reasonId").value),
        equipId: Number(document.getElementById("equipId").value),
        sectorId: Number(document.getElementById("sectorId").value),
        search: document.getElementById("txtSearch").value.trim()
    };
}

function markRead(id) {
    send("mark_read", currentFiltersPayload(id));
}

function preview(id) {
    console.debug("[HikitsuguiLeaderRead][PreviewClick]", {
        HikitsuguiId: id
    });
    send("preview", { id });
}

function previewAndMarkRead(id) {
    console.debug("[HikitsuguiLeaderRead][PreviewAndMarkReadClick]", {
        HikitsuguiId: id
    });
    send("preview_and_mark_read", currentFiltersPayload(id));
}

function openModal(row) {
    const modal = document.getElementById("modal");
    modal.classList.remove("hidden");

    const body = document.getElementById("modal-body");
    const operatorName = getLocalizedValue(row, "OperatorNamePt", "OperatorNameJp");
    const categoryName = getLocalizedValue(row, "CategoryPt", "CategoryJp");
    const equipmentName = getLocalizedValue(row, "EquipmentPt", "EquipmentJp");
    const localName = getLocalizedValue(row, "LocalPt", "LocalJp");
    const sectorName = getLocalizedValue(row, "SectorPt", "SectorJp");
    const descriptionInfo = resolveDescriptionField(row);

    console.debug("[HikitsuguiLeaderRead][PreviewDescription]", {
        HikitsuguiId: row.Id,
        DescriptionFromPayload: descriptionInfo.value,
        DescriptionLength: descriptionInfo.value.length,
        FieldNameUsed: descriptionInfo.fieldName
    });

    body.innerHTML = `
        <b>${escapeHtml(t("labelId"))}:</b> ${row.Id}<br>
        <b>${escapeHtml(t("labelDate"))}:</b> ${escapeHtml((row.Date || "").split(" ")[0])}<br>
        <b>${escapeHtml(t("labelOperator"))}:</b> ${escapeHtml(operatorName)}<br>
        <b>${escapeHtml(t("labelCategory"))}:</b> ${escapeHtml(categoryName)}<br>
        <b>${escapeHtml(t("labelEquipment"))}:</b> ${escapeHtml(equipmentName)}<br>
        <b>${escapeHtml(t("labelLocal"))}:</b> ${escapeHtml(localName)}<br>
        <b>${escapeHtml(t("labelSector"))}:</b> ${escapeHtml(sectorName)}<br><br>

        <b>${escapeHtml(t("labelDescription"))}:</b><br>
        <div id="descHtml" class="description-box"></div>
        <br><br>

        <h3>${escapeHtml(t("repliesTitle"))}</h3>
        <div id="replyList"></div>

        <h3 class="mt-4">${escapeHtml(t("attachmentsTitle"))}</h3>
        <div id="attachmentList" class="mt-2"></div>
    `;

    document.getElementById("descHtml").innerHTML = descriptionInfo.value;

    const replyEditor = document.getElementById("replyEditor");
    if (replyEditor) {
        replyEditor.dataset.hikitsuguiId = row.Id;
        replyEditor.innerHTML = "";
    }

    setTimeout(() => {
        send("select_replies", { id: row.Id });
        send("select_attachments", { id: row.Id });
    }, 50);
}

function resolveDescriptionField(row) {
    const candidates = [
        ["DescriptionHtml", row.DescriptionHtml],
        ["descriptionHtml", row.descriptionHtml],
        ["Description", row.Description],
        ["description", row.description],
        ["Descricao", row.Descricao],
        ["descricao", row.descricao]
    ];

    for (const [fieldName, value] of candidates) {
        if (value !== undefined && value !== null && String(value).length > 0) {
            return {
                fieldName,
                value: String(value)
            };
        }
    }

    return {
        fieldName: "none",
        value: ""
    };
}

function openEditModal(row) {
    editNewAttachments = [];
    currentEditId = row.Id;

    document.getElementById("editCategoria").value = row.CategoryId;
    document.getElementById("editEquipamento").value = row.EquipmentId ?? 0;
    document.getElementById("editLocal").value = row.LocalId ?? 0;
    document.getElementById("editSector").value = row.SectorId ?? 0;
    document.getElementById("editDescricao").innerHTML = row.Description ?? "";

    document.getElementById("modalEdit").classList.remove("hidden");
    initEditEditor();
}

function renderEditProfileOptions(selectedCodes) {
    const selected = new Set((selectedCodes || []).map(String));
    const host = document.getElementById("editAudienceProfiles");
    host.innerHTML = "";
    profileOptions.forEach(option => {
        const code = option.Code || option.code;
        const displayCode = code === "MANAGER" ? "MA" : code;
        const name = currentLocale === "ja-JP"
            ? (option.NameJp || option.nameJp || displayCode)
            : (option.NamePt || option.namePt || displayCode);
        const label = document.createElement("label");
        label.className = "audience-profile-option";
        label.innerHTML = `<input type="checkbox" value="${escapeHtml(code)}" ${selected.has(code) ? "checked" : ""} />`
            + `<span><strong>${escapeHtml(displayCode)}</strong>${name !== displayCode ? `<small>${escapeHtml(name)}</small>` : ""}</span>`;
        host.appendChild(label);
    });
}

function renderEditAudience(config) {
    const type = String(config.audienceType || "sector").toLowerCase();
    document.getElementById("editAudienceType").value = type;
    const selectedSectors = new Set((config.audienceSectorIds || []).map(Number));
    Array.from(document.getElementById("editAudienceSectorIds").options)
        .forEach(option => option.selected = selectedSectors.has(Number(option.value)));
    document.getElementById("editAudienceOrganizationNodeId").value = String(config.organizationNodeId || 0);
    updateEditAudienceFields();
}

function updateEditAudienceFields() {
    const type = document.getElementById("editAudienceType").value;
    document.getElementById("editAudienceSectorsField").classList.toggle("hidden", type !== "sector");
    document.getElementById("editAudienceOrganizationField").classList.toggle("hidden", type !== "organization");
}

function renderReplies(rows) {
    const list = document.getElementById("replyList");
    list.innerHTML = "";

    if (!rows.length) {
        list.innerHTML = `<div class="border rounded p-2 mb-4"><i>${escapeHtml(t("noReplies"))}</i></div>`;
        return;
    }

    rows.forEach(r => {
        const responderName = getLocalizedValue(r, "ResponderNamePt", "ResponderNameJp");
        list.innerHTML += `
            <div class="border rounded p-2 mb-4">
                <b>${escapeHtml(responderName)}</b> (${escapeHtml(r.Date)})<br>
                ${r.Message}<br><br>
            </div>
        `;
    });
}

function closeModal() {
    document.getElementById("modal").classList.add("hidden");
    document.getElementById("replyEditor").innerHTML = "";
}

function closeEditModal() {
    document.getElementById("modalEdit").classList.add("hidden");
}

function openEdit(id) {
    send("load_for_edit", { id });
}

function saveEdit() {
    const selectedProfiles = Array.from(document.querySelectorAll("#editAudienceProfiles input:checked"))
        .map(input => input.value);
    if (selectedProfiles.length === 0) {
        alert(t("validationAudienceProfiles"));
        return;
    }
    const audienceType = document.getElementById("editAudienceType").value;
    const audienceSectorIds = Array.from(document.getElementById("editAudienceSectorIds").selectedOptions)
        .map(option => Number(option.value));
    const organizationNodeId = Number(document.getElementById("editAudienceOrganizationNodeId").value || 0);
    if (audienceType === "sector" && audienceSectorIds.length === 0) {
        alert(t("validationAudienceSectors"));
        return;
    }
    if (audienceType === "organization" && organizationNodeId === 0) {
        alert(t("validationAudienceOrganization"));
        return;
    }

    const payload = {
        id: currentEditId,
        categoryId: Number(document.getElementById("editCategoria").value),
        equipmentId: Number(document.getElementById("editEquipamento").value),
        localId: Number(document.getElementById("editLocal").value),
        sectorId: Number(document.getElementById("editSector").value),
        description: document.getElementById("editDescricao").innerHTML,
        profileCodes: selectedProfiles,
        audienceType,
        sectorIds: audienceSectorIds,
        organizationNodeId,
        existingAttachments: editExistingAttachments.map(x => ({
            FileName: x.FileName ?? x.fileName,
            FilePath: x.FilePath ?? x.filePath
        })),
        newAttachments: editNewAttachments.map(x => ({
            fileName: x.fileName,
            base64: x.base64
        })),
        dtInicial: document.getElementById("dtInicial").value,
        dtFinal: document.getElementById("dtFinal").value,
        shiftId: Number(document.getElementById("shiftId").value),
        operatorId: Number(document.getElementById("operatorId").value),
        reasonId: Number(document.getElementById("reasonId").value),
        equipId: Number(document.getElementById("equipId").value),
        sectorIdFilter: Number(document.getElementById("sectorId").value),
        search: document.getElementById("txtSearch").value.trim()
    };

    document.activeElement.blur();
    send("save_edit", payload);
}

function toBase64(file) {
    return new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = () => resolve(reader.result.split(",")[1]);
        reader.onerror = reject;
        reader.readAsDataURL(file);
    });
}

function deleteHikitsugui(id) {
    if (!confirm(t("confirmDelete"))) return;

    send("delete_hikitsugui", currentFiltersPayload(id));
}

function initEditEditor() {
    const editor = document.getElementById("editDescricao");
    const toolbar = document.querySelector("#modalEdit .edit-toolbar");

    if (toolbar.dataset.initialized) return;
    toolbar.dataset.initialized = "true";

    toolbar.addEventListener("click", ev => {
        const btn = ev.target.closest("button");
        if (!btn) return;

        const cmd = btn.getAttribute("data-cmd");
        if (!cmd) return;

        editor.focus();
        document.execCommand(cmd, false, null);
    });

    document.getElementById("btnClearEditFormat").addEventListener("click", () => {
        const text = editor.innerText;
        editor.innerHTML = text ? `<p>${text}</p>` : "";
    });
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
