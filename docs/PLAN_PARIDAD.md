# Plan de paridad BFS 3.x → BFS 4.0 (local + nube)

Referencia funcional: **BFS 3.0 v3.22** (Java 25 + JavaFX, `C:\Programas\BFS 3.0`).
La 3.x **no se modifica**: se usa solo como especificación y como fuente de datos para migrar.

Este documento reemplaza, para la 4.0, a `MIGRACION_DOTNET.md` de la 3.0. Ese plan era WPF nativo y solo local; la 4.0 cambia de arquitectura para poder correr también en la nube.

---

## 1. Decisiones tomadas

| # | Tema | Decisión |
|---|------|----------|
| 1 | Base de datos en la nube | **PostgreSQL** (18). Es libre y sin costo de licencia por base, tiene `jsonb` para los campos configurables, el proveedor EF Core (Npgsql) es maduro y `pg_dump` hace backups por base. |
| 2 | Separación de clientes | **Una base por cliente** (tenant). Backups, restauraciones y bajas individuales. |
| 3 | Corte de internet | **La portería sigue operando** y sincroniza cuando vuelve la conexión. Por eso la PC de portería siempre tiene un nodo local (ver §2). |
| 4 | Sistema operativo | La 4.0 exige **Windows 10 u 11** en la PC de portería. Windows 7 solo como navegador para consultar reportes (ver §2.4). Los clientes que necesiten pesar en Win7 siguen con la 3.x. |
| 5 | Datos de la 3.x | **Se importan** desde `truckscale.db` a la base 4.0, sin modificar la original. |
| 6 | Hosting | **El VPS propio** donde corre Leal Control, en contenedores aislados y con límites de memoria (ver §2.5). |
| 7 | Dominio y tenant | **Un solo subdominio** (`pesaje.` + dominio de Balanzas Full Service). El tenant **se detecta por el usuario** al iniciar sesión, sin subdominio por cliente (ver §2.6). |
| 8 | Licencias | Las da de alta BFS a mano al contratar: crea el tenant y el usuario administrador del cliente, que después carga sus usuarios. Si no paga, el **superadmin lo suspende** (ver §2.7). |
| 9 | Dominio | Subdominio `pesaje.` del dominio de **Balanzas Full Service**. |
| 10 | Usuario de login | **Email**, único en todo el sistema. |
| 11 | Gracia del nodo | **15 días** sin sincronizar o suspendido antes de bloquear pesadas nuevas. |

---

## 2. Arquitectura

### 2.1 Piezas

```
           PC de portería (Win10/11)                          Nube (opcional)
 ┌──────────────────────────────────────────┐        ┌───────────────────────────────┐
 │ Bfs.Desktop (WPF + WebView2)             │        │ Bfs.Api (modo Nube)           │
 │   └─ Bfs.Api (modo Nodo)                 │  sync  │   ├─ PostgreSQL: 1 base x     │
 │        ├─ SQLite local  ◄──── outbox ────┼───────►│   │   cliente                 │
 │        ├─ Bfs.Scale ── COM ── balanza    │ HTTPS  │   ├─ Consola vendor (BFS)     │
 │        └─ impresora térmica / PDF        │        │   └─ Web: oficina, clientes,  │
 │ Otras PCs de la LAN → navegador          │        │       reportes, integraciones │
 └──────────────────────────────────────────┘        └───────────────────────────────┘
```

- **Nodo local**: el mismo `Bfs.Api` dentro de `Bfs.Desktop`, con SQLite. Lee la balanza, imprime y pesa **aunque no haya internet**.
- **Nube**: el mismo `Bfs.Api` en modo central, con PostgreSQL y una base por cliente. La usan la oficina, los clientes del portal, BFS (`vendor`) y las integraciones.

### 2.2 Formas de instalarlo

| Modo | Portería | Nube | Para quién |
|------|----------|------|------------|
| **Local** | Nodo local | — | Clientes sin nube (reemplaza a la 3.x tal cual) |
| **Nube** | Nodo local sincronizado | Sí | Clientes que quieren reportes remotos, varias plantas o integraciones centralizadas |

