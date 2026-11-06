const state = {
    locale: "pt-BR",
    creatorNamePt: "",
    creatorNameJp: ""
};

const I18N = {
    "pt-BR": {
        headerTitle: "Registrar Hikitsugui",
        dateLabel: "Data",
        creatorLabel: "Criador",
        shiftLabel: "Turno",
        categoryLabel: "Categoria",
        equipmentLabel: "Equipamento",
        localLabel: "Local",
        sectorLabel: "Setor",
        selectPlaceholder: "Selecionar...",
        audienceTypeLabel: "Audiência",
        audienceSectorOption: "Setor(es)",
        audienceOrganizationOption: "Unidade organizacional",
        audienceGeneralOption: "Toda a organização",
        audienceSectorsLabel: "Setores destinatários",
        audienceOrganizationLabel: "Unidade destinatária",
        descriptionLabel: "Descrição",
        listButton: "• Lista",
        clearButton: "Limpar",
        attachmentsLabel: "Anexos",
        saveButton: "Salvar",
        cancelButton: "Cancelar",
        savedMessage: "Hikitsugui registrado com sucesso!",
        deleteAttachment: "Excluir",
        validationCategory: "Selecione uma categoria.",
        validationEquipment: "Selecione um equipamento.",
        validationLocal: "Selecione um local.",
        validationSector: "Selecione um setor.",
        validationDescription: "Informe a descrição.",
        validationAudienceSectors: "Selecione ao menos um setor destinatário.",
        validationAudienceOrganization: "Selecione uma unidade destinatária."
    },
    "ja-JP": {
        headerTitle: "Hikitsugui を登録",
        dateLabel: "日付",
        creatorLabel: "作成者",
        shiftLabel: "シフト",
        categoryLabel: "カテゴリ",
        equipmentLabel: "設備",
        localLabel: "場所",
        sectorLabel: "セクター",
        selectPlaceholder: "選択してください",
        audienceTypeLabel: "公開範囲",
        audienceSectorOption: "セクター",
        audienceOrganizationOption: "組織単位",
        audienceGeneralOption: "組織全体",
        audienceSectorsLabel: "対象セクター",
        audienceOrganizationLabel: "対象組織単位",
        descriptionLabel: "内容",
        listButton: "• リスト",
        clearButton: "クリア",
        attachmentsLabel: "添付",
        saveButton: "保存",
        cancelButton: "キャンセル",
        savedMessage: "Hikitsugui を登録しました。",
        deleteAttachment: "削除",
        validationCategory: "カテゴリを選択してください。",
        validationEquipment: "設備を選択してください。",
        validationLocal: "場所を選択してください。",
        validationSector: "セクターを選択してください。",
        validationDescription: "内容を入力してください。",
        validationAudienceSectors: "対象セクターを1つ以上選択してください。",
        validationAudienceOrganization: "対象組織単位を選択してください。"
    }
};

function t(key) {
    return I18N[state.locale]?.[key] ?? I18N["pt-BR"][key];
}

function getLocalizedField() {
    return state.locale === "ja-JP" ? "NameJp" : "NamePt";
}

function getCreatorName() {
    return state.locale === "ja-JP"
        ? (state.creatorNameJp || state.creatorNamePt || "")
        : (state.creatorNamePt || state.creatorNameJp || "");
}

function send(action, extra = {}) {
    window.chrome.webview.postMessage({
        action,
        ...extra
    });
}

window.addEventListener("DOMContentLoaded", () => {
    send("load");

    document.getElementById("btnSalvar").addEventListener("click", salvar);
    document.getElementById("btnCancelar").addEventListener("click", () => send("cancel"));
    document.getElementById("fileUpload").addEventListener("change", handleFiles);
    document.getElementById("audienceType").addEventListener("change", updateAudienceFields);
    document.getElementById("sectorId").addEventListener("change", selectOperationalSectorAsAudience);

    initEditorToolbar();
});

window.chrome.webview.addEventListener("message", e => {
    const msg = e.data;

    switch (msg.type) {
        case "filters":
            preencherFiltros(msg);
            break;

        case "saved":
            alert(t("savedMessage"));
            send("cancel");
            break;

        case "error":
            alert(msg.message || "Erro");
            break;
    }
});

