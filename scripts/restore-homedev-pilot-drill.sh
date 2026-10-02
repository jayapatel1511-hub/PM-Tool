#!/usr/bin/env bash
set -Eeuo pipefail
umask 077

fail() { echo "pilot restore drill refused: $*" >&2; exit 1; }
[[ $# -eq 1 && -s "$1" ]] || fail "provide one existing pilot dump"
dump="$(cd "$(dirname "$1")" && pwd -P)/$(basename "$1")"
[[ "$(basename "$dump")" =~ ^hub-pilot-[0-9]{14}\.dump$ ]] || fail "dump name is not a pilot dump"
[[ "$dump" != *review* && "$dump" != *hub_review* ]] || fail "review dump path"
base_dir="$(pwd -P)"
compose="$base_dir/hosting/homedev-pilot.compose.yml"
env_file="$base_dir/.runtime/pilot.env"
[[ -f "$compose" && -f "$env_file" ]] || fail "run from a pilot release"
grep -Eiq 'hub_review|pm-tool-review|review\.env|review-users' "$env_file" && fail "review settings in pilot.env"

inspect() { sudo docker inspect --format "$1" pm-tool-pilot-db-1 </dev/null; }
project="$(inspect '{{ index .Config.Labels "com.docker.compose.project" }}')"
service="$(inspect '{{ index .Config.Labels "com.docker.compose.service" }}')"
volume="$(inspect '{{ range .Mounts }}{{ if eq .Name "pm-tool-pilot-db" }}{{ .Name }}{{ end }}{{ end }}')"
[[ "$project" == pm-tool-pilot && "$service" == db && "$volume" == pm-tool-pilot-db ]] || fail "pilot database identity is not exact"

drill="hub_pilot_drill_$(date -u +%Y%m%d%H%M%S)_$$"
[[ "$drill" =~ ^hub_pilot_drill_[0-9]{14}_[0-9]+$ ]] || fail "unsafe drill database name"
compose_cmd=(sudo env RELEASE_SHA="${RELEASE_SHA:-unknown}" docker compose --env-file "$env_file" -f "$compose")
cleanup() { "${compose_cmd[@]}" exec -T db dropdb -U hub_pilot --if-exists "$drill" </dev/null >/dev/null 2>&1 || true; }
trap cleanup EXIT
"${compose_cmd[@]}" exec -T db createdb -U hub_pilot "$drill" </dev/null
"${compose_cmd[@]}" exec -T db pg_restore -U hub_pilot -d "$drill" --no-owner --no-privileges - <"$dump"
count="$("${compose_cmd[@]}" exec -T db psql -U hub_pilot -d "$drill" -Atc 'SELECT count(*) FROM hub.project' </dev/null | tr -d '[:space:]')"
[[ "$count" =~ ^[0-9]+$ ]] || fail "drill count was not numeric"
echo "pilot restore drill passed: $drill projects=$count"
