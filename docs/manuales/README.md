# Manuales

- **Manual de usuario** (cliente): https://claude.ai/artifact/15RdsNPXHewwSVnBTfRHHz
- **Manual técnico** (instalador BFS): https://claude.ai/artifact/9m5Uzd9uuNWhdbuMRnc24z

Se exportan a PDF o Word desde el propio documento.

`img/` tiene las capturas. Para volver a sacarlas con una versión nueva (instalación de prueba con datos de ejemplo):

1. Levantar la API en un puerto libre con una carpeta de datos vacía y una licencia válida para la PC.
2. `python herramientas/manual_datos.py http://127.0.0.1:<puerto>` carga planta, catálogos, usuarios y pesadas de ejemplo.
3. Copiar `herramientas/Program.cs`, `capturas.csproj` y `pasos.json` a una carpeta temporal y
   `dotnet run -- http://127.0.0.1:<puerto> <ruta>/docs/manuales/img pasos.json` (usa Edge sin ventana).
