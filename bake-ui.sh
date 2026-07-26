#!/usr/bin/env bash
# Build the React UI and bake it into the Playbook Service's wwwroot,
# so `dotnet run --project MessageHook.Playbook.Service` serves the UI + API same-origin (no Vite dev server).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
UI="$ROOT/messagehook-ui"
WWWROOT="$ROOT/MessageHook.Playbook.Service/wwwroot"

echo "[bake-ui] building React UI..."
if [ ! -d "$UI/node_modules" ]; then
  echo "[bake-ui] node_modules missing -> npm install"
  npm --prefix "$UI" install
fi
npm --prefix "$UI" run build

echo "[bake-ui] copying dist -> wwwroot"
rm -rf "$WWWROOT"
mkdir -p "$WWWROOT"
cp -r "$UI/dist/." "$WWWROOT/"

echo "[bake-ui] done. Serve it with:"
echo "  dotnet run --project MessageHook.Playbook.Service"
