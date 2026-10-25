let operators = [];
let requests = [];
let requestMode = "create";

const tooltip = document.getElementById("tooltip");
const tooltipContent = document.getElementById("tooltipContent");

const ACTION_ICONS = {
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

window.addEventListener("DOMContentLoaded", () => {
    closeModal();
    setDefaultDateRange();
    bindEvents();
    send({ action: "paidleave_load" });
});

function bindEvents() {
    document.getElementById("btnAdd").addEventListener("click", () => openCreateModal());
    document.getElementById("btnCancelModal").addEventListener("click", closeModal);
    document.getElementById("btnSaveModal").addEventListener("click", saveRequest);

    document.querySelectorAll("input[name='shiftFilter']").forEach(radio => {
        radio.addEventListener("change", () => {
            send({
                action: "filter_shift",
                shiftId: getCurrentShiftId()
            });
        });
    });

    document.getElementById("startDate").addEventListener("change", applyFilters);
    document.getElementById("endDate").addEventListener("change", applyFilters);
    document.getElementById("searchInput").addEventListener("input", applyFilters);

    document.addEventListener("mousemove", (e) => {
        tooltip.style.left = `${e.clientX + 12}px`;
        tooltip.style.top = `${e.clientY + 12}px`;
    });

    document.addEventListener("mouseover", (e) => {
        const cell = e.target.closest(".motivo-cell");
        if (!cell) return;

        showNotesTooltip(cell.dataset.notes || "");
    });

    document.addEventListener("mouseout", (e) => {
        if (!e.target.closest(".motivo-cell")) return;
        if (e.relatedTarget && e.relatedTarget.closest(".motivo-cell")) return;
        hideTooltip();
    });

    document.addEventListener("click", (e) => {
        if (!e.target.closest(".autocomplete-group")) {
            document.getElementById("opSuggestions").classList.add("hidden");
        }
    });

    document.getElementById("opSearch").addEventListener("input", handleOperatorSearch);
}

function send(payload) {
    window.chrome.webview.postMessage(payload);
}

function getCurrentShiftId() {
    return Number(document.querySelector("input[name='shiftFilter']:checked")?.value ?? 0);
}

window.chrome.webview.addEventListener("message", event => {
    const msg = event.data;

    if (msg.type === "select_all" || msg.type === "select_all_by_shift") {
        requests = msg.data || [];
        applyFilters();
        return;
    }

    if (msg.type === "get_todoke_info" || msg.type === "get_folha_info" || msg.type === "get_conferencia_info") {
        showTooltip(msg.data);
        return;
    }

    if (msg.type === "select_operators") {
        operators = msg.data || [];
        return;
    }

    if (msg.type === "select_motivos") {
        fillMotivos(msg.data || []);
        return;
    }
});

function fillMotivos(items) {
    const sel = document.getElementById("motivoId");
    sel.innerHTML = '<option value="">Selecione...</option>';

    items.forEach(item => {
        sel.innerHTML += `<option value="${item.Id}">${item.NomePt}</option>`;
    });
}

function setDefaultDateRange() {
    const now = new Date();
    const start = new Date(now.getFullYear(), now.getMonth() - 1, 1);
    const end = new Date(now.getFullYear(), now.getMonth() + 1, 0);

    document.getElementById("startDate").value = formatDateInput(start);
    document.getElementById("endDate").value = formatDateInput(end);
}

function formatDateInput(date) {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, "0");
    const day = String(date.getDate()).padStart(2, "0");
    return `${year}-${month}-${day}`;
}

function applyFilters() {
    const searchTerm = document.getElementById("searchInput").value.trim().toLowerCase();
    const startDate = document.getElementById("startDate").value;
    const endDate = document.getElementById("endDate").value;

    const filtered = requests.filter(item => {
        const isConferenciaCompleted = Number(item.hasConferencia) === 1;
        const itemDate = normalizeDate(item.requestDate);
        const matchesDate =
            (!startDate || itemDate >= startDate) &&
            (!endDate || itemDate <= endDate);

        if (!matchesDate || (!searchTerm && isConferenciaCompleted)) {
            return false;
        }

        if (!searchTerm) {
            return true;
        }

        const haystack = [
            item.operatorName,
            item.operatorCodigoFJ,
            item.todokeMotivoName,
            item.notes,
            item.requestDate
        ]
            .filter(Boolean)
            .join(" ")
            .toLowerCase();

        return haystack.includes(searchTerm);
    });

    renderTable(filtered);
}

function normalizeDate(value) {
    return String(value || "").slice(0, 10);
}

function isYukyu(item) {
    return Number(item.todokeMotivoId) === 1;
}

function canMarkConferencia(item) {
    return Number(item.hasConferencia) !== 1 &&
        Number(item.hasTodoke) === 1 &&
        (!isYukyu(item) || Number(item.hasFolha) === 1);
}

