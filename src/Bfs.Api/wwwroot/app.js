const ICONS = {
  pesaje: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M12 3v3M7 21h10M8 8h8l1 13H7L8 8z"/><path d="M10 12h4"/></svg>',
  reportes: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M4 19V5h12l4 4v10H4z"/><path d="M16 5v4h4M8 13h8M8 17h5"/></svg>',
  catalogos: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M4 7h16M4 12h16M4 17h10"/></svg>',
  usuarios: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M12 12a4 4 0 1 0-4-4 4 4 0 0 0 4 4z"/><path d="M4 20a8 8 0 0 1 16 0"/></svg>',
  planta: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M3 21V9l6-4v4l6-4v16"/><path d="M15 21V3h6v18M3 21h18M7 13h2M7 17h2M11 13h2M11 17h2"/></svg>',
  tablero: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M4 20V10M10 20V4M16 20v-7M22 20H2"/></svg>',
  sistema: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><ellipse cx="12" cy="5" rx="8" ry="3"/><path d="M4 5v14c0 1.7 3.6 3 8 3s8-1.3 8-3V5"/><path d="M4 12c0 1.7 3.6 3 8 3s8-1.3 8-3"/></svg>',
  config: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.7 1.7 0 0 0 .3 1.8l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.7 1.7 0 0 0-1.8-.3 1.7 1.7 0 0 0-1 1.5V21a2 2 0 1 1-4 0v-.1a1.7 1.7 0 0 0-1-1.5 1.7 1.7 0 0 0-1.8.3l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.7 1.7 0 0 0 .3-1.8 1.7 1.7 0 0 0-1.5-1H3a2 2 0 1 1 0-4h.1a1.7 1.7 0 0 0 1.5-1 1.7 1.7 0 0 0-.3-1.8l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.7 1.7 0 0 0 1.8.3H9a1.7 1.7 0 0 0 1-1.5V3a2 2 0 1 1 4 0v.1a1.7 1.7 0 0 0 1 1.5 1.7 1.7 0 0 0 1.8-.3l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.7 1.7 0 0 0-.3 1.8V9a1.7 1.7 0 0 0 1.5 1H21a2 2 0 1 1 0 4h-.1a1.7 1.7 0 0 0-1.5 1z"/></svg>'
};

const DEFAULT_FIELDS = [
  { key: "tipo", label: "Tipo de movimiento (ingreso/egreso)", visible: false, required: false },
  { key: "patenteChasis", label: "Patente chasis", visible: true, required: true },
  { key: "patenteAcoplado", label: "Patente acoplado", visible: true, required: false },
  { key: "conductor", label: "Conductor", visible: true, required: false },
  { key: "documento", label: "Documento", visible: true, required: false },
  { key: "tipoDocumento", label: "Tipo de documento", visible: true, required: false },
  { key: "nacionalidad", label: "Nacionalidad", visible: true, required: false },
  { key: "cliente", label: "Cliente", visible: true, required: false },
  { key: "transporte", label: "Transporte", visible: true, required: false },
  { key: "producto", label: "Producto", visible: true, required: false },
  { key: "procedencia", label: "Procedencia", visible: true, required: false },
  { key: "destino", label: "Destino", visible: true, required: false },
  { key: "comprobante", label: "Remito", visible: true, required: false },
  { key: "observaciones", label: "Observaciones", visible: true, required: false },
  { key: "taraPallets", label: "Tara pallets (kg)", visible: true, required: false }
];

const state = {
  token: localStorage.getItem("bfs_token"),
  user: null,
  view: "pesaje",
  configTab: "general",
  empresas: [],
  pesadas: [],
  usuarios: [],
  catalogos: { clientes: [], productos: [], transportes: [] },
  config: {},
  branding: {},
  campos: DEFAULT_FIELDS,
  scale: { kilogramos: 0, puerto: "", estado: "sin balanza", estable: false, nombre: "" },
  balanzas: [],
  balanzaSel: localStorage.getItem("bfs_balanza") || "",
  cfgBalanzas: [],
  modelos: [],
  balEdit: null,
  modoPesaje: "normal",
  filtros: {},
  pagina: 1,
  dias: 30,
  pendientes: [],
  pendientesQ: "",
  proximosTickets: {},
  selectedPesadaId: null,
  /** Peso capturado esperando que se acepte la pesada ({ id, kilogramos, balanza, balanzaId, en }). */
  captura: null,
  /** Cómo se toma el peso: "simple", "partes" (balanza corta) o "ejes" (balanza de ejes). */
  capturaModo: "simple",
  /** En partes o por ejes: lo capturado hasta ahora ({ id, kilogramos, manual, etiqueta }). */
  partes: [],
  /** Por ejes: los grupos del vehículo, en orden desde adelante (ids de tipo de eje). */
  grupos: ["simple_simple", "simple_doble", "tandem_triple"],
  licencia: null,
  rolesAsignables: [],
  funciones: {},
  catTab: "clientes",
  catEdit: null,
  catQ: "",
  catItems: []
};

async function api(path, options = {}) {
  const headers = { ...(options.headers || {}) };
  if (!(options.body instanceof FormData)) headers["Content-Type"] = "application/json";
  if (state.token) headers.Authorization = "Bearer " + state.token;
  const res = await fetch(path, { ...options, headers });
  if (res.status === 401 && path !== "/api/auth/login") {
    logout();
    throw new Error("Sesión vencida");
  }
  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || res.statusText);
  }
  if (res.status === 204) return null;
  const type = res.headers.get("content-type") || "";
  return type.includes("application/json") ? res.json() : res.text();
}

function logout() {
  state.token = null;
  state.user = null;
  guardarCapturaLocal(null);
  state.partes = [];
  localStorage.removeItem("bfs_token");
  try { localStorage.removeItem("bfs_token_panel"); } catch {}
  render();
}

function esc(value) {
  return String(value ?? "").replace(/[&<>"']/g, ch => ({
    "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;"
  }[ch]));
}

function toast(message, type = "ok") {
  const el = document.getElementById("toast");
  el.className = `toast ${type}`;
  el.textContent = message;
  clearTimeout(toast._t);
  toast._t = setTimeout(() => el.classList.add("hidden"), 2600);
}

function val(id) {
  return document.getElementById(id)?.value ?? "";
}

function num(id) {
  return Number(val(id) || 0);
}

/** Edición Estándar: una empresa, pesaje normal y aduana, local. Sin API para otros sistemas ni nube. */
function esEstandar() { return state.edicion === "Estandar"; }
function etiquetaVersion() { return state.version ? ` <span class="brand-ver">v${esc(state.version)}</span>` : ""; }

function isVendor() { return state.user?.rol === "vendor"; }
function can(permiso) { return (state.user?.permisos || []).includes(permiso); }
function canVerPesadas() { return can("pesadas.ver") || can("pesadas.ver_propias"); }

function navItems() {
  const nav = [];
  // En la nube no hay balanza (se pesa en la portería, que sincroniza) y los backups los hace el servidor.
  const nube = state.modo === "Nube";
  if (can("pesaje.operar") && !nube) nav.push(["pesaje", "Pesaje"]);
  if (canVerPesadas()) nav.push(["reportes", "Pesadas"], ["tablero", "Tablero"]);
  if (can("catalogos.ver")) nav.push(["catalogos", "Catálogos"]);
  const admin = [];
  if (can("usuarios.gestionar")) admin.push(["usuarios", "Usuarios"]);
  if (can("config.planta")) admin.push(["planta", "Planta"]);
  if (can("sistema.gestionar") && !nube) admin.push(["sistema", "Sistema"]);
  if (can("config.bfs")) admin.push(["config", "Configuración"]);
  return { nav, admin };
}

function licenciaPill() {
  const l = state.licencia;
  if (!l || l.puedePesar) return "";
  return `<span class="pill pill-err" title="${esc(l.mensaje)}">Sin licencia</span>`;
}

function fields() {
  return state.campos.length ? state.campos : DEFAULT_FIELDS;
}

function field(key) {
  return fields().find(f => f.key === key);
}

function visible(key) {
  // La tara de pallets además depende de Configuración → General: apagada ahí, no aparece aunque Campos la muestre.
  if (key === "taraPallets" && state.funciones?.taraPallets === false) return false;
  return field(key)?.visible !== false;
}

function required(key) {
  return field(key)?.required === true;
}

function empresaName(id) {
  return state.empresas.find(e => e.id === id)?.nombre || (id ? id.slice(0, 8) : "-");
}

function empresaOptions(selected) {
  return `<option value="">Todas las empresas</option>` + state.empresas.map(e =>
    `<option value="${e.id}" ${e.id === selected ? "selected" : ""}>${esc(e.nombre)}</option>`
  ).join("");
}

/** Empresa con la que se abren los tickets. Se recuerda en esta PC para no elegirla en cada pesada. */
function empresaEnCurso() {
  const activas = state.empresas.filter(e => e.activa !== false);
  let id = null;
  try { id = localStorage.getItem("bfs_empresa"); } catch {}
  return activas.some(e => e.id === id) ? id : activas[0]?.id || "";
}

function proximoTicketActual() {
  return state.proximosTickets?.[empresaEnCurso()] || "";
}

function actualizarProximoTicketUI() {
  if (state.selectedPesadaId) return;
  const t = proximoTicketActual() || "-";
  const box = document.getElementById("proximoTicketValor");
  if (box) box.textContent = t;
  const head = document.getElementById("formProximoTicket");
  if (head) head.textContent = t;
}

function elegirEmpresa(id) {
  try { localStorage.setItem("bfs_empresa", id); } catch {}
  document.querySelectorAll(".empresa-btn").forEach(b => b.classList.toggle("active", b.dataset.id === id));
  actualizarProximoTicketUI();
}

/** Con <fijaId> (salida de una pendiente) muestra la empresa de esa pesada y no deja cambiarla. */
function selectorEmpresa(fijaId) {
  const activas = state.empresas.filter(e => e.activa !== false || e.id === fijaId);
  const actual = fijaId || empresaEnCurso();
  if (activas.length === 1) return `<strong>${esc(activas[0].nombre)}</strong>`;
  return `<div class="empresa-switch">${activas.map((e, i) => fijaId && e.id !== fijaId ? "" :
    `<button type="button" class="empresa-btn ${e.id === actual ? "active" : ""}" data-id="${e.id}" ${fijaId ? "disabled" : `onclick="elegirEmpresa('${e.id}')"`}
      title="Empresa ${i + 1}">${i + 1}. ${esc(e.nombre)}</button>`).join("")}</div>`;
}

function bfsLogo(extraClass = "brand-logo") {
  return `<img class="${extraClass}" src="/images/bfs_logo.png" alt="BFS Balanzas Full Service" />`;
}

function balanzaActual() {
  return state.balanzas.find(b => b.balanzaId === state.balanzaSel) || state.balanzas[0] || state.scale;
}

function elegirBalanza(id) {
  state.balanzaSel = id;
  try { localStorage.setItem("bfs_balanza", id); } catch {}
  pintarPeso();
}

function pintarPeso() {
  const p = balanzaActual();
  state.scale = p;
  const cont = document.getElementById("displays");
  if (cont) {
    const lista = balanzasEnPantalla();
    cont.classList.toggle("dos", lista.length > 1);
    const iguales = cont.children.length === lista.length
      && lista.every((b, i) => cont.children[i].dataset.balanza === String(b.balanzaId || ""));
    if (!iguales) {
      cont.innerHTML = lista.map(displayBalanza).join("");
      // Cambiaron las balanzas (o llegaron después de dibujar la pantalla): los botones de captura también.
      pintarCaptura();
    } else {
      // Se cambia solo el contenido: el recuadro que se toca queda, así un toque no se pierde en medio de un repintado.
      lista.forEach((b, i) => {
        const el = cont.children[i];
        const nuevo = document.createElement("div");
        nuevo.innerHTML = displayBalanza(b);
        el.className = nuevo.firstElementChild.className;
        el.innerHTML = nuevo.firstElementChild.innerHTML;
      });
    }
  }
  const st = document.getElementById("scalePill");
  if (st) st.innerHTML = pillForScale(p.estado) + (p.estable ? ` <span class="pill pill-ok">Estable</span>` : "");
}

/** Las balanzas en el orden de la pantalla. Con dos, el operador elige cuál va a la izquierda (se recuerda en la PC). */
function balanzasEnPantalla() {
  const lista = state.balanzas.length ? [...state.balanzas] : [state.scale];
  let invertidas = false;
  try { invertidas = localStorage.getItem("bfs_balanzas_invertidas") === "1"; } catch {}
  return invertidas && lista.length === 2 ? lista.reverse() : lista;
}

function intercambiarBalanzas() {
  try {
    const actual = localStorage.getItem("bfs_balanzas_invertidas") === "1";
    localStorage.setItem("bfs_balanzas_invertidas", actual ? "0" : "1");
  } catch {}
  pintarPeso();
  pintarCaptura();
}

/** Estado de la lectura → color del display: verde estable, ámbar en movimiento, rojo sin lectura. */
function estadoDisplay(b) {
  if (["error", "sin señal", "sin balanza"].includes(b.estado)) return ["lcd-err", b.estado === "error" ? "Error" : "Sin señal"];
  return b.estable ? ["lcd-ok", "Estable"] : ["lcd-mov", "En movimiento"];
}

function capturaDeBalanza(b) {
  const c = state.captura;
  if (!c) return null;
  if (c.balanzaId && b.balanzaId) return c.balanzaId === b.balanzaId ? c : null;
  return state.balanzas.length <= 1 || c.balanza === (b.nombre || b.puerto) ? c : null;
}

function displayBalanza(b) {
  const [cls, estado] = estadoDisplay(b);
  const varias = state.balanzas.length > 1;
  const enUso = varias && b.balanzaId === balanzaActual().balanzaId;
  const cap = capturaDeBalanza(b);
  return `<div class="lcd ${cls} ${enUso ? "en-uso" : ""} ${cap ? "con-captura" : ""}" data-balanza="${b.balanzaId || ""}"
      ${varias ? `onclick="elegirBalanza('${b.balanzaId}')" title="Tocar para pesar con esta balanza"` : ""}>
    <div class="lcd-top">
      <span class="lcd-nombre">${esc(b.nombre || b.puerto || "Balanza")}</span>
      <span class="lcd-chips">
        ${cap ? `<span class="lcd-chip lcd-chip-captura" title="Peso capturado: persiste aunque el camión se retire">Fijo ${cap.kilogramos.toLocaleString("es-AR")} kg</span>` : ""}
        ${enUso ? `<span class="lcd-chip">En uso</span>` : ""}
      </span>
    </div>
    <div class="lcd-num">${sieteSegmentos(cls === "lcd-err" ? "------" : String(b.kilogramos ?? 0))}<span class="lcd-kg">kg</span></div>
    <div class="lcd-pie"><span class="lcd-led"></span>${esc(estado)}${b.estado === "simulada" ? " · simulada" : ""}</div>
  </div>`;
}

const SEGMENTOS = {
  "0": "abcdef", "1": "bc", "2": "abged", "3": "abgcd", "4": "fgbc", "5": "afgcd", "6": "afgedc",
  "7": "abc", "8": "abcdefg", "9": "abcdfg", "-": "g", " ": ""
};

/** Número como display de 7 segmentos (SVG, sin fuentes externas): los segmentos apagados se ven tenues, como en un LCD. */
function sieteSegmentos(texto, digitos = 6) {
  const t = texto.length >= digitos ? texto.slice(-digitos) : texto.padStart(digitos, " ");
  const W = 50, H = 90, m = 4.5, g = 1.6, paso = 62;
  const hor = (cy, x1, x2) => `${x1},${cy} ${x1 + m},${cy - m} ${x2 - m},${cy - m} ${x2},${cy} ${x2 - m},${cy + m} ${x1 + m},${cy + m}`;
  const ver = (cx, y1, y2) => `${cx},${y1} ${cx + m},${y1 + m} ${cx + m},${y2 - m} ${cx},${y2} ${cx - m},${y2 - m} ${cx - m},${y1 + m}`;
  const forma = {
    a: hor(m, m + g, W - m - g), g: hor(H / 2, m + g, W - m - g), d: hor(H - m, m + g, W - m - g),
    f: ver(m, m + g, H / 2 - g), b: ver(W - m, m + g, H / 2 - g),
    e: ver(m, H / 2 + g, H - m - g), c: ver(W - m, H / 2 + g, H - m - g)
  };
  const cuerpo = [...t].map((ch, i) => {
    const prendidos = SEGMENTOS[ch] ?? "";
    return `<g transform="translate(${i * paso + 8} 0) skewX(-6)">${Object.entries(forma).map(([seg, pts]) =>
      `<polygon points="${pts}" class="${prendidos.includes(seg) ? "on" : "off"}"/>`).join("")}</g>`;
  }).join("");
  return `<svg class="lcd-svg" viewBox="0 0 ${digitos * paso + 6} ${H}" role="img" aria-label="${esc(texto.trim())} kg">${cuerpo}</svg>`;
}

function pillForScale(estado) {
  if (estado === "abierta") return `<span class="pill pill-ok">Serie abierta</span>`;
  if (estado === "simulada") return `<span class="pill pill-info">Simulada</span>`;
  if (estado === "error") return `<span class="pill pill-err">Error</span>`;
  if (estado === "sin señal") return `<span class="pill pill-err">Sin señal</span>`;
  return `<span class="pill pill-warn">${esc(estado || "sin señal")}</span>`;
}

function render() {
  if (!state.token) return renderLogin();
  if (esSuperadmin()) return renderSuperadmin();
  const { nav, admin } = navItems();
  const todas = nav.concat(admin);
  if (!todas.some(([id]) => id === state.view)) state.view = todas[0]?.[0] || "";
  const navBtn = ([id, label]) => `
          <button class="nav-btn ${state.view === id ? "active" : ""}" onclick="go('${id}')">${ICONS[id]}<span>${label}</span></button>
        `;

  document.getElementById("app").innerHTML = `${bannerSoporte()}
    <div class="app-shell">
      <header class="topbar">
        <div class="brand">
          ${bfsLogo()}
          <div>
            <h1>Sistema de Pesaje BFS 4.0${esEstandar() ? " <small>Estándar</small>" : ""}${etiquetaVersion()}</h1>
            <p>${esc(state.user?.tenant || state.branding.clienteNombre || "Planta local")}</p>
          </div>
        </div>
        <div class="userchip">
          <span id="nubePill">${pillNube()}</span>
          ${licenciaPill()}
          <div>
            <strong>${esc(state.user?.nombre || state.user?.email || "")}</strong>
            <span>${esc(state.user?.rol || "")}</span>
          </div>
          <button class="btn-ghost" onclick="logout()">Salir</button>
        </div>
      </header>
      <aside class="sidebar">
        ${nav.length ? `<div class="nav-label">Operación</div>` : ""}
        ${nav.map(navBtn).join("")}
        ${admin.length ? `<div class="nav-label">Administración</div>` : ""}
        ${admin.map(navBtn).join("")}
      </aside>
      <main class="workspace" id="main"></main>
    </div>
  `;
  const views = { pesaje: renderPesaje, reportes: renderReportes, catalogos: renderCatalogos, usuarios: renderUsuarios, planta: renderPlanta, tablero: renderTablero, sistema: renderSistema, config: renderConfig };
  views[state.view]?.();
}

function go(view) {
  state.view = view;
  render();
}

function renderLogin() {
  document.getElementById("app").innerHTML = `
    <div class="login-wrap">
      <form class="login-card" onsubmit="event.preventDefault();doLogin()">
        ${bfsLogo("login-logo")}
        <h2>Ingreso</h2>
        <p style="color:var(--muted);margin-top:0">${esc(state.branding.clienteNombre || "Sistema de pesaje")}${state.version ? ` · v${esc(state.version)}` : ""} · la configuración la hace BFS.</p>
        <label for="user">Usuario o email</label>
        <input id="user" type="text" autocomplete="username" autocapitalize="none" spellcheck="false"
          onkeydown="if(event.key==='Enter'&&!val('pass')){event.preventDefault();document.getElementById('pass')?.focus()}" />
        <label for="pass" style="margin-top:12px">Contraseña</label>
        <input id="pass" type="password" autocomplete="current-password" />
        <p class="error" id="err"></p>
        <button type="submit" class="btn-primary" style="width:100%;margin-top:8px">Entrar</button>
      </form>
    </div>
  `;
  document.getElementById("user")?.focus();
}

async function doLogin() {
  try {
    const data = await api("/api/auth/login", {
      method: "POST",
      body: JSON.stringify({ email: val("user"), password: val("pass") })
    });
    state.token = data.token;
    state.user = data;
    localStorage.setItem("bfs_token", data.token);
    if (data.rol === "superadmin") return render();
    await boot();
  } catch (e) {
    const msg = e.message || "";
    document.getElementById("err").textContent =
      msg && msg !== "Unauthorized" && msg !== "Sesión vencida" ? msg : "Email o clave incorrectos";
  }
}

async function boot() {
  const [empresas, config, branding, campos, licencia, funciones] = await Promise.all([
    api("/api/empresas"),
    can("config.bfs") ? api("/api/config") : Promise.resolve({}),
    api("/api/branding"),
    api("/api/campos"),
    api("/api/licencia"),
    api("/api/funciones")
  ]);
  state.funciones = funciones;
  state.empresas = empresas;
  state.config = config;
  state.branding = branding;
  state.campos = campos?.length ? campos : DEFAULT_FIELDS;
  state.licencia = licencia;
  await Promise.all([actualizarNube(), restaurarCapturaGuardada()]);
  render();
  if (can("pesaje.operar")) pollPeso();
  // Solo se actualiza el indicador: repintar la pantalla borraría lo que el operador está cargando.
  if (!boot._nube) boot._nube = setInterval(async () => {
    await actualizarNube();
    const pill = document.getElementById("nubePill");
    if (pill) pill.innerHTML = pillNube();
  }, 60000);
}

async function pollPeso() {
  if (!state.token) return;
  try {
    state.balanzas = await api("/api/balanzas/lecturas");
    pintarPeso();
    // Automatización: solo si está habilitada y se está mirando Pesaje (si no, no se pide nada).
    if (state.funciones.automatizacion && state.view === "pesaje" && document.getElementById("auto")) {
      state.auto = await api("/api/automatizacion/estado");
      pintarAutomatizacion();
    }
  } catch {}
  setTimeout(pollPeso, 500);
}

