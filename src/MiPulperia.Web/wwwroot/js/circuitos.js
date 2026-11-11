(() => {
"use strict";
if (!window.signalR) return;
const estados = new Map();
const connection = new signalR.HubConnectionBuilder().withUrl("/hubs/sucursales").withAutomaticReconnect().build();
const status = document.getElementById("signalr-status");
let aviso;
function aplicar(estado) {
    const anterior = estados.get(estado.sucursalId);
    if (anterior && anterior.version > estado.version) return;
    estados.set(estado.sucursalId, estado);
    document.querySelectorAll('[data-sucursal="' + estado.sucursalId + '"]').forEach(panel => {
        const inactiva = estado.estado !== "CERRADO";
        panel.classList.toggle("sucursal-inactiva", inactiva);
        panel.querySelectorAll(".circuit-state").forEach(el => el.textContent = estado.estado);
        panel.querySelectorAll(".circuit-message").forEach(el => el.textContent = inactiva ? estado.mensaje : "");
        panel.querySelectorAll("[data-operacion]").forEach(el => {
            el.setAttribute("aria-disabled", String(inactiva));
            if (el.tagName === "BUTTON") el.disabled = inactiva;
        });
    });
    if (anterior && anterior.estado !== "CERRADO" && estado.estado === "CERRADO")
        avisar("La sucursal se recuperó. Actualiza la consulta para ver sus datos.");
}
function avisar(texto) {
    if (!aviso) {
        aviso = document.createElement("div"); aviso.className = "alert success"; aviso.setAttribute("role", "status");
        document.getElementById("contenido")?.prepend(aviso);
    }
    aviso.replaceChildren(document.createTextNode(texto + " "));
    const boton = document.createElement("button"); boton.className = "button secondary"; boton.textContent = "Actualizar";
    boton.addEventListener("click", () => location.reload()); aviso.append(boton);
}
document.addEventListener("click", event => {
    if (event.target.closest('[data-operacion][aria-disabled="true"]')) event.preventDefault();
});
connection.on("CircuitoActualizado", aplicar);
connection.on("EstadosIniciales", valores => { estados.clear(); valores.forEach(aplicar); });
connection.on("ReservasVencidas", () => avisar("Se procesaron reservas vencidas. Actualiza los saldos y el historial."));
connection.onreconnecting(() => { if (status) status.textContent = "Reconectando las actualizaciones…"; });
connection.onreconnected(async () => { if (status) status.textContent = "Actualizaciones en vivo conectadas."; await sincronizar(); });
async function sincronizar() {
    const response = await fetch("/Estado/Actual", { headers: { Accept: "application/json" } });
    if (response.ok) (await response.json()).forEach(aplicar);
}
async function iniciar() {
    try { await connection.start(); if (status) status.textContent = "Actualizaciones en vivo conectadas."; await sincronizar(); }
    catch { if (status) status.textContent = "Sin conexión en vivo; reintentando…"; setTimeout(iniciar, 5000); }
}
connection.onclose(() => setTimeout(iniciar, 5000));
iniciar();
})();