function getConferenciaStatusMessage(item) {
    if (Number(item.hasConferencia) === 1) {
        return "Conferência já registrada.";
    }

    if (Number(item.hasTodoke) !== 1) {
        return "Registre o Todoke antes da Conferência.";
    }

    if (isYukyu(item) && Number(item.hasFolha) !== 1) {
        return "Para Yukyu, registre a Folha Yukyu antes da Conferência.";
    }

    return "Registrar Conferência";
}

function renderTable(items) {
    const container = document.getElementById("tableContainer");

    if (!items || items.length === 0) {
        container.innerHTML = `<div class="empty-state">Nenhum registro encontrado.</div>`;
        return;
    }

    let html = `
        <table class="min-w-full paidleave-table">
            <thead>
                <tr>
                    <th>Operador</th>
                    <th>Data Solicitação</th>
                    <th>Lançado por</th>
                    <th>Motivo</th>
                    <th>Todoke</th>
                    <th>Folha Yukyu</th>
                    <th>Conferência</th>
                    <th class="text-center actions-col">Ações</th>
                </tr>
            </thead>
            <tbody>
    `;

    for (const item of items) {
        html += `
            <tr>
                <td>${item.operatorName}</td>
                <td class="text-center">${item.requestDate}</td>
                <td class="text-center">${item.authorizedBy}</td>
                <td class="text-center motivo-cell" data-notes="${escapeHtmlAttr(item.notes || "")}">
                    ${item.todokeMotivoName ?? ""}
                </td>
                <td class="text-center">
                    <span class="cursor-pointer"
                          onclick="toggleTodoke(${item.id})"
                          onmouseenter="requestTodokeInfo(${item.id})"
                          onmouseleave="hideTooltip()">
                        ${item.hasTodoke
                            ? "<span class='maru'>○</span>"
                            : "<span class='batsu'>×</span>"}
                    </span>
                </td>
                <td class="text-center">
                    ${item.todokeMotivoName === "Yukyu"
                        ? `
                            <span class="cursor-pointer"
                                  onclick="toggleFolha(${item.id})"
                                  onmouseenter="requestFolhaInfo(${item.id})"
                                  onmouseleave="hideTooltip()">
                                ${item.hasFolha
                                    ? "<span class='maru'>○</span>"
                                    : "<span class='batsu'>×</span>"}
                            </span>
                        `
                        : "<span class='text-muted'>—</span>"}
                </td>
                <td class="text-center">
                    <span class="${canMarkConferencia(item) ? "cursor-pointer" : "status-disabled"}"
                          ${canMarkConferencia(item) ? `onclick="toggleConferencia(${item.id})"` : `title="${getConferenciaStatusMessage(item)}"`}
                          onmouseenter="requestConferenciaInfo(${item.id})"
                          onmouseleave="hideTooltip()">
                        ${item.hasConferencia
                            ? "<span class='maru'>○</span>"
                            : "<span class='batsu'>×</span>"}
                    </span>
                </td>
                <td class="text-center actions-cell">
                    <div class="action-buttons">
                        <button
                            type="button"
                            class="icon-btn icon-btn-edit"
                            onclick="openEditModal(${item.id})"
                            title="Editar"
                            aria-label="Editar solicitação ${item.id}">
                            ${ACTION_ICONS.edit}
                        </button>
                        <button
                            type="button"
                            class="icon-btn icon-btn-delete"
                            onclick="deleteRequest(${item.id})"
                            title="Excluir"
                            aria-label="Excluir solicitação ${item.id}">
                            ${ACTION_ICONS.delete}
                        </button>
                    </div>
                </td>
            </tr>
        `;
    }

    html += `
            </tbody>
        </table>
    `;

    container.innerHTML = html;
}

function requestTodokeInfo(id) {
    send({ action: "get_todoke_info", id });
}

function requestFolhaInfo(id) {
    send({ action: "get_folha_info", id });
}

function requestConferenciaInfo(id) {
    send({ action: "get_conferencia_info", id });
}

function toggleTodoke(id) {
    if (!confirm("Registrar Todoke para este operador?")) return;

    send({
        action: "toggle_todoke",
        id,
        shiftId: getCurrentShiftId()
    });
}

function toggleFolha(id) {
    if (!confirm("Registrar Folha de Controle para este operador?")) return;

    send({
        action: "toggle_folha",
        id,
        shiftId: getCurrentShiftId()
    });
}

function toggleConferencia(id) {
    const item = requests.find(request => Number(request.id) === Number(id));
    if (!item) return;

    if (!canMarkConferencia(item)) {
        alert(getConferenciaStatusMessage(item));
        return;
    }

    if (!confirm("Registrar Conferência para este operador?")) return;

    send({
        action: "toggle_conferencia",
        id,
        shiftId: getCurrentShiftId()
    });
}

