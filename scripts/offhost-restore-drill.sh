#!/usr/bin/env bash
set -euo pipefail
set +x
umask 077
# FR-OPS-05 / AC-OPS-04, AC-OPS-05. Input is a privately retrieved off-host dump.
# This wrapper proves the logical restore only; the runbook retains the application/marker acceptance gate.
[[ $# -eq 2 ]] || { echo 'Usage: offhost-restore-drill.sh review|pilot <retrieved-dump>' >&2; exit 1; }
stack=$1
source_dump=$2
[[ -f "$source_dump" && ! -L "$source_dump" && -s "$source_dump" ]] || { echo 'Drill refused: private nonempty regular dump required.' >&2; exit 1; }
case "$stack" in
  review) prefix=hub-review-; helper=scripts/restore-homedev-review-drill.sh ;;
  pilot) echo 'Drill refused: pilot off-host transfer has no recorded approval.' >&2; exit 1 ;;
  *) echo 'Drill refused: unknown stack.' >&2; exit 1 ;;
esac
[[ "$(basename "$source_dump")" =~ ^${prefix}[0-9]{8}T[0-9]{6}[0-9]{0,9}Z\.dump$ && "$source_dump" != *pilot* ]] || { echo 'Drill refused: dump stack does not match review.' >&2; exit 1; }
[[ -f "$helper" ]] || { echo 'Run from the corresponding prepared review release.' >&2; exit 1; }
scratch=$(mktemp -d)
cleanup() { rm -f "$scratch/$(basename "$source_dump")"; rmdir "$scratch"; }
trap cleanup EXIT
start=$(date +%s)
cp "$source_dump" "$scratch/$(basename "$source_dump")"
chmod 600 "$scratch/$(basename "$source_dump")"
bash "$helper" "$scratch/$(basename "$source_dump")"
echo "PASS isolated logical restore: elapsed=$(( $(date +%s) - start ))s; application health, markers, search/export and recovery-window evidence remain operator checks."