En los dos modos la portería es igual: el mismo instalador y la misma pantalla. "Nube" es activar la sincronización con un tenant.

### 2.3 Sincronización (portería ↔ nube)

- **Ids `Guid`** generados en el nodo (ya se usan en el spike), así no hay choques al subir.
- **Outbox** en el nodo: cada alta o cambio de pesada genera un mensaje que se reintenta hasta que la nube confirma. Se reutiliza el outbox que hoy existe para WORMS.
- **Dueños de los datos**:
  - *Pesadas*: las crea y modifica el nodo de la portería; la nube las recibe. Si la oficina anula desde la nube, eso baja como un comando que el nodo aplica.
  - *Catálogos y configuración* (clientes, productos, transportes, taras, usuarios, campos): se editan en cualquier lado y gana la versión más nueva (`RowVersion` + fecha de modificación). Bajan al nodo en cada sincronización.
- **Números de ticket sin conexión**: cada puesto tiene un **prefijo** (`01-000123`, `02-000045`). Así dos puestos o una portería offline nunca repiten un número. Al importar de la 3.x se conserva el número original.
- **Visibilidad**: la portería muestra el estado (`En línea` / `Sin conexión, N pendientes`) y el vendor ve en la consola cuándo sincronizó cada nodo por última vez.
- Los integradores (WORMS, Centum) en modo nube salen desde la nube. En modo local salen desde el nodo cuando haya internet.

### 2.4 Windows 7

- **Portería en Win7: no.** .NET 10 no corre en Win7 y la portería necesita el nodo local para pesar sin internet.
- **Consulta en Win7: con reservas.** La parte web de la nube se puede abrir desde un navegador, pero en Win7 ya no hay navegadores con actualizaciones de seguridad (Chrome dejó de soportarlo en 2023 y Firefox ESR 115 fue el último). Puede funcionar para mirar reportes, pero no lo vamos a soportar oficialmente.
- Los clientes que hoy pesan en Win7 siguen con la 3.x (o con el prototipo `BFS Local Win7` de la 3.0) hasta que cambien la PC.

### 2.5 Hosting en el VPS de Leal Control

```
                 Nginx del VPS (ya existente) ── TLS (Let's Encrypt)
                  ├─ *.lealcontrol...        → Leal Control (PHP-FPM + MySQL, sin cambios)
                  └─ pesaje.<dominio BFS>    → contenedor bfs-api (Kestrel :8080)
                                                   │
                                     contenedor bfs-postgres (PostgreSQL 18)
                                       ├─ bfs_central   (tenants, nodos, índice de logins, superadmin)
                                       ├─ bfs_t_<id1>   (cliente 1)
                                       └─ bfs_t_<id2>   (cliente 2) ...
```

- Docker Compose con dos contenedores: `bfs-api` y `bfs-postgres`. Un PostgreSQL propio, separado del MySQL de Leal Control, para que un problema en uno no afecte al otro.
- **Límites de memoria y CPU** por contenedor, para que el pesaje nunca deje sin recursos a Leal Control.
- PostgreSQL escucha solo dentro de Docker; no se expone a internet.
- Backups: `pg_dump` diario **por base**, con retención y copia fuera del VPS (lo mismo que se use para Leal Control en `infra/backup`).

**Estimación de consumo** (a confirmar con los datos del VPS). Una portería hace del orden de decenas a cientos de pesadas por día, así que la carga es baja:

| Pieza | RAM aproximada |
|-------|----------------|
| `bfs-api` (.NET 10) | 150–300 MB |
| PostgreSQL base (`shared_buffers` 256 MB) | 300–500 MB |
| Por cada cliente activo (conexiones del pool) | ~10–20 MB |
| **Total para ~30 clientes** | **~1–1,5 GB** |
| Disco: por cliente y por año (~100 pesadas/día) | ~50–100 MB + backups |

Con 2 GB de RAM libres y 2 vCPU compartidos con Leal Control alcanza holgado para empezar. Cada base vacía no consume RAM; lo que cuesta son las conexiones, por eso el pool por tenant se limita a pocas (ej. `Max Pool Size=5`).

### 2.6 Detección del tenant por usuario