function inputField(id, label, opts = {}) {
  const req = required(id) ? " *" : "";
  if (opts.type === "select") {
    return `<div><label>${esc(label)}${req}</label><select id="${id}">${opts.options}</select></div>`;
  }
  if (opts.list) {
    const input = `<input id="${id}" list="${opts.list}" autocomplete="off" />`;
    return `<div><label>${esc(label)}${req}</label>${opts.alta ? conAlta(input, opts.alta, id) : input}</div>`;
  }
  if (opts.blur) {
    return `<div><label>${esc(label)}${req}</label><input id="${id}" onblur="${opts.blur}" autocomplete="off" /></div>`;
  }
  if (opts.type === "textarea") {
    return `<div style="grid-column:1/-1"><label>${esc(label)}${req}</label><textarea id="${id}"></textarea></div>`;
  }
  return `<div><label>${esc(label)}${req}</label><input id="${id}" ${opts.type ? `type="${opts.type}"` : ""} value="${opts.value ?? ""}" /></div>`;
}

function datalist(id, items) {
  return `<datalist id="${id}">${items.map(x => `<option value="${esc(x)}"></option>`).join("")}</datalist>`;
}

/**
 * Alta rápida desde Pesaje: de qué catálogo sale cada campo, con qué lista se autocompleta y qué columna es el nombre.
 * <extra> son valores fijos del alta (un ATA se da de alta como transporte marcado ATA).
 */
const ALTA_RAPIDA = {
  clientes: { lista: "dl_clientes", nombre: "razonSocial" },
  transportes: { lista: "dl_transportes", nombre: "razonSocial" },
  productos: { lista: "dl_productos", nombre: "nombre" },
  exportadores: { lista: "dl_exportadores", nombre: "razonSocial", cuit: "ad_exportadorCuit" },
  atas: { catalogo: "transportes", lista: "dl_atas", nombre: "razonSocial", extra: { esAta: true }, cuit: "ad_ataCuit" }
};

/** El campo con un botón + al lado para dar de alta en el catálogo sin salir de la pantalla. */
function conAlta(input, tipo, campoId) {
  if (!can("catalogos.editar")) return input;
  return `<div class="con-alta">${input}<button type="button" class="btn btn-alta" title="Agregar al catálogo"
    onclick="altaRapida('${tipo}', '${campoId}')">+</button></div>`;
}

function camposAlta(tipo) {
  const a = ALTA_RAPIDA[tipo];
  const campos = CATALOGOS[a.catalogo || tipo].campos;
  // El ATA ya va marcado: no se pregunta.
  return campos.filter(([key]) => !(a.extra && key in a.extra));
}

/** Ventana de alta: el nombre arranca con lo que el operador ya escribió en el campo. */
function altaRapida(tipo, campoId) {
  const a = ALTA_RAPIDA[tipo];
  const titulo = tipo === "atas" ? "ATA" : CATALOGOS[a.catalogo || tipo].titulo;
  cerrarModal();
  const fondo = document.createElement("div");
  fondo.className = "modal-fondo";
  fondo.id = "modal";
  fondo.innerHTML = `
    <div class="card modal">
      <h3 style="margin-top:0">Agregar a ${esc(titulo)}</h3>
      <div class="grid2">
        ${camposAlta(tipo).map(([key, label, t]) => t === "check"
          ? `<div><label class="check"><input type="checkbox" id="ar_${key}" /> ${label}</label></div>`
          : `<div><label>${label}</label><input id="ar_${key}" ${t ? `type="${t}"` : ""} autocomplete="off"
              value="${key === a.nombre ? esc(val(campoId)) : ""}" /></div>`).join("")}
      </div>
      <p class="error" id="ar_err"></p>
      <div class="btn-row">
        <button class="btn-primary" onclick="guardarAltaRapida('${tipo}', '${campoId}')">Agregar</button>
        <button class="btn" onclick="cerrarModal()">Cancelar</button>
      </div>
    </div>`;
  document.body.appendChild(fondo);
  fondo.addEventListener("keydown", e => {
    if (e.key === "Enter") guardarAltaRapida(tipo, campoId);
    if (e.key === "Escape") cerrarModal();
  });
  document.getElementById("ar_" + a.nombre)?.focus();
}

async function guardarAltaRapida(tipo, campoId) {
  const a = ALTA_RAPIDA[tipo];
  const catalogo = a.catalogo || tipo;
  const body = { ...(a.extra || {}) };
  for (const [key, , t] of camposAlta(tipo)) {
    const el = document.getElementById("ar_" + key);
    body[key] = t === "check" ? el.checked : t === "number" ? Number(el.value || 0) : el.value;
  }
  try {
    const nuevo = await api(`/api/catalogos/${catalogo}`, { method: "POST", body: JSON.stringify(body) });
    state.catalogos[catalogo] = [...(state.catalogos[catalogo] || []), nuevo];
    // La lista del campo ya incluye el nuevo, y el campo queda completo.
    const lista = document.getElementById(a.lista);
    if (lista) lista.insertAdjacentHTML("beforeend", `<option value="${esc(nuevo[a.nombre])}"></option>`);
    const campo = document.getElementById(campoId);
    if (campo) {
      campo.value = nuevo[a.nombre];
      campo.dispatchEvent(new Event("change", { bubbles: true }));
    }
    if (a.cuit && nuevo.cuit && document.getElementById(a.cuit)) document.getElementById(a.cuit).value = nuevo.cuit;
    cerrarModal();
    toast(`Agregado: ${nuevo[a.nombre]}`);
  } catch (e) {
    document.getElementById("ar_err").textContent = limpiarError(e);
  }
}

function cerrarModal() {
  document.getElementById("modal")?.remove();
}

const ESCENARIOS_ADUANA = ["Ingresa con Contenedor", "Carga Contenedor Lleno"];

function puedeManual() {
  return can("pesaje.peso_manual") && state.funciones.ingresoManual;
}

function modosPesaje() {
  const modos = [["normal", "Normal"]];
  if (state.funciones.taraConocida) modos.push(["tara", "Tara conocida"], ["tomar", "Tomar tara"]);
  return modos;
}

function bloqueManual(titulo) {
  if (!puedeManual()) return "";
  return `
    <details style="margin-top:12px">
      <summary>${titulo}</summary>
      <div class="grid2" style="margin-top:8px">
        <div><label>Peso (kg) — vacío = balanza</label><input id="pm_kg" type="number" min="1" /></div>
        ${state.funciones.claveManual ? `<div><label>Clave de ingreso manual</label><input id="pm_clave" type="password" autocomplete="off" /></div>` : ""}
      </div>
    </details>`;
}

function manual() {
  const kg = val("pm_kg");
  return { peso: kg ? Number(kg) : null, clave: val("pm_clave") || null };
}

const TIPOS_DOCUMENTO = ["DNI", "Pasaporte", "CI", "Otro"];

function opciones(values, selected) {
  return values.map(v => `<option value="${esc(v)}" ${v === selected ? "selected" : ""}>${esc(v)}</option>`).join("");
}

function seccion(titulo, html) {
  return html.trim() ? `<fieldset class="seccion"><legend>${titulo}</legend><div class="grid3">${html}</div></fieldset>` : "";
}

function bloqueAduana() {
  if (!state.funciones.aduana) return "";
  const atas = (state.catalogos.transportes || []).filter(t => t.esAta);
  return `
    <div id="aduanaBox" style="display:none">
      <fieldset class="seccion"><legend>Operación aduanera</legend>
        <div class="grid3">
          ${state.funciones.aduanaEscenario
            ? `<div><label>Escenario</label><select id="ad_escenario" disabled>${opciones([state.funciones.aduanaEscenario])}</select></div>`
            : `<div><label>Escenario *</label><select id="ad_escenario"><option value="">Elegir...</option>${opciones(ESCENARIOS_ADUANA)}</select></div>`}
          <div><label>N° de operación</label><input id="ad_operacionId" /></div>
          <div><label>LOT</label><input value="${esc(state.funciones.aduanaLot || "")}" disabled title="Sale de Planta → Aduana" /></div>
          <div><label>Exportador / importador</label>${conAlta(`<input id="ad_exportador" list="dl_exportadores" autocomplete="off" onchange="completarCuit('ad_exportador', 'ad_exportadorCuit', state.catalogos.exportadores)" />`, "exportadores", "ad_exportador")}</div>
          <div><label>CUIT</label><input id="ad_exportadorCuit" /></div>
          <div><label>Destinación aduanera</label><input id="ad_destinacion" /></div>
          <div><label>Agente de transporte (ATA)</label>${conAlta(`<input id="ad_ata" list="dl_atas" autocomplete="off" onchange="completarCuit('ad_ata', 'ad_ataCuit', state.catalogos.transportes)" />`, "atas", "ad_ata")}</div>
          <div><label>CUIT ATA</label><input id="ad_ataCuit" /></div>
          <div></div>
          <div><label>Contenedor N°</label><input id="ad_contenedorId" /></div>
          <div><label>Tara contenedor (kg)</label><input id="ad_contenedorTara" type="number" value="0" /></div>
          <div></div>
          <div><label>Bulto</label><input id="ad_bultoId" /></div>
          <div><label>Bulto bruto (kg)</label><input id="ad_bultoBruto" type="number" /></div>
          <div><label>Bulto tara (kg)</label><input id="ad_bultoTara" type="number" /></div>
        </div>
      </fieldset>
      ${datalist("dl_exportadores", (state.catalogos.exportadores || []).map(x => x.razonSocial))}
      ${datalist("dl_atas", atas.map(x => x.razonSocial))}
    </div>`;
}

function toggleAduana() {
  const box = document.getElementById("aduanaBox");
  if (box) box.style.display = val("tipoOperacion") === "Aduana" ? "" : "none";
}

/** Al elegir del catálogo, completa el CUIT si estaba vacío. */
function completarCuit(campo, campoCuit, catalogo) {
  const x = (catalogo || []).find(e => e.razonSocial === val(campo));
  if (x?.cuit && !val(campoCuit)) document.getElementById(campoCuit).value = x.cuit;
}

function datosAduana() {
  if (val("tipoOperacion") !== "Aduana") return { tipoOperacion: null, aduana: null };
  const n = id => val(id) === "" ? null : Number(val(id));
  return {
    tipoOperacion: "Aduana",
    aduana: {
      escenario: val("ad_escenario"),
      exportador: val("ad_exportador") || null,
      exportadorCuit: val("ad_exportadorCuit") || null,
      operacionId: val("ad_operacionId") || null,
      ata: val("ad_ata") || null,
      ataCuit: val("ad_ataCuit") || null,
      destinacionAduanera: val("ad_destinacion") || null,
      contenedorId: val("ad_contenedorId") || null,
      contenedorTara: n("ad_contenedorTara") || 0,
      bultoId: val("ad_bultoId") || null,
      bultoBruto: n("ad_bultoBruto"),
      bultoTara: n("ad_bultoTara")
    }
  };
}

async function renderPesaje() {
  const pedidos = [
    api("/api/pesadas/pendientes"),
    api("/api/pesadas/proximos-tickets"),
    api("/api/catalogos/clientes"),
    api("/api/catalogos/transportes"),
    api("/api/catalogos/productos"),
    state.funciones.aduana ? api("/api/catalogos/exportadores") : Promise.resolve([])
  ];
  const [pendientes, proximosTickets, clientes, transportes, productos, exportadores] = await Promise.all(pedidos);
  state.pendientes = pendientes;
  state.proximosTickets = proximosTickets || {};
  state.catalogos = { clientes, transportes, productos, exportadores };
  if (!modosPesaje().some(([id]) => id === state.modoPesaje)) state.modoPesaje = "normal";
  const modo = state.modoPesaje;
  const sinLicencia = state.licencia && !state.licencia.puedePesar;
  // Pendiente elegida: la pantalla muestra sus datos de entrada y solo deja capturar la salida.
  const salida = modo === "normal" ? pendientes.find(p => p.id === state.selectedPesadaId) : null;
  if (!salida) state.selectedPesadaId = null;
  const f = (key, html) => visible(key) ? html : "";
  const lbl = (key, porDefecto) => field(key)?.label || porDefecto;
  const proximoTicket = proximoTicketActual() || "-";

  const operacion = `
        ${f("tipo", inputField("tipo", lbl("tipo", "Tipo"), { type: "select", options: "<option>INGRESO</option><option>EGRESO</option>" }))}
        ${state.funciones.aduana ? `<div><label>Operación</label><select id="tipoOperacion" onchange="toggleAduana()">
          <option value="Normal">Normal</option><option value="Aduana">Aduana</option>
        </select></div>` : ""}`;
  const carga = `
        ${f("producto", inputField("producto", lbl("producto", "Producto"), { list: "dl_productos", alta: "productos" }))}
        ${f("cliente", inputField("cliente", lbl("cliente", "Cliente"), { list: "dl_clientes", alta: "clientes" }))}
        ${f("transporte", inputField("transporte", lbl("transporte", "Transporte"), { list: "dl_transportes", alta: "transportes" }))}
        ${f("procedencia", inputField("procedencia", lbl("procedencia", "Procedencia")))}
        ${f("destino", inputField("destino", lbl("destino", "Destino")))}
        ${f("comprobante", inputField("comprobante", lbl("comprobante", "Remito")))}
        ${f("taraPallets", inputField("taraPallets", lbl("taraPallets", "Tara pallets (kg)"), { type: "number", value: "0" }))}`;
  const vehiculo = `
        ${f("patenteChasis", inputField("patenteChasis", modo === "tara" ? "Patente / carro" : lbl("patenteChasis", "Patente chasis"), { blur: "autocompletarPatente()" }))}
        ${modo === "tara" ? `<div class="status-box"><span>Tara guardada</span><strong id="taraGuardada">-</strong></div>` : ""}
        ${f("patenteAcoplado", inputField("patenteAcoplado", lbl("patenteAcoplado", "Patente acoplado")))}
        ${f("conductor", inputField("conductor", lbl("conductor", "Conductor")))}
        ${f("tipoDocumento", inputField("tipoDocumento", lbl("tipoDocumento", "Tipo de documento"), { type: "select", options: `<option></option>${opciones(TIPOS_DOCUMENTO)}` }))}
        ${f("documento", inputField("documento", lbl("documento", "Documento")))}
        ${f("nacionalidad", inputField("nacionalidad", lbl("nacionalidad", "Nacionalidad")))}`;

  // Primero Vehículo y chofer: el primer dato que carga el operador es la patente (que además autocompleta el resto).
  const datosVehiculo = `
      ${seccion("Vehículo y chofer", vehiculo)}
      ${seccion("Operación", operacion)}
      ${seccion("Carga", carga)}
      ${bloqueAduana()}
      ${visible("observaciones") ? `<div style="margin-top:12px">${inputField("observaciones", lbl("observaciones", "Observaciones"), { type: "textarea" })}</div>` : ""}
      ${datalist("dl_clientes", clientes.map(c => c.razonSocial))}
      ${datalist("dl_transportes", transportes.map(t => t.razonSocial))}
      ${datalist("dl_productos", productos.map(p => p.nombre))}`;

  const formulario = modo === "tomar" ? `
    <div class="card">
      <h3 style="margin-top:0">Tomar tara</h3>
      <p>Subí el vehículo vacío a la balanza (o capturá primero el peso). La tara queda guardada para pesar después con "Tara conocida".</p>
      <div class="grid2">
        <div><label>Patente / carro *</label><input id="tt_id" autocomplete="off" /></div>
        <div><label>Descripción</label><input id="tt_desc" placeholder="Batea, chasis, carro 12..." /></div>
      </div>
      ${bloqueManual("Cargar la tara a mano")}
      <p class="error" id="err"></p>
      <div class="btn-row"><button class="btn-primary" onclick="tomarTara()">Tomar tara</button></div>
      <p id="ultimo"></p>
    </div>` : `
    <div class="card">
      ${salida ? `
      <div class="salida-head">
        <div><h3 style="margin:0">Salida del ticket ${esc(salida.ticketNumero)}</h3>
          <p style="margin:4px 0 0">${esc(empresaName(salida.empresaId))} · entrada ${esc(hora(salida.entradaEn))} · ${salida.pesoEntrada} kg${salida.entradaManualPor ? " (manual)" : ""}</p></div>
        <span class="pill pill-warn">Pesada de salida</span>
      </div>` : `<div class="form-head">
        <h3 style="margin:0">${modo === "tara" ? "Pesar con tara conocida" : "Datos del ticket"}</h3>
        <span class="pill pill-ticket">Próximo ticket: <strong id="formProximoTicket">${esc(proximoTicket)}</strong></span>
      </div>`}
      ${datosVehiculo}
      ${modo === "tara" && puedeManual() ? `
      <details style="margin-top:12px"><summary>Usar otra tara (ingreso manual)</summary>
        <div class="grid2" style="margin-top:8px"><div><label>Tara (kg)</label><input id="tc_tara" type="number" min="1" /></div></div>
      </details>` : ""}
      ${bloqueManual(modo === "tara" ? "Cargar el bruto a mano" : "Cargar el peso a mano")}
      <p class="error" id="err"></p>
      <div class="btn-row">
        ${modo === "tara"
          ? `<button class="btn-primary" onclick="guardarTaraConocida()" ${sinLicencia ? "disabled" : ""}>Aceptar pesada</button>`
          : salida
            ? `<button class="btn-primary" onclick="guardarSalida()">Aceptar salida</button>
               <button class="btn" onclick="selectPesada('${salida.id}')">Cancelar</button>`
            : `<button class="btn-primary" onclick="guardarEntrada()" ${sinLicencia ? "disabled" : ""}>Aceptar entrada</button>`}
      </div>
      <p id="ultimo"></p>
    </div>`;

  document.getElementById("main").innerHTML = `
    <div class="page-head page-head-pesaje">
      <div>
        <h2>Pesaje</h2>
        <p>${modo === "tara" ? "Capturá el bruto, completá los datos y aceptá: se descuenta la tara guardada." : modo === "tomar" ? "Guardá la tara de un vehículo o carro." : salida ? "Salida: capturá el peso y aceptá." : "Entrada: capturá el peso, completá la patente y los datos, y aceptá. Para la salida, elegí el ticket en Pendientes."}</p>
      </div>
      <div id="scalePill">${pillForScale(state.scale.estado)}</div>
    </div>
    ${sinLicencia ? `
    <div class="card" style="border-left:4px solid #c0392b;margin-bottom:12px">
      <strong>${esc(state.licencia.mensaje)}</strong>
      <p style="margin:6px 0 0">ID de máquina: <code>${esc(state.licencia.maquina)}</code>. No se pueden registrar entradas nuevas; sí cerrar las salidas pendientes.</p>
    </div>` : ""}
    <div class="peso-fijo">
      <div class="card peso-card">
        <div class="displays ${state.balanzas.length > 1 ? "dos" : ""}" id="displays">${balanzasEnPantalla().map(displayBalanza).join("")}</div>
        ${state.funciones.automatizacion ? `<div id="auto" class="auto">${bloqueAutomatizacion()}</div>` : ""}
        <div class="captura" id="captura">${bloqueCaptura()}</div>
        <div class="peso-pie">
          <div class="peso-meta">
            ${modo !== "tomar" ? `<div class="status-box"><span>Empresa en curso</span>${selectorEmpresa(salida?.empresaId)}</div>
            <div class="status-box status-ticket"><span>${salida ? "Ticket en salida" : "Próximo ticket"}</span><strong id="proximoTicketValor">${esc(salida ? salida.ticketNumero : proximoTicket)}</strong></div>` : `<div class="status-box"><span>Modo</span><strong>Tomar tara</strong></div>`}
          </div>
          <div class="peso-ayuda">
            ${state.balanzas.length > 1 ? `<button class="btn" onclick="intercambiarBalanzas()" title="Pasa la balanza de la izquierda a la derecha">⇄ Intercambiar lados</button>
            <span>Tocá un display para elegir balanza.</span>` : ""}
            ${state.funciones.exigirEstable ? `<span>Solo con peso estable.</span>` : ""}
          </div>
        </div>
      </div>
    </div>
    ${state.funciones.automatizacion ? `<div id="camaras" class="camaras" style="margin-bottom:12px"></div>` : ""}
    ${modosPesaje().length > 1 ? `<div class="tabs">${modosPesaje().map(([id, label]) =>
      `<button class="tab ${modo === id ? "active" : ""}" onclick="cambiarModoPesaje('${id}')">${label}</button>`).join("")}</div>` : ""}
    ${formulario}
    ${modo === "normal" ? `
    <div class="card pendientes-card">
      <div class="pendientes-head">
        <h3 style="margin:0" id="pendientesTitulo">${tituloPendientes()}</h3>
        <input id="pendQ" class="pendientes-buscar" type="search" autocomplete="off"
          placeholder="Buscar por patente, ticket, cliente, producto, chofer, transporte, remito..."
          value="${esc(state.pendientesQ || "")}" oninput="filtrarPendientes(this.value)"
          onkeydown="if(event.key==='Escape'&&this.value){event.preventDefault();filtrarPendientes('');this.value=''}" />
      </div>
      <div class="tabla-scroll" id="pendientesTabla">${tablaPendientesHtml()}</div>
    </div>` : ""}
  `;
  if (salida) mostrarEntrada(salida);
}

function textoBusquedaPendiente(p) {
  const a = p.aduana || {};
  return normalizar([
    p.ticketNumero, empresaName(p.empresaId), p.patenteChasis, p.patenteAcoplado,
    p.conductor, p.documento, p.tipoDocumento, p.nacionalidad,
    p.cliente, p.transporte, p.producto, p.procedencia, p.destino, p.comprobante, p.observaciones,
    p.pesoEntrada, hora(p.entradaEn), p.tipo, p.tipoOperacion,
    a.contenedorId, a.exportador, a.exportadorCuit, a.ata, a.ataCuit, a.operacionId, a.destinacionAduanera, a.bultoId
  ].filter(v => v != null && v !== "").join(" "));
}

function pendientesFiltradas() {
  const palabras = normalizar(state.pendientesQ || "").trim().split(/\s+/).filter(Boolean);
  if (!palabras.length) return state.pendientes;
  return state.pendientes.filter(p => {
    const txt = textoBusquedaPendiente(p);
    return palabras.every(w => txt.includes(w));
  });
}

function tituloPendientes() {
  const total = state.pendientes.length;
  const filtradas = pendientesFiltradas().length;
  return state.pendientesQ?.trim()
    ? `Pendientes de salida (${filtradas} de ${total})`
    : `Pendientes de salida (${total})`;
}

function filtrarPendientes(q) {
  state.pendientesQ = q;
  const t = document.getElementById("pendientesTitulo");
  if (t) t.textContent = tituloPendientes();
  const cont = document.getElementById("pendientesTabla");
  if (cont) cont.innerHTML = tablaPendientesHtml();
}

