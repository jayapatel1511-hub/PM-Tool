#!/usr/bin/env bash
set -Eeuo pipefail
umask 077

base_dir="$(pwd -P)"
compose="$base_dir/hosting/homedev-pilot.compose.yml"
runtime="$base_dir/.runtime"
env_file="$runtime/pilot.env"
backup_dir="$base_dir/data/backups"

fail() { echo "pilot backup refused: $*" >&2; exit 1; }
[[ -f "$compose" && -f "$env_file" ]] || fail "run from a pilot release with pilot.env and pilot compose"
resolved_runtime="$(cd "$runtime" && pwd -P)"
[[ "$resolved_runtime" != *review* && "$resolved_runtime" != *hub_review* ]] || fail "review runtime path"
grep -Eiq 'hub_review|pm-tool-review|review\.env|review-users' "$env_file" && fail "review settings in pilot.env"
grep -q '^PILOT_HOSTNAME=pm\.engcalchub\.com$' "$env_file" || fail "unexpected pilot hostname"
mkdir -p "$backup_dir"
backup_dir="$(cd "$backup_dir" && pwd -P)"
[[ "$backup_dir" != *review* && "$backup_dir" != *hub_review* ]] || fail "review backup path"

inspect() { sudo docker inspect --format "$1" pm-tool-pilot-db-1 </dev/null; }
project="$(inspect '{{ index .Config.Labels "com.docker.compose.project" }}')"
service="$(inspect '{{ index .Config.Labels "com.docker.compose.service" }}')"
volume="$(inspect '{{ range .Mounts }}{{ if eq .Name "pm-tool-pilot-db" }}{{ .Name }}{{ end }}{{ end }}')"
[[ "$project" == pm-tool-pilot && "$service" == db && "$volume" == pm-tool-pilot-db ]] || fail "pilot database identity is not exact"

stamp="$(date -u +%Y%m%d%H%M%S)"
target="$backup_dir/hub-pilot-$stamp.dump"
partial="$target.partial"
compose_cmd=(sudo env RELEASE_SHA="${RELEASE_SHA:-unknown}" docker compose --env-file "$env_file" -f "$compose")
trap 'rm -f -- "$partial"' EXIT
"${compose_cmd[@]}" exec -T db pg_dump -U hub_pilot -d hub_pilot -Fc --no-owner --no-privileges </dev/null >"$partial"
[[ -s "$partial" ]] || fail "empty dump"
"${compose_cmd[@]}" exec -T db pg_restore --list - <"$partial" >/dev/null
mv -f -- "$partial" "$target"
trap - EXIT
chmod 600 "$target"
echo "pilot backup ready: $target"