function preencherFiltros(data) {
    state.locale = data.locale === "ja-JP" ? "ja-JP" : "pt-BR";
    state.creatorNamePt = data.creatorNamePt || "";
    state.creatorNameJp = data.creatorNameJp || data.creatorNamePt || "";

    applyLocale();

    const now = new Date();
    const yyyy = now.getFullYear();
    const mm = String(now.getMonth() + 1).padStart(2, "0");
    const dd = String(now.getDate()).padStart(2, "0");
    document.getElementById("txtDate").value = `${yyyy}-${mm}-${dd}`;
    document.getElementById("txtCreator").value = getCreatorName();

    fillSelect("shiftId", data.shifts, "Id", getLocalizedField());
    document.getElementById("shiftId").value = data.shiftId;

    fillSelect("categoryId", data.categories, "Id", getLocalizedField(), true);
    fillSelect("equipId", data.equipments, "Id", getLocalizedField(), true);
    fillSelect("localId", data.locals, "Id", getLocalizedField(), true);
    fillSelect("sectorId", data.sectors, "Id", getLocalizedField(), true);
    fillSelect("audienceSectorIds", data.sectors, "Id", getLocalizedField());
    fillSelect("audienceOrganizationNodeId", data.organizationNodes, "Id", getLocalizedField(), true);

    const generalOption = document.getElementById("txtAudienceGeneralOption");
    generalOption.disabled = !data.canTargetGeneral;
    updateAudienceFields();
}

function applyLocale() {
    document.documentElement.lang = state.locale;
    document.title = t("headerTitle");

    document.getElementById("txtHeaderTitle").textContent = t("headerTitle");
    document.getElementById("txtDateLabel").textContent = t("dateLabel");
    document.getElementById("txtCreatorLabel").textContent = t("creatorLabel");
    document.getElementById("txtShiftLabel").textContent = t("shiftLabel");
    document.getElementById("txtCategoryLabel").textContent = t("categoryLabel");
    document.getElementById("txtEquipmentLabel").textContent = t("equipmentLabel");
    document.getElementById("txtLocalLabel").textContent = t("localLabel");
    document.getElementById("txtSectorLabel").textContent = t("sectorLabel");
    document.getElementById("txtAudienceTypeLabel").textContent = t("audienceTypeLabel");
    document.getElementById("txtAudienceSectorOption").textContent = t("audienceSectorOption");
    document.getElementById("txtAudienceOrganizationOption").textContent = t("audienceOrganizationOption");
    document.getElementById("txtAudienceGeneralOption").textContent = t("audienceGeneralOption");
    document.getElementById("txtAudienceSectorsLabel").textContent = t("audienceSectorsLabel");
    document.getElementById("txtAudienceOrganizationLabel").textContent = t("audienceOrganizationLabel");
    document.getElementById("txtDescriptionLabel").textContent = t("descriptionLabel");
    document.getElementById("btnBulletList").textContent = t("listButton");
    document.getElementById("btnClearFormat").textContent = t("clearButton");
    document.getElementById("txtAttachmentsLabel").textContent = t("attachmentsLabel");
    document.getElementById("btnSalvar").textContent = t("saveButton");
    document.getElementById("btnCancelar").textContent = t("cancelButton");
}

function fillSelect(id, list, valueField, textField, allowZero = false) {
    const sel = document.getElementById(id);
    sel.innerHTML = "";

    if (allowZero) {
        sel.innerHTML += `<option value="0">${escapeHtml(t("selectPlaceholder"))}</option>`;
    }

    list.forEach(item => {
        sel.innerHTML += `<option value="${item[valueField]}">${escapeHtml(item[textField])}</option>`;
    });
}

function updateAudienceFields() {
    const type = document.getElementById("audienceType").value;
    document.getElementById("audienceSectorsField").hidden = type !== "sector";
    document.getElementById("audienceOrganizationField").hidden = type !== "organization";
}