function tablaPendientesHtml() {
  const lista = pendientesFiltradas();
  const mostrarPallets = visible("taraPallets");
  const variasEmpresas = state.empresas.filter(e => e.activa !== false).length > 1;
  return `<table class="tabla-pendientes">
    <tr>
      <th>Ticket</th><th>Patente / Chofer</th><th>Cliente</th><th>Producto</th>
      <th>Transporte / Detalle</th><th>Entrada</th><th></th>
    </tr>
    ${lista.length ? lista.map(p => {
      const sel = state.selectedPesadaId === p.id;
      const detalle = [
        p.comprobante ? `Rem. ${p.comprobante}` : "",
        p.aduana?.contenedorId ? `Cont. ${p.aduana.contenedorId}` : "",
        [p.procedencia, p.destino].filter(Boolean).join(" → ")
      ].filter(Boolean).join(" · ");
      const subEntrada = [
        hora(p.entradaEn),
        mostrarPallets && p.taraPallets ? `Pal. ${p.taraPallets} kg` : ""
      ].filter(Boolean).join(" · ");
      return `<tr class="${sel ? "selected" : ""}" onclick="selectPesada('${p.id}')">
        <td class="pend-nw">
          <strong>${sel ? "● " : ""}${esc(p.ticketNumero)}</strong>${p.tipoOperacion === "Aduana" ? ` <span class="pill pill-info pill-xs">Aduana</span>` : ""}
          ${variasEmpresas ? `<div class="pend-sub">${esc(empresaName(p.empresaId))}</div>` : ""}
        </td>
        <td>
          <strong>${esc(p.patenteChasis)}</strong>${p.patenteAcoplado ? ` <span class="pend-sec">· ${esc(p.patenteAcoplado)}</span>` : ""}
          ${p.conductor ? `<div class="pend-sub">${esc(p.conductor)}</div>` : ""}
        </td>
        <td>${esc(p.cliente || "-")}</td>
        <td>${esc(p.producto || "-")}</td>
        <td>
          ${p.transporte ? `<div>${esc(p.transporte)}</div>` : ""}
          ${detalle ? `<div class="pend-sub">${esc(detalle)}</div>` : (!p.transporte ? "-" : "")}
        </td>
        <td class="pend-nw">
          <strong>${p.pesoEntrada.toLocaleString("es-AR")}</strong> kg${p.entradaManualPor ? " ✎" : ""}
          ${subEntrada ? `<div class="pend-sub">${esc(subEntrada)}</div>` : ""}
        </td>
        <td class="pend-acciones"><div class="btn-row" style="margin:0;gap:6px;flex-wrap:nowrap">${botonesTicket(p.id)}${can("pesaje.anular") ? `<button class="btn-danger" onclick="event.stopPropagation();anularPesada('${p.id}')">Anular</button>` : ""}</div></td>
      </tr>`;
    }).join("") : `<tr><td colspan="7">${state.pendientes.length ? "Ninguna pendiente coincide con la búsqueda." : "No hay entradas pendientes."}</td></tr>`}
  </table>`;
}

/** Campos del formulario de pesaje y de dónde sale cada valor en una pesada. */
const CAMPOS_PANTALLA = [
  ["tipo", p => p.tipo], ["tipoOperacion", p => p.tipoOperacion], ["producto", p => p.producto], ["cliente", p => p.cliente],
  ["transporte", p => p.transporte], ["procedencia", p => p.procedencia], ["destino", p => p.destino], ["comprobante", p => p.comprobante],
  ["taraPallets", p => p.taraPallets], ["patenteChasis", p => p.patenteChasis], ["patenteAcoplado", p => p.patenteAcoplado],
  ["conductor", p => p.conductor], ["tipoDocumento", p => p.tipoDocumento], ["documento", p => p.documento],
  ["nacionalidad", p => p.nacionalidad], ["observaciones", p => p.observaciones],
  ["ad_escenario", p => p.aduana?.escenario], ["ad_operacionId", p => p.aduana?.operacionId], ["ad_exportador", p => p.aduana?.exportador],
  ["ad_exportadorCuit", p => p.aduana?.exportadorCuit], ["ad_ata", p => p.aduana?.ata], ["ad_ataCuit", p => p.aduana?.ataCuit],
  ["ad_destinacion", p => p.aduana?.destinacionAduanera],
  ["ad_contenedorId", p => p.aduana?.contenedorId], ["ad_contenedorTara", p => p.aduana?.contenedorTara],
  ["ad_bultoId", p => p.aduana?.bultoId], ["ad_bultoBruto", p => p.aduana?.bultoBruto], ["ad_bultoTara", p => p.aduana?.bultoTara]
];

/** Carga en pantalla los datos de la entrada y los deja de solo lectura (salvo la tara de pallets, que se ajusta al salir). */
function mostrarEntrada(p) {
  for (const [id, leer] of CAMPOS_PANTALLA) {
    const el = document.getElementById(id);
    if (!el) continue;
    el.value = leer(p) ?? "";
    if (id !== "taraPallets") el.disabled = true;
  }
  // En la salida los datos son los de la entrada: no se cambian ni se agregan.
  document.querySelectorAll(".btn-alta").forEach(b => b.disabled = true);
  toggleAduana();
  document.querySelector(".salida-head")?.scrollIntoView({ block: "center" });
}

async function bajarArchivo(path) {
  const res = await fetch(path, { headers: { Authorization: "Bearer " + state.token } });
  if (!res.ok) throw new Error((await res.text()) || res.statusText);
  const nombre = /filename="?([^";]+)"?/.exec(res.headers.get("content-disposition") || "")?.[1] || "archivo";
  return { blob: await res.blob(), nombre };
}

async function verTicket(id) {
  try {
    const { blob, nombre } = await bajarArchivo(`/api/pesadas/${id}/ticket`);
    const url = URL.createObjectURL(blob);
    // Se abre en el visor (desde ahí se imprime o reimprime). Si el navegador bloquea la ventana, se descarga.
    if (!window.open(url, "_blank")) guardarComo(url, nombre);
  } catch (e) {
    toast("No se pudo generar el ticket: " + limpiarError(e), "err");
  }
}

async function imprimirTermica(id) {
  try {
    await api(`/api/pesadas/${id}/termica`, { method: "POST" });
    toast("Ticket enviado a la impresora");
  } catch (e) {
    toast(limpiarError(e), "err");
  }
}

function guardarComo(url, nombre) {
  const a = document.createElement("a");
  a.href = url;
  a.download = nombre;
  document.body.appendChild(a);
  a.click();
  a.remove();
}


function usaTermica() {
  return state.funciones.termica && can("pesaje.reimprimir");
}

/** Al cerrar una pesada: el ticket se abre solo y, si hay térmica, también sale por ahí. */
function ticketAlCerrar(pesada) {
  verTicket(pesada.id);
  if (usaTermica()) imprimirTermica(pesada.id);
}

function botonesTicket(id) {
  return `<button class="btn" onclick="event.stopPropagation();verTicket('${id}')">Ticket</button>` +
    (usaTermica() ? `<button class="btn" onclick="event.stopPropagation();imprimirTermica('${id}')">Térmica</button>` : "");
}

function hora(iso) {
  if (!iso) return "";
  const d = new Date(iso);
  return d.toLocaleDateString() === new Date().toLocaleDateString()
    ? d.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })
    : d.toLocaleString([], { dateStyle: "short", timeStyle: "short" });
}

function cambiarModoPesaje(modo) {
  state.modoPesaje = modo;
  state.selectedPesadaId = null;
  renderPesaje();
}

/**
 * Capturar el peso primero y aceptar después: el operador congela el peso apenas el camión está quieto y completa
 * los datos sin apuro. La captura queda en pantalla aunque se elija otra pendiente; se descarta al aceptar o a mano.
 */
/** Teclas de captura: F2 la balanza de la izquierda, F3 la de la derecha. */
const TECLAS_CAPTURA = ["F2", "F3"];

/** Con varias balanzas, un botón por cada una (en el orden de los displays): se elige de cuál capturar. */
function botonesCaptura(clase, texto, accion = "capturarPeso") {
  const lista = balanzasEnPantalla();
  if (lista.length <= 1) {
    return `<button class="${clase}" onclick="${accion}()">${texto} <small>(F2)</small></button>`;
  }
  return lista.map((b, i) => `<button class="${clase}" onclick="${accion}('${b.balanzaId}')">${texto}: ${esc(b.nombre || "Balanza " + (i + 1))}
    ${TECLAS_CAPTURA[i] ? `<small>(${TECLAS_CAPTURA[i]})</small>` : ""}</button>`).join("");
}

function modosCaptura() {
  const modos = [["simple", "Completo"]];
  if (state.funciones.pesajePartes) modos.push(["partes", "En partes"]);
  if (state.funciones.pesajeEjes) modos.push(["ejes", "Por ejes"]);
  return modos;
}

function bloqueCaptura() {
  const modos = state.modoPesaje === "normal" ? modosCaptura() : [["simple", ""]];
  if (!modos.some(([id]) => id === state.capturaModo)) state.capturaModo = "simple";
  const selector = modos.length > 1 ? `<div class="captura-modos" role="group" aria-label="Cómo se pesa">${modos.map(([id, label]) =>
    `<button type="button" class="tab ${state.capturaModo === id ? "active" : ""}" onclick="cambiarCapturaModo('${id}')">${label}</button>`).join("")}</div>` : "";
  if (state.capturaModo === "partes") return selector + bloquePartes();
  if (state.capturaModo === "ejes") return selector + bloqueEjes();
  return selector + bloqueCapturaSimple();
}

function guardarCapturaLocal(c) {
  state.captura = c;
  try {
    if (c) localStorage.setItem("bfs_captura", JSON.stringify(c));
    else localStorage.removeItem("bfs_captura");
  } catch {}
}

async function restaurarCapturaGuardada() {
  if (!can("pesaje.operar") || state.modo === "Nube") return;
  let raw = null;
  try { raw = localStorage.getItem("bfs_captura"); } catch {}
  if (!raw) return;
  try {
    const guardada = JSON.parse(raw);
    if (!guardada?.id) return guardarCapturaLocal(null);
    state.captura = await api(`/api/pesadas/capturas/${encodeURIComponent(guardada.id)}`);
  } catch {
    guardarCapturaLocal(null);
  }
}

function bloqueCapturaSimple() {
  const c = state.captura;
  if (!c) {
    return `${botonesCaptura("btn-primary btn-capturar", "Capturar peso")}
      <span class="captura-ayuda">Capturá el peso con el camión quieto; queda guardado aunque se retire.</span>`;
  }
  return `<div class="captura-ok"><span>Peso capturado</span><strong>${c.kilogramos.toLocaleString("es-AR")} kg</strong>
      <small>${esc(c.balanza || "")} · ${esc(hora(c.en))} · fijo aunque se retire el camión</small></div>
    ${botonesCaptura("btn", "Volver a capturar")}
    <button class="btn" onclick="descartarCaptura()">Descartar</button>`;
}

function pintarCaptura() {
  const el = document.getElementById("captura");
  if (el) el.innerHTML = bloqueCaptura();
}

/** Con <balanzaId>, captura de esa balanza y la deja "en uso"; sin él, de la que está en uso. */
async function capturarPeso(balanzaId) {
  if (balanzaId && balanzaId !== balanzaActual().balanzaId) elegirBalanza(balanzaId);
  try {
    const c = await api("/api/pesadas/capturar", {
      method: "POST",
      body: JSON.stringify({ balanzaId: balanzaId || balanzaActual().balanzaId || null })
    });
    guardarCapturaLocal(c);
    pintarCaptura();
    pintarPeso();
    api("/api/pesadas/proximos-tickets").then(t => {
      state.proximosTickets = t || {};
      actualizarProximoTicketUI();
    }).catch(() => {});
    const err = document.getElementById("err");
    if (err) err.textContent = "";
  } catch (e) {
    toast(limpiarError(e), "err");
  }
}

function descartarCaptura() {
  guardarCapturaLocal(null);
  pintarCaptura();
  pintarPeso();
}

function cambiarCapturaModo(modo) {
  if (state.partes.length && modo !== state.capturaModo && !confirm("Se descartan las partes ya capturadas. ¿Seguir?")) return;
  state.capturaModo = modo;
  state.partes = [];
  guardarCapturaLocal(null);
  pintarCaptura();
  pintarPeso();
}

/** Balanza corta: chasis, acoplado y, si hiciera falta, más partes. Vale la suma. */
const NOMBRES_PARTES = ["Chasis", "Acoplado"];


function filasPartes() {
  return state.partes.map((p, i) => `<tr><td>${esc(p.etiqueta)}</td><td style="text-align:right">${kg(p.kilogramos)}${p.manual ? " ✎" : ""}</td></tr>`).join("");
}

function bloquePartes() {
  const total = state.partes.reduce((s, p) => s + p.kilogramos, 0);
  const siguiente = NOMBRES_PARTES[state.partes.length] || `Parte ${state.partes.length + 1}`;
  return `<div class="partes">
      <table class="partes-tabla">${filasPartes() || `<tr><td colspan="2">Todavía no se capturó ninguna parte.</td></tr>`}
        <tr class="partes-total"><td>Total${state.partes.length < 2 ? " (faltan partes)" : ""}</td><td style="text-align:right">${kg(total)}</td></tr></table>
      <div class="btn-row" style="margin-top:8px">
        ${botonesCaptura("btn-primary btn-capturar", `Capturar ${siguiente.toLowerCase()}`, "capturarParte")}
        ${state.partes.length ? `<button class="btn" onclick="deshacerParte()">Deshacer la última</button>` : ""}
      </div>
      <span class="captura-ayuda">Balanza corta: subí primero el chasis y capturalo, después el acoplado. El peso es la suma.</span>
    </div>`;
}

/** Por ejes: grupos del vehículo con sus ejes, y cada grupo comparado con su límite. */
function tipoEje(id) { return (state.funciones.tiposEje || []).find(t => t.id === id); }

const CONFIGURACIONES_EJES = [
  ["Camión 2 ejes", ["simple_simple", "simple_doble"]],
  ["Camión 3 ejes (tándem)", ["simple_simple", "tandem_doble"]],
  ["Semirremolque 5 ejes", ["simple_simple", "simple_doble", "tandem_triple"]],
  ["Semirremolque 5 ejes (2 tándem)", ["simple_simple", "tandem_doble", "tandem_doble"]],
  ["Semirremolque 6 ejes", ["simple_simple", "tandem_doble", "tandem_triple"]],
  ["Chasis y acoplado 4 ejes", ["simple_simple", "simple_doble", "simple_doble", "simple_doble"]],
  ["Chasis y acoplado 5 ejes", ["simple_simple", "tandem_doble", "simple_doble", "simple_doble"]]
];

/** Recorre los grupos y reparte los ejes capturados: [{ tipo, ejes: [kg|null], total, limite, exceso }]. */
function gruposConPesos() {
  let i = 0;
  return state.grupos.map(id => {
    const t = tipoEje(id) || { nombre: id, ejes: 1, limite: 0 };
    const ejes = Array.from({ length: t.ejes }, () => state.partes[i++]?.kilogramos ?? null);
    const total = ejes.reduce((s, k) => s + (k || 0), 0);
    const completo = ejes.every(k => k != null);
    return { id, nombre: t.nombre, ejes, total, limite: t.limite, exceso: completo ? Math.max(0, total - t.limite) : 0, desde: i - t.ejes };
  });
}

function totalEjes() { return state.grupos.reduce((s, id) => s + (tipoEje(id)?.ejes || 1), 0); }

function bloqueEjes() {
  const grupos = gruposConPesos();
  const cantidad = totalEjes();
  const total = state.partes.reduce((s, p) => s + p.kilogramos, 0);
  const limiteTotal = state.funciones.ejesLimiteTotal || 0;
  const completo = state.partes.length >= cantidad;
  const excesoTotal = completo ? Math.max(0, total - limiteTotal) : 0;
  const excesos = grupos.filter(g => g.exceso > 0);
  const preset = CONFIGURACIONES_EJES.findIndex(([, g]) => g.join() === state.grupos.join());
  return `<div class="ejes">
      <div class="ejes-config">
        <label for="ej_config">Vehículo</label>
        <select id="ej_config" onchange="elegirConfiguracionEjes(this.value)" ${state.partes.length ? "disabled" : ""}>
          ${CONFIGURACIONES_EJES.map(([nombre], i) => `<option value="${i}" ${i === preset ? "selected" : ""}>${esc(nombre)}</option>`).join("")}
          <option value="-1" ${preset < 0 ? "selected" : ""}>Otra (armada a mano)</option>
        </select>
        ${state.partes.length ? "" : `<span class="ejes-armar">${(state.funciones.tiposEje || []).map(t =>
          `<button type="button" class="btn" onclick="agregarGrupoEjes('${t.id}')" title="Agregar ${esc(t.nombre)}">+ ${esc(t.nombre)}</button>`).join("")}
          ${state.grupos.length > 1 ? `<button type="button" class="btn" onclick="quitarGrupoEjes()">Quitar el último grupo</button>` : ""}</span>`}
      </div>
      <table class="partes-tabla">
        <tr><th>Grupo</th><th>Ejes</th><th style="text-align:right">Peso</th><th style="text-align:right">Límite</th><th style="text-align:right">Exceso</th></tr>
        ${grupos.map((g, n) => `<tr class="${g.exceso ? "ejes-exceso" : ""}">
          <td>${n + 1}. ${esc(g.nombre)}</td>
          <td>${g.ejes.map((k, j) => `<span class="eje ${k == null && g.desde + j === state.partes.length ? "eje-siguiente" : ""}">${k == null ? "—" : k.toLocaleString("es-AR")}</span>`).join("")}</td>
          <td style="text-align:right">${kg(g.total)}</td><td style="text-align:right">${kg(g.limite)}</td>
          <td style="text-align:right">${g.exceso ? kg(g.exceso) : ""}</td></tr>`).join("")}
        <tr class="partes-total ${excesoTotal ? "ejes-exceso" : ""}"><td>Total</td><td>${state.partes.length} de ${cantidad} ejes</td>
          <td style="text-align:right">${kg(total)}</td><td style="text-align:right">${kg(limiteTotal)}</td><td style="text-align:right">${excesoTotal ? kg(excesoTotal) : ""}</td></tr>
      </table>
      ${excesos.length || excesoTotal ? `<p class="ejes-alerta" role="alert">Exceso de peso: ${[...excesos.map(g => `${esc(g.nombre)} +${kg(g.exceso)}`),
        ...(excesoTotal ? [`total +${kg(excesoTotal)}`] : [])].join(" · ")}${state.funciones.ejesExceso === "ticket" ? " (se marca en el ticket)" : ""}</p>` : ""}
      <div class="btn-row" style="margin-top:8px">
        ${completo ? `<span class="captura-ayuda">Todos los ejes capturados.</span>` : botonesCaptura("btn-primary btn-capturar", `Capturar eje ${state.partes.length + 1}`, "capturarParte")}
        ${state.partes.length ? `<button class="btn" onclick="deshacerParte()">Deshacer el último</button>` : ""}
      </div>
      <span class="captura-ayuda">Avanzá el camión y capturá cada eje cuando esté quieto sobre la balanza, desde el de adelante.</span>
    </div>`;
}

function elegirConfiguracionEjes(i) {
  const c = CONFIGURACIONES_EJES[Number(i)];
  if (c) state.grupos = [...c[1]];
  pintarCaptura();
}

function agregarGrupoEjes(id) {
  state.grupos.push(id);
  pintarCaptura();
}

function quitarGrupoEjes() {
  state.grupos.pop();
  pintarCaptura();
}

/** Una parte o un eje más: de la balanza (sin pedir el camión entero arriba) o, si se escribió, el peso a mano. */
async function capturarParte(balanzaId) {
  if (state.capturaModo === "ejes" && state.partes.length >= totalEjes()) return toast("Ya están todos los ejes", "err");
  const etiqueta = state.capturaModo === "ejes"
    ? `Eje ${state.partes.length + 1}`
    : NOMBRES_PARTES[state.partes.length] || `Parte ${state.partes.length + 1}`;
  const aMano = val("pm_kg");
  if (aMano) {
    state.partes.push({ id: null, kilogramos: Number(aMano), manual: true, etiqueta });
    document.getElementById("pm_kg").value = "";
    pintarCaptura();
    return;
  }
  if (balanzaId && balanzaId !== balanzaActual().balanzaId) elegirBalanza(balanzaId);
  try {
    const c = await api("/api/pesadas/capturar", {
      method: "POST",
      body: JSON.stringify({ balanzaId: balanzaId || balanzaActual().balanzaId || null, parcial: true })
    });
    state.partes.push({ id: c.id, kilogramos: c.kilogramos, manual: false, etiqueta });
    pintarCaptura();
  } catch (e) {
    toast(limpiarError(e), "err");
  }
}

function deshacerParte() {
  state.partes.pop();
  pintarCaptura();
}

/** Lo que va en el pedido cuando se pesó en partes o por ejes. */
function partesParaGuardar() {
  if (state.capturaModo === "partes" && state.partes.length < 2) throw new Error("Capturá el chasis y el acoplado (al menos dos partes)");
  if (state.capturaModo === "ejes" && state.partes.length < totalEjes()) throw new Error(`Faltan ejes: se capturaron ${state.partes.length} de ${totalEjes()}`);
  let i = 0;
  const grupoDe = [];
  state.grupos.forEach((id, g) => { for (let k = 0; k < (tipoEje(id)?.ejes || 1); k++) grupoDe[i++] = { tipoEje: id, grupo: g }; });
  return state.partes.map((p, n) => ({
    capturaId: p.id,
    kilogramos: p.manual ? p.kilogramos : null,
    etiqueta: p.etiqueta,
    ...(state.capturaModo === "ejes" ? grupoDe[n] : {})
  }));
}

/** Lo que va en el pedido: el peso manual si se escribió; si no, el capturado (que es obligatorio). */
function pesoParaGuardar() {
  const m = manual();
  if (state.modoPesaje === "normal" && state.capturaModo !== "simple") {
    return { peso: null, clave: m.clave, capturaId: null, modoCaptura: state.capturaModo, partes: partesParaGuardar() };
  }
  if (m.peso == null && !state.captura) throw new Error("Primero capturá el peso (botón Capturar peso, F2 o F3)");
  return { ...m, capturaId: m.peso == null ? state.captura.id : null };
}

/** Después de guardar: se limpia lo capturado (el modo y el vehículo quedan para el próximo camión). */
function limpiarCapturas() {
  guardarCapturaLocal(null);
  state.partes = [];
}

/** Si la captura venció (o el servidor se reinició), se borra para que se vuelva a capturar. */
function revisarCaptura(e) {
  if (/capturado venci/i.test(limpiarError(e))) descartarCaptura();
}

document.addEventListener("keydown", e => {
  const i = TECLAS_CAPTURA.indexOf(e.key);
  if (i < 0 || state.view !== "pesaje" || !document.getElementById("captura")) return;
  const lista = balanzasEnPantalla();
  if (lista.length <= 1 && i > 0) return;
  e.preventDefault();
  const id = lista.length > 1 ? lista[i]?.balanzaId : undefined;
  if (state.modoPesaje === "normal" && state.capturaModo !== "simple") capturarParte(id);
  else capturarPeso(id);
});

