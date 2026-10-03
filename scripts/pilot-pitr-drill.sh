#!/usr/bin/env bash
set -Eeuo pipefail
umask 077

fail() { echo "pilot PITR drill refused: $*" >&2; exit 1; }
base_dir="$(pwd -P)"
compose="$base_dir/hosting/homedev-pilot.compose.yml"
env_file="$base_dir/.runtime/pilot.env"
base_helper="$base_dir/scripts/pilot-base-backup.sh"
[[ -f "$compose" && -f "$env_file" && -f "$base_helper" ]] || fail "run from a pilot release"
grep -Eiq 'hub_review|pm-tool-review|review\.env|review-users' "$env_file" && fail "review settings in pilot.env"
inspect() { sudo docker inspect --format "$1" pm-tool-pilot-db-1 </dev/null; }
[[ "$(inspect '{{ index .Config.Labels "com.docker.compose.project" }}')" == pm-tool-pilot &&
   "$(inspect '{{ index .Config.Labels "com.docker.compose.service" }}')" == db &&
   "$(inspect '{{ range .Mounts }}{{ if eq .Destination "/var/lib/postgresql/data" }}{{ .Name }}{{ end }}{{ end }}')" == pm-tool-pilot-db ]] || fail "pilot database identity is not exact"
compose_cmd=(sudo env RELEASE_SHA="${RELEASE_SHA:-unknown}" docker compose --env-file "$env_file" -f "$compose")
token="$(python3 -c 'import uuid; print(uuid.uuid4().hex)')"
db_name="hub_pilot_pitr_$token"; container="pm-tool-pilot-pitr-$token"
backup_id="$(date -u +%Y%m%dT%H%M%S%NZ)"
cleanup() {
  if [[ "$(sudo docker inspect --format '{{ index .Config.Labels "pm-tool.pitr-drill" }}' "$container" </dev/null 2>/dev/null || true)" == "$token" ]]; then
    sudo docker rm -f "$container" </dev/null >/dev/null 2>&1 || true
  fi
  "${compose_cmd[@]}" exec -T db dropdb -U hub_pilot --if-exists "$db_name" </dev/null >/dev/null 2>&1 || true
}
trap cleanup EXIT
"${compose_cmd[@]}" exec -T db createdb -U hub_pilot "$db_name" </dev/null
"${compose_cmd[@]}" exec -T db psql -U hub_pilot -d "$db_name" -v ON_ERROR_STOP=1 -c 'CREATE TABLE pitr_marker (id text PRIMARY KEY)' </dev/null
PILOT_BASE_BACKUP_ID="$backup_id" bash "$base_helper"
# Both markers are post-backup WAL changes: restoration must actually replay WAL.
"${compose_cmd[@]}" exec -T db psql -U hub_pilot -d "$db_name" -v ON_ERROR_STOP=1 -c "INSERT INTO pitr_marker VALUES ('before')" </dev/null
target="$("${compose_cmd[@]}" exec -T db psql -U hub_pilot -d "$db_name" -Atqc 'SELECT clock_timestamp()' </dev/null | tr -d '\r')"
[[ "$target" =~ ^[0-9]{4}-[0-9]{2}-[0-9]{2} ]] || fail "recovery timestamp missing"
"${compose_cmd[@]}" exec -T db psql -U hub_pilot -d "$db_name" -v ON_ERROR_STOP=1 -c "INSERT INTO pitr_marker VALUES ('after')" </dev/null
wal="$("${compose_cmd[@]}" exec -T db psql -U hub_pilot -d hub_pilot -Atqc 'SELECT pg_walfile_name(pg_current_wal_lsn())' </dev/null | tr -d '\r')"
[[ "$wal" =~ ^[0-9A-F]{24}$ ]] || fail "unsafe WAL segment name"
"${compose_cmd[@]}" exec -T db psql -U hub_pilot -d hub_pilot -Atqc 'SELECT pg_switch_wal()' </dev/null >/dev/null
for _ in {1..60}; do
  "${compose_cmd[@]}" exec -T db test -f "/var/lib/postgresql/wal-archive/$wal" </dev/null && break
  sleep 1
done
if ! "${compose_cmd[@]}" exec -T db test -f "/var/lib/postgresql/wal-archive/$wal" </dev/null; then
  printf "Expected archive segment: %q\n" "$wal" >&2
  "${compose_cmd[@]}" exec -T db ls -la /var/lib/postgresql/wal-archive </dev/null >&2
  "${compose_cmd[@]}" exec -T db psql -U hub_pilot -d hub_pilot -c 'SELECT archived_count,last_archived_wal,last_archived_time,failed_count,last_failed_wal FROM pg_stat_archiver' </dev/null >&2
  fail "post-marker WAL was not archived"
fi
restore_script="$(cat <<'RESTORE_SH'
set -eu
base=$1; database=$2; target=$3
test -d "$base" && test -d /var/lib/postgresql/wal-archive
mkdir -m 700 /tmp/pitr-restore /tmp/pitr-socket
cp -a "$base"/. /tmp/pitr-restore/
printf "restore_command = 'cp /var/lib/postgresql/wal-archive/%%f %%p'\nrecovery_target_time = '%s'\nrecovery_target_action = 'pause'\n" "$target" >> /tmp/pitr-restore/postgresql.conf
touch /tmp/pitr-restore/recovery.signal
trap 'pg_ctl -D /tmp/pitr-restore -m fast stop >/dev/null 2>&1 || true' EXIT
pg_ctl -D /tmp/pitr-restore -o "-p 55432 -k /tmp/pitr-socket -c listen_addresses='' -c archive_mode=off" -w start
for attempt in $(seq 1 60); do
  paused=$(psql -U hub_pilot -h /tmp/pitr-socket -p 55432 -d "$database" -Atqc 'SELECT pg_is_wal_replay_paused()')
  test "$paused" = t && break
  sleep 1
done
test "$paused" = t
counts=$(psql -U hub_pilot -h /tmp/pitr-socket -p 55432 -d "$database" -Atqc "SELECT (SELECT count(*) FROM pitr_marker WHERE id='before') || ':' || (SELECT count(*) FROM pitr_marker WHERE id='after')")
test "$counts" = 1:0
echo 'pilot PITR drill passed: paused=true before=1 after=0'
RESTORE_SH
)"
# The recovery container sees only read-only backups/WAL; never the source database volume.
timeout 180 sudo docker run --rm --user postgres --name "$container" --label "pm-tool.pitr-drill=$token" --pull never --network none \
  --mount type=volume,src=pm-tool-pilot-base-backups,dst=/var/lib/postgresql/base-backups,readonly \
  --mount type=volume,src=pm-tool-pilot-wal-archive,dst=/var/lib/postgresql/wal-archive,readonly \
  --entrypoint sh postgres:17-alpine -ceu "$restore_script" sh "/var/lib/postgresql/base-backups/base-$backup_id" "$db_name" "$target" </dev/null
