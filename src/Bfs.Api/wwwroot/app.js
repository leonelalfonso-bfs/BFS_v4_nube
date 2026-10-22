const ICONS = {
  pesaje: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M12 3v3M7 21h10M8 8h8l1 13H7L8 8z"/><path d="M10 12h4"/></svg>',
  reportes: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M4 19V5h12l4 4v10H4z"/><path d="M16 5v4h4M8 13h8M8 17h5"/></svg>',
  catalogos: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M4 7h16M4 12h16M4 17h10"/></svg>',
  usuarios: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M12 12a4 4 0 1 0-4-4 4 4 0 0 0 4 4z"/><path d="M4 20a8 8 0 0 1 16 0"/></svg>',
  planta: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M3 21V9l6-4v4l6-4v16"/><path d="M15 21V3h6v18M3 21h18M7 13h2M7 17h2M11 13h2M11 17h2"/></svg>',
  config: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.7 1.7 0 0 0 .3 1.8l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.7 1.7 0 0 0-1.8-.3 1.7 1.7 0 0 0-1 1.5V21a2 2 0 1 1-4 0v-.1a1.7 1.7 0 0 0-1-1.5 1.7 1.7 0 0 0-1.8.3l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.7 1.7 0 0 0 .3-1.8 1.7 1.7 0 0 0-1.5-1H3a2 2 0 1 1 0-4h.1a1.7 1.7 0 0 0 1.5-1 1.7 1.7 0 0 0-.3-1.8l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.7 1.7 0 0 0 1.8.3H9a1.7 1.7 0 0 0 1-1.5V3a2 2 0 1 1 4 0v.1a1.7 1.7 0 0 0 1 1.5 1.7 1.7 0 0 0 1.8-.3l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.7 1.7 0 0 0-.3 1.8V9a1.7 1.7 0 0 0 1.5 1H21a2 2 0 1 1 0 4h-.1a1.7 1.7 0 0 0-1.5 1z"/></svg>'
};