Como en Leal Control, hay una **base central** separada de las bases de cada cliente. La diferencia es que en vez de un subdominio por cliente se usa uno solo y el tenant sale del usuario:

1. La base central tiene un **índice de logins**: `usuario → tenant`. Solo guarda el nombre de usuario y a qué tenant pertenece; la contraseña y los datos del usuario viven en la base del cliente.
2. El usuario es el **email**, único en todo el sistema: el alta de usuario consulta el índice central y rechaza los repetidos.
3. Login: la API busca el usuario en el índice, abre la base de ese tenant, valida la contraseña ahí y emite un token con el `tenant_id`. Cada pedido posterior usa la base de ese tenant.
4. El **superadmin** vive solo en la base central, con su propia pantalla (`/admin`), y no tiene acceso operativo a los datos de los clientes.
5. **Nodo local**: no depende del índice. Se registra contra un tenant una sola vez con un **código de activación** que genera el superadmin, y desde ahí sincroniza con un token de nodo. Los usuarios se validan contra su copia local, así que el login funciona sin internet.

### 2.7 Licencias y suspensión

- Alta (superadmin): crear tenant → se crea la base y se corren las migraciones → se crea el usuario administrador del cliente → se genera el código de activación para cada nodo de portería.
- Estados del tenant: `Activo`, `Suspendido`, `Baja`.
- **Suspendido en la nube**: la web rechaza el login con un mensaje ("Cuenta suspendida, comuníquese con BFS") y la sincronización deja de aceptar datos.
- **Suspendido en el nodo local**: como el nodo trabaja sin internet, recibe una **licencia firmada con vencimiento** en cada sincronización (válida 15 días). Si deja de sincronizar o recibe estado suspendido, al vencer **bloquea las pesadas nuevas** pero deja consultar, reimprimir y exportar lo que ya tiene. Así una suspensión no hace perder datos y un corte de internet largo no frena la portería antes de tiempo.
- Reactivar = volver a `Activo` en el superadmin; el nodo se desbloquea en la siguiente sincronización.
- Modo Local (sin nube): sigue la licencia por máquina (`license.key`), generada por BFS.

---

## 3. Estado actual del spike 4.0

| Área | 3.x | Spike 4.0 |
|------|-----|-----------|
| Login + roles | `users` (texto plano, privilegios 0/1/2, `client_razon`) | JWT, contraseñas con hash, roles `vendor` / `admin` / `operador` |
| Empresas | Una sola (`client_config`) | Varias empresas por base ✅ |
| Catálogos | clientes, transportes, productos, exportadores, taras | clientes, transportes, productos (alta básica) |
| Balanza | STX / Line, regex, varias balanzas, `balanza_modelos` | Una balanza, simulada o serie configurable (sin probar en hardware) |
| Pesaje | Normal, tara conocida, tomar tara, aduana, pallets, retomar pendiente | Entrada/salida, tara pallets, neto |
| Tickets | PDF A4 + térmica | — |
| Reportes / Excel / dashboard | Sí | Solo listado de pesadas |
| Portal cliente | `/reportes` + Cloudflare | — |
| Sistema | Backup, restore, limpiar, espejo, número de ticket | — |
| Integraciones | Centum (documentada) | Outbox + mapper WORMS ✅ |
| Licencia | `license.key` por MAC | — |
| Campos configurables | — | `FieldCatalog` / `fields_json` ✅ (nuevo) |
| Sincronización | — | — |
| Instalador | MSI (jpackage) | — |

---

## 4. Orden de trabajo

Cada fase termina compilando y con algo que se pueda demostrar. Las fases 0 a 6 dejan listo el **modo Local**, que sirve para reemplazar a la 3.x. Las fases 7 y 8 suman la nube. Desde la fase 0 el modelo de datos ya contempla la sincronización, para no rehacer nada después.

### Fase 0 — Bases

> **Estado (2026-10-22):** hecha en la rama `fase-0`, salvo el repo remoto. Los servicios quedaron en carpetas por módulo dentro de `Bfs.Api` (ej. `Pesadas/PesajeService.cs`) porque usan EF y WORMS, que `Core` no conoce; `Core` mantiene las reglas puras (`NetoCalculator`). PostgreSQL se verificó a nivel modelo y migraciones; falta correrlo contra un servidor real.

