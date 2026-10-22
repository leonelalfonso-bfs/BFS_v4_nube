const ICONS = {
  pesaje: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M12 3v3M7 21h10M8 8h8l1 13H7L8 8z"/><path d="M10 12h4"/></svg>',
  reportes: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M4 19V5h12l4 4v10H4z"/><path d="M16 5v4h4M8 13h8M8 17h5"/></svg>',
  catalogos: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M4 7h16M4 12h16M4 17h10"/></svg>',
  usuarios: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M12 12a4 4 0 1 0-4-4 4 4 0 0 0 4 4z"/><path d="M4 20a8 8 0 0 1 16 0"/></svg>',
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
  scale: { kilogramos: 0, puerto: "SIM", estado: "simulada" },
  scaleMeta: { status: "", lastError: "", puertos: [] },
  selectedPesadaId: null,
  licencia: null,
  rolesAsignables: []
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

function pillForScale(estado) {
  if (estado === "abierta") return `<span class="pill pill-ok">Serie abierta</span>`;
  if (estado === "simulada") return `<span class="pill pill-info">Simulada</span>`;
  if (estado === "error") return `<span class="pill pill-err">Error</span>`;
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
  const views = { pesaje: renderPesaje, reportes: renderReportes, catalogos: renderCatalogos, usuarios: renderUsuarios, config: renderConfig };
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
  const [empresas, config, branding, campos, licencia] = await Promise.all([
    api("/api/empresas"),
    can("config.bfs") ? api("/api/config") : Promise.resolve({}),
    api("/api/branding"),
    api("/api/campos"),
    api("/api/licencia")
  ]);
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
    const p = await api("/api/scale/peso");
    state.scale = p;
    const led = document.getElementById("pesoLed");
    if (led) led.innerHTML = `${p.kilogramos} <small>${esc(p.puerto)} · ${esc(p.estado)}</small>`;
    const st = document.getElementById("scalePill");
    if (st) st.innerHTML = pillForScale(p.estado);
    const kg = document.getElementById("scaleKg");
    if (kg) kg.textContent = p.kilogramos + " kg";
  } catch {}
  setTimeout(pollPeso, 500);
}

function inputField(id, label, opts = {}) {
  const req = required(id) ? " *" : "";
  if (opts.type === "select") {
    return `<div><label>${esc(label)}${req}</label><select id="${id}">${opts.options}</select></div>`;
  }
  if (opts.type === "textarea") {
    return `<div style="grid-column:1/-1"><label>${esc(label)}${req}</label><textarea id="${id}"></textarea></div>`;
  }
  return `<div><label>${esc(label)}${req}</label><input id="${id}" ${opts.type ? `type="${opts.type}"` : ""} value="${opts.value ?? ""}" /></div>`;
}

