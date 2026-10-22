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

- Vendor: `bfs` / `bfs-setup`
- Admin cliente: `admin` / `admin`

La ventana de escritorio:

```powershell
dotnet run --project src/Bfs.Desktop
```

Otra carpeta de datos o modo nube, por línea de comandos o variables de entorno:

```powershell
dotnet run --project src/Bfs.Api -- --Bfs:DataPath=C:\temp\bfs-prueba
dotnet run --project src/Bfs.Api -- --Bfs:Modo=Nube --ConnectionStrings:Bfs="Host=localhost;Database=bfs_t_demo;Username=bfs;Password=..."
```

## Tests

```powershell
dotnet test tests/Bfs.Tests
```

## Migraciones de base

Cada motor tiene sus migraciones: `src/Bfs.Infrastructure.Sqlite` y `src/Bfs.Infrastructure.Postgres`. Al cambiar el modelo hay que generar **las dos**:

```powershell
dotnet tool restore
dotnet ef migrations add <Nombre> --project src/Bfs.Infrastructure.Sqlite --startup-project src/Bfs.Infrastructure.Sqlite --output-dir Migraciones
dotnet ef migrations add <Nombre> --project src/Bfs.Infrastructure.Postgres --startup-project src/Bfs.Infrastructure.Postgres --output-dir Migraciones
```

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

## Qué ya está

- Empresas (dos de ejemplo, para el caso Worms)
- Pesada entrada/salida, tara pallets, neto
- Balanza simulada (peso en vivo)
- Outbox + mapper WORMS (se activa desde Configuración BFS)
- Roles vendor / admin / operador
- Columnas de sincronización en todas las entidades (`Version`, `ModificadoEn`, borrado lógico, nodo de origen)
