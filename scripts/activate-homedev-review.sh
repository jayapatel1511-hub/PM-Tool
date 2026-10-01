#!/usr/bin/env bash
set -euo pipefail
umask 077

# Activate one prepared, reviewed release on homedev. Run as jaypatel04 from
# releases/<full-sha>; sudo prompts once in your own terminal for Docker.
#   bash scripts/activate-homedev-review.sh <full-sha> [--install-timer]
# Order: build image, dump current database, start new API, probe the private
# origin. The database volume is reused, never recreated.
expected=${1:?Pass the reviewed full commit SHA}
revision=$(basename "$PWD")
[[ "$revision" == "$expected" && "$revision" =~ ^[0-9a-f]{40}$ ]] || {
  echo "Run from releases/$expected (current directory is $revision)." >&2; exit 1; }
[[ -L .runtime && -L data && -f .runtime/review.env ]] || { echo 'Shared .runtime/data links missing.' >&2; exit 1; }
compose=(sudo env RELEASE_SHA="$revision" docker compose --env-file .runtime/review.env -f hosting/homedev.compose.yml)
log() { printf '\n== %s %s\n' "$(date -u +%H:%M:%SZ)" "$*"; }

sudo -v
previous=$(sudo docker inspect --format '{{ .Config.Image }}' pm-tool-review-api-1 2>/dev/null || echo none)
log "Previous API image: $previous"
log "Building pm-tool-review:$revision"
"${compose[@]}" build api

log 'Dumping current review database before activation'
bash scripts/backup-homedev-review.sh

log 'Starting new API image (database volume reused)'
"${compose[@]}" up -d --no-build
volume=$(sudo docker inspect --format '{{ range .Mounts }}{{ if eq .Destination "/var/lib/postgresql/data" }}{{ .Name }}{{ end }}{{ end }}' pm-tool-review-db-1)
[[ "$volume" == pm-tool-review-db ]] || { echo "Unexpected DB volume: $volume" >&2; exit 1; }
for _ in $(seq 60); do
  curl -fsS -H "Host: pm.engcalchub.com" http://127.0.0.1:3080/health >/dev/null 2>&1 && break
  sleep 2
done
running=$(sudo docker inspect --format '{{ .Config.Image }}' pm-tool-review-api-1)
log "Running API image: $running (volume $volume)"
sudo docker logs --since 5m pm-tool-review-api-1 2>&1 | grep -iE 'migrat|fail|error|exception' | grep -viE 'password|secret|token' | tail -20 || true

log 'Private origin probes'
python3 scripts/verify-homedev-review.py || {
  echo "Probes failed. Roll back: cd ../${previous#pm-tool-review:} && sudo env RELEASE_SHA=${previous#pm-tool-review:} docker compose --env-file .runtime/review.env -f hosting/homedev.compose.yml up -d --no-build" >&2
  exit 1; }

if [[ "${2:-}" == --install-timer ]]; then
  log 'Installing root-owned daily backup helper and timer'
  sudo install -D -o root -g root -m 0755 hosting/pm-tool-review-backup-root.sh /usr/local/libexec/pm-tool-review-backup
  sudo install -o root -g root -m 0644 hosting/pm-tool-review-backup.service /etc/systemd/system/pm-tool-review-backup.service
  sudo install -o root -g root -m 0644 hosting/pm-tool-review-backup.timer /etc/systemd/system/pm-tool-review-backup.timer
  sudo systemctl daemon-reload
  sudo systemd-analyze verify /etc/systemd/system/pm-tool-review-backup.service /etc/systemd/system/pm-tool-review-backup.timer
  sudo systemctl start pm-tool-review-backup.service
  sudo systemctl enable --now pm-tool-review-backup.timer
  systemctl list-timers pm-tool-review-backup.timer --all --no-pager
  latest=$(ls -t data/backups/hub-review-*.dump | head -1)
  log "Restore drill on timer-produced dump $latest"
  bash scripts/restore-homedev-review-drill.sh "$latest"
fi

ln -sfn "releases/$revision" ../../current.new && mv -T ../../current.new ../../current
log "Activated $revision (current symlink updated). Previous image: $previous"
