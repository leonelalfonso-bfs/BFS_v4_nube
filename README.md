# BFS 4.0 Nube (spike)

Proyecto nuevo. La versión Java 3.x **no se toca**.

Aplicación instalable con backend ASP.NET Core y frontend web embebido (WebView2). Una base SQLite por cliente; varias empresas adentro. La configuración de funciones la hace BFS (`usuario bfs`), el cliente solo carga usuarios y datos menores.

## Cómo correr el spike

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

La base queda en `%ProgramData%\bfs-4\bfs.db`.

## Qué ya está

- Empresas (dos de ejemplo, para el caso Worms)
- Pesada entrada/salida, tara pallets, neto
- Balanza simulada (peso en vivo)
- Outbox + mapper WORMS (se activa desde Configuración BFS)
- Roles vendor / admin / operador

## Siguiente

Puerto serie real, tickets PDF, migración desde 3.x, y el conector WORMS contra el endpoint de prueba.