1. Primer commit y repo remoto. Proyecto de tests (`Bfs.Tests`, xUnit) y CI con build + tests.
2. **Migraciones de EF Core** en lugar de `EnsureCreated`, con dos proveedores: **SQLite** (nodo) y **PostgreSQL** (nube). Migraciones separadas por proveedor.
3. Columnas comunes para sincronizar en todas las entidades: `Id` Guid, `CreadoEn`, `ModificadoEn`, `RowVersion`, `Eliminado` (borrado lógico), `OrigenNodoId`.
4. Separar `ApiHost.cs` en módulos de endpoints (`Pesadas`, `Catalogos`, `Usuarios`, `Config`, `Scale`). La lógica de negocio va a servicios en `Core`, no a los lambdas.
5. `Modo` (`Nodo` / `Nube`) en `appsettings` y DI según el modo.
6. Borrar los `Class1.cs` sobrantes.

**Terminada cuando:** la base se crea con migraciones en SQLite y en PostgreSQL, y CI está en verde.

### Fase 1 — Usuarios, roles y licencia

> **Estado (2026-10-22):** hecha en la rama `fase-1`. Permisos en `Bfs.Core/Permisos.cs`; licencia firmada (ECDSA) con herramienta en `tools/Bfs.Licencias` (ver `docs/LICENCIAS.md`). El índice central de logins por email se hace en la Fase 7. Pendiente para el importador (Fase 6): asociar las pesadas viejas a su cliente del catálogo por razón social.

1. Equivalencia de roles: privilegio 0 → `operador`, 1 → `supervisor` (rol nuevo), 2 → `admin`, más `vendor` (BFS).
2. Rol **`cliente`** asociado a un `Cliente` del catálogo. Equivale a `client_razon`, pero con FK y no con texto exacto. Solo ve sus pesadas.
3. Permisos por pantalla y por acción (quién reimprime, quién anula, quién carga peso manual).
4. Login sin conexión en el nodo, con los usuarios ya sincronizados.
5. Licencia: `license.key` por máquina en el nodo (misma regla que `DatabaseManager.getMachineId()`) y suscripción por tenant en la nube.

**Terminada cuando:** el admin da de alta un usuario cliente y ese usuario solo ve lo suyo.

### Fase 2 — Catálogos y configuración

> **Estado (2026-10-22):** hecha en la rama `fase-2`. Catálogos con un mismo CRUD (`/api/catalogos/{tipo}`: búsqueda, nombre sin repetir, CUIT validado, borrado lógico que se recupera al volver a darlo de alta). Pantalla **Planta** para el admin del cliente (empresas, aduana, clave de ingreso manual con hash). Las funciones (aduana, tara conocida, térmica, ingreso manual) son de toda la instalación y las habilita BFS; por empresa queda para cuando un cliente lo pida. Autocompletado por patente y búsqueda rápida con listas desplegables en el pesaje. La clave de ingreso manual se exige en la Fase 4.

1. Clientes (CUIT), transportes (CUIT, `es_ata`, `codigo_ata`), productos (código), **exportadores**, **taras conocidas** (`taras`).
2. Datos de la empresa: razón social, CUIT, dirección, localidad, provincia, teléfono, mail y logo (hoy `client_config`).
3. Funciones que se activan por empresa: tara conocida, ingreso manual (+ clave), térmica (impresora, ancho), aduana, contenedores, pallets.
4. Configuración de aduana: código de aduana, LOT, certificados y vencimientos (también los de la balanza).
5. Autocompletado de datos frecuentes (equivale a `weighing_data_cache`: chofer, documento, nacionalidad).
6. Búsqueda rápida en los combos (equivale a `ComboBoxBusquedaRapida`).

**Terminada cuando:** todo lo que se carga en *Configuraciones* de la 3.x se puede cargar en la 4.0.

### Fase 3 — Balanza

