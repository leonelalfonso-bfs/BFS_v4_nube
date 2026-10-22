# Licencias del nodo (BFS 4.0)

En la 3.x, `license.key` era la MAC en texto plano: cualquiera podía escribirla. En la 4.0 la licencia está **firmada** con una clave privada que tiene solo BFS. El sistema trae la clave pública, que sirve para verificar pero no para firmar.

## Dónde está cada cosa

| Qué | Dónde |
|-----|-------|
| Clave **privada** (firma) | `C:\Users\fulls\.bfs\bfs-licencias-privada.pem`. **No va al repositorio** (`*.pem` está en `.gitignore`). |
| Clave pública (verifica) | `src/Bfs.Core/Licencias/LicenciaClave.cs` |
| Licencia de cada PC | `%ProgramData%\bfs-4\license.key` |
| Herramienta | `tools/Bfs.Licencias` |

> **Hacé una copia de la clave privada fuera de esta PC** (pendrive, gestor de contraseñas). Si se pierde, hay que generar otra, cambiar la pública en el código y reemitir todas las licencias. Si alguien la obtiene, puede fabricar licencias.

## Emitir una licencia

1. En la PC del cliente: entrar como `bfs@bfs.local` → **Configuración → Licencia** y anotar el **ID de máquina** (`MAC-XXXXXXXXXXXX`). Sin licencia también aparece en la pantalla de Pesaje.
2. En tu PC:

   ```powershell
   dotnet run --project tools/Bfs.Licencias -- generar --privada "$env:USERPROFILE\.bfs\bfs-licencias-privada.pem" --cliente "Razón social" --maquina MAC-XXXXXXXXXXXX
   ```

   Opcional: `--vence 2027-03-31` (vale hasta el final de ese día) y `--salida license.key` para guardarla en un archivo.
3. Copiar el texto (`eyJ...`) y pegarlo en **Configuración → Licencia → Activar licencia**. También se puede copiar el archivo a `%ProgramData%\bfs-4\license.key`.

Para revisar una licencia: `dotnet run --project tools/Bfs.Licencias -- ver license.key`.

## Qué pasa sin licencia válida

- No se registran **entradas** nuevas.
- Sí se puede cerrar la **salida** de los camiones que ya entraron, consultar, reimprimir y exportar.
- La licencia vale con cualquier placa de red física de la PC (Ethernet o Wi-Fi), así que no se invalida si cambia el orden de las placas.

Estados: `Valida`, `Ausente`, `Invalida` (firma incorrecta), `OtraMaquina`, `Vencida`, `NoRequerida` (modo Nube o desarrollo).

## Desarrollo

`dotnet run --project src/Bfs.Api` usa el entorno `Development` (`Properties/launchSettings.json`), donde `appsettings.Development.json` desactiva la licencia. Ese archivo no se publica en el instalador, y en cualquier otro entorno la licencia es obligatoria y la clave pública no se puede cambiar por configuración.

## Nube (Fase 7)

El mismo formato sirve para la licencia que la nube le renueva al nodo en cada sincronización, con vencimiento a 15 días.