const DEFAULT_FIELDS = [
  { key: "tipo", label: "Tipo (ingreso/egreso)", visible: true, required: true },
  { key: "patenteChasis", label: "Patente chasis", visible: true, required: true },
  { key: "patenteAcoplado", label: "Patente acoplado", visible: true, required: false },
  { key: "conductor", label: "Conductor", visible: true, required: false },
  { key: "documento", label: "Documento", visible: true, required: false },
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
  pendientes: [],
  selectedPesadaId: null,
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
  if (res.status === 401) {
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

function isVendor() { return state.user?.rol === "vendor"; }
function can(permiso) { return (state.user?.permisos || []).includes(permiso); }
function canVerPesadas() { return can("pesadas.ver") || can("pesadas.ver_propias"); }

function navItems() {
  const nav = [];
  if (can("pesaje.operar")) nav.push(["pesaje", "Pesaje"]);
  if (canVerPesadas()) nav.push(["reportes", "Pesadas"]);
  if (can("catalogos.ver")) nav.push(["catalogos", "Catálogos"]);
  const admin = [];
  if (can("usuarios.gestionar")) admin.push(["usuarios", "Usuarios"]);
  if (can("config.planta")) admin.push(["planta", "Planta"]);
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
  const led = document.getElementById("pesoLed");
  if (led) led.innerHTML = `${p.kilogramos} <small>${esc(p.nombre || p.puerto)} · ${esc(p.estado)}${p.estable ? " · estable" : ""}</small>`;
  const st = document.getElementById("scalePill");
  if (st) st.innerHTML = pillForScale(p.estado) + (p.estable ? ` <span class="pill pill-ok">Estable</span>` : "");
  const kg = document.getElementById("scaleKg");
  if (kg) kg.textContent = p.kilogramos + " kg";
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
  const { nav, admin } = navItems();
  const todas = nav.concat(admin);
  if (!todas.some(([id]) => id === state.view)) state.view = todas[0]?.[0] || "";
  const navBtn = ([id, label]) => `
          <button class="nav-btn ${state.view === id ? "active" : ""}" onclick="go('${id}')">${ICONS[id]}<span>${label}</span></button>
        `;

  document.getElementById("app").innerHTML = `
    <div class="app-shell">
      <header class="topbar">
        <div class="brand">
          ${bfsLogo()}
          <div>
            <h1>Sistema de Pesaje BFS 4.0</h1>
            <p>${esc(state.branding.clienteNombre || "Planta local")}</p>
          </div>
        </div>
        <div class="userchip">
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
  const views = { pesaje: renderPesaje, reportes: renderReportes, catalogos: renderCatalogos, usuarios: renderUsuarios, planta: renderPlanta, config: renderConfig };
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
        <label>Email</label>
        <input id="user" type="email" autocomplete="username" />
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
  render();
  if (can("pesaje.operar")) pollPeso();
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
    return `<div><label>${esc(label)}${req}</label><input id="${id}" list="${opts.list}" autocomplete="off" /></div>`;
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

function bloqueAduana() {
  if (!state.funciones.aduana) return "";
  const opt = values => values.map(v => `<option value="${esc(v)}">${esc(v)}</option>`).join("");
  return `
    <div class="grid2" style="margin-top:12px">
      <div><label>Operación</label><select id="tipoOperacion" onchange="toggleAduana()">
        <option value="Normal">Normal</option><option value="Aduana">Aduana</option>
      </select></div>
    </div>
    <div id="aduanaBox" style="display:none;margin-top:12px">
      <h4 style="margin:0 0 8px">Operación aduanera</h4>
      <div class="grid2">
        <div><label>Escenario *</label><select id="ad_escenario"><option value="">Elegir...</option>${opt(ESCENARIOS_ADUANA)}</select></div>
        <div><label>Exportador</label><input id="ad_exportador" list="dl_exportadores" autocomplete="off" onchange="completarExportador()" /></div>
        <div><label>CUIT exportador</label><input id="ad_exportadorCuit" /></div>
        <div><label>N° de operación</label><input id="ad_operacionId" /></div>
        <div><label>Tipo de documento del chofer</label><select id="ad_tipoDoc"><option></option>${opt(["DNI", "Pasaporte", "Otro"])}</select></div>
        <div><label>Nacionalidad del chofer</label><input id="ad_nacionalidad" /></div>
        <div><label>Contenedor</label><input id="ad_contenedorId" /></div>
        <div><label>Tara contenedor (kg)</label><input id="ad_contenedorTara" type="number" value="0" /></div>
        <div><label>Bulto</label><input id="ad_bultoId" /></div>
        <div><label>Bulto bruto (kg)</label><input id="ad_bultoBruto" type="number" /></div>
        <div><label>Bulto tara (kg)</label><input id="ad_bultoTara" type="number" /></div>
      </div>
      ${datalist("dl_exportadores", (state.catalogos.exportadores || []).map(x => x.razonSocial))}
    </div>`;
}

function toggleAduana() {
  const box = document.getElementById("aduanaBox");
  if (box) box.style.display = val("tipoOperacion") === "Aduana" ? "" : "none";
}

function completarExportador() {
  const x = (state.catalogos.exportadores || []).find(e => e.razonSocial === val("ad_exportador"));
  if (x?.cuit && !val("ad_exportadorCuit")) document.getElementById("ad_exportadorCuit").value = x.cuit;
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
      choferTipoDocumento: val("ad_tipoDoc") || null,
      choferNacionalidad: val("ad_nacionalidad") || null,
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
    state.funciones.aduana ? api("/api/catalogos/exportadores") : Promise.resolve([])
  ];
  const [pendientes, clientes, transportes, productos, exportadores] = await Promise.all(pedidos);
  state.pendientes = pendientes;
  state.catalogos = { clientes, transportes, productos, exportadores };
  if (!modosPesaje().some(([id]) => id === state.modoPesaje)) state.modoPesaje = "normal";
  const modo = state.modoPesaje;
  const sinLicencia = state.licencia && !state.licencia.puedePesar;
  const empresas = state.empresas.map(e => `<option value="${e.id}">${esc(e.nombre)}</option>`).join("");
  const f = (key, html) => visible(key) ? html : "";

  const datosVehiculo = `
      <div class="grid2">
        ${f("tipo", inputField("tipo", field("tipo")?.label || "Tipo", { type: "select", options: "<option>INGRESO</option><option>EGRESO</option>" }))}
        ${f("patenteChasis", inputField("patenteChasis", modo === "tara" ? "Patente / carro" : field("patenteChasis")?.label || "Patente chasis", { blur: "autocompletarPatente()" }))}
        ${modo === "tara" ? `<div class="status-box"><span>Tara guardada</span><strong id="taraGuardada">-</strong></div>` : ""}
        ${f("patenteAcoplado", inputField("patenteAcoplado", field("patenteAcoplado")?.label || "Patente acoplado"))}
        ${f("conductor", inputField("conductor", field("conductor")?.label || "Conductor"))}
        ${f("documento", inputField("documento", field("documento")?.label || "Documento"))}
        ${f("cliente", inputField("cliente", field("cliente")?.label || "Cliente", { list: "dl_clientes" }))}
        ${f("transporte", inputField("transporte", field("transporte")?.label || "Transporte", { list: "dl_transportes" }))}
        ${f("producto", inputField("producto", field("producto")?.label || "Producto", { list: "dl_productos" }))}
        ${f("procedencia", inputField("procedencia", field("procedencia")?.label || "Procedencia"))}
        ${f("destino", inputField("destino", field("destino")?.label || "Destino"))}
        ${f("comprobante", inputField("comprobante", field("comprobante")?.label || "Remito"))}
        ${f("taraPallets", inputField("taraPallets", field("taraPallets")?.label || "Tara pallets (kg)", { type: "number", value: "0" }))}
        ${f("observaciones", inputField("observaciones", field("observaciones")?.label || "Observaciones", { type: "textarea" }))}
      </div>
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
      <h3 style="margin-top:0">${modo === "tara" ? "Pesar con tara conocida" : "Datos del vehículo"}</h3>
      ${datosVehiculo}
      ${bloqueAduana()}
      ${modo === "tara" && puedeManual() ? `
      <details style="margin-top:12px"><summary>Usar otra tara (ingreso manual)</summary>
        <div class="grid2" style="margin-top:8px"><div><label>Tara (kg)</label><input id="tc_tara" type="number" min="1" /></div></div>
      </details>` : ""}
      ${bloqueManual(modo === "tara" ? "Cargar el bruto a mano" : "Cargar el peso a mano")}
      <p class="error" id="err"></p>
      <div class="btn-row">
        ${modo === "tara"
          ? `<button class="btn-primary" onclick="guardarTaraConocida()" ${sinLicencia ? "disabled" : ""}>Pesar</button>`
          : `<button class="btn-primary" onclick="guardarEntrada()" ${sinLicencia ? "disabled" : ""}>Capturar entrada</button>
             <button class="btn" onclick="guardarSalida()">Capturar salida</button>`}
      </div>
      <p id="ultimo"></p>
    </div>`;

  document.getElementById("main").innerHTML = `
    <div class="page-head">
      <div>
        <h2>Pesaje</h2>
        <p>${modo === "tara" ? "Un solo paso: bruto de la balanza menos la tara guardada." : modo === "tomar" ? "Guardá la tara de un vehículo o carro." : "Capturá entrada y salida. El neto descuenta tara de camión y pallets."}</p>
      </div>
      <div id="scalePill">${pillForScale(state.scale.estado)}</div>
    </div>
    ${sinLicencia ? `
    <div class="card" style="border-left:4px solid #c0392b">
      <strong>${esc(state.licencia.mensaje)}</strong>
      <p style="margin:6px 0 0">ID de máquina: <code>${esc(state.licencia.maquina)}</code>. No se pueden registrar entradas nuevas; sí cerrar las salidas pendientes.</p>
    </div>` : ""}
    <div class="card peso-card">
      <div class="peso" id="pesoLed">${state.scale.kilogramos} <small>${esc(state.scale.puerto)} · ${esc(state.scale.estado)}</small></div>
      <div class="status-grid">
        <div class="status-box"><span>Empresa en curso</span>
          <select id="empresa">${empresas}</select>
        </div>
        ${state.balanzas.length > 1 ? `<div class="status-box"><span>Balanza</span>
          <select id="balanza" onchange="elegirBalanza(this.value)">${state.balanzas.map(b =>
            `<option value="${b.balanzaId}" ${b.balanzaId === balanzaActual().balanzaId ? "selected" : ""}>${esc(b.nombre)}</option>`).join("")}</select>
        </div>` : `<div class="status-box"><span>Peso vivo</span><strong id="scaleKg">${state.scale.kilogramos} kg</strong></div>`}
      </div>
      ${state.funciones.exigirEstable ? `<p style="margin:8px 0 0;color:var(--muted)">Se captura solo con el peso estable.</p>` : ""}
    </div>
    ${modosPesaje().length > 1 ? `<div class="tabs">${modosPesaje().map(([id, label]) =>
      `<button class="tab ${modo === id ? "active" : ""}" onclick="cambiarModoPesaje('${id}')">${label}</button>`).join("")}</div>` : ""}
    ${formulario}
    ${modo === "normal" ? `
    <div class="card">
      <h3 style="margin-top:0">Pendientes de salida (${pendientes.length})</h3>
      <table>
        <tr><th></th><th>Ticket</th><th>Empresa</th><th>Patente</th><th>Entrada</th><th>Hora</th><th>Pallets</th>${can("pesaje.anular") ? "<th></th>" : ""}</tr>
        ${pendientes.length ? pendientes.map(p => `<tr class="${state.selectedPesadaId === p.id ? "selected" : ""}" onclick="selectPesada('${p.id}')">
          <td>${state.selectedPesadaId === p.id ? "●" : ""}</td>
          <td>${esc(p.ticketNumero)}${p.tipoOperacion === "Aduana" ? ` <span class="pill pill-info">Aduana</span>` : ""}</td>
          <td>${esc(empresaName(p.empresaId))}</td>
          <td>${esc(p.patenteChasis)}</td>
          <td>${p.pesoEntrada}${p.entradaManualPor ? " ✎" : ""}</td>
          <td>${esc(hora(p.entradaEn))}</td>
          <td>${p.taraPallets}</td>
          ${can("pesaje.anular") ? `<td><button class="btn-danger" onclick="event.stopPropagation();anularPesada('${p.id}')">Anular</button></td>` : ""}
        </tr>`).join("") : `<tr><td colspan="8">No hay entradas pendientes.</td></tr>`}
      </table>
    </div>` : ""}
  `;
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

function selectPesada(id) {
  state.selectedPesadaId = state.selectedPesadaId === id ? null : id;
  renderPesaje();
}

function collectPesaje() {
  const missing = fields().filter(f => f.visible && f.required && !val(f.key) && f.key !== "taraPallets");
  if (missing.length) throw new Error("Completá: " + missing.map(f => f.label).join(", "));
  const m = manual();
  return {
    empresaId: val("empresa"),
    balanzaId: balanzaActual().balanzaId || null,
    tipo: visible("tipo") ? val("tipo") : undefined,
    patenteChasis: val("patenteChasis"),
    patenteAcoplado: val("patenteAcoplado") || null,
    conductor: val("conductor") || null,
    documento: val("documento") || null,
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
    toast(`Entrada ticket ${pesada.ticketNumero}`);
    await renderPesaje();
    document.getElementById("ultimo").textContent = `Entrada ticket ${pesada.ticketNumero} · ${pesada.pesoEntrada} kg`;
  } catch (e) {
    mostrarError(e);
  }
}

async function guardarSalida() {
  if (!state.selectedPesadaId) {
    document.getElementById("err").textContent = "Elegí un ticket de la lista de pendientes";
    return;
  }
  try {
    const m = manual();
    const pesada = await api(`/api/pesadas/${state.selectedPesadaId}/salida`, {
      method: "POST",
      body: JSON.stringify({
        taraPallets: visible("taraPallets") ? num("taraPallets") : null,
        balanzaId: balanzaActual().balanzaId || null,
        pesoSalida: m.peso,
        claveManual: m.clave
      })
    });
    state.selectedPesadaId = null;
    toast(`Ticket ${pesada.ticketNumero} cerrado · neto ${pesada.neto} kg`);
    await renderPesaje();
    document.getElementById("ultimo").textContent =
      `Ticket ${pesada.ticketNumero} cerrado · neto ${pesada.neto} kg (pallets ${pesada.taraPallets})` +
      (pesada.aduana?.contenedorNeto != null ? ` · contenedor neto ${pesada.aduana.contenedorNeto} kg` : "");
  } catch (e) {
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
    toast(`Ticket ${pesada.ticketNumero} · neto ${pesada.neto} kg`);
    await renderPesaje();
    document.getElementById("ultimo").textContent =
      `Ticket ${pesada.ticketNumero} · bruto ${pesada.bruto} − tara ${pesada.tara} = neto ${pesada.neto} kg`;
  } catch (e) {
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

async function renderReportes() {
  const rows = await api("/api/pesadas");
  const acciones = can("pesaje.operar") || can("pesaje.anular");
  document.getElementById("main").innerHTML = `
    <div class="page-head"><div><h2>Pesadas</h2><p>${can("pesadas.ver") ? "Últimos 200 movimientos de la planta." : "Tus últimos 200 movimientos."} ✎ = peso cargado a mano.</p></div></div>
    <div class="card" style="overflow-x:auto">
      <table>
        <tr><th>Ticket</th><th>Fecha</th><th>Empresa</th><th>Patente</th><th>Producto</th><th>Estado</th><th>Entrada</th><th>Salida</th><th>Pallets</th><th>Neto</th>${acciones ? "<th></th>" : ""}</tr>
        ${rows.map(p => `<tr>
          <td>${esc(p.ticketNumero)}${p.modo === "TaraConocida" ? ` <span class="pill pill-info">Tara conocida</span>` : ""}${p.tipoOperacion === "Aduana" ? ` <span class="pill pill-info">Aduana</span>` : ""}</td>
          <td>${esc(hora(p.entradaEn))}</td>
          <td>${esc(empresaName(p.empresaId))}</td>
          <td>${esc(p.patenteChasis)}</td>
          <td>${esc(p.producto || "")}</td>
          <td>${pillEstado(p)}</td>
          <td>${p.pesoEntrada}${p.entradaManualPor ? " ✎" : ""}</td>
          <td>${p.pesoSalida ?? "-"}${p.salidaManualPor ? " ✎" : ""}</td>
          <td>${p.taraPallets}</td>
          <td>${p.estado === "ANULADA" ? "-" : (p.neto ?? "-")}</td>
          ${acciones ? `<td><div class="btn-row" style="margin:0">
            ${p.estado === "ENTRADA" && can("pesaje.operar") ? `<button class="btn" onclick="retomarPesada('${p.id}')">Retomar</button>` : ""}
            ${p.estado !== "ANULADA" && can("pesaje.anular") ? `<button class="btn-danger" onclick="anularPesada('${p.id}')">Anular</button>` : ""}
          </div></td>` : ""}
        </tr>`).join("")}
      </table>
    </div>`;
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
  try {
    await api(`/api/catalogos/${state.catTab}/${id}`, { method: "DELETE" });
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
    </div>` : ""}`;
}

async function guardarEmpresa(id) {
  const body = {};
  for (const key of ["nombre", "cuit", "direccion", "localidad", "provincia", "telefono", "email"]) {
    body[key] = val(`em_${key}_${id}`);
  }
  try {
    await api(`/api/empresas/${id}`, { method: "PUT", body: JSON.stringify(body) });
    toast("Empresa guardada");
    renderPlanta();
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
  fill("ad_nacionalidad", d.nacionalidad);
  const tipoDoc = document.getElementById("ad_tipoDoc");
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
    ["api", "API"],
    ["licencia", "Licencia"]
  ];
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
    licencia: renderConfigLicencia
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

function renderConfigGeneral() {
  const c = state.config;
  document.getElementById("configBody").innerHTML = `
    <div class="card">
      <div class="grid2">
        <div><label>Nombre del cliente</label><input id="c_nombre" value="${esc(c.cliente_nombre || "")}" /></div>
        <div><label>Tara pallets</label>${yesNo("c_pal", c.tara_pallets)}</div>
        <div><label>Tara conocida</label>${yesNo("c_tara", c.tara_conocida)}</div>
        <div><label>Aduana</label>${yesNo("c_adu", c.aduana)}</div>
        <div><label>Contenedores</label>${yesNo("c_cont", c.contenedores)}</div>
        <div><label>Impresora térmica</label>${yesNo("c_term", c.termica)}</div>
        <div><label>Nombre de la impresora térmica</label><input id="c_term_imp" value="${esc(c.termica_impresora || "")}" /></div>
        <div><label>Ancho de papel</label>
          <select id="c_term_ancho">
            ${["58", "80"].map(a => `<option value="${a}" ${(c.termica_ancho || "80") === a ? "selected" : ""}>${a} mm</option>`).join("")}
          </select>
        </div>
        <div><label>Ingreso manual de peso</label>${yesNo("c_manual", c.ingreso_manual ?? "true")}</div>
        <div><label>Capturar solo con peso estable</label>${yesNo("c_estable", c.exigir_estable ?? "true")}</div>
        <div><label>Número de puesto (va en el ticket: 01-000123)</label><input id="c_puesto" type="number" min="1" max="99" value="${esc(c.puesto || "1")}" /></div>
      </div>
      <div class="btn-row"><button class="btn-primary" onclick="saveGeneral()">Guardar general</button></div>
    </div>`;
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

function renderConfigTicket() {
  const c = state.config;
  document.getElementById("configBody").innerHTML = `
    <div class="grid2">
      <div class="card">
        <div><label>Título del ticket</label><input id="t_titulo" value="${esc(c.ticket_titulo || "")}" /></div>
        <div style="margin-top:12px"><label>Pie de ticket</label><input id="t_pie" value="${esc(c.ticket_pie || "")}" /></div>
        <div style="margin-top:12px">
          <label>Logo</label>
          <input id="t_logo" type="file" accept=".png,.jpg,.jpeg,.webp,.svg" />
        </div>
        <div class="btn-row">
          <button class="btn-primary" onclick="saveTicket()">Guardar ticket</button>
          <button class="btn" onclick="subirLogo()">Subir logo</button>
        </div>
      </div>
      <div class="card ticket-preview">
        ${state.branding.logoUrl ? `<img src="${state.branding.logoUrl}?t=${Date.now()}" alt="logo" />` : bfsLogo("login-logo")}
        <h3>${esc(c.ticket_titulo || "Comprobante de pesaje")}</h3>
        <p>${esc(c.cliente_nombre || "Cliente")}</p>
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
      </div>
      <div class="btn-row"><button class="btn-primary" onclick="saveApi()">Guardar API</button></div>
    </div>`;
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
    termica: val("c_term"),
    termica_impresora: val("c_term_imp"),
    termica_ancho: val("c_term_ancho"),
    ingreso_manual: val("c_manual"),
    exigir_estable: val("c_estable"),
    puesto: String(Math.min(99, Math.max(1, num("c_puesto") || 1)))
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
  await saveConfig({
    ticket_titulo: val("t_titulo"),
    ticket_pie: val("t_pie")
  });
  renderConfig();
}

async function subirLogo() {
  const file = document.getElementById("t_logo")?.files?.[0];
  if (!file) return toast("Elegí un archivo", "err");
  const body = new FormData();
  body.append("file", file);
  await api("/api/branding/logo", { method: "POST", body });
  state.branding = await api("/api/branding");
  toast("Logo actualizado");
  render();
}

async function saveApi() {
  await saveConfig({
    worms_enabled: val("c_api"),
    worms_endpoint: val("c_ep"),
    worms_api_key: val("c_key"),
    worms_balanza: val("c_bal"),
    worms_tipo_default: val("c_tipo")
  });
  renderConfig();
}

async function start() {
  try { state.branding = await api("/api/branding"); } catch { state.branding = {}; }
  if (state.token) {
    try {
      state.user = await api("/api/me");
      await boot();
    } catch {
      logout();
    }
  } else {
    render();
  }
}

start();
