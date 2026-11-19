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
  selectedPesadaId: null,
  /** Peso capturado esperando que se acepte la pesada ({ id, kilogramos, balanza, en }). */
  captura: null,
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
function nombreEdicion() { return esEstandar() ? " <small>Estándar</small>" : state.edicion === "Inagro" ? " <small>INAGRO</small>" : ""; }

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

function elegirEmpresa(id) {
  try { localStorage.setItem("bfs_empresa", id); } catch {}
  document.querySelectorAll(".empresa-btn").forEach(b => b.classList.toggle("active", b.dataset.id === id));
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

function displayBalanza(b) {
  const [cls, estado] = estadoDisplay(b);
  const varias = state.balanzas.length > 1;
  const enUso = varias && b.balanzaId === balanzaActual().balanzaId;
  return `<div class="lcd ${cls} ${enUso ? "en-uso" : ""}" data-balanza="${b.balanzaId || ""}"
      ${varias ? `onclick="elegirBalanza('${b.balanzaId}')" title="Tocar para pesar con esta balanza"` : ""}>
    <div class="lcd-top">
      <span class="lcd-nombre">${esc(b.nombre || b.puerto || "Balanza")}</span>
      ${enUso ? `<span class="lcd-chip">En uso</span>` : ""}
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
            <h1>Sistema de Pesaje BFS 4.0${nombreEdicion()}</h1>
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
      <div class="login-card">
        ${bfsLogo("login-logo")}
        <h2>Ingreso</h2>
        <p style="color:var(--muted);margin-top:0">${esc(state.branding.clienteNombre || "Sistema de pesaje")} · la configuración la hace BFS.</p>
        <label>Usuario o email</label>
        <input id="user" type="text" autocomplete="username" autocapitalize="none" spellcheck="false" />
        <label style="margin-top:12px">Contraseña</label>
        <input id="pass" type="password" autocomplete="current-password" />
        <p class="error" id="err"></p>
        <button class="btn-primary" style="width:100%;margin-top:8px" onclick="doLogin()">Entrar</button>
      </div>
    </div>
  `;
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
  await actualizarNube();
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
  atas: { catalogo: "transportes", lista: "dl_atas", nombre: "razonSocial", extra: { esAta: true }, cuit: "ad_ataCuit" },
  // Edición INAGRO
  tanques: { lista: "dl_tanques", nombre: "nombre" },
  sucursales: { lista: "dl_sucursales", nombre: "nombre" }
};

/** El campo con un botón + al lado para dar de alta en el catálogo sin salir de la pantalla. */
function conAlta(input, tipo, campoId) {
  if (!can("catalogos.editar")) return input;
  return `<div class="con-alta">${input}<button type="button" class="btn btn-alta" title="Agregar al catálogo"
    onclick="altaRapida('${tipo}', '${campoId}')">+</button></div>`;
}

function camposAlta(tipo) {
  const a = ALTA_RAPIDA[tipo];
  // Con los campos extra de la edición (en INAGRO, el domicilio de clientes y transportes).
  const campos = camposCatalogo(CATALOGOS[a.catalogo || tipo]);
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
    api("/api/catalogos/clientes"),
    api("/api/catalogos/transportes"),
    api("/api/catalogos/productos"),
    state.funciones.aduana ? api("/api/catalogos/exportadores") : Promise.resolve([]),
    esInagro() ? api("/api/catalogos/tanques") : Promise.resolve([]),
    esInagro() ? api("/api/catalogos/sucursales") : Promise.resolve([])
  ];
  const [pendientes, clientes, transportes, productos, exportadores, tanques, sucursales] = await Promise.all(pedidos);
  state.pendientes = pendientes;
  state.catalogos = { clientes, transportes, productos, exportadores, tanques, sucursales };
  const inagro = esInagro();
  const lugar = inagro ? { list: "dl_sucursales", alta: "sucursales" } : {};
  if (!modosPesaje().some(([id]) => id === state.modoPesaje)) state.modoPesaje = "normal";
  const modo = state.modoPesaje;
  const sinLicencia = state.licencia && !state.licencia.puedePesar;
  // Pendiente elegida: la pantalla muestra sus datos de entrada y solo deja capturar la salida.
  const salida = modo === "normal" ? pendientes.find(p => p.id === state.selectedPesadaId) : null;
  if (!salida) state.selectedPesadaId = null;
  const f = (key, html) => visible(key) ? html : "";
  const lbl = (key, porDefecto) => field(key)?.label || porDefecto;

  const operacion = `
        ${inagro ? inagroOperacion() : f("tipo", inputField("tipo", lbl("tipo", "Tipo"), { type: "select", options: "<option>INGRESO</option><option>EGRESO</option>" }))}
        ${state.funciones.aduana ? `<div><label>Operación</label><select id="tipoOperacion" onchange="toggleAduana()">
          <option value="Normal">Normal</option><option value="Aduana">Aduana</option>
        </select></div>` : ""}`;
  const carga = `
        ${f("producto", inputField("producto", lbl("producto", "Producto"), { list: "dl_productos", alta: "productos" }))}
        ${f("cliente", inputField("cliente", lbl("cliente", "Cliente"), { list: "dl_clientes", alta: "clientes" }))}
        ${f("transporte", inputField("transporte", lbl("transporte", "Transporte"), { list: "dl_transportes", alta: "transportes" }))}
        ${f("procedencia", inputField("procedencia", lbl("procedencia", "Procedencia"), lugar))}
        ${f("destino", inputField("destino", lbl("destino", "Destino"), lugar))}
        ${f("comprobante", inputField("comprobante", lbl("comprobante", "Remito")))}
        ${inagro ? inagroCarga() : ""}
        ${f("taraPallets", inputField("taraPallets", lbl("taraPallets", "Tara pallets (kg)"), { type: "number", value: "0" }))}`;
  const vehiculo = `
        ${f("patenteChasis", inputField("patenteChasis", modo === "tara" ? "Patente / carro" : lbl("patenteChasis", "Patente chasis"), { blur: "autocompletarPatente()" }))}
        ${modo === "tara" ? `<div class="status-box"><span>Tara guardada</span><strong id="taraGuardada">-</strong></div>` : ""}
        ${f("patenteAcoplado", inputField("patenteAcoplado", lbl("patenteAcoplado", "Patente acoplado")))}
        ${f("conductor", inputField("conductor", lbl("conductor", "Conductor")))}
        ${f("tipoDocumento", inputField("tipoDocumento", lbl("tipoDocumento", "Tipo de documento"), { type: "select", options: `<option></option>${opciones(TIPOS_DOCUMENTO)}` }))}
        ${f("documento", inputField("documento", lbl("documento", "Documento")))}
        ${f("nacionalidad", inputField("nacionalidad", lbl("nacionalidad", "Nacionalidad")))}
        ${inagro ? `<div><label>CUIL chofer</label><input id="in_cuilChofer" inputmode="numeric" maxlength="13" placeholder="11 números" /></div>` : ""}`;

  const datosVehiculo = `
      ${seccion("Operación", operacion)}
      ${seccion("Carga", carga)}
      ${seccion("Vehículo y chofer", vehiculo)}
      ${bloqueAduana()}
      ${visible("observaciones") ? `<div style="margin-top:12px">${inputField("observaciones", lbl("observaciones", "Observaciones"), { type: "textarea" })}</div>` : ""}
      ${inagro ? `<div style="margin-top:12px"><label>Observaciones del remito</label><textarea id="in_observacionesRemito"></textarea></div>
      ${datalist("dl_tanques", tanques.map(t => t.nombre))}
      ${datalist("dl_sucursales", sucursales.map(s => s.nombre))}` : ""}
      ${datalist("dl_clientes", clientes.map(c => c.razonSocial))}
      ${datalist("dl_transportes", transportes.map(t => t.razonSocial))}
      ${datalist("dl_productos", productos.map(p => p.nombre))}`;

  const formulario = modo === "tomar" ? `
    <div class="card">
      <h3 style="margin-top:0">Tomar tara</h3>
      <p>Subí el vehículo vacío a la balanza. La tara queda guardada para pesar después con "Tara conocida".</p>
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
      </div>` : `<h3 style="margin-top:0">${modo === "tara" ? "Pesar con tara conocida" : "Datos del ticket"}</h3>`}
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
    <div class="page-head">
      <div>
        <h2>Pesaje</h2>
        <p>${modo === "tara" ? "Capturá el bruto, completá los datos y aceptá: se descuenta la tara guardada." : modo === "tomar" ? "Guardá la tara de un vehículo o carro." : salida ? "Salida: capturá el peso y aceptá." : "Entrada: capturá el peso, completá los datos y aceptá. Para la salida, elegí el ticket en Pendientes."}</p>
      </div>
      <div id="scalePill">${pillForScale(state.scale.estado)}</div>
    </div>
    ${sinLicencia ? `
    <div class="card" style="border-left:4px solid #c0392b">
      <strong>${esc(state.licencia.mensaje)}</strong>
      <p style="margin:6px 0 0">ID de máquina: <code>${esc(state.licencia.maquina)}</code>. No se pueden registrar entradas nuevas; sí cerrar las salidas pendientes.</p>
    </div>` : ""}
    <div class="card peso-card">
      <div class="displays ${state.balanzas.length > 1 ? "dos" : ""}" id="displays">${balanzasEnPantalla().map(displayBalanza).join("")}</div>
      ${modo !== "tomar" ? `<div class="captura" id="captura">${bloqueCaptura()}</div>` : ""}
      <div class="peso-pie">
        <div class="status-box"><span>Empresa en curso</span>${selectorEmpresa(salida?.empresaId)}</div>
        <div class="peso-ayuda">
          ${state.balanzas.length > 1 ? `<button class="btn" onclick="intercambiarBalanzas()" title="Pasa la balanza de la izquierda a la derecha">⇄ Intercambiar lados</button>
          <span>Tocá un display para pesar con esa balanza.</span>` : ""}
          ${state.funciones.exigirEstable ? `<span>Se captura solo con el peso estable.</span>` : ""}
        </div>
      </div>
    </div>
    ${modosPesaje().length > 1 ? `<div class="tabs">${modosPesaje().map(([id, label]) =>
      `<button class="tab ${modo === id ? "active" : ""}" onclick="cambiarModoPesaje('${id}')">${label}</button>`).join("")}</div>` : ""}
    ${formulario}
    ${modo === "normal" ? `
    <div class="card">
      <h3 style="margin-top:0">Pendientes de salida (${pendientes.length})</h3>
      <table>
        <tr><th></th><th>Ticket</th><th>Empresa</th><th>Patente</th><th>Entrada</th><th>Hora</th><th>Pallets</th><th></th></tr>
        ${pendientes.length ? pendientes.map(p => `<tr class="${state.selectedPesadaId === p.id ? "selected" : ""}" onclick="selectPesada('${p.id}')">
          <td>${state.selectedPesadaId === p.id ? "●" : ""}</td>
          <td>${esc(p.ticketNumero)}${p.tipoOperacion === "Aduana" ? ` <span class="pill pill-info">Aduana</span>` : ""}</td>
          <td>${esc(empresaName(p.empresaId))}</td>
          <td>${esc(p.patenteChasis)}</td>
          <td>${p.pesoEntrada}${p.entradaManualPor ? " ✎" : ""}</td>
          <td>${esc(hora(p.entradaEn))}</td>
          <td>${p.taraPallets}</td>
          <td><div class="btn-row" style="margin:0">${botonesTicket(p.id)}${can("pesaje.anular") ? `<button class="btn-danger" onclick="event.stopPropagation();anularPesada('${p.id}')">Anular</button>` : ""}</div></td>
        </tr>`).join("") : `<tr><td colspan="8">No hay entradas pendientes.</td></tr>`}
      </table>
    </div>` : ""}
  `;
  if (inagro) prepararInagro();
  if (salida) mostrarEntrada(salida);
}

/** Id de operación (compras / ventas / OT) y peso de origen, que solo va en compras y OT. */
function inagroOperacion() {
  return `
        <div><label>Id de operación *</label><select id="in_movimiento" onchange="togglePesoOrigen()">
          <option value="">Elegir...</option><option value="1">1: Compras</option><option value="2">2: Ventas</option><option value="3">3: OT</option>
        </select></div>
        <div><label>Peso de origen (kg)</label><input id="in_pesoOrigen" type="number" min="0" disabled placeholder="Compras y OT" /></div>`;
}

function inagroCarga() {
  return `
        <div><label>Tanque</label>${conAlta(`<input id="in_tanque" list="dl_tanques" autocomplete="off" maxlength="40" />`, "tanques", "in_tanque")}</div>
        <div><label>Código producto</label><input id="in_productoCodigo" maxlength="10" /></div>
        <div><label>Análisis</label><input id="in_analisis" inputmode="numeric" maxlength="6" /></div>
        <div><label>Precintos</label><input id="in_precintos" /></div>`;
}

/** Al elegir el producto se completa su código; el remito se escribe con la máscara 00000-00000000. */
function prepararInagro() {
  document.getElementById("producto")?.addEventListener("change", () => {
    const p = (state.catalogos.productos || []).find(x => x.nombre === val("producto"));
    const cod = document.getElementById("in_productoCodigo");
    if (p && cod) cod.value = p.codigo || "";
  });
  document.getElementById("comprobante")?.addEventListener("input", e => mascaraRemito(e.target));
}

function togglePesoOrigen() {
  const lleva = ["1", "3"].includes(val("in_movimiento"));
  const el = document.getElementById("in_pesoOrigen");
  if (!el) return;
  el.disabled = !lleva || state.selectedPesadaId != null;
  if (!lleva) el.value = "";
}

/** Punto de venta de 5 y número de 8, como el campo de la 3.x. */
function mascaraRemito(el) {
  const d = el.value.replace(/\D/g, "").slice(0, 13);
  const nuevo = d.length > 5 ? `${d.slice(0, 5)}-${d.slice(5)}` : d;
  if (nuevo !== el.value) el.value = nuevo;
}

function datosInagro() {
  if (!esInagro()) return null;
  if (!val("in_movimiento")) throw new Error("Elegí el Id de operación");
  const origen = val("in_pesoOrigen");
  return {
    movimiento: Number(val("in_movimiento")),
    pesoOrigen: origen === "" ? null : Number(origen),
    tanque: val("in_tanque") || null,
    analisis: val("in_analisis") || null,
    precintos: val("in_precintos") || null,
    productoCodigo: val("in_productoCodigo") || null,
    cuilChofer: val("in_cuilChofer") || null,
    observacionesRemito: val("in_observacionesRemito") || null
  };
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
  ["ad_bultoId", p => p.aduana?.bultoId], ["ad_bultoBruto", p => p.aduana?.bultoBruto], ["ad_bultoTara", p => p.aduana?.bultoTara],
  ["in_movimiento", p => p.inagro?.movimiento], ["in_pesoOrigen", p => p.inagro?.pesoOrigen], ["in_tanque", p => p.inagro?.tanque],
  ["in_productoCodigo", p => p.inagro?.productoCodigo], ["in_analisis", p => p.inagro?.analisis], ["in_precintos", p => p.inagro?.precintos],
  ["in_cuilChofer", p => p.inagro?.cuilChofer], ["in_observacionesRemito", p => p.inagro?.observacionesRemito]
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
  if (esInagro()) togglePesoOrigen();
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

/**
 * Al cerrar una pesada: el ticket se abre solo y, si hay térmica, también sale por ahí.
 * En INAGRO, según Configuración: el ticket, el remito preimpreso o preguntar cuál.
 */
function ticketAlCerrar(pesada) {
  if (esInagro()) {
    const modo = state.funciones.inagroImpresion || "preguntar";
    if (modo === "remito") verRemito(pesada.id);
    else if (modo === "ticket") verTicket(pesada.id);
    else elegirImpresion(pesada);
  } else {
    verTicket(pesada.id);
  }
  if (usaTermica()) imprimirTermica(pesada.id);
}

async function verRemito(id) {
  try {
    const { blob, nombre } = await bajarArchivo(`/api/pesadas/${id}/remito`);
    const url = URL.createObjectURL(blob);
    if (!window.open(url, "_blank")) guardarComo(url, nombre);
  } catch (e) {
    toast("No se pudo generar el remito: " + limpiarError(e), "err");
  }
}

/** Ventana para elegir qué imprimir (INAGRO, modo "preguntar"). */
function elegirImpresion(pesada) {
  cerrarModal();
  const fondo = document.createElement("div");
  fondo.className = "modal-fondo";
  fondo.id = "modal";
  fondo.innerHTML = `
    <div class="card modal">
      <h3 style="margin-top:0">Ticket ${esc(pesada.ticketNumero)} guardado</h3>
      <p>¿Cómo lo imprimís?</p>
      <div class="btn-row">
        <button class="btn-primary" onclick="cerrarModal();verTicket('${pesada.id}')">Ticket estándar</button>
        <button class="btn-primary" onclick="cerrarModal();verRemito('${pesada.id}')">Remito preimpreso</button>
        <button class="btn" onclick="cerrarModal()">No imprimir</button>
      </div>
    </div>`;
  document.body.appendChild(fondo);
}

function botonesTicket(id) {
  return `<button class="btn" onclick="event.stopPropagation();verTicket('${id}')">Ticket</button>` +
    (esInagro() ? `<button class="btn" onclick="event.stopPropagation();verRemito('${id}')">Remito</button>` : "") +
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
function botonesCaptura(clase, texto) {
  const lista = balanzasEnPantalla();
  if (lista.length <= 1) {
    return `<button class="${clase}" onclick="capturarPeso()">${texto} <small>(F2)</small></button>`;
  }
  return lista.map((b, i) => `<button class="${clase}" onclick="capturarPeso('${b.balanzaId}')">${texto}: ${esc(b.nombre || "Balanza " + (i + 1))}
    ${TECLAS_CAPTURA[i] ? `<small>(${TECLAS_CAPTURA[i]})</small>` : ""}</button>`).join("");
}

function bloqueCaptura() {
  const c = state.captura;
  if (!c) {
    return `${botonesCaptura("btn-primary btn-capturar", "Capturar peso")}
      <span class="captura-ayuda">Capturá el peso con el camión quieto y después completá los datos.</span>`;
  }
  return `<div class="captura-ok"><span>Peso capturado</span><strong>${c.kilogramos.toLocaleString("es-AR")} kg</strong>
      <small>${esc(c.balanza || "")} · ${esc(hora(c.en))}</small></div>
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
    state.captura = await api("/api/pesadas/capturar", {
      method: "POST",
      body: JSON.stringify({ balanzaId: balanzaId || balanzaActual().balanzaId || null })
    });
    pintarCaptura();
    const err = document.getElementById("err");
    if (err) err.textContent = "";
  } catch (e) {
    toast(limpiarError(e), "err");
  }
}