function showTooltip(info) {
    if (!info || info.length === 0) {
        tooltipContent.innerHTML = "Nenhum registro";
    } else {
        const row = info[0];
        tooltipContent.innerHTML = `
            <div><strong>${row.TakenByName}</strong></div>
            <div>${row.TakenAt}</div>
        `;
    }

    tooltip.classList.remove("hidden");
    tooltip.classList.add("show");
}

function showNotesTooltip(text) {
    tooltipContent.innerHTML = text && text.trim() !== "" ? text : "Sem notas";
    tooltip.classList.remove("hidden");
    tooltip.classList.add("show");
}

function hideTooltip() {
    tooltip.classList.remove("show");
    setTimeout(() => tooltip.classList.add("hidden"), 150);
}

function openCreateModal() {
    requestMode = "create";
    document.getElementById("modalTitle").textContent = "Adicionar solicitação de yukyu";
    document.getElementById("modalSubtitle").textContent = "Crie um novo registro de solicitação.";
    document.getElementById("btnSaveModal").textContent = "Salvar";
    document.getElementById("requestId").value = "";
    clearForm();
    document.getElementById("modalRequest").classList.remove("hidden");
}

function openEditModal(id) {
    const item = requests.find(x => Number(x.id) === Number(id));
    if (!item) return;

    requestMode = "edit";
    document.getElementById("modalTitle").textContent = "Editar solicitação";
    document.getElementById("modalSubtitle").textContent = "Atualize os dados do cadastro selecionado.";
    document.getElementById("btnSaveModal").textContent = "Atualizar";

    document.getElementById("requestId").value = item.id;
    document.getElementById("opSearch").value = `${item.operatorName} (${item.operatorCodigoFJ})`;
    document.getElementById("opCodigoFJ").value = item.operatorCodigoFJ;
    document.getElementById("reqDate").value = item.requestDate;
    document.getElementById("motivoId").value = item.todokeMotivoId ?? "";
    document.getElementById("notes").value = item.notes ?? "";
    document.getElementById("opSuggestions").classList.add("hidden");

    document.getElementById("modalRequest").classList.remove("hidden");
}

function closeModal() {
    document.getElementById("modalRequest").classList.add("hidden");
    clearForm();
}

function clearForm() {
    document.getElementById("requestId").value = "";
    document.getElementById("opSearch").value = "";
    document.getElementById("opCodigoFJ").value = "";
    document.getElementById("reqDate").value = "";
    document.getElementById("notes").value = "";
    document.getElementById("motivoId").value = "";
    document.getElementById("opSuggestions").classList.add("hidden");
}

function saveRequest() {
    const id = Number(document.getElementById("requestId").value || 0);
    const opCodigoFJ = document.getElementById("opCodigoFJ").value;
    const reqDate = document.getElementById("reqDate").value;
    const notes = document.getElementById("notes").value;
    const motivoIdStr = document.getElementById("motivoId").value;

    if (!opCodigoFJ || !reqDate) {
        alert("Operador e Data Solicitação são obrigatórios.");
        return;
    }

    if (!motivoIdStr) {
        alert("Selecione um motivo.");
        return;
    }

    const payload = {
        action: requestMode === "edit" ? "update_request" : "add_request",
        id,
        opCodigoFJ,
        reqDate,
        notes,
        motivoId: Number(motivoIdStr),
        shiftId: getCurrentShiftId()
    };

    send(payload);
    closeModal();
}

function deleteRequest(id) {
    if (!confirm("Tem certeza que deseja excluir esta solicitação?")) return;

    send({
        action: "delete_request",
        id,
        shiftId: getCurrentShiftId()
    });
}

function handleOperatorSearch() {
    const text = document.getElementById("opSearch").value.toLowerCase();
    const suggestions = document.getElementById("opSuggestions");

    if (!text) {
        suggestions.classList.add("hidden");
        return;
    }

    const filtered = operators.filter(op =>
        op.NameRomanji.toLowerCase().includes(text) ||
        op.NameNihongo.includes(text) ||
        op.CodigoFJ.toLowerCase().includes(text)
    );

    if (filtered.length === 0) {
        suggestions.classList.add("hidden");
        return;
    }

    let html = "";
    for (const op of filtered) {
        html += `
            <div class="suggestion-item"
                 onclick='selectOperator(${JSON.stringify(op.CodigoFJ)}, ${JSON.stringify(op.NameRomanji)})'>
                ${op.NameRomanji} (${op.CodigoFJ})
            </div>
        `;
    }

    suggestions.innerHTML = html;
    suggestions.classList.remove("hidden");
}

function selectOperator(codigoFJ, name) {
    document.getElementById("opSearch").value = `${name} (${codigoFJ})`;
    document.getElementById("opCodigoFJ").value = codigoFJ;
    document.getElementById("opSuggestions").classList.add("hidden");
}

function escapeHtmlAttr(value) {
    return String(value)
        .replace(/&/g, "&amp;")
        .replace(/"/g, "&quot;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;");
}
