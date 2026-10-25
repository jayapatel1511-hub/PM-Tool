#!/usr/bin/env bash
set -euo pipefail
umask 077

# Restore a private review dump into a new temporary database in the review
# PostgreSQL container. Never replace hub_review or any production database.
# The guards match restore-homedev-pilot-drill.sh. The dump may sit outside
# data/backups: offhost-restore-drill.sh stages it in a private scratch directory.
fail() { echo "review restore drill refused: $*" >&2; exit 1; }
[[ $# -eq 1 && -f "$1" && ! -L "$1" && -s "$1" ]] || fail 'pass one existing nonempty review dump path (not a symlink)'
source_dump=$1
[[ "$(basename "$source_dump")" =~ ^hub-review-[0-9]{8}T[0-9]{6}[0-9]{0,9}Z\.dump$ ]] || fail 'dump name is not a review dump'
[[ "$source_dump" != *pilot* ]] || fail 'pilot dump path'
owner="$(stat -c '%u' "$source_dump" 2>/dev/null || stat -f '%u' "$source_dump")"
mode="$(stat -c '%a' "$source_dump" 2>/dev/null || stat -f '%Lp' "$source_dump")"
[[ "$owner" == "$(id -u)" && "$mode" == 600 ]] || fail 'dump is not owner-only (600)'
[[ -f hosting/homedev.compose.yml && -f .runtime/review.env ]] || fail 'run from a prepared homedev PM-Tool release'

inspect() { sudo docker inspect --format "$1" pm-tool-review-db-1 </dev/null; }
project="$(inspect '{{ index .Config.Labels "com.docker.compose.project" }}')"
service="$(inspect '{{ index .Config.Labels "com.docker.compose.service" }}')"
volume="$(inspect '{{ range .Mounts }}{{ if eq .Destination "/var/lib/postgresql/data" }}{{ .Name }}{{ end }}{{ end }}')"
[[ "$project" == pm-tool-review && "$service" == db && "$volume" == pm-tool-review-db ]] || fail 'review database identity is not exact'

drill_db="hub_review_drill_$(date -u +%Y%m%d%H%M%S)_$$"
compose=(sudo docker compose --env-file .runtime/review.env -f hosting/homedev.compose.yml)
created=0
cleanup() {
  if [[ "$created" == 1 ]]; then
    "${compose[@]}" exec -T db dropdb -U hub_review --if-exists "$drill_db" </dev/null >/dev/null
  fi
}
trap cleanup EXIT
"${compose[@]}" exec -T db createdb -U hub_review "$drill_db" </dev/null
created=1
"${compose[@]}" exec -T db pg_restore -U hub_review --exit-on-error --no-owner --no-privileges -d "$drill_db" < "$source_dump"
project_count=$("${compose[@]}" exec -T db psql -U hub_review -d "$drill_db" -Atc 'SELECT count(*) FROM hub.project' </dev/null)
[[ "$project_count" =~ ^[0-9]+$ ]] || { echo 'Restored project count was invalid.' >&2; exit 1; }
echo "Restore drill passed in isolated $drill_db: $project_count project records restored; temporary database will be removed."