function descartarCaptura() {
  state.captura = null;
  pintarCaptura();
}

/** Lo que va en el pedido: el peso manual si se escribió; si no, el capturado (que es obligatorio). */
function pesoParaGuardar() {
  const m = manual();
  if (m.peso == null && !state.captura) throw new Error("Primero capturá el peso (botón Capturar peso, F2 o F3)");
  return { ...m, capturaId: m.peso == null ? state.captura.id : null };
}

/** Si la captura venció (o el servidor se reinició), se borra para que se vuelva a capturar. */
function revisarCaptura(e) {
  if (/capturado venci/i.test(limpiarError(e))) descartarCaptura();
}

document.addEventListener("keydown", e => {
  const i = TECLAS_CAPTURA.indexOf(e.key);
  if (i < 0 || state.view !== "pesaje" || state.modoPesaje === "tomar" || !document.getElementById("captura")) return;
  const lista = balanzasEnPantalla();
  if (lista.length <= 1 && i > 0) return;
  e.preventDefault();
  capturarPeso(lista.length > 1 ? lista[i]?.balanzaId : undefined);
});

function selectPesada(id) {
  state.selectedPesadaId = state.selectedPesadaId === id ? null : id;
  renderPesaje();
}

function collectPesaje() {
  // En INAGRO el tipo (ingreso / egreso) sale del Id de operación: no está en el formulario.
  const missing = fields().filter(f => f.visible && f.required && !val(f.key) && f.key !== "taraPallets" && !(esInagro() && f.key === "tipo"));
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
    ...datosAduana(),
    inagro: datosInagro()
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
    state.captura = null;
    toast(`Entrada ticket ${pesada.ticketNumero}`);
    await renderPesaje();
    document.getElementById("ultimo").textContent = `Entrada ticket ${pesada.ticketNumero} · ${pesada.pesoEntrada} kg`;
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
        capturaId: m.capturaId
      })
    });
    state.selectedPesadaId = null;
    state.captura = null;
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
    state.captura = null;
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
        claveManual: m.clave
      })
    });
    toast(`Tara de ${tara.identificador}: ${tara.kilogramos} kg`);
    await renderPesaje();
    document.getElementById("ultimo").textContent = `Tara guardada: ${tara.identificador} · ${tara.kilogramos} kg`;
  } catch (e) {
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

/** Edición INAGRO: campos de su 3.x, tanques, sucursales, remito preimpreso y envío a Centum. */
function esInagro() { return state.edicion === "Inagro" || state.funciones.inagro === true; }

/** Dirección, localidad e IVA de clientes y transportes: los usa el remito preimpreso de INAGRO. */
const DOMICILIO = [["direccion", "Dirección"], ["localidad", "Localidad"], ["condicionIva", "Condición IVA"]];

const CATALOGOS = {
  clientes: { titulo: "Clientes", campos: [["razonSocial", "Razón social"], ["cuit", "CUIT"]], extra: () => esInagro() ? DOMICILIO : [] },
  transportes: { titulo: "Transportes", campos: [["razonSocial", "Razón social"], ["cuit", "CUIT"], ["esAta", "Es ATA", "check"], ["codigoAta", "Código ATA"]], extra: () => esInagro() ? DOMICILIO : [] },
  productos: { titulo: "Productos", campos: [["nombre", "Nombre"], ["codigo", "Código"]] },
  exportadores: { titulo: "Exportadores", campos: [["razonSocial", "Razón social"], ["cuit", "CUIT"]], visible: () => state.funciones.aduana },
  taras: { titulo: "Taras conocidas", campos: [["identificador", "Patente / carro"], ["kilogramos", "Tara (kg)", "number"], ["descripcion", "Descripción"]], visible: () => state.funciones.taraConocida },
  tanques: { titulo: "Tanques", campos: [["nombre", "Tanque / sección"]], visible: () => esInagro(), soloEdicion: true },
  sucursales: { titulo: "Sucursales", campos: [["nombre", "Procedencia / destino"]], visible: () => esInagro(), soloEdicion: true }
};

/** Los campos del catálogo, con los extra de la edición. */
function camposCatalogo(cat) {
  return [...cat.campos, ...(cat.extra ? cat.extra() : [])];
}

function catalogosVisibles() {
  // Los de una edición (INAGRO) no existen en las otras: ni BFS los ve.
  return Object.entries(CATALOGOS).filter(([, c]) => !c.visible || c.visible() || (isVendor() && !c.soloEdicion));
}

async function renderCatalogos() {
  const tabs = catalogosVisibles();
  if (!tabs.some(([id]) => id === state.catTab)) state.catTab = tabs[0][0];
  const cat = CATALOGOS[state.catTab];
  const campos = camposCatalogo(cat);
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
        ${campos.map(([key, label, tipo]) => tipo === "check"
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
        <tr>${campos.map(([, label]) => `<th>${label}</th>`).join("")}${editable ? "<th></th>" : ""}</tr>
        ${items.length ? items.map(item => `<tr class="${item.id === state.catEdit ? "selected" : ""}">
          ${campos.map(c => `<td>${celda(item, c)}</td>`).join("")}
          ${editable ? `<td><div class="btn-row" style="margin:0">
            <button class="btn" onclick="editarCatalogo('${item.id}')">Editar</button>
            <button class="btn-danger" onclick="borrarCatalogo('${item.id}')">Borrar</button>
          </div></td>` : ""}
        </tr>`).join("") : `<tr><td colspan="${campos.length + 1}">Sin datos</td></tr>`}
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
  for (const [key, , tipo] of camposCatalogo(CATALOGOS[state.catTab])) {
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
        ${esInagro() ? `<div><label>Sociedad Centum (1 S.A., 2 S.R.L., 3 Otros, 4 No vincula)</label>
          <input id="em_codigoSociedad_${e.id}" value="${esc(e.codigoSociedad ?? "")}" placeholder="Vacío o 4 = no se envía a Centum" /></div>` : ""}
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
  // Solo existe en la pantalla de INAGRO (desde Configuración → Ticket no se manda y no se toca).
  if (document.getElementById(`em_codigoSociedad_${id}`)) body.codigoSociedad = val(`em_codigoSociedad_${id}`);
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
      <div class="btn-row"><button class="btn-primary" onclick="saveGeneral()">Guardar general</button></div>
    </div>
    ${esInagro() ? `
    <div class="card">
      <h3 style="margin-top:0">INAGRO</h3>
      <div class="grid2">
        <div><label>Planta de esta instalación (va en cada pesada y a Centum)</label><select id="c_in_planta">
          ${opciones(["1: Alvear", "2: Campana"], c.inagro_planta || "1: Alvear")}</select></div>
        <div><label>Al cerrar una pesada imprimir</label><select id="c_in_impresion">
          ${[["preguntar", "Preguntar cada vez"], ["ticket", "Ticket estándar"], ["remito", "Remito preimpreso"]].map(([v, t]) =>
            `<option value="${v}" ${(c.inagro_impresion || "preguntar") === v ? "selected" : ""}>${t}</option>`).join("")}</select></div>
        <div><label>Remito: correr a la derecha (mm, negativo = izquierda)</label><input id="c_in_ax" type="number" step="0.5" value="${esc(c.inagro_remito_ajuste_x || "0")}" /></div>
        <div><label>Remito: correr hacia abajo (mm, negativo = arriba)</label><input id="c_in_ay" type="number" step="0.5" value="${esc(c.inagro_remito_ajuste_y || "0")}" /></div>
      </div>
      <div class="btn-row"><button class="btn-primary" onclick="saveInagro()">Guardar INAGRO</button></div>
    </div>` : ""}`;
}

async function saveInagro() {
  await saveConfig({
    inagro_planta: val("c_in_planta"),
    inagro_impresion: val("c_in_impresion"),
    inagro_remito_ajuste_x: val("c_in_ax") || "0",
    inagro_remito_ajuste_y: val("c_in_ay") || "0"
  });
  state.funciones = await api("/api/funciones");
  renderConfig();
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
    ${esInagro() ? `
    <div class="card">
      <h3 style="margin-top:0">Centum</h3>
      <p>Cada pesada cerrada se manda una vez, con el formato de la 3.x. No se envían las empresas sin sociedad o con sociedad 4 (No vincula); la sociedad se carga en Planta.</p>
      <div class="grid2">
        <div><label>Envío a Centum</label>${yesNo("c_centum", c.centum_enabled)}</div>
        <div style="grid-column:1/-1"><label>Webhook</label><input id="c_centum_ep" value="${esc(c.centum_endpoint || "")}" placeholder="https://automate.gauchocode.com/webhook/..." /></div>
      </div>
      <div class="btn-row"><button class="btn-primary" onclick="saveCentum()">Guardar Centum</button></div>
    </div>` : ""}
    <div class="card" id="envios"></div>`;
  cargarEnvios();
}

async function saveCentum() {
  await saveConfig({ centum_enabled: val("c_centum"), centum_endpoint: val("c_centum_ep").trim() });
  renderConfig();
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
    ticket_solo_numero: val("c_solonum")
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
  ["tipo", "Tipo", p => p.tipo, "tipo"],
  ["producto", "Producto", p => p.producto, "dl_productos"],
  ["cliente", "Cliente", p => p.cliente, "dl_clientes"],
  ["transporte", "Transporte", p => p.transporte, "dl_transportes"],
  ["procedencia", "Procedencia", p => p.procedencia],
  ["destino", "Destino", p => p.destino],
  ["comprobante", "Remito", p => p.comprobante],
  ["patenteChasis", "Patente chasis", p => p.patenteChasis],
  ["patenteAcoplado", "Patente acoplado", p => p.patenteAcoplado],
  ["conductor", "Conductor", p => p.conductor],
  ["tipoDocumento", "Tipo de documento", p => p.tipoDocumento, "tipoDoc"],
  ["documento", "Documento", p => p.documento],
  ["nacionalidad", "Nacionalidad", p => p.nacionalidad],
  ["observaciones", "Observaciones", p => p.observaciones],
  ["aduana.exportador", "Exportador / importador", p => p.aduana?.exportador],
  ["aduana.exportadorCuit", "CUIT exportador", p => p.aduana?.exportadorCuit],
  ["aduana.ata", "ATA", p => p.aduana?.ata],
  ["aduana.ataCuit", "CUIT ATA", p => p.aduana?.ataCuit],
  ["aduana.operacionId", "N° de operación", p => p.aduana?.operacionId],
  ["aduana.destinacionAduanera", "Destinación aduanera", p => p.aduana?.destinacionAduanera],
  ["aduana.contenedorId", "Contenedor", p => p.aduana?.contenedorId],
  ["aduana.bultoId", "Bulto", p => p.aduana?.bultoId],
  // Edición Inagro
  ["inagro.movimiento", "Id de operación", p => p.inagro?.movimiento != null ? String(p.inagro.movimiento) : "", "movimiento"],
  ["inagro.pesoOrigen", "Peso de origen (kg)", p => p.inagro?.pesoOrigen != null ? String(p.inagro.pesoOrigen) : ""],
  ["inagro.tanque", "Tanque", p => p.inagro?.tanque, "dl_tanques"],
  ["inagro.productoCodigo", "Código producto", p => p.inagro?.productoCodigo],
  ["inagro.analisis", "Análisis", p => p.inagro?.analisis],
  ["inagro.precintos", "Precintos", p => p.inagro?.precintos],
  ["inagro.cuilChofer", "CUIL chofer", p => p.inagro?.cuilChofer],
  ["inagro.observacionesRemito", "Observaciones del remito", p => p.inagro?.observacionesRemito]
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
  const [clientes, transportes, productos, tanques] = await Promise.all([
    api("/api/catalogos/clientes"), api("/api/catalogos/transportes"), api("/api/catalogos/productos"),
    p.inagro ? api("/api/catalogos/tanques") : Promise.resolve([])]);
  state.catalogos = { ...state.catalogos, clientes, transportes, productos, tanques };
  const campos = CORREGIBLES.filter(([key]) => !key.startsWith("aduana.") || p.aduana)
    .filter(([key]) => !key.startsWith("inagro.") || p.inagro)
    // En INAGRO el tipo sale del Id de operación.
    .filter(([key]) => !(key === "tipo" && p.inagro))
    .filter(([key]) => key.startsWith("aduana.") || key.startsWith("inagro.") || visible(key));
  const input = ([key, label, leer, extra]) => {
    const v = leer(p) || "";
    const id = "co_" + key.replace(".", "_");
    if (extra === "tipo") return `<div><label>${label}</label><select id="${id}">${opciones(["INGRESO", "EGRESO"], v)}</select></div>`;
    if (extra === "movimiento") return `<div><label>${label}</label><select id="${id}">${[["1", "1: Compras"], ["2", "2: Ventas"], ["3", "3: OT"]]
      .map(([x, t]) => `<option value="${x}" ${x === v ? "selected" : ""}>${t}</option>`).join("")}</select></div>`;
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
      ${datalist("dl_tanques", (state.catalogos.tanques || []).map(x => x.nombre))}
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
      <h3 style="margin-top:0">Importar desde la 3.x</h3>
      <p>${d.existe
        ? `Se encontró <code>${esc(d.ruta)}</code>: ${Object.entries(d.contenido).map(([k, v]) => `${esc(k)} ${v}`).join(" · ")}.`
        : `No se encontró la base de la 3.x en <code>${esc(d.ruta)}</code>. Podés subir una copia.`}
        La base de la 3.x no se modifica; lo que ya existe en la 4.0 se omite y antes se guarda un backup.</p>
      <div class="grid2"><div><label>O subir otra truckscale.db</label><input id="imp_archivo" type="file" accept=".db" /></div></div>
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
  if (!confirm("¿Importar los datos de la 3.x a esta instalación?")) return;
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
        <div class="brand">${bfsLogo()}<div><h1>BFS Nube · Clientes</h1><p>Panel de superadmin</p></div></div>
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

start();
