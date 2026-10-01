#!/usr/bin/env bash
set -euo pipefail
umask 077

# Install this script root-owned under /usr/local/libexec before enabling the
# system timer. Do not execute a user-writable release script as root.
(( EUID == 0 )) || { echo 'This helper requires root.' >&2; exit 1; }
backup_dir=/home/jaypatel04/Workspace/Projects/pm-tool-pilot/data/backups
container=pm-tool-pilot-db-1
project=$(/usr/bin/docker inspect --format '{{ index .Config.Labels "com.docker.compose.project" }}' "$container")
service=$(/usr/bin/docker inspect --format '{{ index .Config.Labels "com.docker.compose.service" }}' "$container")
volume=$(/usr/bin/docker inspect --format '{{ range .Mounts }}{{ if eq .Destination "/var/lib/postgresql/data" }}{{ .Name }}{{ end }}{{ end }}' "$container")
[[ "$project" == pm-tool-pilot && "$service" == db && "$volume" == pm-tool-pilot-db ]] || {
  echo 'Refusing to back up an unexpected database container or volume.' >&2
  exit 1
}
[[ -d "$backup_dir" && ! -L "$backup_dir" ]] || {
  echo 'Private pilot backup directory is missing or a symlink.' >&2
  exit 1
}
owner=$(stat -c '%u:%g' "$backup_dir")
mode=$(stat -c '%a' "$backup_dir")
[[ ${owner%%:*} != 0 && "$mode" == 700 ]] || {
  echo 'Private pilot backup directory must be user-owned and mode 700.' >&2
  exit 1
}
stamp=$(date -u +%Y%m%dT%H%M%S%NZ)
target="$backup_dir/hub-pilot-${stamp}.dump"
temp=$(mktemp "$target.partial.XXXXXX")
trap 'rm -f "$temp"' EXIT
/usr/bin/docker exec "$container" pg_dump -U hub_pilot -d hub_pilot -Fc --no-owner --no-privileges > "$temp"
[[ -s "$temp" ]] || { echo 'Pilot database dump is empty.' >&2; exit 1; }
/usr/bin/docker exec -i "$container" pg_restore --list < "$temp" > /dev/null
chown "$owner" "$temp"
mv -T "$temp" "$target"
trap - EXIT
echo "Verified private pilot dump: $target"
