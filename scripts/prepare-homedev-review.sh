#!/usr/bin/env bash
set -euo pipefail

# Transfer one committed source tree; never copy local databases, secrets or
# unrelated worktree files. This prepares a release but does not start it.
revision=$(git rev-parse --verify "${1:-HEAD}^{commit}")
[[ "$revision" =~ ^[0-9a-f]{40}$ ]] || { echo 'Expected a full commit SHA' >&2; exit 1; }
base=/home/jaypatel04/Workspace/Projects/pm-tool
release="$base/releases/$revision"
if ssh -o BatchMode=yes homedev "test -e '$release'"; then
  echo "Release already prepared: $revision"
  exit 0
fi
stage=$(ssh -o BatchMode=yes homedev "mktemp -d '$base/incoming/$revision.XXXXXX'")
echo "Preparing committed source $revision on homedev"
git archive "$revision" | ssh -o BatchMode=yes homedev "tar -x -C '$stage'"
ssh -o BatchMode=yes homedev "ln -s ../../.runtime '$stage/.runtime' && ln -s ../../data '$stage/data' && mv '$stage' '$release'"
echo "Prepared release: $release"
