#!/usr/bin/env bash
set -euo pipefail

# Restore a private review dump into a new temporary database in the review
# PostgreSQL container. Never replace hub_review or any production database.
[[ $# -eq 1 && -f "$1" && -s "$1" ]] || {
  echo 'Pass one existing nonempty review dump path.' >&2
  exit 1
}
[[ -f hosting/homedev.compose.yml && -f .runtime/review.env ]] || {
  echo 'Run from a prepared homedev PM-Tool release.' >&2
  exit 1
}
source_dump=$1
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
