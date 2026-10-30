# Despliegue de la nube en el VPS

Todo corre en Docker, en el mismo VPS de Leal Control pero aparte: su propio PostgreSQL (Leal Control sigue con su MySQL), límites de memoria y CPU, y la API escuchando solo en `127.0.0.1:8080`. El Nginx que ya tenés publica `pesaje.<dominio de BFS>` con HTTPS.

```
internet ──HTTPS──► Nginx del VPS ──► 127.0.0.1:8080  contenedor api (Bfs.Api, modo Nube)
                     └─ Leal Control (sin cambios)          │
                                                  contenedor postgres (bfs_central, bfs_t_<cliente>...)
```

Los comandos son para Ubuntu/Debian. Donde dice `pesaje.EJEMPLO.com.ar` va el subdominio real.

## 1. DNS

En el proveedor del dominio de Balanzas Full Service: registro **A** `pesaje` → IP del VPS. Esperar a que resuelva (`ping pesaje.EJEMPLO.com.ar`).

## 2. Docker (si el VPS todavía no lo tiene)

```bash
docker --version || curl -fsSL https://get.docker.com | sudo sh
```

## 3. El código y la configuración

```bash
sudo git clone https://github.com/leonelalfonso-bfs/BFS_v4_nube.git /opt/bfs-nube
cd /opt/bfs-nube/deploy
sudo cp .env.example .env
sudo nano .env        # PG_PASSWORD (openssl rand -base64 30), SUPERADMIN_EMAIL, SUPERADMIN_PASSWORD
sudo chmod 600 .env
```

La clave con la que la nube firma las licencias de las porterías está en tu PC (`C:\Users\fulls\.bfs\bfs-nube-licencias-privada.pem`). Desde tu PC:

```bash
scp "$env:USERPROFILE\.bfs\bfs-nube-licencias-privada.pem" usuario@VPS:/tmp/
```

y en el VPS:

```bash
sudo mkdir -p /opt/bfs-nube/deploy/secretos
sudo mv /tmp/bfs-nube-licencias-privada.pem /opt/bfs-nube/deploy/secretos/licencias-nube.pem
sudo chmod 644 /opt/bfs-nube/deploy/secretos/licencias-nube.pem
```

> Es de lectura para el usuario del contenedor; la carpeta `secretos` no va al repositorio. Sin este archivo la nube funciona igual pero no renueva licencias (queda un aviso en el log).

## 4. Levantar

```bash
cd /opt/bfs-nube/deploy
sudo docker compose up -d --build
sudo docker compose ps                     # api y postgres "healthy"
sudo docker compose logs -f api            # "Se creó el superadmin ..." la primera vez
curl -s http://127.0.0.1:8080/api/health   # {"status":"ok",...,"modo":"Nube"}
```

## 5. Nginx y HTTPS

```bash
sudo cp /opt/bfs-nube/deploy/nginx-pesaje.conf /etc/nginx/sites-available/pesaje
sudo nano /etc/nginx/sites-available/pesaje      # poner el subdominio real
sudo ln -s /etc/nginx/sites-available/pesaje /etc/nginx/sites-enabled/pesaje
sudo nginx -t && sudo systemctl reload nginx
sudo certbot --nginx -d pesaje.EJEMPLO.com.ar
```

Entrar a `https://pesaje.EJEMPLO.com.ar` con el superadmin. Después se pueden borrar `SUPERADMIN_EMAIL` y `SUPERADMIN_PASSWORD` del `.env` (el usuario ya quedó creado).

## 6. Backups

Uno por base (la central y una por cliente), todos los días, 30 días de historia:

```bash
sudo crontab -e
# agregar:
15 3 * * * /opt/bfs-nube/deploy/backup.sh >> /var/log/bfs-backup.log 2>&1
```

Probarlo una vez a mano: `sudo /opt/bfs-nube/deploy/backup.sh` → queda en `/var/backups/bfs/<fecha>/`.

**Copia fuera del VPS** (recomendado): con [rclone](https://rclone.org) configurado a un Drive, S3, etc.:

```bash
15 3 * * * BFS_COPIA_REMOTA="rclone copy {dir} remoto:bfs-backups/{fecha}" /opt/bfs-nube/deploy/backup.sh >> /var/log/bfs-backup.log 2>&1
```

**Restaurar un solo cliente** (no toca a los demás; antes guarda cómo estaba):

```bash
cd /opt/bfs-nube/deploy
sudo ./restaurar-cliente.sh /var/backups/bfs/2026-11-02_0315/bfs_t_acopio_sur.dump
```

## 7. Actualizar a una versión nueva

```bash
cd /opt/bfs-nube && sudo git pull
cd deploy && sudo docker compose up -d --build
```

Al arrancar, la API pone al día la base central y la de cada cliente (migraciones). Conviene correr `backup.sh` antes.

## 8. Dar de alta un cliente y su portería

1. Panel → **Nuevo cliente** (nombre, código, email y clave del administrador).
2. **Porterías** → nombre → **Crear y generar código**. El código se ve una sola vez y vale 7 días.
3. En la PC de portería (instalada con el MSI de la versión actual): entrar como `bfs@bfs.local` → Configuración → **Nube** → dirección `https://pesaje.EJEMPLO.com.ar` y el código → **Conectar**.
4. Si la portería venía de la 3.x, importar primero (Sistema → Importar) y después conectar: todo lo importado sube a la nube.

## Consumo

Con los límites del `.env` (API 512 MB, PostgreSQL 768 MB) alcanza para decenas de clientes; cada cliente nuevo suma poco (una base vacía no ocupa memoria). Para ver el consumo real: `sudo docker stats`.

## Si algo falla

| Síntoma | Dónde mirar |
|---------|-------------|
| La web no abre | `sudo docker compose ps`, `sudo docker compose logs api`, `sudo nginx -t` |
| "Código inválido o vencido" al conectar una portería | Generar otro en el panel (dura 7 días y se usa una vez) |
| La portería dice "sin conexión" | Que la PC llegue a `https://pesaje...` desde el navegador; el log de la API |
| Las porterías no reciben licencia | `deploy/secretos/licencias-nube.pem` y el log ("No se encontró la clave de licencias") |
