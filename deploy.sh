#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

git -C "$SCRIPT_DIR" pull

if [[ -f "$SCRIPT_DIR/../trade-journal-compose.sh" ]]; then
  "$SCRIPT_DIR/../trade-journal-compose.sh" up -d --build
else
  docker compose -f "$SCRIPT_DIR/docker-compose.yml" up -d --build
fi