function selectPesada(id) {
  state.selectedPesadaId = state.selectedPesadaId === id ? null : id;
  renderPesaje();
}

function collectPesaje() {
  const missing = fields().filter(f => f.visible && f.required && !val(f.key) && f.key !== "taraPallets");
  if (missing.length) throw new Error("Completá: " + missing.map(f => f.label).join(", "));
  const m = pesoParaGuardar();
  return {
    empresaId: empresaEnCurso(),
    balanzaId: balanzaActual().balanzaId || null,
    tipo: visible("tipo") ? val("tipo") : undefined,
    patenteChasis: val("patenteChasis"),
    patenteAcoplado: val("patenteAcoplado") || null,
    conductor: val("conductor") || null,
    documento: val("documento") || null,
    tipoDocumento: val("tipoDocumento") || null,
    nacionalidad: val("nacionalidad") || null,
    cliente: val("cliente") || null,
    transporte: val("transporte") || null,
    producto: val("producto") || null,
    procedencia: val("procedencia") || null,
    destino: val("destino") || null,
    comprobante: val("comprobante") || null,
    observaciones: val("observaciones") || null,
    taraPallets: visible("taraPallets") ? num("taraPallets") : 0,
    pesoEntrada: m.peso,
    claveManual: m.clave,
    capturaId: m.capturaId,
    modoCaptura: m.modoCaptura || null,
    partes: m.partes || null,
    ...datosAduana()
  };
}

function mostrarError(e) {
  const el = document.getElementById("err");
  if (el) el.textContent = limpiarError(e);
  else toast(limpiarError(e), "err");
}

async function guardarEntrada() {
  try {
    const pesada = await api("/api/pesadas/entrada", { method: "POST", body: JSON.stringify(collectPesaje()) });
    state.selectedPesadaId = null;
    limpiarCapturas();
    // Por ejes en una sola pasada el ticket ya sale cerrado (control de carga).
    const cerrada = pesada.estado === "SALIDA";
    toast(cerrada ? `Ticket ${pesada.ticketNumero} · total ${kg(pesada.pesoEntrada)}` : `Entrada ticket ${pesada.ticketNumero}`);
    if (cerrada) ticketAlCerrar(pesada);
    await renderPesaje();
    document.getElementById("ultimo").textContent = `${cerrada ? "Ticket" : "Entrada ticket"} ${pesada.ticketNumero} · ${pesada.pesoEntrada} kg`;
  } catch (e) {
    revisarCaptura(e);
    mostrarError(e);
  }
}

async function guardarSalida() {
  if (!state.selectedPesadaId) {
    document.getElementById("err").textContent = "Elegí un ticket de la lista de pendientes";
    return;
  }
  try {
    const m = pesoParaGuardar();
    const pesada = await api(`/api/pesadas/${state.selectedPesadaId}/salida`, {
      method: "POST",
      body: JSON.stringify({
        taraPallets: visible("taraPallets") ? num("taraPallets") : null,
        balanzaId: balanzaActual().balanzaId || null,
        pesoSalida: m.peso,
        claveManual: m.clave,
        capturaId: m.capturaId,
        modoCaptura: m.modoCaptura || null,
        partes: m.partes || null
      })
    });
    state.selectedPesadaId = null;
    limpiarCapturas();
    toast(`Ticket ${pesada.ticketNumero} cerrado · neto ${pesada.neto} kg`);
    ticketAlCerrar(pesada);
    await renderPesaje();
    document.getElementById("ultimo").textContent =
      `Ticket ${pesada.ticketNumero} cerrado · neto ${pesada.neto} kg (pallets ${pesada.taraPallets})` +
      (pesada.aduana?.contenedorNeto != null ? ` · contenedor neto ${pesada.aduana.contenedorNeto} kg` : "");
  } catch (e) {
    revisarCaptura(e);
    mostrarError(e);
  }
}

async function guardarTaraConocida() {
  try {
    const datos = collectPesaje();
    const taraManual = val("tc_tara") ? Number(val("tc_tara")) : null;
    const pesada = await api("/api/pesadas/tara-conocida", {
      method: "POST",
      body: JSON.stringify({ datos, taraManual, claveManual: datos.claveManual })
    });
    limpiarCapturas();
    toast(`Ticket ${pesada.ticketNumero} · neto ${pesada.neto} kg`);
    ticketAlCerrar(pesada);
    await renderPesaje();
    document.getElementById("ultimo").textContent =
      `Ticket ${pesada.ticketNumero} · bruto ${pesada.bruto} − tara ${pesada.tara} = neto ${pesada.neto} kg`;
  } catch (e) {
    revisarCaptura(e);
    mostrarError(e);
  }
}

async function tomarTara() {
  try {
    const m = manual();
    const tara = await api("/api/taras/tomar", {
      method: "POST",
      body: JSON.stringify({
        identificador: val("tt_id"),
        descripcion: val("tt_desc") || null,
        balanzaId: balanzaActual().balanzaId || null,
        pesoManual: m.peso,
        claveManual: m.clave,
        capturaId: m.peso == null ? (state.captura?.id || null) : null
      })
    });
    limpiarCapturas();
    toast(`Tara de ${tara.identificador}: ${tara.kilogramos} kg`);
    await renderPesaje();
    document.getElementById("ultimo").textContent = `Tara guardada: ${tara.identificador} · ${tara.kilogramos} kg`;
  } catch (e) {
    revisarCaptura(e);
    mostrarError(e);
  }
}

async function anularPesada(id) {
  const motivo = prompt("Motivo de la anulación");
  if (motivo == null) return;
  try {
    const p = await api(`/api/pesadas/${id}/anular`, { method: "POST", body: JSON.stringify({ motivo }) });
    toast(`Ticket ${p.ticketNumero} anulado`);
    if (state.selectedPesadaId === id) state.selectedPesadaId = null;
    render();
  } catch (e) {
    toast(limpiarError(e), "err");
  }
}

function retomarPesada(id) {
  state.modoPesaje = "normal";
  state.selectedPesadaId = id;
  go("pesaje");
}

function pillEstado(p) {
  if (p.estado === "ANULADA") return `<span class="pill pill-err" title="${esc((p.motivoAnulacion || "") + " · " + (p.anuladaPor || ""))}">Anulada</span>`;
  if (p.estado === "ENTRADA") return `<span class="pill pill-warn">Pendiente</span>`;
  return `<span class="pill pill-ok">Cerrada</span>`;
}

const CATALOGOS = {
  clientes: { titulo: "Clientes", campos: [["razonSocial", "Razón social"], ["cuit", "CUIT"]] },
  transportes: { titulo: "Transportes", campos: [["razonSocial", "Razón social"], ["cuit", "CUIT"], ["esAta", "Es ATA", "check"], ["codigoAta", "Código ATA"]] },
  productos: { titulo: "Productos", campos: [["nombre", "Nombre"], ["codigo", "Código"]] },
  exportadores: { titulo: "Exportadores", campos: [["razonSocial", "Razón social"], ["cuit", "CUIT"]], visible: () => state.funciones.aduana },
  taras: { titulo: "Taras conocidas", campos: [["identificador", "Patente / carro"], ["kilogramos", "Tara (kg)", "number"], ["descripcion", "Descripción"]], visible: () => state.funciones.taraConocida }
};

function catalogosVisibles() {
  return Object.entries(CATALOGOS).filter(([, c]) => !c.visible || c.visible() || isVendor());
}

async function renderCatalogos() {
  const tabs = catalogosVisibles();
  if (!tabs.some(([id]) => id === state.catTab)) state.catTab = tabs[0][0];
  const cat = CATALOGOS[state.catTab];
  const q = state.catQ || "";
  const items = await api(`/api/catalogos/${state.catTab}${q ? "?q=" + encodeURIComponent(q) : ""}`);
  state.catItems = items;
  const editando = items.find(x => x.id === state.catEdit);
  const editable = can("catalogos.editar");
  const celda = (item, [key, , tipo]) =>
    tipo === "check" ? (item[key] ? "Sí" : "No") : esc(item[key] ?? "");

  document.getElementById("main").innerHTML = `
    <div class="page-head"><div><h2>Catálogos</h2><p>Compartidos por las empresas de esta planta.</p></div></div>
    <div class="tabs">
      ${tabs.map(([id, c]) => `<button class="tab ${state.catTab === id ? "active" : ""}" onclick="goCatalogo('${id}')">${c.titulo}</button>`).join("")}
    </div>
    ${editable ? `
    <div class="card">
      <h3 style="margin-top:0">${editando ? "Editar" : "Nuevo"}</h3>
      <div class="grid2">
        ${cat.campos.map(([key, label, tipo]) => tipo === "check"
          ? `<div><label class="check"><input type="checkbox" id="cf_${key}" ${editando?.[key] ? "checked" : ""} /> ${label}</label></div>`
          : `<div><label>${label}</label><input id="cf_${key}" ${tipo ? `type="${tipo}"` : ""} value="${esc(editando?.[key] ?? "")}" /></div>`
        ).join("")}
      </div>
      <div class="btn-row">
        <button class="btn-primary" onclick="guardarCatalogo()">${editando ? "Guardar cambios" : "Agregar"}</button>
        ${editando ? `<button class="btn" onclick="editarCatalogo(null)">Cancelar</button>` : ""}
      </div>
    </div>` : ""}
    <div class="card">
      <input id="catQ" placeholder="Buscar..." value="${esc(q)}" oninput="buscarCatalogo(this.value)" />
      <table style="margin-top:14px">
        <tr>${cat.campos.map(([, label]) => `<th>${label}</th>`).join("")}${editable ? "<th></th>" : ""}</tr>
        ${items.length ? items.map(item => `<tr class="${item.id === state.catEdit ? "selected" : ""}">
          ${cat.campos.map(c => `<td>${celda(item, c)}</td>`).join("")}
          ${editable ? `<td><div class="btn-row" style="margin:0">
            <button class="btn" onclick="editarCatalogo('${item.id}')">Editar</button>
            <button class="btn-danger" onclick="borrarCatalogo('${item.id}')">Borrar</button>
          </div></td>` : ""}
        </tr>`).join("") : `<tr><td colspan="${cat.campos.length + 1}">Sin datos</td></tr>`}
      </table>
    </div>`;
  if (q) {
    const input = document.getElementById("catQ");
    input.focus();
    input.setSelectionRange(q.length, q.length);
  }
}

function goCatalogo(id) {
  state.catTab = id;
  state.catEdit = null;
  state.catQ = "";
  renderCatalogos();
}

function buscarCatalogo(q) {
  state.catQ = q;
  clearTimeout(buscarCatalogo._t);
  buscarCatalogo._t = setTimeout(renderCatalogos, 250);
}

function editarCatalogo(id) {
  state.catEdit = id;
  renderCatalogos();
}

async function guardarCatalogo() {
  const body = {};
  for (const [key, , tipo] of CATALOGOS[state.catTab].campos) {
    const el = document.getElementById("cf_" + key);
    body[key] = tipo === "check" ? el.checked : tipo === "number" ? Number(el.value || 0) : el.value;
  }
  try {
    const ruta = `/api/catalogos/${state.catTab}` + (state.catEdit ? `/${state.catEdit}` : "");
    await api(ruta, { method: state.catEdit ? "PUT" : "POST", body: JSON.stringify(body) });
    toast(state.catEdit ? "Cambios guardados" : "Agregado");
    state.catEdit = null;
    renderCatalogos();
  } catch (e) {
    toast(limpiarError(e), "err");
  }
}

async function borrarCatalogo(id) {
  if (!confirm("¿Borrar este registro?")) return;
  // Si la planta puso una clave de borrado, se pide acá.
  let clave = null;
  if (state.funciones.claveBorrado) {
    clave = prompt("Clave de borrado");
    if (clave == null) return;
  }
  try {
    await api(`/api/catalogos/${state.catTab}/${id}`, { method: "DELETE", headers: clave ? { "X-Clave-Borrado": clave } : {} });
    toast("Borrado");
    if (state.catEdit === id) state.catEdit = null;
    renderCatalogos();
  } catch (e) {
    toast(limpiarError(e), "err");
  }
}

function limpiarError(e) {
  return String(e.message || e).replace(/^"|"$/g, "");
}

async function renderPlanta() {
  const [empresas, aduana] = await Promise.all([
    api("/api/empresas"),
    api("/api/planta/aduana")
  ]);
  state.empresas = empresas;
  const campoEmp = (e, key, label) =>
    `<div><label>${label}</label><input id="em_${key}_${e.id}" value="${esc(e[key] ?? "")}" /></div>`;
  const fecha = d => d ? String(d).slice(0, 10) : "";
  document.getElementById("main").innerHTML = `
    <div class="page-head"><div><h2>Planta</h2><p>Datos que salen en los tickets y en los documentos de aduana.</p></div></div>
    ${empresas.map(e => `
    <div class="card">
      <h3 style="margin-top:0">${esc(e.nombre)}</h3>
      <div class="grid2">
        ${campoEmp(e, "nombre", "Razón social")}
        ${campoEmp(e, "cuit", "CUIT")}
        ${campoEmp(e, "direccion", "Dirección")}
        ${campoEmp(e, "localidad", "Localidad")}
        ${campoEmp(e, "provincia", "Provincia")}
        ${campoEmp(e, "telefono", "Teléfono")}
        ${campoEmp(e, "email", "Email")}
      </div>
      <div class="btn-row"><button class="btn-primary" onclick="guardarEmpresa('${e.id}')">Guardar empresa</button></div>
    </div>`).join("")}
    ${state.funciones.aduana || isVendor() ? `
    <div class="card">
      <h3 style="margin-top:0">Aduana</h3>
      <div class="grid2">
        <div><label>Código de aduana</label><input id="ad_codigo" value="${esc(aduana.codigo ?? "")}" /></div>
        <div><label>Código LOT</label><input id="ad_lot" value="${esc(aduana.lot ?? "")}" /></div>
        <div><label>Certificado balanza 1</label><input id="ad_c1" value="${esc(aduana.certificado1 ?? "")}" /></div>
        <div><label>Vence</label><input id="ad_c1v" type="date" value="${fecha(aduana.certificado1Vence)}" /></div>
        <div><label>Certificado balanza 2</label><input id="ad_c2" value="${esc(aduana.certificado2 ?? "")}" /></div>
        <div><label>Vence</label><input id="ad_c2v" type="date" value="${fecha(aduana.certificado2Vence)}" /></div>
      </div>
      <div class="btn-row"><button class="btn-primary" onclick="guardarAduana()">Guardar aduana</button></div>
    </div>` : ""}
    ${state.funciones.ingresoManual ? `
    <div class="card">
      <h3 style="margin-top:0">Clave de ingreso manual</h3>
      <p>${state.funciones.claveManual ? "Hay una clave configurada." : "Sin clave: alcanza con el permiso de supervisor."} Dejala vacía para quitarla.</p>
      <div class="grid2"><div><label>Nueva clave</label><input id="cm_clave" type="password" autocomplete="new-password" /></div></div>
      <div class="btn-row"><button class="btn-primary" onclick="guardarClaveManual()">Guardar clave</button></div>
    </div>` : ""}
    <div class="card">
      <h3 style="margin-top:0">Clave de corrección de pesadas</h3>
      <p>Para corregir datos de un ticket (nunca los pesos) desde Pesadas → Corregir. Queda registrado quién lo cambió, cuándo y por qué.
        ${state.funciones.claveCorreccion ? "Hay una clave configurada." : "Sin clave: alcanza con el permiso de supervisor."} Dejala vacía para quitarla.</p>
      <div class="grid2"><div><label>Nueva clave</label><input id="cc_clave" type="password" autocomplete="new-password" /></div></div>
      <div class="btn-row"><button class="btn-primary" onclick="guardarClaveCorreccion()">Guardar clave</button></div>
    </div>
    <div class="card">
      <h3 style="margin-top:0">Clave para borrar de los catálogos</h3>
      <p>Los operadores cargan y corrigen clientes, productos, transportes, etc. (también con el + de Pesaje). Con una clave, para borrar un registro hay que escribirla.
        ${state.funciones.claveBorrado ? "Hay una clave configurada." : "Sin clave: cualquiera que cargue catálogos puede borrar."} Dejala vacía para quitarla.</p>
      <div class="grid2"><div><label>Nueva clave</label><input id="cb_clave" type="password" autocomplete="new-password" /></div></div>
      <div class="btn-row"><button class="btn-primary" onclick="guardarClaveBorrado()">Guardar clave</button></div>
    </div>`;
}

async function guardarClaveBorrado() {
  try {
    const r = await api("/api/planta/clave-borrado", { method: "PUT", body: JSON.stringify({ clave: val("cb_clave") }) });
    state.funciones.claveBorrado = r.claveBorrado;
    toast(r.claveBorrado ? "Clave guardada" : "Clave quitada");
    renderPlanta();
  } catch (e) {
    toast(limpiarError(e), "err");
  }
}

async function guardarClaveCorreccion() {
  try {
    const r = await api("/api/planta/clave-correccion", { method: "PUT", body: JSON.stringify({ clave: val("cc_clave") }) });
    state.funciones.claveCorreccion = r.claveCorreccion;
    toast(r.claveCorreccion ? "Clave guardada" : "Clave quitada");
    renderPlanta();
  } catch (e) {
    toast(limpiarError(e), "err");
  }
}

/** <desdeTicket>: se guardó desde Configuración → Ticket y logo (vuelve ahí en vez de a Planta). */
async function guardarEmpresa(id, desdeTicket = false) {
  const body = {};
  for (const key of ["nombre", "cuit", "direccion", "localidad", "provincia", "telefono", "email"]) {
    body[key] = val(`em_${key}_${id}`);
  }
  try {
    await api(`/api/empresas/${id}`, { method: "PUT", body: JSON.stringify(body) });
    // Con una sola empresa, su razón social es también el nombre que se ve arriba y en el ingreso.
    if (state.empresas.filter(e => e.activa !== false).length === 1 && isVendor() && body.nombre) {
      await saveConfig({ cliente_nombre: body.nombre });
    }
    state.empresas = await api("/api/empresas");
    toast(desdeTicket ? "Encabezado guardado" : "Empresa guardada");
    if (desdeTicket) render(); else renderPlanta();
  } catch (e) {
    toast(limpiarError(e), "err");
  }
}

async function guardarAduana() {
  try {
    await api("/api/planta/aduana", { method: "PUT", body: JSON.stringify({
      codigo: val("ad_codigo"),
      lot: val("ad_lot"),
      certificado1: val("ad_c1"),
      certificado1Vence: val("ad_c1v") || null,
      certificado2: val("ad_c2"),
      certificado2Vence: val("ad_c2v") || null
    })});
    toast("Datos de aduana guardados");
  } catch (e) {
    toast(limpiarError(e), "err");
  }
}

async function guardarClaveManual() {
  try {
    const r = await api("/api/planta/clave-manual", { method: "PUT", body: JSON.stringify({ clave: val("cm_clave") }) });
    state.funciones.claveManual = r.claveManual;
    toast(r.claveManual ? "Clave guardada" : "Clave quitada");
    renderPlanta();
  } catch (e) {
    toast(limpiarError(e), "err");
  }
}

async function autocompletarPatente() {
  const patente = val("patenteChasis").trim();
  const taraEl = document.getElementById("taraGuardada");
  if (patente.length < 3) return;
  let r = null;
  try { r = await api(`/api/vehiculos/${encodeURIComponent(patente)}`); } catch {}
  if (taraEl) taraEl.textContent = r?.tara ? `${r.tara.kilogramos} kg${r.tara.descripcion ? " · " + r.tara.descripcion : ""}` : "Sin tara guardada";
  const d = r?.datos;
  if (!d) return;
  const fill = (id, value) => {
    const el = document.getElementById(id);
    if (el && !el.value && value) el.value = value;
  };
  fill("patenteAcoplado", d.patenteAcoplado);
  fill("conductor", d.conductor);
  fill("documento", d.documento);
  fill("transporte", d.transporte);
  fill("nacionalidad", d.nacionalidad);
  const tipoDoc = document.getElementById("tipoDocumento");
  if (tipoDoc && !tipoDoc.value && d.tipoDocumento) tipoDoc.value = d.tipoDocumento;
  toast("Datos de la última vez completados");
}

function rolOptions(selected) {
  const roles = !selected || state.rolesAsignables.includes(selected) ? state.rolesAsignables : [selected, ...state.rolesAsignables];
  return roles.map(r => `<option value="${r}" ${r === selected ? "selected" : ""}>${r}</option>`).join("");
}

function clienteOptions(selected) {
  return `<option value="">— (solo rol cliente) —</option>` + state.catalogos.clientes.map(c =>
    `<option value="${c.id}" ${c.id === selected ? "selected" : ""}>${esc(c.razonSocial)}</option>`
  ).join("");
}

async function renderUsuarios() {
  const [usuarios, roles, clientes] = await Promise.all([
    api("/api/usuarios"),
    api("/api/usuarios/roles"),
    api("/api/catalogos/clientes")
  ]);
  state.usuarios = usuarios;
  state.rolesAsignables = roles;
  state.catalogos.clientes = clientes;
  const empresas = empresaOptions("");
  document.getElementById("main").innerHTML = `
    <div class="page-head"><div><h2>Usuarios</h2><p>El ingreso es con email. Un usuario <b>cliente</b> solo ve las pesadas de su cliente.</p></div></div>
    <div class="card">
      <h3 style="margin-top:0">Nuevo usuario</h3>
      <div class="grid2">
        <div><label>Email</label><input id="uu" type="email" /></div>
        <div><label>Contraseña (mínimo 6)</label><input id="up" type="password" /></div>
        <div><label>Nombre</label><input id="un" /></div>
        <div><label>Rol</label><select id="ur">${rolOptions("operador")}</select></div>
        <div><label>Empresa</label><select id="ue">${empresas}</select></div>
        <div><label>Cliente</label><select id="uc">${clienteOptions("")}</select></div>
      </div>
      <div class="btn-row"><button class="btn-primary" onclick="altaUsuario()">Crear usuario</button></div>
    </div>
    <div class="card">
      <table>
        <tr><th>Email</th><th>Nombre</th><th>Rol</th><th>Empresa</th><th>Cliente</th><th>Estado</th><th></th></tr>
        ${state.usuarios.map(u => {
          const editable = state.rolesAsignables.includes(u.rol);
          const dis = editable ? "" : "disabled";
          return `<tr>
          <td>${esc(u.email)}</td>
          <td><input id="n_${u.id}" value="${esc(u.nombre)}" ${dis} /></td>
          <td><select id="r_${u.id}" ${dis}>${rolOptions(u.rol)}</select></td>
          <td><select id="e_${u.id}" ${dis}>${empresaOptions(u.empresaId || "")}</select></td>
          <td><select id="c_${u.id}" ${dis}>${clienteOptions(u.clienteId || "")}</select></td>
          <td>${u.activo ? `<span class="pill pill-ok">Activo</span>` : `<span class="pill pill-err">Inactivo</span>`}</td>
          <td>${editable ? `
            <div class="btn-row" style="margin:0">
              <button class="btn" onclick="guardarUsuario('${u.id}')">Guardar</button>
              <button class="btn" onclick="claveUsuario('${u.id}')">Clave</button>
              <button class="${u.activo ? "btn-danger" : "btn"}" onclick="toggleUsuario('${u.id}', ${u.activo})">${u.activo ? "Baja" : "Alta"}</button>
            </div>` : ""}
          </td>
        </tr>`;
        }).join("")}
      </table>
    </div>`;
}