async function renderPesaje() {
  state.pesadas = await api("/api/pesadas");
  const empresas = state.empresas.map(e => `<option value="${e.id}">${esc(e.nombre)}</option>`).join("");
  const f = (key, html) => visible(key) ? html : "";
  const abiertas = state.pesadas.filter(p => p.estado === "ENTRADA");

  document.getElementById("main").innerHTML = `
    <div class="page-head">
      <div>
        <h2>Pesaje</h2>
        <p>Capturá entrada y salida. El neto descuenta tara de camión y pallets.</p>
      </div>
      <div id="scalePill">${pillForScale(state.scale.estado)}</div>
    </div>
    ${state.licencia && !state.licencia.puedePesar ? `
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
        <div class="status-box"><span>Peso vivo</span><strong id="scaleKg">${state.scale.kilogramos} kg</strong></div>
      </div>
    </div>
    <div class="card">
      <h3 style="margin-top:0">Datos del vehículo</h3>
      <div class="grid2">
        ${f("tipo", inputField("tipo", field("tipo")?.label || "Tipo", { type: "select", options: "<option>INGRESO</option><option>EGRESO</option>" }))}
        ${f("patenteChasis", inputField("patenteChasis", field("patenteChasis")?.label || "Patente chasis"))}
        ${f("patenteAcoplado", inputField("patenteAcoplado", field("patenteAcoplado")?.label || "Patente acoplado"))}
        ${f("conductor", inputField("conductor", field("conductor")?.label || "Conductor"))}
        ${f("documento", inputField("documento", field("documento")?.label || "Documento"))}
        ${f("cliente", inputField("cliente", field("cliente")?.label || "Cliente"))}
        ${f("transporte", inputField("transporte", field("transporte")?.label || "Transporte"))}
        ${f("producto", inputField("producto", field("producto")?.label || "Producto"))}
        ${f("procedencia", inputField("procedencia", field("procedencia")?.label || "Procedencia"))}
        ${f("destino", inputField("destino", field("destino")?.label || "Destino"))}
        ${f("comprobante", inputField("comprobante", field("comprobante")?.label || "Remito"))}
        ${f("taraPallets", inputField("taraPallets", field("taraPallets")?.label || "Tara pallets (kg)", { type: "number", value: "0" }))}
        ${f("observaciones", inputField("observaciones", field("observaciones")?.label || "Observaciones", { type: "textarea" }))}
      </div>
      <p class="error" id="err"></p>
      <div class="btn-row">
        <button class="btn-primary" onclick="guardarEntrada()" ${state.licencia && !state.licencia.puedePesar ? "disabled" : ""}>Capturar entrada</button>
        <button class="btn" onclick="guardarSalida()">Capturar salida</button>
      </div>
      <p id="ultimo"></p>
    </div>
    <div class="card">
      <h3 style="margin-top:0">Tickets abiertos</h3>
      <table>
        <tr><th></th><th>Ticket</th><th>Empresa</th><th>Patente</th><th>Entrada</th><th>Pallets</th></tr>
        ${abiertas.length ? abiertas.map(p => `<tr class="${state.selectedPesadaId === p.id ? "selected" : ""}" onclick="selectPesada('${p.id}')">
          <td>${state.selectedPesadaId === p.id ? "●" : ""}</td>
          <td>${esc(p.ticketNumero)}</td>
          <td>${esc(empresaName(p.empresaId))}</td>
          <td>${esc(p.patenteChasis)}</td>
          <td>${p.pesoEntrada}</td>
          <td>${p.taraPallets}</td>
        </tr>`).join("") : `<tr><td colspan="6">No hay entradas abiertas.</td></tr>`}
      </table>
    </div>
  `;
}

function selectPesada(id) {
  state.selectedPesadaId = id;
  renderPesaje();
}

function collectPesaje() {
  const missing = fields().filter(f => f.visible && f.required && !val(f.key) && f.key !== "taraPallets");
  if (missing.length) throw new Error("Completá: " + missing.map(f => f.label).join(", "));
  return {
    empresaId: val("empresa"),
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
    taraPallets: visible("taraPallets") ? num("taraPallets") : 0
  };
}

async function guardarEntrada() {
  try {
    const body = collectPesaje();
    const pesada = await api("/api/pesadas/entrada", { method: "POST", body: JSON.stringify(body) });
    state.selectedPesadaId = pesada.id;
    toast(`Entrada ticket ${pesada.ticketNumero}`);
    await renderPesaje();
    document.getElementById("ultimo").textContent = `Entrada ticket ${pesada.ticketNumero} · ${pesada.pesoEntrada} kg`;
  } catch (e) {
    document.getElementById("err").textContent = e.message;
  }
}

async function guardarSalida() {
  if (!state.selectedPesadaId) {
    document.getElementById("err").textContent = "Elegí un ticket abierto o capturá una entrada";
    return;
  }
  try {
    const pesada = await api(`/api/pesadas/${state.selectedPesadaId}/salida`, {
      method: "POST",
      body: JSON.stringify({ taraPallets: visible("taraPallets") ? num("taraPallets") : null })
    });
    state.selectedPesadaId = null;
    toast(`Ticket ${pesada.ticketNumero} cerrado · neto ${pesada.neto} kg`);
    await renderPesaje();
    document.getElementById("ultimo").textContent =
      `Ticket ${pesada.ticketNumero} cerrado · neto ${pesada.neto} kg (pallets ${pesada.taraPallets})`;
  } catch (e) {
    document.getElementById("err").textContent = e.message;
  }
}

async function renderReportes() {
  const rows = await api("/api/pesadas");
  document.getElementById("main").innerHTML = `
    <div class="page-head"><div><h2>Pesadas</h2><p>${can("pesadas.ver") ? "Últimos 200 movimientos de la planta." : "Tus últimos 200 movimientos."}</p></div></div>
    <div class="card">
      <table>
        <tr><th>Ticket</th><th>Empresa</th><th>Patente</th><th>Producto</th><th>Estado</th><th>Entrada</th><th>Salida</th><th>Pallets</th><th>Neto</th></tr>
        ${rows.map(p => `<tr>
          <td>${esc(p.ticketNumero)}</td>
          <td>${esc(empresaName(p.empresaId))}</td>
          <td>${esc(p.patenteChasis)}</td>
          <td>${esc(p.producto || "")}</td>
          <td>${esc(p.estado)}</td>
          <td>${p.pesoEntrada}</td>
          <td>${p.pesoSalida ?? "-"}</td>
          <td>${p.taraPallets}</td>
          <td>${p.neto ?? "-"}</td>
        </tr>`).join("")}
      </table>
    </div>`;
}

async function renderCatalogos() {
  const [clientes, productos, transportes] = await Promise.all([
    api("/api/catalogos/clientes"),
    api("/api/catalogos/productos"),
    api("/api/catalogos/transportes")
  ]);
  state.catalogos = { clientes, productos, transportes };
  document.getElementById("main").innerHTML = `
    <div class="page-head"><div><h2>Catálogos</h2><p>Compartidos por las empresas de esta planta.</p></div></div>
    <div class="grid3">
      ${catalogBlock("Productos", "np", "altaProducto", productos.map(p => p.nombre))}
      ${catalogBlock("Clientes", "nc", "altaCliente", clientes.map(c => c.razonSocial))}
      ${catalogBlock("Transportes", "nt", "altaTransporte", transportes.map(t => t.razonSocial))}
    </div>`;
}

function catalogBlock(title, inputId, fn, items) {
  return `<div class="card">
    <h3 style="margin-top:0">${title}</h3>
    ${can("catalogos.editar") ? `
    <input id="${inputId}" placeholder="Nuevo..." />
    <button class="btn-primary" style="margin-top:10px" onclick="${fn}()">${title === "Productos" ? "Agregar producto" : "Agregar"}</button>` : ""}
    <table style="margin-top:14px">${items.length ? items.map(x => `<tr><td>${esc(x)}</td></tr>`).join("") : "<tr><td>Sin datos</td></tr>"}</table>
  </div>`;
}

async function altaProducto() {
  if (!val("np")) return;
  await api("/api/catalogos/productos", { method: "POST", body: JSON.stringify({ nombre: val("np") }) });
  toast("Producto creado");
  renderCatalogos();
}
async function altaCliente() {
  if (!val("nc")) return;
  await api("/api/catalogos/clientes", { method: "POST", body: JSON.stringify({ razonSocial: val("nc") }) });
  toast("Cliente creado");
  renderCatalogos();
}
async function altaTransporte() {
  if (!val("nt")) return;
  await api("/api/catalogos/transportes", { method: "POST", body: JSON.stringify({ razonSocial: val("nt") }) });
  toast("Transporte creado");
  renderCatalogos();
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
  state.scaleMeta = await api("/api/scale/estado");
  const tabs = [
    ["general", "General"],
    ["balanza", "Balanza"],
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
      </div>
      <div class="btn-row"><button class="btn-primary" onclick="saveGeneral()">Guardar general</button></div>
    </div>`;
}

