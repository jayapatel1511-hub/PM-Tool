#!/bin/sh
set -eu

source_path=${1:?missing WAL path}
wal_name=${2:?missing WAL name}
archive_dir=/var/lib/postgresql/wal-archive

# PostgreSQL also archives backup history and timeline history files.
case "$wal_name" in *[!A-Za-z0-9.]*|''|.*) echo "invalid WAL name" >&2; exit 1 ;; esac
[ "${#wal_name}" -le 64 ] || { echo "invalid WAL name" >&2; exit 1; }
case "$source_path" in
  "pg_wal/$wal_name") source="${PGDATA:-/var/lib/postgresql/data}/$source_path" ;;
  *) echo "invalid WAL path" >&2; exit 1 ;;
esac
[ -f "$source" ] || { echo "WAL source is not a regular file" >&2; exit 1; }
[ -d "$archive_dir" ] || { echo "WAL archive directory is missing" >&2; exit 1; }

target="$archive_dir/$wal_name"
if [ -e "$target" ]; then
  cmp -s "$source" "$target" || { echo "existing WAL differs" >&2; exit 1; }
  sync -f "$target"
  sync -f "$archive_dir"
  exit 0
fi

tmp="$archive_dir/.${wal_name}.$$"
trap 'rm -f "$tmp"' EXIT HUP INT TERM
umask 077
cp -- "$source" "$tmp"
sync -f "$tmp"
if ln -- "$tmp" "$target" 2>/dev/null; then
  rm -f -- "$tmp"
else
  [ -e "$target" ] || { echo "WAL publish failed" >&2; exit 1; }
  cmp -s "$source" "$target" || { echo "WAL publish race differs" >&2; exit 1; }
  rm -f -- "$tmp"
fi
sync -f "$archive_dir"
trap - EXIT HUP INT TERM