function selectOperationalSectorAsAudience() {
    const sectorId = document.getElementById("sectorId").value;
    const audienceSelect = document.getElementById("audienceSectorIds");
    if (!sectorId || sectorId === "0" || Array.from(audienceSelect.selectedOptions).length > 0) return;

    const option = Array.from(audienceSelect.options).find(item => item.value === sectorId);
    if (option) option.selected = true;
}

let attachments = [];

async function handleFiles(ev) {
    for (const file of ev.target.files) {
        const base64 = await toBase64(file);

        attachments.push({
            fileName: file.name,
            base64
        });
    }

    renderAttachmentList();
    ev.target.value = "";
}

function renderAttachmentList() {
    const list = document.getElementById("fileList");
    list.innerHTML = "";

    attachments.forEach((file, index) => {
        const li = document.createElement("li");
        li.innerHTML = `<span>${escapeHtml(file.fileName)}</span>`;

        const btn = document.createElement("button");
        btn.textContent = t("deleteAttachment");

        btn.onclick = () => {
            attachments.splice(index, 1);
            renderAttachmentList();
        };

        li.appendChild(btn);
        list.appendChild(li);
    });
}

function toBase64(file) {
    return new Promise(resolve => {
        const reader = new FileReader();
        reader.onload = () => resolve(reader.result.split(",")[1]);
        reader.readAsDataURL(file);
    });
}

function initEditorToolbar() {
    const toolbar = document.querySelector(".editor-toolbar");
    const editor = document.getElementById("txtDescricao");

    toolbar.addEventListener("click", ev => {
        const btn = ev.target.closest("button");
        if (!btn) return;

        const cmd = btn.getAttribute("data-cmd");
        if (!cmd) return;

        editor.focus();
        document.execCommand(cmd, false, null);
    });

    document.getElementById("btnClearFormat").addEventListener("click", () => {
        const text = editor.innerText;
        editor.innerHTML = text ? `<p>${text}</p>` : "";
    });
}

function getFullDateTime() {
    const dateOnly = document.getElementById("txtDate").value;
    const now = new Date();

    const hh = String(now.getHours()).padStart(2, "0");
    const mi = String(now.getMinutes()).padStart(2, "0");
    const ss = String(now.getSeconds()).padStart(2, "0");

    return `${dateOnly} ${hh}:${mi}:${ss}`;
}

function validar() {
    if (Number(document.getElementById("categoryId").value) === 0) {
        alert(t("validationCategory"));
        return false;
    }

    if (Number(document.getElementById("equipId").value) === 0) {
        alert(t("validationEquipment"));
        return false;
    }

    if (Number(document.getElementById("localId").value) === 0) {
        alert(t("validationLocal"));
        return false;
    }

    if (Number(document.getElementById("sectorId").value) === 0) {
        alert(t("validationSector"));
        return false;
    }

    const audienceType = document.getElementById("audienceType").value;
    if (audienceType === "sector"
        && document.getElementById("audienceSectorIds").selectedOptions.length === 0) {
        alert(t("validationAudienceSectors"));
        return false;
    }

    if (audienceType === "organization"
        && Number(document.getElementById("audienceOrganizationNodeId").value) === 0) {
        alert(t("validationAudienceOrganization"));
        return false;
    }

    const editor = document.getElementById("txtDescricao");
    const plain = editor.innerText.trim();
    if (!plain) {
        alert(t("validationDescription"));
        return false;
    }

    return true;
}

function salvar() {
    if (!validar()) return;

    const payload = {
        action: "save",
        date: getFullDateTime(),
        shiftId: Number(document.getElementById("shiftId").value),
        reasonId: Number(document.getElementById("categoryId").value),
        equipId: Number(document.getElementById("equipId").value),
        localId: Number(document.getElementById("localId").value),
        sectorId: Number(document.getElementById("sectorId").value),
        audienceType: document.getElementById("audienceType").value,
        sectorIds: Array.from(document.getElementById("audienceSectorIds").selectedOptions)
            .map(option => Number(option.value)),
        organizationNodeId: Number(document.getElementById("audienceOrganizationNodeId").value || 0),
        text: document.getElementById("txtDescricao").innerHTML,
        attachments
    };

    send("save", payload);
}

function escapeHtml(value) {
    return String(value ?? "")
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#39;");
}
