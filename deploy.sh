#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

git -C "$SCRIPT_DIR" pull

if [[ -f "$SCRIPT_DIR/../trade-journal-compose.sh" ]]; then
  "$SCRIPT_DIR/../trade-journal-compose.sh" up -d --build
else
  compose_args=(-f "$SCRIPT_DIR/docker-compose.yml")
  shared_db_network="${TRADE_JOURNAL_SHARED_DB_NETWORK:-app-db}"

  # On rkserver, Postgres is exposed to app stacks over the shared Docker
  # network. Only add the override when that network actually exists so local
  # development keeps working without extra setup.
  if docker network inspect "$shared_db_network" >/dev/null 2>&1; then
    compose_args+=(-f "$SCRIPT_DIR/docker-compose.shared-db.yml")
  fi

  docker compose "${compose_args[@]}" up -d --build
fi
