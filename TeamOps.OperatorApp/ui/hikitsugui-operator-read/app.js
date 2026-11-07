const txtFJ = document.getElementById("txtFJ");
const txtNome = document.getElementById("txtNome");
const btnSair = document.getElementById("btnSair");
const cboLocal = document.getElementById("cboLocal");
const dtInicial = document.getElementById("dtInicial");
const dtFinal = document.getElementById("dtFinal");
const btnFiltrar = document.getElementById("btnFiltrar");
const tblBody = document.querySelector("#tblHikitsugui tbody");
const statusBadge = document.getElementById("statusBadge");

const modal = document.getElementById("modal");
const btnCloseModal = document.getElementById("btnCloseModal");
const modalBody = document.getElementById("modalBody");
const attachmentList = document.getElementById("attachmentList");

let currentFJ = "";
let currentRows = [];
let currentLocals = [];
let fjTimer = null;

window.addEventListener("DOMContentLoaded", () => {
    applyDefaultDates();
    bindEvents();
});

function bindEvents() {
    txtFJ.addEventListener("input", handleFjInput);
    btnSair.addEventListener("click", logoutOperator);
    btnFiltrar.addEventListener("click", runFilter);
    cboLocal.addEventListener("change", handleLocalChange);
    btnCloseModal.addEventListener("click", closeModal);

    document.addEventListener("click", event => {
        if (event.target === modal) {
            closeModal();
        }
    });
}

function applyDefaultDates() {
    const today = new Date();
    const start = new Date(today);
    start.setDate(today.getDate() - 31);

    dtFinal.value = formatDate(today);
    dtInicial.value = formatDate(start);
}

function handleFjInput() {
    txtFJ.value = txtFJ.value.toUpperCase();
    if (currentFJ && txtFJ.value.trim() !== currentFJ) {
        send("logout");
        resetOperatorState(false);
    }
    clearTimeout(fjTimer);
    const fj = txtFJ.value.trim();
    if (fj.length < 4) {
        setStatus("Informe ao menos 4 caracteres do codigo FJ.");
        return;
    }
    fjTimer = setTimeout(() => {
        setStatus("Buscando operador...");
        send("load_operator", { fj });
    }, 220);
}

function logoutOperator() {
    send("logout");
    resetOperatorState(true);
    setStatus("Informe o codigo FJ para iniciar.");
}

function handleLocalChange() {
    if (!currentFJ) return;

    const localId = Number(cboLocal.value || 0);
    if (localId > 0) {
        send("register_presence", { localId });
    }

    setStatus(localId > 0 ? "Presenca confirmada. Carregando hikitsugui..." : "Carregando hikitsugui...");
    runFilter();
}

function runFilter() {
    if (!currentFJ) {
        setStatus("Informe um operador valido antes de filtrar.");
        return;
    }

    const localId = Number(cboLocal.value || 0);
    if (localId <= 0) {
        setStatus("Selecione a area / local para continuar.");
        return;
    }

    setStatus("Carregando hikitsugui...");

    send("filter", {
        dtInicial: dtInicial.value,
        dtFinal: dtFinal.value,
        localId
    });
}

window.chrome.webview.addEventListener("message", event => {
    const msg = event.data;

    switch (msg.type) {
        case "operator_not_found":
            resetOperatorState(false);
            setStatus("Operador nao encontrado ou sem acesso ativo.");
            break;

        case "operator_loaded":
            hydrateOperator(msg.data);
            break;

        case "hikitsugui_list":
            currentRows = msg.data || [];
            renderTable(currentRows);
            setStatus(`${currentRows.length} hikitsugui encontrado(s).`);
            break;

        case "hikitsugui_preview":
            renderPreview(msg.data);
            break;

        case "attachments":
            renderAttachments(msg.data || []);
            break;

        case "read_status":
            updateReadStatus(msg.data.id, msg.data.lido);
            break;

        case "error":
            setStatus(msg.data?.message || msg.message || "Nao foi possivel concluir a operacao.");
            break;
    }
});

function hydrateOperator(data) {
    currentFJ = data.CodigoFJ;
    txtNome.value = data.NameRomanji || "";
    txtFJ.readOnly = true;
    btnSair.classList.remove("hidden");
    currentLocals = data.locals || [];

    fillLocalOptions();
    setStatus("Operador carregado. Selecione a area / local.");
}

function fillLocalOptions() {
    cboLocal.innerHTML = `<option value="0">Selecione...</option>`;
    currentLocals.forEach(local => {
        const option = document.createElement("option");
        option.value = local.Id;
        option.textContent = local.SectorNamePt
            ? `${local.SectorNamePt} — ${local.NamePt}`
            : local.NamePt;
        cboLocal.appendChild(option);
    });
    if (currentLocals.length === 1) {
        cboLocal.value = String(currentLocals[0].Id);
        handleLocalChange();
    }
}