> **Estado (2026-10-22):** hecha en la rama `fase-3`, **salvo la prueba en hardware real** (puntos 4 con tramas de campo y 5). Varias balanzas por nodo con reconexión automática, estado "sin señal" y peso estable (sin cambios por 1,5 s). Extracción `inicio:fin` igual que la 3.x (el signo negativo ahora se respeta). Los 8 modelos de `default_models.sql` se siembran solos y la balanza que tenía el spike pasa a "Balanza 1". En Configuración BFS → Balanzas: ABM, últimas tramas recibidas y probador de extracción. El pesaje no toma peso de una balanza que no está leyendo. Pendiente para la Fase 4: decidir si se exige peso estable para capturar.

1. Protocolos **STX** y **Line** con regex de extracción, portados de `SerialReader.java` / `SerialPortHelper.java`.
2. **Varias balanzas** por instalación y catálogo de **modelos** (importar `default_models.sql`).
3. Estabilidad del peso, reconexión automática, log con muestreo (sin escribir a disco en cada byte).
4. Tests con tramas reales capturadas de clientes.
5. Prueba en hardware real con al menos dos modelos distintos.

**Terminada cuando:** se lee el peso de una balanza real configurada desde la UI, sin tocar archivos.

### Fase 4 — Pesaje (corazón del sistema)

> **Estado (2026-10-22):** hecha en la rama `fase-4`. Normal, Tara conocida (un paso), Tomar tara, Aduana (escenarios y contenedor con la regla de la 3.x), ingreso manual (permiso + habilitación + clave, queda registrado quién), anulación con motivo (no borra como la 3.x; el ticket no se reutiliza), pendientes sin límite y "Retomar" desde Pesadas. Tickets `PP-NNNNNN` con contador por empresa y puesto (control de versión + reintento). **Decisión:** por defecto solo se captura con peso estable (BFS lo puede apagar; el ingreso manual no lo necesita). Pendiente: la anulación no se avisa a WORMS (ver Fase 8).

`PesajeScene.java` tiene 3.600 líneas: **no se clona**. Se separa en servicio de dominio + componentes de UI.

1. Selector: **Normal / Tara conocida / Tomar tara**.
2. Normal: entrada → queda pendiente → salida, con bruto/tara/neto (`NetoCalculator`).
3. Tara conocida: salida en un solo paso usando `taras`. Tomar tara: registra la tara del vehículo.
4. **Aduana**: operación normal o aduanera, escenarios, bultos, `contenedor_peso_neto`, exportador.
5. Tara de pallets (ya está) y carro (`carro_id`).
6. Ingreso manual de peso con clave y auditoría (quién y cuándo).
7. Retomar pendientes y anular (con motivo).
8. Numeración de tickets **por puesto con prefijo** (ver §2.3), sin huecos ni repetidos.

**Terminada cuando:** se pueden hacer en la 4.0 todos los tipos de pesada de la 3.x con los mismos datos.

### Fase 5 — Tickets y documentos

1. Ticket PDF A4 con QuestPDF (layout de `TicketGenerator.java`, logo, título y pie configurables).
2. Ticket térmico ESC/POS (`ThermalTicketGenerator.java`) desde el nodo.
3. Reimpresión desde reportes.
4. Excel con ClosedXML.

**Terminada cuando:** el ticket impreso por la 4.0 tiene los mismos datos que el de la 3.x, comparándolos uno al lado del otro.

### Fase 6 — Reportes, sistema, importador e instalador (**primer cliente local**)

1. Reportes: filtros (fechas, cliente, transporte, producto, patente, estado), totales, Excel, reimpresión.
2. Dashboard / estadísticas (`DashboardService.java`).
3. Portal cliente: la misma web con rol `cliente`. En modo Local, túnel quick de Cloudflare opcional (`CloudflareTunnelManager.java`).
4. Sistema (admin): backup / restore / limpiar BD, espejo automático, número de ticket inicial.
5. **Importador desde 3.x**: lee `%ProgramData%\bfs-truckscale-data\truckscale.db` en solo lectura y carga la base 4.0.
   - Detecta la base al instalar y ofrece importarla. También se puede correr después desde Sistema.
   - Las contraseñas en texto plano se convierten a hash.
   - `client_razon` se asocia al `Cliente` por razón social; lo que no coincida se informa.
   - Los IDs enteros pasan a Guid, conservando el número de ticket original.
   - Las columnas de fotos (`foto_entrada`, etc.) se pasan como dato aunque la 4.0 no capture fotos.
   - Al final muestra un informe: cuántas pesadas, clientes y usuarios se importaron y cuáles se rechazaron y por qué.
   - Se prueba con copias reales de bases de clientes.
