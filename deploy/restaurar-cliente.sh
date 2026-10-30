#!/usr/bin/env bash
# Restaura la base de UN cliente desde un backup, sin tocar a los demás.
#   ./restaurar-cliente.sh /var/backups/bfs/2026-11-02_0315/bfs_t_acopio_sur.dump
# Antes guarda una copia de cómo estaba, por si hay que volver atrás.
set -euo pipefail

cd "$(dirname "$0")"
ARCHIVO="${1:?Uso: restaurar-cliente.sh <archivo .dump>}"
BASE="$(basename "$ARCHIVO" .dump)"
[[ "$BASE" =~ ^bfs_t_[a-z0-9_]+$ ]] || { echo "El archivo tiene que ser el backup de un cliente (bfs_t_<código>.dump)" >&2; exit 1; }
[ -f "$ARCHIVO" ] || { echo "No existe $ARCHIVO" >&2; exit 1; }

read -r -p "Se va a reemplazar TODA la base $BASE con $ARCHIVO. Escribí el nombre de la base para confirmar: " ok
[ "$ok" = "$BASE" ] || { echo "Cancelado"; exit 1; }

pg() { docker compose exec -T postgres "$@"; }

antes="/var/backups/bfs/antes-de-restaurar-$BASE-$(date +%Y%m%d-%H%M).dump"
mkdir -p "$(dirname "$antes")"
pg pg_dump -U bfs -Fc "$BASE" > "$antes"
echo "Copia de cómo estaba: $antes"

pg psql -U bfs -d postgres -v ON_ERROR_STOP=1 -c "DROP DATABASE IF EXISTS \"$BASE\" WITH (FORCE)" -c "CREATE DATABASE \"$BASE\""
pg pg_restore -U bfs -d "$BASE" --no-owner < "$ARCHIVO"
echo "Listo: $BASE restaurada. Las porterías de ese cliente vuelven a mandar lo que tengan más nuevo en la próxima sincronización."
