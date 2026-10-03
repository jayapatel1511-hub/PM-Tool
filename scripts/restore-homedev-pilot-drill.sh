#!/usr/bin/env bash
set -Eeuo pipefail
umask 077

fail() { echo "pilot restore drill refused: $*" >&2; exit 1; }
[[ $# -eq 1 && -f "$1" && ! -L "$1" && -s "$1" ]] || fail "provide one existing pilot dump"
dump_dir="$(cd "$(dirname "$1")" && pwd -P)"
dump="$dump_dir/$(basename "$1")"
[[ "$(basename "$dump")" =~ ^hub-pilot-[0-9]{8}T[0-9]{6}[0-9]{6,9}Z\.dump$ ]] || fail "dump name is not a pilot dump"
[[ "$dump" != *review* && "$dump" != *hub_review* ]] || fail "review dump path"
base_dir="$(pwd -P)"
compose="$base_dir/hosting/homedev-pilot.compose.yml"
env_file="$base_dir/.runtime/pilot.env"
[[ -f "$compose" && -f "$env_file" ]] || fail "run from a pilot release"
if [[ "$(basename "$(dirname "$base_dir")")" == releases ]]; then
  pilot_base="$(cd "$base_dir/../.." && pwd -P)"
else
  pilot_base="$base_dir"
fi
canonical_runtime="$(cd "$base_dir/.runtime" 2>/dev/null && pwd -P)" || fail "pilot runtime missing"
canonical_keys="$(cd "$base_dir/.runtime/keys" 2>/dev/null && pwd -P)" || fail "pilot keys missing"
canonical_data="$(cd "$base_dir/data" 2>/dev/null && pwd -P)" || fail "pilot data missing"
[[ "$canonical_runtime" == "$pilot_base/.runtime" && "$canonical_keys" == "$pilot_base/.runtime/keys" && "$canonical_data" == "$pilot_base/data" ]] || fail "pilot runtime or data escapes base"
backup_dir="$(cd "$base_dir/data/backups" 2>/dev/null && pwd -P)" || fail "pilot backup directory missing"
[[ "$dump_dir" == "$backup_dir" ]] || fail "dump is outside the pilot backup directory"
for path in "$canonical_runtime" "$canonical_keys" "$canonical_data" "$backup_dir"; do
  owner="$(stat -c '%u' "$path" 2>/dev/null || stat -f '%u' "$path")"
  mode="$(stat -c '%a' "$path" 2>/dev/null || stat -f '%Lp' "$path")"
  [[ "$owner" == "$(id -u)" && ( "$mode" == 700 || "$mode" == 600 ) ]] || fail "pilot path is not private: $path"
done
for path in "$canonical_runtime/pilot.env" "$canonical_runtime/pilot-users.json" "$dump"; do
  [[ -f "$path" && ! -L "$path" ]] || fail "pilot private file missing or symlinked: $path"
  owner="$(stat -c '%u' "$path" 2>/dev/null || stat -f '%u' "$path")"
  mode="$(stat -c '%a' "$path" 2>/dev/null || stat -f '%Lp' "$path")"
  [[ "$owner" == "$(id -u)" && "$mode" == 600 ]] || fail "pilot private file is not owner-only: $path"
done
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
"${compose_cmd[@]}" exec -T db pg_restore -U hub_pilot -d "$drill" --exit-on-error --no-owner --no-privileges <"$dump"
count="$("${compose_cmd[@]}" exec -T db psql -U hub_pilot -d "$drill" -Atc 'SELECT count(*) FROM hub.project' </dev/null | tr -d '[:space:]')"
[[ "$count" =~ ^[0-9]+$ ]] || fail "drill count was not numeric"
echo "pilot restore drill passed: $drill projects=$count"
