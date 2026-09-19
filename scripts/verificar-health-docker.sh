#!/usr/bin/env bash
# Verificación de health checks en el entorno Docker compuesto (T166; research.md §19).
#
# Comprueba, sobre docker-compose.yml:
#   1. /health/live y /health/ready responden 200 con SQL Server disponible;
#   2. al detener SQL Server, /health/ready pasa a 503 mientras /health/live sigue en 200
#      (readiness fallida no significa proceso roto: el orquestador no debe reiniciar la API);
#   3. al volver SQL Server, /health/ready se recupera sin reiniciar la API.
#
# Uso: scripts/verificar-health-docker.sh            (desde la raíz del repositorio)
#      API_PORT=18080 scripts/verificar-health-docker.sh
#      MANTENER=1 scripts/verificar-health-docker.sh  (no baja los contenedores al terminar)

set -euo pipefail

API_PORT="${API_PORT:-8080}"
BASE="http://localhost:${API_PORT}"
fallos=0

estado() { curl -s -o /dev/null -w '%{http_code}' --max-time 20 "$1" || echo "000"; }

esperar() {
  local url="$1" esperado="$2" limite="${3:-120}" inicio
  inicio=$(date +%s)
  while [ "$(estado "$url")" != "$esperado" ]; do
    if [ $(( $(date +%s) - inicio )) -ge "$limite" ]; then
      return 1
    fi
    sleep 3
  done
}

comprobar() {
  local descripcion="$1" url="$2" esperado="$3" limite="${4:-120}"
  if esperar "$url" "$esperado" "$limite"; then
    echo "  OK   ${descripcion}: ${esperado}"
  else
    echo "  FALLO ${descripcion}: se esperaba ${esperado}, se obtuvo $(estado "$url")"
    fallos=$((fallos + 1))
  fi
}

limpiar() {
  if [ "${MANTENER:-0}" != "1" ]; then
    docker compose stop api sqlserver >/dev/null 2>&1 || true
  fi
}
trap limpiar EXIT

echo "== Levantando SQL Server y la API =="
API_PORT="$API_PORT" docker compose up -d --build >/dev/null

echo "== 1. Con SQL Server disponible =="
comprobar "/health/live " "$BASE/health/live" 200
comprobar "/health/ready" "$BASE/health/ready" 200

echo "== 2. SQL Server detenido =="
docker compose stop sqlserver >/dev/null
comprobar "/health/ready" "$BASE/health/ready" 503 60
comprobar "/health/live " "$BASE/health/live" 200 10

echo "== 3. SQL Server recuperado =="
docker compose start sqlserver >/dev/null
comprobar "/health/ready" "$BASE/health/ready" 200 180
comprobar "/health/live " "$BASE/health/live" 200 10

if [ "$fallos" -eq 0 ]; then
  echo "Verificación de health checks: correcta"
else
  echo "Verificación de health checks: ${fallos} fallo(s)"
  exit 1
fi
