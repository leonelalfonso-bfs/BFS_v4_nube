# BFS 4.0 Nube

Proyecto nuevo. La versión Java 3.x **no se toca**. Plan y decisiones: [docs/PLAN_PARIDAD.md](docs/PLAN_PARIDAD.md).

Backend ASP.NET Core con frontend web, embebido en una ventana WPF (WebView2) para la PC de portería. El mismo backend corre en dos modos:

| Modo | Base | Balanza |
|------|------|---------|
| `Nodo` (por defecto) | SQLite en `%ProgramData%\bfs-4\bfs.db` | COM / simulada |
| `Nube` | PostgreSQL (`ConnectionStrings:Bfs`) | Sin balanza |

## Cómo correrlo

```powershell
cd "C:\Programas\Nueva BFS 4.0 Nube"
dotnet run --project src/Bfs.Api
```

Abrir http://127.0.0.1:5080

- Vendor (BFS): `bfs@bfs.local` / `bfs-setup`
- Admin cliente: `admin@bfs.local` / `admin`

`dotnet run` arranca en entorno `Development`, sin pedir licencia. La app de escritorio y el instalador sí la piden: ver [docs/LICENCIAS.md](docs/LICENCIAS.md).

La ventana de escritorio:

```powershell
dotnet run --project src/Bfs.Desktop
```

Otra carpeta de datos o modo nube, por línea de comandos o variables de entorno:

```powershell
dotnet run --project src/Bfs.Api -- --Bfs:DataPath=C:\temp\bfs-prueba
dotnet run --project src/Bfs.Api -- --Bfs:Modo=Nube --ConnectionStrings:Bfs="Host=localhost;Username=bfs;Password=..." --Bfs:Nube:SuperadminEmail=sa@bfs.com.ar --Bfs:Nube:SuperadminPassword=...
```

En modo nube hay una base central (`bfs_central`: clientes, índice de emails, superadmins) y una base por cliente (`bfs_t_<código>`), en el mismo servidor PostgreSQL; el usuario de la cadena necesita permiso para crear bases. El superadmin entra con su email y ve el panel de clientes. Para probar sin PostgreSQL: `--Bfs:Nube:Proveedor=Sqlite` (un archivo por cliente en la carpeta de datos).

## Instalador (PC de portería)

```powershell
powershell -ExecutionPolicy Bypass -File installeruild.ps1 -Version 4.0.0
```

Deja `artifacts\BFS-Pesaje-4.0.0.msi` (self-contained, Windows 10/11). Después de instalar, BFS entra con su usuario, activa la licencia (Configuración → Licencia) y, si el cliente venía de la 3.x, importa sus datos desde Sistema → Importar desde la 3.x.

## Tests

```powershell
dotnet test tests/Bfs.Tests
```

## Migraciones de base

Cada motor tiene sus migraciones: `src/Bfs.Infrastructure.Sqlite` y `src/Bfs.Infrastructure.Postgres`. Al cambiar el modelo hay que generar **las dos**:

```powershell
dotnet tool restore
dotnet ef migrations add <Nombre> --context BfsDbContext --project src/Bfs.Infrastructure.Sqlite --startup-project src/Bfs.Infrastructure.Sqlite --output-dir Migraciones
dotnet ef migrations add <Nombre> --context BfsDbContext --project src/Bfs.Infrastructure.Postgres --startup-project src/Bfs.Infrastructure.Postgres --output-dir Migraciones
```

La base central de la nube tiene sus propias migraciones: `--context BfsCentralContext --output-dir MigracionesCentral`.

Si falta alguna, falla un test (`Migraciones_*_al_dia_con_el_modelo`). Las migraciones se aplican solas al arrancar.

Las bases creadas por el spike anterior (sin migraciones) se renombran a `bfs.spike-<fecha>.db` al arrancar y se crea una nueva. No se borran.

## Estructura

- `Bfs.Core`: entidades, reglas de dominio, opciones.
- `Bfs.Infrastructure`: `BfsDbContext`, columnas de sincronización, selección de proveedor.
- `Bfs.Infrastructure.Sqlite` / `.Postgres`: migraciones por motor.
- `Bfs.Scale`: lectores de balanza.
- `Bfs.Integrations`: outbox y WORMS.
- `Bfs.Api`: host y endpoints por módulo (`Auth`, `Pesadas`, `Catalogos`, `Usuarios`, `Config`, `Balanza`, `Branding`).
- `Bfs.Desktop`: ventana WPF para la portería.
- `tools/Bfs.Licencias`: herramienta de BFS para emitir licencias.

## Qué ya está

- Empresas (dos de ejemplo, para el caso Worms)
- Pesada entrada/salida, tara pallets, neto
- Balanza simulada (peso en vivo)
- Outbox + mapper WORMS (se activa desde Configuración BFS)
- Login por email; roles vendor / admin / supervisor / operador / cliente con permisos por acción (`Bfs.Core/Permisos.cs`)
- Usuario cliente: solo ve las pesadas de su cliente del catálogo
- Catálogos: clientes, transportes (ATA), productos, exportadores, taras conocidas; búsqueda y CUIT validado
- Planta: datos de empresas, aduana y clave de ingreso manual; autocompletado por patente
- Pesaje: Normal, Tara conocida, Tomar tara, Aduana, ingreso manual con clave, anulación y tickets por puesto (`PP-NNNNNN`)
- Documentos: ticket PDF (normal y aduana), ticket térmico ESC/POS, Excel de pesadas
- Pesadas con filtros y totales, tablero; Sistema: backups, restauración, espejo, numeración; importador de la 3.x; instalador MSI
- Balanzas: varias por nodo (serie STX / Line o simuladas), modelos de la 3.x, reconexión automática, peso estable, diagnóstico de tramas
- Licencia firmada por PC
- Columnas de sincronización en todas las entidades (`Version`, `ModificadoEn`, borrado lógico, nodo de origen)