async function usuarioAccion(fn, okMsg) {
  try {
    await fn();
    toast(okMsg);
    renderUsuarios();
  } catch (e) {
    toast(e.message.replace(/^"|"$/g, ""), "err");
  }
}

async function altaUsuario() {
  await usuarioAccion(() => api("/api/usuarios", { method: "POST", body: JSON.stringify({
    email: val("uu"),
    password: val("up"),
    nombre: val("un"),
    rol: val("ur"),
    empresaId: val("ue") || null,
    clienteId: val("uc") || null
  })}), "Usuario creado");
}

async function guardarUsuario(id) {
  await usuarioAccion(() => api(`/api/usuarios/${id}`, { method: "PUT", body: JSON.stringify({
    nombre: val("n_" + id),
    rol: val("r_" + id),
    empresaId: val("e_" + id) || null,
    clienteId: val("c_" + id) || null
  })}), "Usuario actualizado");
}

async function claveUsuario(id) {
  const password = prompt("Nueva contraseña (mínimo 6 caracteres)");
  if (!password) return;
  await usuarioAccion(() => api(`/api/usuarios/${id}`, { method: "PUT", body: JSON.stringify({ password, empresaId: val("e_" + id) || null }) }),
    "Contraseña actualizada");
}

async function toggleUsuario(id, activo) {
  await usuarioAccion(() => api(`/api/usuarios/${id}`, { method: "PUT", body: JSON.stringify({
    activo: !activo,
    empresaId: val("e_" + id) || null
  })}), activo ? "Usuario dado de baja" : "Usuario reactivado");
}

async function renderConfig() {
  if (!isVendor()) {
    document.getElementById("main").innerHTML = `<div class="card">Solo BFS puede cambiar esta configuración.</div>`;
    return;
  }
  const tabs = [
    ["general", "General"],
    ["balanza", "Balanzas"],
    ["campos", "Campos"],
    ["ticket", "Ticket y logo"],
    ...(esEstandar() ? [] : [["api", "API"]]),
    // La placa y las cámaras están en la portería: en la nube no hay automatización.
    ...(state.modo === "Nube" ? [] : [["automatizacion", "Automatización"]]),
    ["licencia", "Licencia"],
    ...(state.modo === "Nube" || esEstandar() ? [] : [["nube", "Nube"]])
  ];
  if (!tabs.some(([id]) => id === state.configTab)) state.configTab = "general";
  document.getElementById("main").innerHTML = `
    <div class="page-head"><div><h2>Configuración BFS</h2><p>Estas opciones las arma el instalador. El cliente no las ve.</p></div></div>
    <div class="tabs">
      ${tabs.map(([id, label]) => `<button class="tab ${state.configTab === id ? "active" : ""}" onclick="goConfig('${id}')">${label}</button>`).join("")}
    </div>
    <div id="configBody"></div>
  `;
  const body = {
    general: renderConfigGeneral,
    balanza: renderConfigBalanza,
    campos: renderConfigCampos,
    ticket: renderConfigTicket,
    api: renderConfigApi,
    automatizacion: renderConfigAutomatizacion,
    licencia: renderConfigLicencia,
    nube: renderConfigNube
  };
  body[state.configTab]();
}

function goConfig(tab) {
  state.configTab = tab;
  renderConfig();
}

function yesNo(id, value) {
  return `<select id="${id}">
    <option value="true" ${value === "true" ? "selected" : ""}>Sí</option>
    <option value="false" ${value !== "true" ? "selected" : ""}>No</option>
  </select>`;
}

async function renderConfigGeneral() {
  const c = state.config;
  let impresoras = [];
  try { impresoras = await api("/api/impresoras"); } catch {}
  const opcionesImpresora = [...new Set([...(impresoras || []), c.termica_impresora].filter(Boolean))];
  document.getElementById("configBody").innerHTML = `
    <div class="card">
      <div class="grid2">
        <div><label>Nombre del cliente</label><input id="c_nombre" value="${esc(c.cliente_nombre || "")}" /></div>
        <div><label>Tara pallets</label>${yesNo("c_pal", c.tara_pallets)}</div>
        <div><label>Tara conocida</label>${yesNo("c_tara", c.tara_conocida)}</div>
        <div><label>Aduana</label>${yesNo("c_adu", c.aduana)}</div>
        <div><label>Contenedores</label>${yesNo("c_cont", c.contenedores)}</div>
        <div><label>Escenario de aduana</label><select id="c_adu_esc">
          <option value="">Lo elige el operador</option>${opciones(ESCENARIOS_ADUANA.map(x => x), c.aduana_escenario || "")}
        </select></div>
        <div><label>Impresora térmica</label>${yesNo("c_term", c.termica)}</div>
        <div><label>Impresora térmica</label><select id="c_term_imp">
          <option value="">${impresoras.length ? "Elegir..." : "No se encontraron impresoras"}</option>
          ${opcionesImpresora.map(n => `<option value="${esc(n)}" ${n === c.termica_impresora ? "selected" : ""}>${esc(n)}</option>`).join("")}
        </select></div>
        <div><label>Ancho de papel</label>
          <select id="c_term_ancho">
            ${["58", "80"].map(a => `<option value="${a}" ${(c.termica_ancho || "80") === a ? "selected" : ""}>${a} mm</option>`).join("")}
          </select>
        </div>
        <div><label>Ingreso manual de peso</label>${yesNo("c_manual", c.ingreso_manual ?? "true")}</div>
        <div><label>Capturar solo con peso estable</label>${yesNo("c_estable", c.exigir_estable ?? "true")}</div>
        <div><label>Número de puesto (va en el ticket: 01-000123)</label><input id="c_puesto" type="number" min="1" max="99" value="${esc(c.puesto || "1")}" /></div>
        <div><label>Tickets solo con número (sigue la numeración del cliente; un solo puesto)</label>${yesNo("c_solonum", c.ticket_solo_numero || "false")}</div>
      </div>
      <h3 style="margin:22px 0 4px">Balanza corta y balanza de ejes</h3>
      <div class="grid2">
        <div><label>Pesaje en partes (balanza corta: chasis y acoplado por separado)</label>${yesNo("c_partes", c.pesaje_partes || "false")}</div>
        <div><label>Pesaje por ejes (control de Vialidad)</label>${yesNo("c_ejes", c.pesaje_ejes || "false")}</div>
        <div><label>Por ejes se pesa</label><select id="c_ejes_pasadas">
          ${opcionesValor([["dos", "En la entrada y/o la salida (hay neto)"], ["una", "En una sola pasada (control de carga, sin neto)"]], c.ejes_pasadas || "dos")}
        </select></div>
        <div><label>Si un eje o el total supera el límite</label><select id="c_ejes_exceso">
          ${opcionesValor([["aviso", "Avisar en pantalla (no sale en el ticket)"], ["ticket", "Avisar y marcarlo en el ticket"]], c.ejes_exceso || "aviso")}
        </select></div>
      </div>
      <p style="margin:12px 0 6px;color:var(--muted)">Límites por grupo de ejes (kg). Vienen con los valores de referencia de la Ley 24.449: verificalos con la tabla vigente de Vialidad Nacional.</p>
      <div class="grid3">
        ${(state.funciones.tiposEje || []).map(t => `<div><label>${esc(t.nombre)}</label><input id="c_lim_${t.id}" type="number" min="1" value="${t.limite}" /></div>`).join("")}
        <div><label>Peso total máximo</label><input id="c_lim_total" type="number" min="1" value="${state.funciones.ejesLimiteTotal || 45000}" /></div>
      </div>
      <div class="btn-row"><button class="btn-primary" onclick="saveGeneral()">Guardar general</button></div>
    </div>`;
}

function opcionesValor(pares, elegido) {
  return pares.map(([v, l]) => `<option value="${v}" ${v === elegido ? "selected" : ""}>${esc(l)}</option>`).join("");
}

const PARAMS_BALANZA = ["baudios", "dataBits", "paridad", "stopBits", "protocolo", "extraccion"];

async function renderConfigBalanza() {
  const [lista, modelos, puertos] = await Promise.all([
    api("/api/balanzas"),
    api("/api/balanzas/modelos"),
    api("/api/balanzas/puertos")
  ]);
  state.cfgBalanzas = lista;
  state.modelos = modelos;
  const editando = lista.find(x => x.balanza.id === state.balEdit)?.balanza;
  const b = editando || { nombre: "", modo: "serial", puerto: "", modelo: "", baudios: 9600, dataBits: 8, paridad: "None", stopBits: 1, protocolo: "STX", extraccion: "", orden: lista.length + 1, activa: true };
  const opt = (values, sel) => values.map(v => `<option value="${esc(v)}" ${String(v) === String(sel) ? "selected" : ""}>${esc(v)}</option>`).join("");
  const puertosOpts = [...new Set([...(puertos || []), b.puerto].filter(Boolean))];

  document.getElementById("configBody").innerHTML = `
    <div class="card">
      <h3 style="margin-top:0">Balanzas del nodo</h3>
      <table>
        <tr><th>Nombre</th><th>Modo</th><th>Puerto</th><th>Modelo</th><th>Estado</th><th>Peso</th><th></th></tr>
        ${lista.length ? lista.map(({ balanza: x, lectura: l }) => `<tr class="${x.id === state.balEdit ? "selected" : ""}">
          <td>${esc(x.nombre)}${x.activa ? "" : " (inactiva)"}</td>
          <td>${x.modo === "sim" ? "Simulada" : "Serie"}</td>
          <td>${esc(x.puerto || "-")}</td>
          <td>${esc(x.modelo || "-")}</td>
          <td>${l ? pillForScale(l.estado) + (l.error ? `<br><small>${esc(l.error)}</small>` : "") : "-"}</td>
          <td>${l ? `${l.kilogramos} kg ${l.estable ? "· estable" : ""}` : "-"}</td>
          <td><div class="btn-row" style="margin:0">
            <button class="btn" onclick="editarBalanza('${x.id}')">Editar</button>
            <button class="btn" onclick="verTramas('${x.id}')">Tramas</button>
            <button class="btn-danger" onclick="borrarBalanza('${x.id}')">Borrar</button>
          </div></td>
        </tr>`).join("") : `<tr><td colspan="7">No hay balanzas.</td></tr>`}
      </table>
      <div class="btn-row"><button class="btn" onclick="reconectarBalanzas()">Reconectar todas</button></div>
      <pre id="tramas" style="white-space:pre-wrap;margin:0"></pre>
    </div>
    <div class="card">
      <h3 style="margin-top:0">${editando ? "Editar balanza" : "Nueva balanza"}</h3>
      <div class="grid2">
        <div><label>Nombre</label><input id="b_nombre" value="${esc(b.nombre)}" /></div>
        <div><label>Modo</label><select id="b_modo">
          <option value="serial" ${b.modo === "serial" ? "selected" : ""}>Puerto serie</option>
          <option value="sim" ${b.modo === "sim" ? "selected" : ""}>Simulada</option>
        </select></div>
        <div><label>Puerto COM</label><input id="b_puerto" list="dl_puertos" value="${esc(b.puerto)}" placeholder="${puertos.length ? "" : "No se detectaron puertos"}" />
          <datalist id="dl_puertos">${puertosOpts.map(p => `<option value="${esc(p)}"></option>`).join("")}</datalist></div>
        <div><label>Modelo (completa los parámetros)</label><select id="b_modelo" onchange="aplicarModelo(this.value)">
          <option value="">— personalizado —</option>${opt(modelos.map(m => m.nombre), b.modelo)}
        </select></div>
        <div><label>Baudios</label><select id="b_baudios">${opt([1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200], b.baudios)}</select></div>
        <div><label>Data bits</label><select id="b_dataBits">${opt([7, 8], b.dataBits)}</select></div>
        <div><label>Paridad</label><select id="b_paridad">${opt(["None", "Even", "Odd", "Mark", "Space"], b.paridad)}</select></div>
        <div><label>Stop bits</label><select id="b_stopBits">${opt([1, 2], b.stopBits)}</select></div>
        <div><label>Protocolo</label><select id="b_protocolo">
          <option value="STX" ${b.protocolo === "STX" ? "selected" : ""}>STX / ETX</option>
          <option value="Line" ${b.protocolo === "Line" ? "selected" : ""}>Línea (CR/LF)</option>
        </select></div>
        <div><label>Extracción (inicio:fin, vacío = todos los dígitos)</label><input id="b_extraccion" value="${esc(b.extraccion || "")}" placeholder="ej. 5:10" /></div>
        <div><label>Orden</label><input id="b_orden" type="number" value="${b.orden}" /></div>
        <div><label class="check"><input type="checkbox" id="b_activa" ${b.activa ? "checked" : ""} /> Activa</label></div>
      </div>
      <div class="btn-row">
        <button class="btn-primary" onclick="guardarBalanza()">${editando ? "Guardar y reconectar" : "Agregar balanza"}</button>
        ${editando ? `<button class="btn" onclick="editarBalanza(null)">Cancelar</button>` : ""}
      </div>
    </div>
    <div class="card">
      <h3 style="margin-top:0">Probar extracción</h3>
      <p>Pegá una trama (de "Tramas" o del manual del indicador; se aceptan <code>\\x02</code>, <code>\\r</code>) y la extracción.</p>
      <div class="grid2">
        <div><label>Trama</label><input id="p_trama" placeholder="\\x02 +012480kg\\x03" /></div>
        <div><label>Extracción</label><input id="p_extraccion" placeholder="5:10" /></div>
      </div>
      <div class="btn-row"><button class="btn" onclick="probarExtraccion()">Probar</button> <strong id="p_resultado"></strong></div>
    </div>`;
}

function aplicarModelo(nombre) {
  const m = state.modelos.find(x => x.nombre === nombre);
  if (!m) return;
  for (const key of PARAMS_BALANZA) {
    const el = document.getElementById("b_" + key);
    if (el) el.value = m[key] ?? "";
  }
}

function editarBalanza(id) {
  state.balEdit = id;
  renderConfigBalanza();
}

async function guardarBalanza() {
  const body = {
    nombre: val("b_nombre"),
    modo: val("b_modo"),
    puerto: val("b_puerto"),
    modelo: val("b_modelo") || null,
    baudios: num("b_baudios"),
    dataBits: num("b_dataBits"),
    paridad: val("b_paridad"),
    stopBits: num("b_stopBits"),
    protocolo: val("b_protocolo"),
    extraccion: val("b_extraccion") || null,
    orden: num("b_orden"),
    activa: document.getElementById("b_activa").checked
  };
  try {
    await api("/api/balanzas" + (state.balEdit ? `/${state.balEdit}` : ""), { method: state.balEdit ? "PUT" : "POST", body: JSON.stringify(body) });
    toast("Balanza guardada");
    state.balEdit = null;
    setTimeout(renderConfigBalanza, 800);
  } catch (e) {
    toast(limpiarError(e), "err");
  }
}

async function borrarBalanza(id) {
  if (!confirm("¿Borrar esta balanza?")) return;
  await api(`/api/balanzas/${id}`, { method: "DELETE" });
  toast("Balanza borrada");
  state.balEdit = null;
  renderConfigBalanza();
}

async function reconectarBalanzas() {
  await api("/api/balanzas/reconectar", { method: "POST" });
  toast("Reconectando...");
  setTimeout(renderConfigBalanza, 1500);
}

async function verTramas(id) {
  const tramas = await api(`/api/balanzas/${id}/tramas`);
  document.getElementById("tramas").textContent = tramas.length
    ? "Últimas tramas recibidas:\n" + tramas.join("\n")
    : "Todavía no llegó ninguna trama. Revisá puerto, baudios y cable.";
}

async function probarExtraccion() {
  try {
    const r = await api("/api/balanzas/probar", { method: "POST", body: JSON.stringify({ trama: val("p_trama"), extraccion: val("p_extraccion") || null }) });
    document.getElementById("p_resultado").textContent = r.kilogramos == null ? "No se encontró un número" : `${r.kilogramos} kg`;
  } catch (e) {
    document.getElementById("p_resultado").textContent = limpiarError(e);
  }
}

function renderConfigCampos() {
  document.getElementById("configBody").innerHTML = `
    <div class="card">
      <p>Marcá qué datos ve el operador en esta planta. Los campos ocultos no se piden en el pesaje.</p>
      <div class="field-row" style="font-weight:700;color:var(--muted)"><div>Campo</div><div>Visible</div><div>Obligatorio</div></div>
      ${fields().map(f => `
        <div class="field-row">
          <div>${esc(f.label)}</div>
          <label class="check"><input type="checkbox" id="fv_${f.key}" ${f.visible ? "checked" : ""} /> Visible</label>
          <label class="check"><input type="checkbox" id="fr_${f.key}" ${f.required ? "checked" : ""} /> Obligatorio</label>
        </div>`).join("")}
      <div class="btn-row"><button class="btn-primary" onclick="saveCampos()">Guardar campos</button></div>
    </div>`;
}

const CAMPOS_ENCABEZADO = [
  ["nombre", "Razón social"], ["cuit", "CUIT"], ["direccion", "Dirección"], ["localidad", "Localidad"],
  ["provincia", "Provincia"], ["email", "Mail"], ["telefono", "Teléfono"]
];

/** Encabezado del ticket: los datos de cada empresa (los mismos de Planta), como en la configuración de la 3.x. */
function encabezadoEmpresa(e, varias) {
  return `
    <div class="card">
      <h3 style="margin-top:0">Encabezado del ticket${varias ? ` · ${esc(e.nombre)}` : ""}</h3>
      <div class="grid2">
        ${CAMPOS_ENCABEZADO.map(([key, label]) =>
          `<div><label>${label}</label><input id="em_${key}_${e.id}" value="${esc(e[key] ?? "")}" /></div>`).join("")}
      </div>
      <div class="btn-row"><button class="btn-primary" onclick="guardarEmpresa('${e.id}', true)">Guardar encabezado</button></div>
    </div>`;
}

async function renderConfigTicket() {
  const c = state.config;
  state.empresas = await api("/api/empresas");
  const empresas = state.empresas.filter(e => e.activa !== false);
  const e = empresas[0];
  document.getElementById("configBody").innerHTML = `
    ${empresas.map(x => encabezadoEmpresa(x, empresas.length > 1)).join("")}
    <div class="grid2">
      <div class="card">
        <div><label>Título del ticket</label><input id="t_titulo" value="${esc(c.ticket_titulo || "")}" /></div>
        <div style="margin-top:12px"><label>Pie de ticket</label><input id="t_pie" value="${esc(c.ticket_pie || "")}" /></div>
        <div style="margin-top:12px">
          <label>Logo del cliente (sale arriba del ticket)</label>
          <input id="t_logo" type="file" accept=".png,.jpg,.jpeg,.webp" onchange="subirLogo()" />
          <p style="margin:6px 0 0;color:var(--muted);font-size:13px">Se guarda apenas lo elegís. PNG, JPG o WEBP, hasta 2 MB.</p>
        </div>
        <div class="btn-row">
          <button class="btn-primary" onclick="saveTicket()">Guardar</button>
          ${state.branding.logoUrl ? `<button class="btn-danger" onclick="quitarLogo()">Quitar logo</button>` : ""}
        </div>
      </div>
      <div class="card ticket-preview">
        ${state.branding.logoUrl ? `<img src="${state.branding.logoUrl}?t=${Date.now()}" alt="logo" />` : bfsLogo("login-logo")}
        <h3>${esc(c.ticket_titulo || "Comprobante de pesaje")}</h3>
        <p><strong>${esc(e?.nombre || c.cliente_nombre || "Cliente")}</strong>${e?.cuit ? ` · CUIT ${esc(e.cuit)}` : ""}</p>
        <p>${esc([e?.direccion, e?.localidad, e?.provincia].filter(Boolean).join(", "))}</p>
        <p>${esc([e?.telefono, e?.email].filter(Boolean).join(" · "))}</p>
        <p>Ticket 0001 · INGRESO · 12.480 kg</p>
        <p>${esc(c.ticket_pie || "Balanzas Full Service")}</p>
      </div>
    </div>`;
}

function renderConfigApi() {
  const c = state.config;
  document.getElementById("configBody").innerHTML = `
    <div class="card">
      <p>Envío de pesadas al sistema del cliente. El formato se adapta en cada instalación.</p>
      <div class="grid2">
        <div><label>API habilitada</label>${yesNo("c_api", c.worms_enabled)}</div>
        <div><label>Identificador de balanza</label><input id="c_bal" value="${esc(c.worms_balanza || "1")}" /></div>
        <div style="grid-column:1/-1"><label>Endpoint</label><input id="c_ep" value="${esc(c.worms_endpoint || "")}" placeholder="https://..." /></div>
        <div style="grid-column:1/-1"><label>API key</label><input id="c_key" value="${esc(c.worms_api_key || "")}" /></div>
        <div><label>Tipo por defecto</label>
          <select id="c_tipo">
            <option ${c.worms_tipo_default === "INGRESO" ? "selected" : ""}>INGRESO</option>
            <option ${c.worms_tipo_default === "EGRESO" ? "selected" : ""}>EGRESO</option>
          </select>
        </div>
        <div><label>Al corregir un ticket, volver a enviarlo</label>${yesNo("c_reenviar", c.worms_reenviar_correcciones || "false")}</div>
        <div style="grid-column:1/-1"><label>Empresas que envían (cada envío lleva el nombre de la empresa)</label>
          <div class="btn-row" style="margin:4px 0 0">${state.empresas.map(e => `<label style="display:flex;gap:6px;align-items:center;font-weight:normal">
            <input type="checkbox" class="c_wemp" value="${e.id}" style="width:auto" ${empresasWorms(c).includes(e.id) ? "checked" : ""} /> ${esc(e.nombre)}</label>`).join("")}</div>
        </div>
      </div>
      <div class="btn-row"><button class="btn-primary" onclick="saveApi()">Guardar API</button></div>
    </div>
    <div class="card" id="envios"></div>`;
  cargarEnvios();
}

const estadoEnvio = {
  pendiente: ["pill-warn", "Pendiente"],
  enviado: ["pill-ok", "Enviado"],
  error: ["pill-err", "Rechazado"],
  descartado: ["pill-info", "Descartado"]
};

