#!/usr/bin/env bash
set -euo pipefail
umask 077

# Run interactively on homedev from the PM-Tool repository root. The scheduled
# root service uses its separately installed, root-owned helper.
[[ -f hosting/homedev.compose.yml && -f .runtime/review.env ]] || {
  echo 'Run from the homedev PM-Tool directory after review bootstrap.' >&2
  exit 1
}
# Same identity guard as the root helper: dump only the review stack's own database volume.
inspect() { sudo docker inspect --format "$1" pm-tool-review-db-1 </dev/null; }
project="$(inspect '{{ index .Config.Labels "com.docker.compose.project" }}')"
service="$(inspect '{{ index .Config.Labels "com.docker.compose.service" }}')"
volume="$(inspect '{{ range .Mounts }}{{ if eq .Destination "/var/lib/postgresql/data" }}{{ .Name }}{{ end }}{{ end }}')"
[[ "$project" == pm-tool-review && "$service" == db && "$volume" == pm-tool-review-db ]] || {
  echo 'Refusing to back up an unexpected database container or volume.' >&2
  exit 1
}
mkdir -p data/backups
stamp=$(date -u +%Y%m%dT%H%M%S%NZ)
target="data/backups/hub-review-${stamp}.dump"
temp=$(mktemp "${target}.partial.XXXXXX")
trap 'rm -f "$temp"' EXIT
sudo docker compose --env-file .runtime/review.env -f hosting/homedev.compose.yml exec -T db \
  pg_dump -U hub_review -d hub_review -Fc --no-owner --no-privileges </dev/null > "$temp"
[[ -s "$temp" ]] || { echo 'Review database dump is empty.' >&2; exit 1; }
sudo docker compose --env-file .runtime/review.env -f hosting/homedev.compose.yml exec -T db \
  pg_restore --list < "$temp" > /dev/null
mv "$temp" "$target"
trap - EXIT
echo "Verified private review dump: $target"
