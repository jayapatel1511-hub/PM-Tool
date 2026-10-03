#!/usr/bin/env bash
set -Eeuo pipefail
umask 077

fail() { echo "pilot base backup refused: $*" >&2; exit 1; }
base_dir="$(pwd -P)"
compose="$base_dir/hosting/homedev-pilot.compose.yml"
env_file="$base_dir/.runtime/pilot.env"
[[ -f "$compose" && -f "$env_file" ]] || fail "run from a pilot release with pilot.env and pilot compose"
grep -Eiq 'hub_review|pm-tool-review|review\.env|review-users' "$env_file" && fail "review settings in pilot.env"
grep -q '^PILOT_HOSTNAME=pm\.engcalchub\.com$' "$env_file" || fail "unexpected pilot hostname"

inspect() { sudo docker inspect --format "$1" pm-tool-pilot-db-1 </dev/null; }
project="$(inspect '{{ index .Config.Labels "com.docker.compose.project" }}')"
service="$(inspect '{{ index .Config.Labels "com.docker.compose.service" }}')"
volume="$(inspect '{{ range .Mounts }}{{ if eq .Destination "/var/lib/postgresql/data" }}{{ .Name }}{{ end }}{{ end }}')"
[[ "$project" == pm-tool-pilot && "$service" == db && "$volume" == pm-tool-pilot-db ]] || fail "pilot database identity is not exact"

backup_id="${PILOT_BASE_BACKUP_ID:-$(date -u +%Y%m%dT%H%M%S%NZ)}"
[[ "$backup_id" =~ ^[0-9]{8}T[0-9]{6}[0-9]{6,9}Z$ ]] || fail "unsafe base backup id"
target="/var/lib/postgresql/base-backups/base-$backup_id"
compose_cmd=(sudo env RELEASE_SHA="${RELEASE_SHA:-unknown}" docker compose --env-file "$env_file" -f "$compose")
[[ "$("${compose_cmd[@]}" exec -T db psql -U hub_pilot -d hub_pilot -Atqc 'SHOW archive_mode' </dev/null)" == on ]] || fail "WAL archiving is not enabled"
# mkdir reserves this backup id; a failed partial is retained for operator inspection.
"${compose_cmd[@]}" exec -T --user postgres db sh -ceu 'test ! -e "$1"; mkdir -m 700 "$1.partial"' sh "$target" </dev/null
"${compose_cmd[@]}" exec -T --user postgres db pg_basebackup -U hub_pilot -D "$target.partial" -Fp -X stream --checkpoint=fast -P </dev/null
"${compose_cmd[@]}" exec -T --user postgres db pg_verifybackup "$target.partial" </dev/null
"${compose_cmd[@]}" exec -T --user postgres db sh -ceu 'test ! -e "$1"; mv "$1.partial" "$1"' sh "$target" </dev/null
echo "pilot base backup ready: $backup_id"