async function cargarEnvios(filtro = state.enviosFiltro || "") {
  state.enviosFiltro = filtro;
  const el = document.getElementById("envios");
  if (!el) return;
  let r;
  try {
    r = await api("/api/integraciones" + (filtro ? "?estado=" + encodeURIComponent(filtro) : ""));
  } catch (e) {
    el.innerHTML = `<p class="error">${esc(limpiarError(e))}</p>`;
    return;
  }
  const fecha = d => new Date(d).toLocaleString([], { dateStyle: "short", timeStyle: "short" });
  const nombre = d => d.toUpperCase();
  el.innerHTML = `
    <h3 style="margin-top:0">Envíos</h3>
    <div class="grid2">
      ${r.destinos.map(d => `<div class="status-box"><span>${esc(nombre(d.nombre))} · ${d.habilitado ? "activa" : "apagada o sin configurar"}</span>
        <strong>${d.pendientes} pendientes · ${d.errores} rechazados · ${d.enviados} enviados</strong>
        ${d.errores ? `<button class="btn" style="margin-top:6px" onclick="reintentarErrores('${esc(d.nombre)}')">Reintentar rechazados</button>` : ""}
      </div>`).join("")}
    </div>
    <p>Si el sistema del cliente no responde, BFS reintenta solo, al minuto y después cada vez un poco más (hasta 5 minutos). Lo que el sistema rechaza queda acá hasta que lo reintentes o lo descartes; mientras tanto, la salida de esa pesada espera.</p>
    <div class="btn-row">
      <select style="width:auto" onchange="cargarEnvios(this.value)">
        ${[["", "Todos"], ["pendiente", "Pendientes"], ["error", "Rechazados"], ["enviado", "Enviados"], ["descartado", "Descartados"]]
          .map(([v, l]) => `<option value="${v}" ${filtro === v ? "selected" : ""}>${l}</option>`).join("")}
      </select>
      <button class="btn-primary" onclick="enviarAhora()">Enviar ahora</button>
    </div>
    <table>
      <tr><th>Fecha</th><th>Destino</th><th>Ticket</th><th>Evento</th><th>Estado</th><th>Intentos</th><th>Detalle</th><th></th></tr>
      ${r.mensajes.length ? r.mensajes.map(m => {
        const [cls, label] = estadoEnvio[m.estado] || ["pill-info", m.estado];
        const espera = m.estado === "pendiente" && m.intentos ? ` · próximo ${fecha(m.proximoIntento)}` : "";
        return `<tr>
          <td>${fecha(m.creadoEn)}</td><td>${esc(nombre(m.destino))}</td><td>${esc(m.ticket)}</td><td>${esc(m.evento)}</td>
          <td><span class="pill ${cls}">${label}</span></td><td>${m.intentos}${espera}</td>
          <td>${esc(m.ultimoError || "")}</td>
          <td>${m.estado === "error" || m.estado === "pendiente" || m.estado === "descartado" ? `
            <button class="btn" onclick="accionEnvio('${m.id}','reintentar')">Reintentar</button>
            ${m.estado !== "descartado" ? `<button class="btn" onclick="accionEnvio('${m.id}','descartar')">Descartar</button>` : ""}` : ""}</td>
        </tr>`;
      }).join("") : `<tr><td colspan="8">No hay envíos${filtro ? " en este estado" : ""}.</td></tr>`}
    </table>`;
}

async function accionEnvio(id, accion) {
  if (accion === "descartar" && !confirm("Este envío no se va a mandar más. ¿Seguir?")) return;
  try {
    await api(`/api/integraciones/${id}/${accion}`, { method: "POST" });
    toast(accion === "descartar" ? "Envío descartado" : "Vuelve a la cola");
  } catch (e) {
    toast(limpiarError(e), "err");
  }
  cargarEnvios();
}

async function reintentarErrores(destino) {
  try {
    const r = await api(`/api/integraciones/${destino}/reintentar-errores`, { method: "POST" });
    toast(`${r.reintentados} vuelven a la cola`);
  } catch (e) {
    toast(limpiarError(e), "err");
  }
  cargarEnvios();
}

async function enviarAhora() {
  try {
    await api("/api/integraciones/enviar", { method: "POST" });
    toast("Envío terminado");
  } catch (e) {
    toast(limpiarError(e), "err");
  }
  cargarEnvios();
}

async function renderConfigLicencia() {
  const l = state.licencia = await api("/api/licencia");
  document.getElementById("configBody").innerHTML = `
    <div class="card">
      <div class="grid2">
        <div class="status-box"><span>Estado</span><strong>${esc(l.mensaje)}</strong></div>
        <div class="status-box"><span>Vence</span><strong>${l.vence ? esc(new Date(l.vence).toLocaleDateString()) : (l.cliente ? "Sin vencimiento" : "-")}</strong></div>
        <div class="status-box"><span>ID de máquina</span><strong><code>${esc(l.maquina)}</code></strong></div>
        <div class="status-box"><span>Otras placas de red</span><strong>${esc(l.maquinas.slice(1).join(", ") || "ninguna")}</strong></div>
      </div>
      <p>La licencia se genera con la herramienta de BFS para el ID de máquina de arriba. Pegala acá:</p>
      <textarea id="lic_texto" rows="4" placeholder="eyJ..."></textarea>
      <div class="btn-row"><button class="btn-primary" onclick="activarLicencia()">Activar licencia</button></div>
    </div>`;
}

async function activarLicencia() {
  try {
    state.licencia = await api("/api/licencia", { method: "POST", body: JSON.stringify({ texto: val("lic_texto") }) });
    toast("Licencia activada");
    render();
  } catch (e) {
    toast(e.message.replace(/^"|"$/g, ""), "err");
  }
}

async function saveConfig(body) {
  await api("/api/config", { method: "PUT", body: JSON.stringify(body) });
  state.config = await api("/api/config");
  state.branding = await api("/api/branding");
  state.campos = await api("/api/campos");
  toast("Configuración guardada");
}

async function saveGeneral() {
  await saveConfig({
    cliente_nombre: val("c_nombre"),
    tara_pallets: val("c_pal"),
    tara_conocida: val("c_tara"),
    aduana: val("c_adu"),
    contenedores: val("c_cont"),
    aduana_escenario: val("c_adu_esc"),
    termica: val("c_term"),
    termica_impresora: val("c_term_imp"),
    termica_ancho: val("c_term_ancho"),
    ingreso_manual: val("c_manual"),
    exigir_estable: val("c_estable"),
    puesto: String(Math.min(99, Math.max(1, num("c_puesto") || 1))),
    ticket_solo_numero: val("c_solonum"),
    pesaje_partes: val("c_partes"),
    pesaje_ejes: val("c_ejes"),
    ejes_pasadas: val("c_ejes_pasadas"),
    ejes_exceso: val("c_ejes_exceso"),
    ejes_limites: JSON.stringify(Object.fromEntries((state.funciones.tiposEje || [])
      .map(t => [t.id, num("c_lim_" + t.id) || t.limite]))),
    ejes_limite_total: String(num("c_lim_total") || 45000)
  });
  state.funciones = await api("/api/funciones");
  renderConfig();
}

async function saveCampos() {
  const next = fields().map(f => ({
    key: f.key,
    label: f.label,
    visible: document.getElementById("fv_" + f.key)?.checked === true,
    required: document.getElementById("fr_" + f.key)?.checked === true
  }));
  await saveConfig({ fields_json: JSON.stringify(next) });
  renderConfig();
}

async function saveTicket() {
  // Si eligió un logo y no se subió todavía (o falló), va junto con el resto.
  if (document.getElementById("t_logo")?.files?.[0] && !(await subirLogo())) return;
  await saveConfig({
    ticket_titulo: val("t_titulo"),
    ticket_pie: val("t_pie")
  });
  renderConfig();
}

/** Sube el logo elegido. Devuelve false si no se pudo (y lo avisa). */
async function subirLogo() {
  const input = document.getElementById("t_logo");
  const file = input?.files?.[0];
  if (!file) return true;
  const body = new FormData();
  body.append("file", file);
  try {
    await api("/api/branding/logo", { method: "POST", body });
  } catch (e) {
    toast("No se pudo guardar el logo: " + limpiarError(e), "err");
    return false;
  }
  input.value = "";
  state.branding = await api("/api/branding");
  toast("Logo guardado");
  renderConfig();
  return true;
}

async function quitarLogo() {
  if (!confirm("¿Quitar el logo del cliente? El ticket sale sin logo arriba.")) return;
  try {
    await api("/api/branding/logo", { method: "DELETE" });
    state.branding = await api("/api/branding");
    toast("Logo quitado");
    renderConfig();
  } catch (e) {
    toast(limpiarError(e), "err");
  }
}

/** Lista vacía en la configuración = todas las empresas envían. */
function empresasWorms(c) {
  const lista = (c.worms_empresas || "").split(",").filter(Boolean);
  return lista.length ? lista : state.empresas.map(e => e.id);
}

/** Todas tildadas = "" (también las que se agreguen después); ninguna = "ninguna". */
function listaEmpresasWorms() {
  const tildadas = [...document.querySelectorAll(".c_wemp:checked")].map(x => x.value);
  if (tildadas.length === state.empresas.length) return "";
  return tildadas.length ? tildadas.join(",") : "ninguna";
}

async function saveApi() {
  await saveConfig({
    worms_enabled: val("c_api"),
    worms_endpoint: val("c_ep"),
    worms_api_key: val("c_key"),
    worms_balanza: val("c_bal"),
    worms_tipo_default: val("c_tipo"),
    worms_reenviar_correcciones: val("c_reenviar"),
    worms_empresas: listaEmpresasWorms()
  });
  renderConfig();
}

async function start() {
  try { state.branding = await api("/api/branding"); } catch { state.branding = {}; }
  try {
    const h = await api("/api/health");
    state.modo = h.modo;
    state.edicion = h.edicion || "Completa";
    state.version = h.version;
  } catch { state.modo = "Nodo"; state.edicion = "Completa"; }
  if (state.token) {
    try {
      state.user = await api("/api/me");
      if (esSuperadmin()) return render();
      await boot();
    } catch {
      logout();
    }
  } else {
    render();
  }
}


function filtrosQuery(extra = {}) {
  const f = state.filtros;
  const q = new URLSearchParams();
  for (const [k, v] of Object.entries({ ...f, ...extra })) if (v !== "" && v != null) q.set(k, v);
  return q.toString();
}

function leerFiltros() {
  state.filtros = {
    desde: val("f_desde"), hasta: val("f_hasta"), empresaId: val("f_empresa"), cliente: val("f_cliente"),
    transporte: val("f_transporte"), producto: val("f_producto"), texto: val("f_texto"), estado: val("f_estado")
  };
  state.pagina = 1;
}

function kg(n) {
  return `${Number(n || 0).toLocaleString("es-AR")} kg`;
}

async function renderReportes() {
  const f = state.filtros;
  const r = await api(`/api/reportes/pesadas?${filtrosQuery({ pagina: state.pagina, tamano: 50 })}`);
  const paginas = Math.max(1, Math.ceil(r.total / r.tamano));
  state.reporteItems = r.items;
  const t = r.totales;
  const opt = (values, sel) => values.map(([v, l]) => `<option value="${v}" ${v === sel ? "selected" : ""}>${l}</option>`).join("");
  document.getElementById("main").innerHTML = `
    <div class="page-head"><div><h2>Pesadas</h2><p>${can("pesadas.ver") ? "Movimientos de la planta." : "Tus movimientos."} ✎ = peso cargado a mano.</p></div></div>
    <div class="card">
      <div class="grid3">
        <div><label>Desde</label><input id="f_desde" type="date" value="${esc(f.desde)}" /></div>
        <div><label>Hasta</label><input id="f_hasta" type="date" value="${esc(f.hasta)}" /></div>
        <div><label>Estado</label><select id="f_estado">${opt([["", "Todos"], ["SALIDA", "Cerradas"], ["ENTRADA", "Pendientes"], ["ANULADA", "Anuladas"]], f.estado)}</select></div>
        ${state.empresas.length > 1 ? `<div><label>Empresa</label><select id="f_empresa">${empresaOptions(f.empresaId)}</select></div>` : ""}
        ${can("pesadas.ver") ? `<div><label>Cliente</label><input id="f_cliente" value="${esc(f.cliente)}" /></div>` : ""}
        <div><label>Transporte</label><input id="f_transporte" value="${esc(f.transporte)}" /></div>
        <div><label>Producto</label><input id="f_producto" value="${esc(f.producto)}" /></div>
        <div><label>Ticket, patente, chofer o remito</label><input id="f_texto" value="${esc(f.texto)}" /></div>
      </div>
      <div class="btn-row">
        <button class="btn-primary" onclick="leerFiltros();renderReportes()">Buscar</button>
        <button class="btn" onclick="state.filtros={};state.pagina=1;renderReportes()">Limpiar</button>
        <button class="btn" onclick="leerFiltros();exportarExcel()">Exportar a Excel</button>
      </div>
    </div>
    <div class="status-grid" style="margin-bottom:16px">
      <div class="status-box"><span>Pesadas</span><strong>${t.cantidad}</strong></div>
      <div class="status-box"><span>Neto (cerradas)</span><strong>${kg(t.neto)}</strong></div>
      <div class="status-box"><span>Cerradas / pendientes / anuladas</span><strong>${t.cerradas} / ${t.pendientes} / ${t.anuladas}</strong></div>
    </div>
    <div class="card" style="overflow-x:auto">
      <table>
        <tr><th>Ticket</th><th>Fecha</th><th>Empresa</th><th>Patente</th><th>Cliente</th><th>Producto</th><th>Estado</th><th>Entrada</th><th>Salida</th><th>Neto</th><th></th></tr>
        ${r.items.length ? r.items.map(p => `<tr>
          <td>${esc(p.ticketNumero)}${p.modo === "TaraConocida" ? ` <span class="pill pill-info">Tara conocida</span>` : ""}${p.tipoOperacion === "Aduana" ? ` <span class="pill pill-info">Aduana</span>` : ""}</td>
          <td>${esc(hora(p.entradaEn))}</td>
          <td>${esc(empresaName(p.empresaId))}</td>
          <td>${esc(p.patenteChasis)}</td>
          <td>${esc(p.cliente || "")}</td>
          <td>${esc(p.producto || "")}</td>
          <td>${pillEstado(p)}</td>
          <td>${p.pesoEntrada}${p.entradaManualPor ? " ✎" : ""}</td>
          <td>${p.pesoSalida ?? "-"}${p.salidaManualPor ? " ✎" : ""}</td>
          <td>${p.estado === "ANULADA" ? "-" : (p.neto ?? "-")}</td>
          <td><div class="btn-row" style="margin:0">
            ${botonesTicket(p.id)}
            ${p.estado === "ENTRADA" && can("pesaje.operar") ? `<button class="btn" onclick="retomarPesada('${p.id}')">Retomar</button>` : ""}
            ${p.estado !== "ANULADA" && can("pesaje.corregir") ? `<button class="btn" onclick="corregirPesada('${p.id}')">Corregir</button>` : ""}
            ${p.estado !== "ANULADA" && can("pesaje.anular") ? `<button class="btn-danger" onclick="anularPesada('${p.id}')">Anular</button>` : ""}
          </div></td>
        </tr>`).join("") : `<tr><td colspan="11">No hay pesadas con esos filtros.</td></tr>`}
      </table>
      <div class="btn-row">
        <button class="btn" ${state.pagina <= 1 ? "disabled" : ""} onclick="state.pagina--;renderReportes()">Anterior</button>
        <span style="align-self:center">Página ${state.pagina} de ${paginas}</span>
        <button class="btn" ${state.pagina >= paginas ? "disabled" : ""} onclick="state.pagina++;renderReportes()">Siguiente</button>
      </div>
    </div>`;
}

/** Campos que se pueden corregir (los pesos, fechas, empresa y número de ticket nunca). */
const CORREGIBLES = [
  ["patenteChasis", "Patente chasis", p => p.patenteChasis],
  ["patenteAcoplado", "Patente acoplado", p => p.patenteAcoplado],
  ["conductor", "Conductor", p => p.conductor],
  ["tipoDocumento", "Tipo de documento", p => p.tipoDocumento, "tipoDoc"],
  ["documento", "Documento", p => p.documento],
  ["nacionalidad", "Nacionalidad", p => p.nacionalidad],
  ["tipo", "Tipo", p => p.tipo, "tipo"],
  ["producto", "Producto", p => p.producto, "dl_productos"],
  ["cliente", "Cliente", p => p.cliente, "dl_clientes"],
  ["transporte", "Transporte", p => p.transporte, "dl_transportes"],
  ["procedencia", "Procedencia", p => p.procedencia],
  ["destino", "Destino", p => p.destino],
  ["comprobante", "Remito", p => p.comprobante],
  ["observaciones", "Observaciones", p => p.observaciones],
  ["aduana.exportador", "Exportador / importador", p => p.aduana?.exportador],
  ["aduana.exportadorCuit", "CUIT exportador", p => p.aduana?.exportadorCuit],
  ["aduana.ata", "ATA", p => p.aduana?.ata],
  ["aduana.ataCuit", "CUIT ATA", p => p.aduana?.ataCuit],
  ["aduana.operacionId", "N° de operación", p => p.aduana?.operacionId],
  ["aduana.destinacionAduanera", "Destinación aduanera", p => p.aduana?.destinacionAduanera],
  ["aduana.contenedorId", "Contenedor", p => p.aduana?.contenedorId],
  ["aduana.bultoId", "Bulto", p => p.aduana?.bultoId]
];

function historialCorrecciones(p) {
  let lista = [];
  try { lista = JSON.parse(p.correccionesJson || "[]"); } catch {}
  if (!lista.length) return "<p>Sin correcciones.</p>";
  return `<ul class="historial">${lista.slice().reverse().map(c => `<li>
    <strong>${esc(new Date(c.en).toLocaleString([], { dateStyle: "short", timeStyle: "short" }))}</strong> · ${esc(c.por)} · ${esc(c.motivo)}<br>
    ${c.cambios.map(x => `${esc(x.campo)}: <s>${esc(x.antes || "(vacío)")}</s> → ${esc(x.despues || "(vacío)")}`).join("<br>")}
  </li>`).join("")}</ul>`;
}

async function corregirPesada(id) {
  const p = (state.reporteItems || []).find(x => x.id === id);
  if (!p) return;
  const [clientes, transportes, productos] = await Promise.all([
    api("/api/catalogos/clientes"), api("/api/catalogos/transportes"), api("/api/catalogos/productos")]);
  state.catalogos = { ...state.catalogos, clientes, transportes, productos };
  const campos = CORREGIBLES.filter(([key]) => !key.startsWith("aduana.") || p.aduana)
    .filter(([key]) => key.startsWith("aduana.") || visible(key));
  const input = ([key, label, leer, extra]) => {
    const v = leer(p) || "";
    const id = "co_" + key.replace(".", "_");
    if (extra === "tipo") return `<div><label>${label}</label><select id="${id}">${opciones(["INGRESO", "EGRESO"], v)}</select></div>`;
    if (extra === "tipoDoc") return `<div><label>${label}</label><select id="${id}"><option></option>${opciones(TIPOS_DOCUMENTO, v)}</select></div>`;
    return `<div><label>${label}</label><input id="${id}" value="${esc(v)}" ${extra ? `list="${extra}"` : ""} autocomplete="off" /></div>`;
  };
  document.getElementById("main").innerHTML = `
    <div class="page-head"><div><h2>Corregir ticket ${esc(p.ticketNumero)}</h2>
      <p>${esc(empresaName(p.empresaId))} · entrada ${esc(hora(p.entradaEn))} · ${p.pesoEntrada} kg${p.pesoSalida != null ? ` · salida ${p.pesoSalida} kg` : ""}. Los pesos no se pueden corregir.</p></div></div>
    <div class="card">
      <div class="grid3">${campos.map(input).join("")}</div>
      ${datalist("dl_productos", state.catalogos.productos.map(x => x.nombre))}
      ${datalist("dl_clientes", state.catalogos.clientes.map(x => x.razonSocial))}
      ${datalist("dl_transportes", state.catalogos.transportes.map(x => x.razonSocial))}
      <div class="grid2" style="margin-top:12px">
        <div><label>Motivo de la corrección *</label><input id="co_motivo" /></div>
        ${state.funciones.claveCorreccion ? `<div><label>Clave de corrección *</label><input id="co_clave" type="password" autocomplete="off" /></div>` : ""}
      </div>
      <p class="error" id="err"></p>
      <div class="btn-row">
        <button class="btn-primary" onclick="guardarCorreccion('${p.id}')">Guardar corrección</button>
        <button class="btn" onclick="renderReportes()">Volver</button>
      </div>
    </div>
    <div class="card"><h3 style="margin-top:0">Historial</h3>${historialCorrecciones(p)}</div>`;
}

async function guardarCorreccion(id) {
  const p = state.reporteItems.find(x => x.id === id);
  const datos = {};
  for (const [key, , leer] of CORREGIBLES) {
    const el = document.getElementById("co_" + key.replace(".", "_"));
    if (el && el.value.trim() !== (leer(p) || "")) datos[key] = el.value.trim();
  }
  try {
    const r = await api(`/api/pesadas/${id}/corregir`, {
      method: "POST",
      body: JSON.stringify({ motivo: val("co_motivo"), clave: val("co_clave") || null, datos })
    });
    toast(`Ticket ${r.ticketNumero} corregido`);
    await renderReportes();
  } catch (e) {
    mostrarError(e);
  }
}

async function exportarExcel() {
  try {
    const { blob, nombre } = await bajarArchivo(`/api/pesadas/excel?${filtrosQuery()}`);
    guardarComo(URL.createObjectURL(blob), nombre);
  } catch (e) {
    toast("No se pudo exportar: " + limpiarError(e), "err");
  }
}

