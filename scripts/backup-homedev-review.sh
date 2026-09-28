#!/usr/bin/env bash
set -euo pipefail
umask 077

# Run interactively on homedev from the PM-Tool repository root. The scheduled
# root service uses its separately installed, root-owned helper.
[[ -f hosting/homedev.compose.yml && -f .runtime/review.env ]] || {
  echo 'Run from the homedev PM-Tool directory after review bootstrap.' >&2
  exit 1
}
mkdir -p data/backups
stamp=$(date -u +%Y%m%dT%H%M%S%NZ)
target="data/backups/hub-review-${stamp}.dump"
temp=$(mktemp "${target}.partial.XXXXXX")
trap 'rm -f "$temp"' EXIT
sudo docker compose --env-file .runtime/review.env -f hosting/homedev.compose.yml exec -T db \
  pg_dump -U hub_review -d hub_review -Fc --no-owner --no-privileges > "$temp"
[[ -s "$temp" ]] || { echo 'Review database dump is empty.' >&2; exit 1; }
sudo docker compose --env-file .runtime/review.env -f hosting/homedev.compose.yml exec -T db \
  pg_restore --list < "$temp" > /dev/null
mv "$temp" "$target"
trap - EXIT
echo "Verified private review dump: $target"
