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
# Hold directory and file descriptors throughout. Never reopen a user-writable
# pathname as root: a renamed directory or replaced partial file cannot redirect
# output, validation or ownership changes to another file.
/usr/bin/python3 - "$backup_dir" "$container" hub_pilot hub-pilot- <<'BACKUP_PY'
import datetime, os, secrets, stat, subprocess, sys
path, container, database, prefix = sys.argv[1:]
flags = os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW
folder = os.open("/", flags)
try:
    for part in path.strip("/").split("/"):
        child = os.open(part, flags, dir_fd=folder)
        os.close(folder)
        folder = child
    owner = os.fstat(folder)
    if owner.st_uid == 0 or stat.S_IMODE(owner.st_mode) != 0o700:
        raise RuntimeError("Backup directory must be non-root-owned and mode 700")
    stamp = datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
    target = prefix + stamp + ".dump"
    partial = target + ".partial." + secrets.token_hex(12)
    fd = os.open(partial, os.O_RDWR | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600, dir_fd=folder)
    try:
        subprocess.run(["/usr/bin/docker", "exec", container, "pg_dump", "-U", database, "-d", database,
                        "-Fc", "--no-owner", "--no-privileges"], stdout=fd, check=True)
        if os.fstat(fd).st_size == 0:
            raise RuntimeError("Database dump is empty")
        os.lseek(fd, 0, os.SEEK_SET)
        subprocess.run(["/usr/bin/docker", "exec", "-i", container, "pg_restore", "--list"],
                       stdin=fd, stdout=subprocess.DEVNULL, check=True)
        # Do not publish if a user replaced the directory entry while the dump ran.
        entry = os.stat(partial, dir_fd=folder, follow_symlinks=False)
        actual = os.fstat(fd)
        if (entry.st_dev, entry.st_ino) != (actual.st_dev, actual.st_ino):
            raise RuntimeError("Backup file changed during creation")
        os.fchown(fd, owner.st_uid, owner.st_gid)
        os.fsync(fd)
        os.rename(partial, target, src_dir_fd=folder, dst_dir_fd=folder)
        os.fsync(folder)
        print("Verified private dump: " + path + "/" + target)
    finally:
        os.close(fd)
        try:
            os.unlink(partial, dir_fd=folder)
        except FileNotFoundError:
            pass
finally:
    os.close(folder)
BACKUP_PY

# The same daily timer verifies a physical base as well as the portable logical dump.
[[ "$(/usr/bin/docker exec "$container" psql -U hub_pilot -d hub_pilot -Atqc 'SHOW archive_mode' </dev/null)" == on ]] || {
  echo 'Physical backup requires pilot WAL archiving; logical dump is retained.' >&2
  exit 1
}
for mount in 'pm-tool-pilot-wal-archive:/var/lib/postgresql/wal-archive' 'pm-tool-pilot-base-backups:/var/lib/postgresql/base-backups'; do
  name="${mount%%:*}"; destination="${mount#*:}"
  actual=$(/usr/bin/docker inspect --format "{{ range .Mounts }}{{ if eq .Destination \"$destination\" }}{{ .Name }}{{ end }}{{ end }}" "$container")
  [[ "$actual" == "$name" ]] || { echo 'Unexpected physical backup volume.' >&2; exit 1; }
done
backup_id=$(/usr/bin/python3 -c 'import datetime; print(datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%dT%H%M%S%fZ"))')
/usr/bin/docker exec -i --user postgres "$container" sh -s -- "$backup_id" <<'BASE_BACKUP_SH'
set -eu
umask 077
target="/var/lib/postgresql/base-backups/base-$1"
test ! -e "$target"
mkdir -m 700 "$target.partial"
pg_basebackup -U hub_pilot -D "$target.partial" -Fp -X stream --checkpoint=fast
pg_verifybackup "$target.partial"
mv "$target.partial" "$target"
echo "Verified physical pilot base: $1"
BASE_BACKUP_SH
# ponytail: retain all bases/WAL until off-host retrieval is proved; then add verified-chain retention.