async function renderTablero() {
  const r = await api(`/api/reportes/resumen?dias=${state.dias}`);
  const max = Math.max(1, ...r.porDia.map(d => d.neto));
  const tablaTop = (titulo, items) => `
    <div class="card">
      <h3 style="margin-top:0">${titulo}</h3>
      <table>
        <tr><th></th><th>Pesadas</th><th>Neto</th></tr>
        ${items.length ? items.map(x => `<tr><td>${esc(x.nombre)}</td><td>${x.cantidad}</td><td>${kg(x.neto)}</td></tr>`).join("") : `<tr><td colspan="3">Sin datos</td></tr>`}
      </table>
    </div>`;
  document.getElementById("main").innerHTML = `
    <div class="page-head">
      <div><h2>Tablero</h2><p>Neto de pesadas cerradas. Las pendientes y anuladas no suman.</p></div>
      <select onchange="state.dias=Number(this.value);renderTablero()" style="max-width:180px">
        ${[7, 30, 90, 365].map(d => `<option value="${d}" ${d === state.dias ? "selected" : ""}>Últimos ${d} días</option>`).join("")}
      </select>
    </div>
    <div class="status-grid" style="margin-bottom:16px">
      <div class="status-box"><span>Hoy</span><strong>${r.hoy.cerradas} cerradas · ${kg(r.hoy.neto)}</strong></div>
      <div class="status-box"><span>Últimos ${r.dias} días</span><strong>${r.periodo.cerradas} cerradas · ${kg(r.periodo.neto)}</strong></div>
      <div class="status-box"><span>Pendientes de salida ahora</span><strong>${r.pendientesAhora}</strong></div>
    </div>
    <div class="card">
      <h3 style="margin-top:0">Neto por día</h3>
      <div style="display:flex;align-items:flex-end;gap:2px;height:180px;overflow-x:auto">
        ${r.porDia.map(d => `<div title="${esc(d.fecha)}: ${d.cantidad} pesadas · ${kg(d.neto)}"
          style="flex:1;min-width:4px;background:var(--accent,#0f766e);opacity:${d.neto ? 1 : 0.15};height:${Math.max(2, Math.round(d.neto / max * 170))}px"></div>`).join("")}
      </div>
      <div style="display:flex;justify-content:space-between;color:var(--muted);font-size:12px;margin-top:4px">
        <span>${esc(r.porDia[0]?.fecha || "")}</span><span>${esc(r.porDia[r.porDia.length - 1]?.fecha || "")}</span>
      </div>
    </div>
    <div class="grid2">
      ${can("pesadas.ver") ? tablaTop("Clientes con más neto", r.topClientes) : ""}
      ${tablaTop("Productos con más neto", r.topProductos)}
    </div>`;
}

async function renderSistema() {
  const s = await api("/api/sistema");
  const tam = b => b > 1048576 ? `${(b / 1048576).toFixed(1)} MB` : `${Math.ceil(b / 1024)} KB`;
  let importar = "";
  if (can("config.bfs")) {
    try {
      const d = await api("/api/sistema/importar-3x");
      importar = `
    <div class="card">
      <h3 style="margin-top:0">Importar desde versión anterior (1.x o 3.x)</h3>
      <p>${d.existe
        ? `Se encontró <code>${esc(d.ruta)}</code>: ${Object.entries(d.contenido).map(([k, v]) => `${esc(k)} ${v}`).join(" · ")}.`
        : `No se encontró una base 3.x en <code>${esc(d.ruta)}</code>. Podés subir un backup <code>.sql</code> de la 1.x o un <code>truckscale.db</code> de la 3.x.`}
        El archivo original no se modifica; lo que ya existe en la 4.0 se omite y antes se guarda un backup.</p>
      <div class="grid2"><div><label>Subir archivo (.sql de la 1.x o truckscale.db de la 3.x)</label><input id="imp_archivo" type="file" accept=".db,.sql" /></div></div>
      <div class="btn-row"><button class="btn-primary" onclick="importar3x()">Importar</button></div>
      <div id="imp_resultado"></div>
    </div>`;
    } catch (e) {
      importar = `<div class="card">${esc(limpiarError(e))}</div>`;
    }
  }
  document.getElementById("main").innerHTML = `
    <div class="page-head"><div><h2>Sistema</h2><p>Copias de seguridad, espejo y numeración. Todos los días se hace un backup automático (se guardan los últimos 30).</p></div></div>
    ${s.restauracionPendiente ? `
    <div class="card" style="border-left:4px solid #d97706">
      <strong>Hay una restauración pendiente.</strong> Cerrá y volvé a abrir el sistema para aplicarla.
      <div class="btn-row"><button class="btn" onclick="cancelarRestauracion()">Cancelar restauración</button></div>
    </div>` : ""}
    <div class="card">
      <h3 style="margin-top:0">Backups</h3>
      <div class="btn-row" style="margin-top:0"><button class="btn-primary" onclick="crearBackup()">Hacer backup ahora</button></div>
      <table style="margin-top:12px">
        <tr><th>Archivo</th><th>Fecha</th><th>Tamaño</th><th></th></tr>
        ${s.backups.length ? s.backups.slice(0, 15).map(b => `<tr>
          <td>${esc(b.archivo)}</td><td>${esc(new Date(b.fecha).toLocaleString())}</td><td>${tam(b.bytes)}</td>
          <td><button class="btn" onclick="bajarBackup('${esc(b.archivo)}')">Descargar</button></td>
        </tr>`).join("") : `<tr><td colspan="4">Todavía no hay backups.</td></tr>`}
      </table>
    </div>
    <div class="card">
      <h3 style="margin-top:0">Restaurar</h3>
      <p>Reemplaza toda la base por la de un backup. La actual queda guardada en backups. Se aplica al volver a abrir el sistema.</p>
      <div class="grid2"><div><input id="rest_archivo" type="file" accept=".db" /></div></div>
      <div class="btn-row"><button class="btn-danger" onclick="restaurar()">Restaurar este backup</button></div>
    </div>
    <div class="card">
      <h3 style="margin-top:0">Espejo automático</h3>
      <p>Copia de la base en otra carpeta (otro disco o una carpeta de red), cada 10 minutos si hubo cambios.
        ${s.espejoUltimo ? `Última copia: ${esc(new Date(s.espejoUltimo).toLocaleString())}.` : ""}
        ${s.espejoError ? `<br><span class="error">${esc(s.espejoError)}</span>` : ""}</p>
      <div class="grid2"><div><label>Carpeta (vacío = desactivado)</label><input id="esp_carpeta" value="${esc(s.espejoCarpeta)}" placeholder="D:\\EspejoBFS o \\\\servidor\\carpeta" /></div></div>
      <div class="btn-row">
        <button class="btn-primary" onclick="guardarEspejo()">Guardar</button>
        <button class="btn" onclick="probarEspejo()">Copiar ahora</button>
      </div>
    </div>
    <div class="card">
      <h3 style="margin-top:0">Numeración de tickets</h3>
      <table>
        <tr><th>Empresa</th><th>Puesto</th><th>Último usado</th><th>Próximo</th><th></th></tr>
        ${s.tickets.map(t => `<tr>
          <td>${esc(t.empresa)}</td><td>${t.puesto}</td><td>${t.maximoUsado || "-"}</td>
          <td><input id="tk_${t.empresaId}" type="number" min="1" value="${t.proximo}" style="max-width:120px" /></td>
          <td><button class="btn" onclick="fijarTicket('${t.empresaId}')">Guardar</button></td>
        </tr>`).join("")}
      </table>
    </div>
    ${importar}`;
}

async function sistemaAccion(fn, ok) {
  try {
    const r = await fn();
    if (ok) toast(typeof ok === "function" ? ok(r) : ok);
    renderSistema();
  } catch (e) {
    toast(limpiarError(e), "err");
  }
}

function crearBackup() {
  sistemaAccion(() => api("/api/sistema/backups", { method: "POST" }), b => `Backup ${b.archivo}`);
}

async function bajarBackup(archivo) {
  try {
    const { blob } = await bajarArchivo(`/api/sistema/backups/${encodeURIComponent(archivo)}`);
    guardarComo(URL.createObjectURL(blob), archivo);
  } catch (e) {
    toast(limpiarError(e), "err");
  }
}

function restaurar() {
  const file = document.getElementById("rest_archivo")?.files?.[0];
  if (!file) return toast("Elegí el archivo de backup", "err");
  if (!confirm(`Se va a reemplazar TODA la base por ${file.name} al volver a abrir el sistema. ¿Seguir?`)) return;
  const body = new FormData();
  body.append("file", file);
  sistemaAccion(() => api("/api/sistema/restaurar", { method: "POST", body }), r => r.mensaje);
}

function cancelarRestauracion() {
  sistemaAccion(() => api("/api/sistema/restaurar", { method: "DELETE" }), "Restauración cancelada");
}

function guardarEspejo() {
  sistemaAccion(() => api("/api/sistema/espejo", { method: "PUT", body: JSON.stringify({ carpeta: val("esp_carpeta") }) }), "Carpeta de espejo guardada");
}

function probarEspejo() {
  sistemaAccion(() => api("/api/sistema/espejo", { method: "POST" }), "Espejo actualizado");
}

function fijarTicket(empresaId) {
  sistemaAccion(() => api("/api/sistema/tickets", { method: "PUT", body: JSON.stringify({ empresaId, proximo: num("tk_" + empresaId) }) }), "Numeración guardada");
}

async function importar3x() {
  const file = document.getElementById("imp_archivo")?.files?.[0];
  const origen = file ? file.name : "la base 3.x local";
  if (!confirm(`¿Importar los datos de ${origen} a esta instalación?`)) return;
  const body = new FormData();
  if (file) body.append("file", file);
  const out = document.getElementById("imp_resultado");
  out.innerHTML = "<p>Importando...</p>";
  try {
    const r = await api("/api/sistema/importar-3x", { method: "POST", body });
    out.innerHTML = `
      <table style="margin-top:12px">
        <tr><th></th><th>Importados</th><th>Ya existían / omitidos</th></tr>
        ${Object.entries(r.conteos).map(([k, c]) => `<tr><td>${esc(k)}</td><td>${c.importados}</td><td>${c.omitidos}</td></tr>`).join("")}
      </table>
      ${r.avisos.length ? `<ul>${r.avisos.map(a => `<li>${esc(a)}</li>`).join("")}</ul>` : ""}
      ${r.backupPrevio ? `<p>Backup previo: <code>${esc(r.backupPrevio)}</code></p>` : ""}`;
    toast("Importación terminada");
    const [empresas, branding] = await Promise.all([api("/api/empresas"), api("/api/branding")]);
    state.empresas = empresas;
    state.branding = branding;
  } catch (e) {
    out.innerHTML = `<p class="error">${esc(limpiarError(e))}</p>`;
  }
}

function esSuperadmin() {
  return state.user?.rol === "superadmin";
}

/** Si un superadmin entró a un cliente, su sesión de panel queda guardada para volver. */
function tokenPanel() {
  try { return localStorage.getItem("bfs_token_panel"); } catch { return null; }
}

function bannerSoporte() {
  if (!state.user?.soporte) return "";
  return `<div style="background:#d97706;color:#fff;padding:8px 16px;display:flex;justify-content:space-between;align-items:center">
    <span>Estás dentro de <b>${esc(state.user.tenant || "")}</b> como BFS (soporte).</span>
    ${tokenPanel() ? `<button class="btn" onclick="volverAlPanel()">Volver al panel</button>` : ""}
  </div>`;
}

async function renderSuperadmin() {
  const tenants = await api("/api/superadmin/tenants");
  const fecha = d => d ? new Date(d).toLocaleString([], { dateStyle: "short", timeStyle: "short" }) : "-";
  const pill = e => e === "Activo" ? `<span class="pill pill-ok">Activo</span>`
    : e === "Suspendido" ? `<span class="pill pill-err">Suspendido</span>` : `<span class="pill pill-warn">Baja</span>`;
  document.getElementById("app").innerHTML = `
    <div class="app-shell" style="grid-template-columns:1fr">
      <header class="topbar">
        <div class="brand">${bfsLogo()}<div><h1>BFS Nube · Clientes${etiquetaVersion()}</h1><p>Panel de superadmin</p></div></div>
        <div class="userchip"><div><strong>${esc(state.user.email)}</strong><span>superadmin</span></div>
          <button class="btn-ghost" onclick="logout()">Salir</button></div>
      </header>
      <main class="workspace" id="main">
        <div class="card">
          <h3 style="margin-top:0">Nuevo cliente</h3>
          <div class="grid3">
            <div><label>Nombre</label><input id="t_nombre" placeholder="Acopio Sur SA" oninput="sugerirCodigo()" /></div>
            <div><label>Código (base bfs_t_...)</label><input id="t_codigo" placeholder="acopio_sur" /></div>
            <div><label>Email del administrador</label><input id="t_email" type="email" /></div>
            <div><label>Nombre del administrador</label><input id="t_admin" /></div>
            <div><label>Clave inicial (mínimo 8)</label><input id="t_clave" type="password" autocomplete="new-password" /></div>
          </div>
          <div class="btn-row"><button class="btn-primary" onclick="crearTenant()">Crear cliente</button></div>
        </div>
        <div class="card" style="overflow-x:auto">
          <table>
            <tr><th>Cliente</th><th>Código</th><th>Estado</th><th>Usuarios</th><th>Última sincronización</th><th>Alta</th><th></th></tr>
            ${tenants.length ? tenants.map(t => `<tr>
              <td>${esc(t.nombre)}</td><td><code>${esc(t.codigo)}</code></td>
              <td>${pill(t.estado)}${t.motivoEstado ? `<br><small>${esc(t.motivoEstado)}</small>` : ""}</td>
              <td>${t.usuarios}</td><td>${fecha(t.ultimaSincronizacion)}</td><td>${fecha(t.creadoEn)}</td>
              <td><div class="btn-row" style="margin:0">
                ${t.estado !== "Baja" ? `<button class="btn" onclick="entrarTenant('${t.id}')">Entrar como BFS</button>` : ""}
                <button class="btn" onclick="verNodos('${t.id}','${esc(t.nombre)}')">Porterías</button>
                ${t.estado === "Activo" ? `<button class="btn-danger" onclick="estadoTenant('${t.id}','Suspendido')">Suspender</button>` : ""}
                ${t.estado !== "Activo" ? `<button class="btn" onclick="estadoTenant('${t.id}','Activo')">Reactivar</button>` : ""}
                ${t.estado !== "Baja" ? `<button class="btn" onclick="estadoTenant('${t.id}','Baja')">Dar de baja</button>` : ""}
              </div></td>
            </tr>`).join("") : `<tr><td colspan="7">Todavía no hay clientes.</td></tr>`}
          </table>
        </div>
      </main>
    </div>`;
}

function sugerirCodigo() {
  const codigo = document.getElementById("t_codigo");
  if (codigo.dataset.tocado) return;
  codigo.value = val("t_nombre").toLowerCase().normalize("NFD").replace(/[̀-ͯ]/g, "")
    .replace(/\b(sa|srl|sas|s\.a\.|s\.r\.l\.)\b/g, "").replace(/[^a-z0-9]+/g, "_").replace(/^_+|_+$/g, "").slice(0, 40);
}

async function crearTenant() {
  try {
    const t = await api("/api/superadmin/tenants", { method: "POST", body: JSON.stringify({
      nombre: val("t_nombre"), codigo: val("t_codigo"), adminEmail: val("t_email"), adminNombre: val("t_admin"), adminPassword: val("t_clave")
    })});
    toast(`Cliente ${t.nombre} creado`);
    renderSuperadmin();
  } catch (e) {
    toast(limpiarError(e), "err");
  }
}

async function estadoTenant(id, estado) {
  const motivo = estado === "Activo" ? "" : prompt(estado === "Suspendido" ? "Motivo de la suspensión (ej. falta de pago)" : "Motivo de la baja");
  if (motivo === null) return;
  try {
    await api(`/api/superadmin/tenants/${id}/estado`, { method: "PUT", body: JSON.stringify({ estado, motivo }) });
    toast(`Cliente ${estado.toLowerCase()}`);
    renderSuperadmin();
  } catch (e) {
    toast(limpiarError(e), "err");
  }
}

async function entrarTenant(id) {
  try {
    const r = await api(`/api/superadmin/tenants/${id}/entrar`, { method: "POST" });
    try { localStorage.setItem("bfs_token_panel", state.token); } catch {}
    state.token = r.token;
    localStorage.setItem("bfs_token", r.token);
    state.user = await api("/api/me");
    state.view = "config";
    await boot();
  } catch (e) {
    toast(limpiarError(e), "err");
  }
}

async function volverAlPanel() {
  const panel = tokenPanel();
  try { localStorage.removeItem("bfs_token_panel"); } catch {}
  if (!panel) return logout();
  state.token = panel;
  localStorage.setItem("bfs_token", panel);
  state.user = await api("/api/me");
  render();
}

function pillNube() {
  const n = state.nube;
  if (!n?.conectado) return "";
  if (n.ultimoError) return `<span class="pill pill-err" title="${esc(n.ultimoError)}">Nube: sin conexión${n.pendientes ? ` · ${n.pendientes} pendientes` : ""}</span>`;
  return `<span class="pill pill-ok" title="Última sincronización: ${esc(n.ultimaSincronizacion ? new Date(n.ultimaSincronizacion).toLocaleString() : "-")}">Nube: en línea</span>`;
}

async function actualizarNube() {
  if (state.modo === "Nube" || esEstandar() || !state.token) return;
  try { state.nube = await api("/api/nube"); } catch { state.nube = null; }
}

async function renderConfigNube() {
  const n = state.nube = await api("/api/nube");
  document.getElementById("configBody").innerHTML = n.conectado ? `
    <div class="card">
      <div class="grid2">
        <div class="status-box"><span>Conectada a</span><strong>${esc(n.tenant || "")}</strong></div>
        <div class="status-box"><span>Servidor</span><strong>${esc(n.url)}</strong></div>
        <div class="status-box"><span>Última sincronización</span><strong>${n.ultimaSincronizacion ? esc(new Date(n.ultimaSincronizacion).toLocaleString()) : "todavía no"}</strong></div>
        <div class="status-box"><span>Cambios por enviar</span><strong>${n.pendientes}</strong></div>
      </div>
      ${n.ultimoError ? `<p class="error">${esc(n.ultimoError)}</p>` : ""}
      ${n.conflictos.length ? `<p>Conflictos de la última sincronización (no se aplicaron):</p><ul>${n.conflictos.map(c => `<li>${esc(c)}</li>`).join("")}</ul>` : ""}
      <p>Sincroniza sola cada minuto. Sin internet la portería sigue pesando y manda todo cuando vuelve la conexión.</p>
      <div class="btn-row">
        <button class="btn-primary" onclick="sincronizarAhora()">Sincronizar ahora</button>
        <button class="btn-danger" onclick="desvincularNube()">Desconectar de la nube</button>
      </div>
    </div>` : `
    <div class="card">
      <p>Conectá esta portería con la nube de BFS. El código lo genera BFS en el panel de clientes (vale 7 días y se usa una vez).</p>
      <div class="grid2">
        <div><label>Dirección de la nube</label><input id="nb_url" placeholder="https://pesaje.bfs.com.ar" /></div>
        <div><label>Código de activación</label><input id="nb_codigo" placeholder="ABCD-EFGH-JKLM" /></div>
      </div>
      <div class="btn-row"><button class="btn-primary" onclick="activarNube()">Conectar</button></div>
    </div>`;
}

async function nubeAccion(fn, ok) {
  try {
    const r = await fn();
    if (r) state.nube = r;
    toast(ok);
  } catch (e) {
    toast(limpiarError(e), "err");
  }
  await actualizarNube();
  const pill = document.getElementById("nubePill");
  if (pill) pill.innerHTML = pillNube();
  renderConfig();
}

function activarNube() {
  nubeAccion(() => api("/api/nube/activar", { method: "POST", body: JSON.stringify({ url: val("nb_url"), codigo: val("nb_codigo") }) }), "Portería conectada a la nube");
}

function sincronizarAhora() {
  nubeAccion(() => api("/api/nube/sincronizar", { method: "POST" }), "Sincronizado");
}

function desvincularNube() {
  if (!confirm("La portería deja de sincronizar con la nube (los datos locales no se borran). ¿Seguir?")) return;
  nubeAccion(() => api("/api/nube", { method: "DELETE" }), "Portería desconectada");
}

async function verNodos(tenantId, nombre) {
  const nodos = await api(`/api/superadmin/tenants/${tenantId}/nodos`);
  const fecha = d => d ? new Date(d).toLocaleString([], { dateStyle: "short", timeStyle: "short" }) : "-";
  document.getElementById("main").innerHTML = `
    <div class="card">
      <h3 style="margin-top:0">Porterías de ${esc(nombre)}</h3>
      <table>
        <tr><th>Nombre</th><th>PC</th><th>Activada</th><th>Última sincronización</th><th></th></tr>
        ${nodos.length ? nodos.map(n => `<tr>
          <td>${esc(n.nombre)}</td><td>${esc(n.maquina || "-")}</td><td>${fecha(n.activadoEn)}${n.codigoPendiente ? " (código pendiente)" : ""}</td>
          <td>${fecha(n.ultimaSincronizacion)}</td>
          <td><button class="btn" onclick="codigoNodo('${tenantId}','${esc(nombre)}','${n.id}')">Código nuevo</button></td>
        </tr>`).join("") : `<tr><td colspan="5">Todavía no tiene porterías.</td></tr>`}
      </table>
      <div class="grid2" style="margin-top:12px"><div><label>Nueva portería</label><input id="nd_nombre" placeholder="Balanza principal" /></div></div>
      <div class="btn-row">
        <button class="btn-primary" onclick="codigoNodo('${tenantId}','${esc(nombre)}')">Crear y generar código</button>
        <button class="btn" onclick="renderSuperadmin()">Volver</button>
      </div>
      <div id="nd_codigo"></div>
    </div>`;
}

async function codigoNodo(tenantId, nombre, nodoId) {
  if (nodoId && !confirm("Un código nuevo desconecta la PC que estaba usando esta portería. ¿Seguir?")) return;
  try {
    const r = await api(`/api/superadmin/tenants/${tenantId}/nodos`, { method: "POST", body: JSON.stringify({ nombre: val("nd_nombre") || null, nodoId: nodoId || null }) });
    await verNodos(tenantId, nombre);
    document.getElementById("nd_codigo").innerHTML = `
      <div class="status-box" style="margin-top:12px"><span>Código para ${esc(r.nombre)} (se muestra una sola vez, vence ${esc(new Date(r.vence).toLocaleDateString())})</span>
      <strong style="font-size:24px;letter-spacing:2px">${esc(r.codigo)}</strong></div>`;
  } catch (e) {
    toast(limpiarError(e), "err");
  }
}

/* ---------- Automatización: semáforos, barreras, sensores y cámaras ----------
 * Solo aparece si BFS la habilitó (Configuración → Automatización). El estado se pide junto con el peso
 * y las cámaras se refrescan varias veces por segundo solo mientras se está en Pesaje. */

const NOMBRE_TIPO = { semaforo: "Semáforo", barrera: "Barrera", sensor: "Sensor" };

function nombreBalanzaOrden(orden) {
  return state.balanzas[orden - 1]?.nombre || `Balanza ${orden}`;
}