function renderConfigBalanza() {
  const c = state.config;
  const ports = state.scaleMeta.puertos || [];
  const portOpts = [`<option value="">Elegir puerto</option>`]
    .concat(ports.map(p => `<option value="${esc(p)}" ${c.scale_port === p ? "selected" : ""}>${esc(p)}</option>`))
    .join("");
  document.getElementById("configBody").innerHTML = `
    <div class="card">
      <div class="grid2">
        <div class="status-box"><span>Estado actual</span><strong>${esc(state.scaleMeta.status || state.scale.estado)}</strong></div>
        <div class="status-box"><span>Último error</span><strong>${esc(state.scaleMeta.lastError || "ninguno")}</strong></div>
      </div>
      <div class="grid2" style="margin-top:14px">
        <div><label>Modo</label>
          <select id="s_mode">
            <option value="sim" ${c.scale_mode !== "serial" ? "selected" : ""}>Simulada</option>
            <option value="serial" ${c.scale_mode === "serial" ? "selected" : ""}>Puerto serie</option>
          </select>
        </div>
        <div><label>Puerto COM</label><select id="s_port">${portOpts}</select></div>
        <div><label>Baudios</label><input id="s_baud" value="${esc(c.scale_baud || "9600")}" /></div>
        <div><label>Data bits</label>
          <select id="s_bits">
            ${[7,8].map(n => `<option ${String(c.scale_databits || "8") === String(n) ? "selected" : ""}>${n}</option>`).join("")}
          </select>
        </div>
        <div><label>Stop bits</label>
          <select id="s_stop">
            ${[1,2].map(n => `<option ${String(c.scale_stopbits || "1") === String(n) ? "selected" : ""}>${n}</option>`).join("")}
          </select>
        </div>
        <div><label>Paridad</label>
          <select id="s_par">
            ${["None","Even","Odd"].map(n => `<option ${ (c.scale_parity || "None") === n ? "selected" : ""}>${n}</option>`).join("")}
          </select>
        </div>
        <div><label>Protocolo</label>
          <select id="s_proto">
            <option value="Line" ${c.scale_protocol !== "STX" ? "selected" : ""}>Línea (CR/LF)</option>
            <option value="STX" ${c.scale_protocol === "STX" ? "selected" : ""}>STX / ETX</option>
          </select>
        </div>
        <div><label>Extracción (inicio:fin, opcional)</label><input id="s_extract" value="${esc(c.scale_extract || "")}" placeholder="ej. 4:10" /></div>
      </div>
      <div class="btn-row">
        <button class="btn-primary" onclick="saveBalanza()">Guardar y abrir puerto</button>
        <button class="btn" onclick="probarBalanza()">Probar de nuevo</button>
      </div>
    </div>`;
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
    termica: val("c_term")
  });
  renderConfig();
}

async function saveBalanza() {
  await saveConfig({
    scale_mode: val("s_mode"),
    scale_port: val("s_port"),
    scale_baud: val("s_baud"),
    scale_databits: val("s_bits"),
    scale_stopbits: val("s_stop"),
    scale_parity: val("s_par"),
    scale_protocol: val("s_proto"),
    scale_extract: val("s_extract")
  });
  renderConfig();
}

async function probarBalanza() {
  state.scaleMeta = await api("/api/scale/aplicar", { method: "POST" });
  toast(state.scaleMeta.lastError ? "No se pudo abrir el puerto" : "Puerto reaplicado", state.scaleMeta.lastError ? "err" : "ok");
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