function renderTable(list) {
    tblBody.innerHTML = "";

    if (!list || list.length === 0) {
        tblBody.innerHTML = `
            <tr>
                <td colspan="7" class="empty-cell">Nenhum hikitsugui encontrado para o filtro atual.</td>
            </tr>
        `;
        return;
    }

    const fragment = document.createDocumentFragment();

    list.forEach(item => {
        const row = document.createElement("tr");
        const readSymbol = item.IsRead ? "○" : "×";
        const readClass = item.IsRead ? "status-read" : "status-unread";

        row.innerHTML = `
            <td id="read-${item.Id}" class="${readClass}">${readSymbol}</td>
            <td>${item.Id}</td>
            <td>${formatServerDate(item.Date)}</td>
            <td>${escapeHtml(item.CategoryName)}</td>
            <td>${escapeHtml(item.CreatorCodigoFJ)}</td>
            <td class="description-cell">${escapeHtml(truncate(stripHtml(item.Description), 90))}</td>
            <td class="actions-cell">
                <button class="icon-btn icon-btn-view" type="button" data-preview-id="${item.Id}" title="Abrir" aria-label="Abrir hikitsugui ${item.Id}">
                    <svg viewBox="0 0 24 24" aria-hidden="true" focusable="false" xmlns="http://www.w3.org/2000/svg">
                      <circle cx="10" cy="10" r="5.5" fill="none" stroke="currentColor" stroke-width="2"/>
                      <path d="M13.8 13.8L19.5 19.5" fill="none" stroke="currentColor" stroke-width="2.6" stroke-linecap="round"/>
                      <circle cx="8.3" cy="8.3" r="1.1" fill="currentColor" opacity="0.18"/>
                    </svg>
                </button>
            </td>
        `;

        row.querySelector("[data-preview-id]").addEventListener("click", () => openPreview(item.Id));
        fragment.appendChild(row);
    });

    tblBody.appendChild(fragment);
}

function openPreview(id) {
    send("preview", { id });
}

function renderPreview(item) {
    modalBody.innerHTML = `
        <div class="preview-grid">
            <div><span class="preview-label">ID</span><strong>${item.Id}</strong></div>
            <div><span class="preview-label">Data</span><strong>${formatServerDate(item.Date)}</strong></div>
            <div><span class="preview-label">Criador</span><strong>${escapeHtml(item.CreatorCodigoFJ || "")}</strong></div>
            <div><span class="preview-label">Categoria</span><strong>${escapeHtml(item.CategoryName || "")}</strong></div>
        </div>
        <div class="preview-description">${item.Description || ""}</div>
    `;

    attachmentList.innerHTML = `<div class="attachment-empty">Carregando anexos...</div>`;
    modal.classList.remove("hidden");
}

function renderAttachments(list) {
    attachmentList.innerHTML = "";

    if (!list || list.length === 0) {
        attachmentList.innerHTML = `<div class="attachment-empty">Nenhum anexo.</div>`;
        return;
    }

    const fragment = document.createDocumentFragment();

    list.forEach(item => {
        const row = document.createElement("div");
        row.className = "attach-item";
        row.innerHTML = `
            <div class="attach-name">${escapeHtml(item.FileName)}</div>
            <button class="btn-attach" type="button">Abrir</button>
        `;

        row.querySelector("button").addEventListener("click", () => {
            send("open_attachment", { path: item.FilePath });
        });

        fragment.appendChild(row);
    });

    attachmentList.appendChild(fragment);
}

function updateReadStatus(id, isRead) {
    const cell = document.getElementById(`read-${id}`);
    if (!cell) return;

    cell.textContent = isRead ? "○" : "×";
    cell.className = isRead ? "status-read" : "status-unread";

    const row = currentRows.find(item => Number(item.Id) === Number(id));
    if (row) {
        row.IsRead = isRead;
    }
}

function closeModal() {
    modal.classList.add("hidden");
}

function resetOperatorState(clearFj) {
    currentFJ = "";
    if (clearFj) {
        txtFJ.value = "";
    }

    txtFJ.readOnly = false;
    btnSair.classList.add("hidden");
    txtNome.value = "";
    cboLocal.innerHTML = `<option value="">Selecione...</option>`;
    currentLocals = [];
    currentRows = [];
    renderTable([]);
}

function setStatus(text) {
    statusBadge.textContent = text;
}

function send(action, data = {}) {
    window.chrome.webview.postMessage({ action, ...data });
}

function truncate(text, max) {
    if (!text) return "";
    return text.length > max ? `${text.substring(0, max)}...` : text;
}

function stripHtml(html) {
    const div = document.createElement("div");
    div.innerHTML = html || "";
    return div.textContent || div.innerText || "";
}

function escapeHtml(value) {
    return String(value ?? "")
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#39;");
}

function formatDate(date) {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, "0");
    const day = String(date.getDate()).padStart(2, "0");
    return `${year}-${month}-${day}`;
}

function formatServerDate(value) {
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
        return value ?? "";
    }

    return date.toLocaleDateString("pt-BR");
}