function bloqueAutomatizacion() {
  const a = state.auto;
  if (!a) return `<span class="captura-ayuda">Conectando con la placa de automatización…</span>`;
  if (!a.habilitada) return "";
  const ordenes = [...new Set(a.dispositivos.map(d => d.balanza))].sort();
  const estado = a.conectada
    ? `<span class="pill pill-ok">Placa conectada${a.simulada ? " · simulada" : ""}</span>`
    : `<span class="pill pill-err" title="${esc(a.error || "")}">Placa sin conexión</span>`;
  return `<div class="auto-cabecera">${estado}${a.secuenciaAutomatica ? `<span class="pill pill-info">Secuencia automática</span>` : ""}
      ${!a.conectada && a.error ? `<small class="auto-error">${esc(a.error)}</small>` : ""}</div>
    <div class="auto-balanzas">${ordenes.map(orden => {
      const de = tipo => a.dispositivos.filter(d => d.balanza === orden && d.tipo === tipo);
      const sensores = de("sensor");
      const libres = sensores.filter(s => !s.activo);
      return `<div class="auto-balanza">
        <div class="auto-titulo">${esc(nombreBalanzaOrden(orden))}</div>
        <div class="auto-fila">
          ${de("semaforo").map(s => `<div class="semaforo" title="${esc(s.nombre)} (salida ${s.punto})">
            <span class="luz roja ${s.activo ? "" : "on"}" onclick="cambiarSalidaAuto(${s.punto}, false)"></span>
            <span class="luz verde ${s.activo ? "on" : ""}" onclick="cambiarSalidaAuto(${s.punto}, true)"></span>
            <small>${esc(s.nombre)}</small></div>`).join("")}
          ${de("barrera").map(b => `<button class="btn barrera ${b.activo ? "abierta" : ""}" onclick="cambiarSalidaAuto(${b.punto}, ${!b.activo})" title="Salida ${b.punto}">
            ${esc(b.nombre)}<strong>${b.activo ? "Abierta" : "Cerrada"}</strong></button>`).join("")}
          ${sensores.map(s => `<span class="sensor ${s.activo ? "on" : "cortado"}" title="Entrada ${s.punto}: ${s.activo ? "en verde, nadie lo corta" : "cortado"}">
            <span class="led"></span>${esc(s.nombre)}${s.activo ? "" : " · cortado"}</span>`).join("")}
        </div>
        ${sensores.length && libres.length ? `<div class="auto-aviso">Posicionar el camión dentro de la balanza (${libres.map(s => esc(s.nombre)).join(" y ")} ${libres.length === 1 ? "cortado" : "cortados"})</div>` : ""}
      </div>`;
    }).join("")}</div>`;
}

function pintarAutomatizacion() {
  const el = document.getElementById("auto");
  if (!el) return;
  el.innerHTML = bloqueAutomatizacion();
  asegurarCamaras();
}

async function cambiarSalidaAuto(punto, valor) {
  try {
    state.auto = await api(`/api/automatizacion/salidas/${punto}`, { method: "POST", body: JSON.stringify({ valor }) });
    pintarAutomatizacion();
  } catch (e) {
    toast(limpiarError(e), "err");
  }
}

/** Una imagen por cámara; el bucle las va renovando mientras se esté en Pesaje. */
function asegurarCamaras() {
  const cont = document.getElementById("camaras");
  const nombres = state.auto?.camaras || [];
  if (!cont) return;
  if (cont.dataset.n !== String(nombres.length)) {
    cont.dataset.n = String(nombres.length);
    cont.innerHTML = nombres.map((n, i) => `<div class="camara"><img id="cam_${i}" alt="" /><span>${esc(n)}</span></div>`).join("");
  }
  if (nombres.length && !state.camarasCorriendo) loopCamaras();
}

async function loopCamaras() {
  state.camarasCorriendo = true;
  try {
    while (state.view === "pesaje" && document.getElementById("camaras") && (state.auto?.camaras || []).length && state.token) {
      const inicio = Date.now();
      await Promise.all(state.auto.camaras.map((_, i) => refrescarCamara(i)));
      // Con la ventana minimizada no hace falta mirar las cámaras.
      const pausa = document.hidden ? 2000 : Math.max(0, 250 - (Date.now() - inicio));
      await new Promise(r => setTimeout(r, pausa));
    }
  } finally {
    state.camarasCorriendo = false;
  }
}

async function refrescarCamara(i) {
  const img = document.getElementById("cam_" + i);
  if (!img) return;
  try {
    const res = await fetch(`/api/automatizacion/camaras/${i}`, { headers: { Authorization: "Bearer " + state.token } });
    if (!res.ok) throw new Error();
    const url = URL.createObjectURL(await res.blob());
    const vieja = img.src;
    img.src = url;
    img.parentElement.classList.remove("sin-senal");
    if (vieja.startsWith("blob:")) setTimeout(() => URL.revokeObjectURL(vieja), 1000);
  } catch {
    img.parentElement.classList.add("sin-senal");
  }
}

/* Configuración → Automatización (BFS) */

async function renderConfigAutomatizacion() {
  const c = await api("/api/automatizacion/config");
  state.autoConfig = c;
  const lados = ["entrada", "salida"];
  const camaras = [0, 1].map(i => c.camaras[i] || { nombre: `Cámara ${i + 1}`, url: "", usuario: "", clave: "", habilitada: false });
  document.getElementById("configBody").innerHTML = `
    <div class="card">
      <p>Semáforos, barreras y sensores por una placa Modbus TCP, y hasta dos cámaras IP. Se habilita cuando el cliente la compra;
        apagada, el sistema no se conecta a nada.</p>
      <div class="grid3">
        <div><label>Automatización</label>${yesNo("au_hab", String(c.habilitada))}</div>
        <div><label>Placa simulada (para probar sin hardware)</label>${yesNo("au_sim", String(c.simulada))}</div>
        <div></div>
        <div><label>IP de la placa</label><input id="au_ip" value="${esc(c.modbusIp)}" /></div>
        <div><label>Puerto</label><input id="au_puerto" type="number" value="${c.modbusPuerto}" /></div>
        <div><label>Unidad Modbus</label><input id="au_unidad" type="number" value="${c.modbusUnidad}" /></div>
        <div><label>Secuencia automática (al aceptar: semáforo y barrera de salida; cuando el camión pasa por un sensor y todos vuelven a verde, vuelve a la entrada)</label>${yesNo("au_seq", String(c.secuenciaAutomatica))}</div>
        <div><label>Fotos con las cámaras en cada pesada</label>${yesNo("au_fotos", String(c.capturarFotos))}</div>
      </div>
    </div>
    <div class="card">
      <h3 style="margin-top:0">Dispositivos</h3>
      <p>Semáforos y barreras van en las salidas de la placa; los sensores (barreras infrarrojas), en las entradas.
        Los sensores van en las dos puntas de la balanza y están en verde mientras nadie los corta. Con sensores instalados, el peso de esa balanza solo se captura con todos en verde (el camión entero arriba, sin pisar las puntas).</p>
      <table>
        <tr><th>Tipo</th><th>Nombre</th><th>${"Entrada / salida"}</th><th>Balanza</th><th>Punta</th><th>Instalado</th></tr>
        ${c.dispositivos.map((d, i) => `<tr>
          <td>${NOMBRE_TIPO[d.tipo] || esc(d.tipo)}</td>
          <td><input id="au_d_nombre_${i}" value="${esc(d.nombre)}" /></td>
          <td><input id="au_d_punto_${i}" type="number" min="1" max="8" value="${d.punto}" style="max-width:80px" /></td>
          <td><select id="au_d_bal_${i}">${[1, 2].map(n => `<option value="${n}" ${d.balanza === n ? "selected" : ""}>${n}</option>`).join("")}</select></td>
          <td><select id="au_d_lado_${i}">${lados.map(l => `<option ${d.lado === l ? "selected" : ""}>${l}</option>`).join("")}</select></td>
          <td><input type="checkbox" id="au_d_inst_${i}" style="width:auto" ${d.instalado ? "checked" : ""} /></td>
        </tr>`).join("")}
      </table>
    </div>
    <div class="card">
      <h3 style="margin-top:0">Cámaras</h3>
      <p>Dirección de la foto de la cámara. Dahua: <code>http://IP/cgi-bin/snapshot.cgi</code> · Hikvision: <code>http://IP/ISAPI/Streaming/channels/101/picture</code> · <code>sim</code> = cámara de prueba.</p>
      ${camaras.map((cam, i) => `<div class="grid3" style="margin-bottom:10px">
        <div><label>Nombre</label><input id="au_c_nombre_${i}" value="${esc(cam.nombre)}" /></div>
        <div style="grid-column:span 2"><label>Dirección (URL)</label><input id="au_c_url_${i}" value="${esc(cam.url)}" placeholder="http://192.168.1.108/cgi-bin/snapshot.cgi" /></div>
        <div><label>Usuario</label><input id="au_c_usuario_${i}" value="${esc(cam.usuario || "")}" autocomplete="off" /></div>
        <div><label>Clave</label><input id="au_c_clave_${i}" type="password" value="${esc(cam.clave || "")}" autocomplete="new-password" /></div>
        <div><label>Habilitada</label>${yesNo("au_c_hab_" + i, String(cam.habilitada))}</div>
        <div class="btn-row" style="grid-column:1/-1;margin:0"><button class="btn" onclick="probarCamara(${i})">Probar cámara</button>
          <img id="au_c_prueba_${i}" class="prueba-camara" alt="" /></div>
      </div>`).join("")}
    </div>
    <div class="btn-row"><button class="btn-primary" onclick="saveAutomatizacion()">Guardar automatización</button></div>
    <div class="card" id="autoPrueba"></div>`;
  pruebaAutomatizacion();
}

function leerConfigAutomatizacion() {
  const c = state.autoConfig;
  return {
    habilitada: val("au_hab") === "true",
    simulada: val("au_sim") === "true",
    modbusIp: val("au_ip").trim(),
    modbusPuerto: num("au_puerto") || 502,
    modbusUnidad: num("au_unidad"),
    secuenciaAutomatica: val("au_seq") === "true",
    capturarFotos: val("au_fotos") === "true",
    dispositivos: c.dispositivos.map((d, i) => ({
      ...d,
      nombre: val(`au_d_nombre_${i}`) || d.nombre,
      punto: num(`au_d_punto_${i}`) || d.punto,
      balanza: Number(val(`au_d_bal_${i}`)),
      lado: val(`au_d_lado_${i}`),
      instalado: document.getElementById(`au_d_inst_${i}`).checked
    })),
    camaras: [0, 1].map(i => ({
      nombre: val(`au_c_nombre_${i}`) || `Cámara ${i + 1}`,
      url: val(`au_c_url_${i}`).trim(),
      usuario: val(`au_c_usuario_${i}`) || null,
      clave: val(`au_c_clave_${i}`) || null,
      habilitada: val(`au_c_hab_${i}`) === "true"
    })).filter(cam => cam.url)
  };
}

async function saveAutomatizacion() {
  try {
    await api("/api/automatizacion/config", { method: "PUT", body: JSON.stringify(leerConfigAutomatizacion()) });
    state.funciones = await api("/api/funciones");
    toast("Automatización guardada");
    renderConfig();
  } catch (e) {
    toast(limpiarError(e), "err");
  }
}

async function probarCamara(i) {
  const cam = leerConfigAutomatizacion().camaras.find(x => x.nombre === (val(`au_c_nombre_${i}`) || `Cámara ${i + 1}`));
  if (!cam) return toast("Falta la dirección de la cámara", "err");
  try {
    const res = await fetch("/api/automatizacion/camaras/probar", {
      method: "POST",
      headers: { "Content-Type": "application/json", Authorization: "Bearer " + state.token },
      body: JSON.stringify(cam)
    });
    if (!res.ok) throw new Error(await res.text());
    document.getElementById(`au_c_prueba_${i}`).src = URL.createObjectURL(await res.blob());
  } catch (e) {
    toast(limpiarError(e), "err");
  }
}

/** Entradas y salidas de la placa en vivo, mientras esté abierta la pestaña. Con la placa simulada, las entradas se tocan. */
async function pruebaAutomatizacion() {
  const el = document.getElementById("autoPrueba");
  if (!el || state.view !== "config" || state.configTab !== "automatizacion") return;
  try {
    const a = await api("/api/automatizacion/estado");
    const punto = (activo, n, accion, titulo) =>
      `<button class="punto ${activo ? "on" : ""}" ${accion ? `onclick="${accion}(${n}, ${!activo})"` : "disabled"} title="${esc(titulo)}">${n}</button>`;
    const nombre = (tipoSalida, n) => (a.dispositivos.find(d => (tipoSalida ? d.tipo !== "sensor" : d.tipo === "sensor") && d.punto === n)?.nombre) || "";
    el.innerHTML = !a.habilitada
      ? `<h3 style="margin-top:0">Prueba</h3><p>Habilitá la automatización y guardá para probar la placa.</p>`
      : `<h3 style="margin-top:0">Prueba de la placa ${a.conectada ? `<span class="pill pill-ok">conectada</span>` : `<span class="pill pill-err">sin conexión</span>`}</h3>
        ${a.error ? `<p class="error">${esc(a.error)}</p>` : ""}
        <div class="auto-prueba"><span>Entradas${a.simulada ? " (prendida = sensor en verde; tocá para cortarlo)" : " (prendida = sensor en verde)"}</span>
          ${a.entradas.map((v, i) => punto(v, i + 1, a.simulada ? "simularEntrada" : null, nombre(false, i + 1))).join("")}</div>
        <div class="auto-prueba"><span>Salidas (tocá para prender / apagar)</span>
          ${a.salidas.map((v, i) => punto(v, i + 1, "probarSalida", nombre(true, i + 1))).join("")}</div>`;
  } catch {}
  setTimeout(pruebaAutomatizacion, 1000);
}

async function simularEntrada(n, valor) {
  try { await api(`/api/automatizacion/simular/${n}`, { method: "POST", body: JSON.stringify({ valor }) }); } catch (e) { toast(limpiarError(e), "err"); }
}

async function probarSalida(n, valor) {
  try { await api(`/api/automatizacion/salidas/${n}`, { method: "POST", body: JSON.stringify({ valor }) }); } catch (e) { toast(limpiarError(e), "err"); }
}

/* ---------- Listas de catálogos (en lugar del desplegable del navegador) ----------
 * Cualquier campo con list="dl_..." de un catálogo se convierte solo: se escribe en el campo y abajo aparece
 * la lista filtrada, con iniciales, nombre y un detalle (CUIT, IVA, código), ↑↓ / Enter / Esc y, al pie,
 * "Crear ... nuevo" (el alta rápida). Se sigue pudiendo dejar un texto que no está en el catálogo. */

const COMBOS = {
  dl_clientes: { tipo: "clientes", singular: "cliente", items: () => (state.catalogos.clientes || []).map(c => item(c.razonSocial, c.cuit, c.condicionIva)) },
  dl_transportes: { tipo: "transportes", singular: "transporte", items: () => (state.catalogos.transportes || []).map(t => item(t.razonSocial, t.cuit, t.esAta ? `ATA ${t.codigoAta || ""}` : "")) },
  dl_atas: { tipo: "atas", singular: "ATA", items: () => (state.catalogos.transportes || []).filter(t => t.esAta).map(t => item(t.razonSocial, t.cuit, t.codigoAta)) },
  dl_productos: { tipo: "productos", singular: "producto", items: () => (state.catalogos.productos || []).map(p => item(p.nombre, p.codigo ? `Código ${p.codigo}` : "")) },
  dl_exportadores: { tipo: "exportadores", singular: "exportador", items: () => (state.catalogos.exportadores || []).map(x => item(x.razonSocial, x.cuit)) },
  dl_tanques: { tipo: "tanques", singular: "tanque", items: () => (state.catalogos.tanques || []).map(t => item(t.nombre)) },
  dl_sucursales: { tipo: "sucursales", singular: "sucursal", items: () => (state.catalogos.sucursales || []).map(s => item(s.nombre)) }
};

const COMBO_MAX = 80;
const combo = { input: null, lista: [], activo: 0 };

function item(nombre, ...detalle) {
  return { nombre: nombre || "", detalle: detalle.filter(Boolean).join(" · ") };
}

function normalizar(s) {
  return String(s || "").toLowerCase().normalize("NFD").replace(/[̀-ͯ]/g, "");
}

function iniciales(nombre) {
  const palabras = String(nombre).replace(/[^\p{L}\p{N} ]/gu, " ").trim().split(/\s+/).filter(Boolean);
  return ((palabras[0]?.[0] || "") + (palabras[1]?.[0] || palabras[0]?.[1] || "")).toUpperCase();
}

/** Los ítems del catálogo; si no está cargado en pantalla, los del datalist que armó la pantalla. */
function itemsCombo(input) {
  const def = COMBOS[input.dataset.combo];
  const desdeCatalogo = def.items();
  if (desdeCatalogo.length) return desdeCatalogo;
  return [...(document.getElementById(input.dataset.combo)?.options || [])].map(o => item(o.value));
}

function filtrarCombo(input) {
  const q = normalizar(input.value).trim();
  const todos = itemsCombo(input);
  if (!q) return todos;
  const coincide = todos.filter(i => normalizar(i.nombre + " " + i.detalle).includes(q));
  // Primero los que empiezan con lo escrito.
  return coincide.sort((a, b) => (normalizar(b.nombre).startsWith(q) ? 1 : 0) - (normalizar(a.nombre).startsWith(q) ? 1 : 0));
}

function panelCombo() {
  let p = document.getElementById("combo-panel");
  if (!p) {
    p = document.createElement("div");
    p.id = "combo-panel";
    p.className = "combo-panel";
    // mousedown: que el campo no pierda el foco antes de elegir.
    p.addEventListener("mousedown", e => e.preventDefault());
    document.body.appendChild(p);
  }
  return p;
}

function abrirCombo(input) {
  if (input.disabled || input.readOnly) return;
  combo.input = input;
  combo.lista = filtrarCombo(input);
  combo.activo = 0;
  pintarCombo();
}

function pintarCombo() {
  const input = combo.input;
  if (!input) return;
  const def = COMBOS[input.dataset.combo];
  const p = panelCombo();
  const r = input.getBoundingClientRect();
  const ancho = Math.max(r.width, 300);
  p.style.left = Math.min(r.left, window.innerWidth - ancho - 8) + "px";
  p.style.width = ancho + "px";
  // Abajo del campo; si no entra, arriba.
  const abajo = window.innerHeight - r.bottom;
  p.style.top = abajo > 300 || abajo > r.top ? r.bottom + 4 + "px" : "";
  p.style.bottom = abajo > 300 || abajo > r.top ? "" : window.innerHeight - r.top + 4 + "px";

  const lista = combo.lista.slice(0, COMBO_MAX);
  const crear = ALTA_RAPIDA[def.tipo] && can("catalogos.editar");
  p.innerHTML = `
    <div class="combo-lista">
      ${lista.length ? lista.map((i, n) => `
        <div class="combo-item ${n === combo.activo ? "activo" : ""}" data-n="${n}">
          <span class="combo-avatar">${esc(iniciales(i.nombre))}</span>
          <span class="combo-texto"><strong>${esc(i.nombre)}</strong>${i.detalle ? `<small>${esc(i.detalle)}</small>` : ""}</span>
        </div>`).join("")
        : `<div class="combo-vacio">${input.value.trim() ? `No hay ${esc(def.singular)}s con "${esc(input.value.trim())}"` : `Todavía no hay ${esc(def.singular)}s cargados`}</div>`}
      ${combo.lista.length > COMBO_MAX ? `<div class="combo-vacio">${combo.lista.length - COMBO_MAX} más: seguí escribiendo para filtrar</div>` : ""}
    </div>
    ${crear ? `<div class="combo-crear" data-crear="1">+ Crear ${esc(def.singular)} nuevo</div>` : ""}
    <div class="combo-pie"><span><kbd>↑↓</kbd> moverse</span><span><kbd>↵</kbd> elegir</span><span><kbd>esc</kbd> cerrar</span></div>`;
  p.style.display = "flex";
  p.querySelectorAll(".combo-item").forEach(el => el.addEventListener("click", () => elegirCombo(Number(el.dataset.n))));
  p.querySelector("[data-crear]")?.addEventListener("click", () => {
    const campo = combo.input;
    cerrarCombo();
    altaRapida(def.tipo, campo.id);
  });
  p.querySelector(".combo-item.activo")?.scrollIntoView({ block: "nearest" });
}

function elegirCombo(n) {
  const input = combo.input;
  const elegido = combo.lista[n];
  if (!input || !elegido) return;
  input.value = elegido.nombre;
  cerrarCombo();
  // Como si lo hubiera elegido del desplegable: completa CUIT, código de producto, etc.
  input.dispatchEvent(new Event("change", { bubbles: true }));
}

function cerrarCombo() {
  combo.input = null;
  const p = document.getElementById("combo-panel");
  if (p) p.style.display = "none";
}

/** Convierte los campos con list="dl_..." de catálogos en cuanto aparecen en pantalla. */
function convertirCombos(raiz) {
  raiz.querySelectorAll?.("input[list]").forEach(input => {
    const lista = input.getAttribute("list");
    if (!COMBOS[lista]) return;
    input.dataset.combo = lista;
    input.removeAttribute("list");
    input.setAttribute("autocomplete", "off");
    input.classList.add("combo-input");
  });
}

new MutationObserver(cambios => {
  for (const c of cambios) c.addedNodes.forEach(n => n.nodeType === 1 && convertirCombos(n.parentNode || n));
}).observe(document.body, { childList: true, subtree: true });

document.addEventListener("focusin", e => {
  if (e.target.classList?.contains("combo-input")) abrirCombo(e.target);
});
document.addEventListener("click", e => {
  if (e.target.classList?.contains("combo-input") && combo.input !== e.target) abrirCombo(e.target);
});
document.addEventListener("input", e => {
  if (!e.target.classList?.contains("combo-input")) return;
  combo.input = e.target;
  combo.lista = filtrarCombo(e.target);
  combo.activo = 0;
  pintarCombo();
});
document.addEventListener("focusout", e => {
  if (e.target === combo.input) cerrarCombo();
});
document.addEventListener("keydown", e => {
  if (!combo.input || e.target !== combo.input) return;
  const total = Math.min(combo.lista.length, COMBO_MAX);
  if (e.key === "ArrowDown" || e.key === "ArrowUp") {
    e.preventDefault();
    if (!total) return;
    combo.activo = (combo.activo + (e.key === "ArrowDown" ? 1 : total - 1)) % total;
    pintarCombo();
  } else if (e.key === "Enter") {
    if (total) {
      e.preventDefault();
      elegirCombo(combo.activo);
    } else {
      cerrarCombo();
    }
  } else if (e.key === "Escape") {
    e.preventDefault();
    e.stopPropagation();
    cerrarCombo();
  } else if (e.key === "Tab") {
    cerrarCombo();
  }
});
// Al mover la página la lista quedaría despegada del campo: se cierra (la propia lista sí se puede scrollear).
window.addEventListener("scroll", e => {
  if (combo.input && !document.getElementById("combo-panel")?.contains(e.target)) cerrarCombo();
}, true);
window.addEventListener("resize", cerrarCombo);

start();
