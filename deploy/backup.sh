#!/usr/bin/env bash
# Backup diario de la nube: un archivo por base (la central y una por cliente), así se restaura un cliente solo.
# Cron del servidor (todos los días a las 3:15):
#   15 3 * * * /opt/bfs-nube/deploy/backup.sh >> /var/log/bfs-backup.log 2>&1
# Variables opcionales:
#   BFS_BACKUPS=/var/backups/bfs     carpeta de destino
#   BFS_RETENCION=30                 días que se guardan
#   BFS_COPIA_REMOTA="rclone copy {dir} remoto:bfs-backups/{fecha}"   copia fuera del VPS ({dir} y {fecha} se reemplazan)
set -euo pipefail

cd "$(dirname "$0")"
DESTINO="${BFS_BACKUPS:-/var/backups/bfs}"
RETENCION="${BFS_RETENCION:-30}"
FECHA="$(date +%Y-%m-%d_%H%M)"
DIR="$DESTINO/$FECHA"
mkdir -p "$DIR"

pg() { docker compose exec -T postgres "$@"; }

BASES=$(pg psql -U bfs -d postgres -Atc "SELECT datname FROM pg_database WHERE datname = 'bfs_central' OR datname LIKE 'bfs\_t\_%' ORDER BY 1")
if [ -z "$BASES" ]; then
    echo "$(date -Is) ERROR: no se encontraron bases para respaldar" >&2
    exit 1
fi

errores=0
for base in $BASES; do
    if pg pg_dump -U bfs -Fc "$base" > "$DIR/$base.dump"; then
        echo "$(date -Is) ok $base ($(du -h "$DIR/$base.dump" | cut -f1))"
    else
        echo "$(date -Is) ERROR al respaldar $base" >&2
        errores=$((errores + 1))
    fi
done

# Logos y clave de sesiones (volumen /data de la API).
docker compose exec -T api tar -czf - -C /data . > "$DIR/datos-api.tar.gz" || errores=$((errores + 1))

if [ -n "${BFS_COPIA_REMOTA:-}" ]; then
    cmd="${BFS_COPIA_REMOTA//\{dir\}/$DIR}"
    cmd="${cmd//\{fecha\}/$FECHA}"
    eval "$cmd" || { echo "$(date -Is) ERROR en la copia remota" >&2; errores=$((errores + 1)); }
fi

find "$DESTINO" -mindepth 1 -maxdepth 1 -type d -mtime +"$RETENCION" -exec rm -rf {} +
echo "$(date -Is) backup terminado en $DIR con $errores errores"
exit $errores