6. Instalador MSI self-contained `win-x64` (WiX), con icono y accesos directos. Bloquea la instalación en versiones anteriores a Windows 10.

**Terminada cuando:** un cliente actual se pasa a la 4.0 local con todo su historial y sigue trabajando igual o mejor.

### Fase 7 — Nube

1. API en modo `Nube`: base central `bfs_central`, **tenant por usuario** con el índice de logins (§2.6) y una cadena de conexión PostgreSQL por tenant.
2. Panel **superadmin**: alta de tenant (crear base + migraciones + admin del cliente), suspender / reactivar / dar de baja, funciones habilitadas, códigos de activación de nodos y última sincronización de cada uno.
3. **Sincronización** nodo ↔ nube (§2.3): activación del nodo con código, subida del outbox, bajada de catálogos, comandos y licencia firmada, reintentos, estado visible.
4. Licencia del nodo con vencimiento y bloqueo de pesadas nuevas al vencer (§2.7).
5. Pruebas de corte: pesar offline durante horas, reconectar y comprobar que no se pierde ni se duplica nada. Prueba de aislamiento: un usuario de un tenant nunca ve datos de otro.
6. Despliegue en el VPS (§2.5): Docker Compose, Nginx + Let's Encrypt, límites de recursos, logs.
7. **Backups individuales por tenant** (`pg_dump` programado, retención, copia fuera del VPS y restauración de un solo cliente).

**Terminada cuando:** una portería pesa sin internet, al volver la conexión todo aparece en la nube y la oficina lo ve desde el navegador.

### Fase 8 — Integraciones

1. Conector **WORMS** contra el endpoint de prueba (outbox existente).
2. Conector **Centum** (según `DOCUMENTACION_INTEGRACION_CENTUM.md` de la 3.0).

---

## 5. Fuera de alcance

- Modbus TCP
- Cámaras IP / OCR (solo se conservan las rutas de fotos que se importan)
- Portería en Windows 7 / 8 (sigue la 3.x)
- Linux/Mac como puesto de balanza (el servidor de la nube sí corre en Linux)
- Pesar desde el navegador sin nodo local (no puede leer el COM ni operar sin internet)

---

## 6. Decisiones pendientes

| # | Decisión | Opciones | Afecta |
|---|----------|----------|--------|
| 1 | Datos del VPS para confirmar la capacidad | RAM, vCPU, disco libre, sistema operativo, si ya tiene Docker | Fase 7 |

---

## 7. Correspondencia de archivos 3.x → 4.0

| 3.x (`com.balanza`) | 4.0 |
|---------------------|-----|
| `LoginScene`, `User` | `/api/auth`, `Usuario`, `Roles` |
| `MainScene` | Shell web (`wwwroot`) |
| `SelectorPesajeScene`, `PesajeScene`, `TarasScene` | `PesajeService` (Core) + pantallas web de pesaje |
| `ConfiguracionesScene` | Pantallas de catálogos / configuración / usuarios |
| `ReportesScene`, `DashboardScene`, `DashboardService` | Reportes + dashboard web |
| `SistemaScene` | Pantalla de sistema (admin) |
| `DatabaseManager`, `DatabaseConfig` | `BfsDbContext` + migraciones + importador 3.x |
| `SerialReader`, `SerialPortHelper` | `Bfs.Scale` |
| `TicketGenerator`, `ThermalTicketGenerator`, `ThermalPrinterManager` | `Bfs.Documents` (nuevo) |
| `ExcelExporter` | `Bfs.Documents` |
| `ApiServer`, `CloudflareTunnelManager` | Misma web con rol `cliente`; túnel solo en modo Local |
| `LicenseActivator` | `LicenseService` |
| `VersionManager` | Versión de ensamblado + endpoint `/api/health` |
| — | `Bfs.Sync` (nuevo): outbox, cliente de sincronización y estado |
