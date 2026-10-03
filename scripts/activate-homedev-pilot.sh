#!/usr/bin/env bash
set -Eeuo pipefail
# Never inherit shell tracing while handling private database input.
set +x
umask 077

fail() { echo "pilot activation refused: $*" >&2; exit 1; }
[[ $# -ge 1 ]] || fail "release SHA is required"
release_sha="$1"; shift
[[ "$release_sha" =~ ^[0-9a-f]{40}$ ]] || fail "release SHA must be 40 lowercase hex characters"
install_timer=0; verify_file=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    --install-timer) install_timer=1 ;;
    --verify-file) shift; [[ $# -gt 0 ]] || fail "missing verifier file"; verify_file="$1" ;;
    --verify-file=*) verify_file="${1#*=}" ;;
    *) fail "unknown option $1" ;;
  esac
  shift
done

base="/home/jaypatel04/Workspace/Projects/pm-tool-pilot"
expected="$base/releases/$release_sha"
[[ "$(pwd -P)" == "$expected" ]] || fail "run from the requested pilot release"
compose="$expected/hosting/homedev-pilot.compose.yml"
runtime="$expected/.runtime"; env_file="$runtime/pilot.env"; app_file="$runtime/pilot-app.env"
role_sql="$expected/hosting/pilot-runtime-role.sql"; users_file="$runtime/pilot-users.json"; keys="$runtime/keys"
[[ -f "$compose" && -f "$env_file" && -f "$app_file" && -f "$role_sql" && -f "$users_file" && -d "$keys" ]] || fail "pilot runtime is incomplete; legacy runtime needs explicit --add-app-credential"
[[ ! -L "$env_file" && ! -L "$app_file" && ! -L "$users_file" ]] || fail "pilot credential files must not be symlinks"
canonical() { cd "$1" && pwd -P; }
canonical_runtime="$(canonical "$runtime")"; canonical_keys="$(canonical "$keys")"; canonical_data="$(canonical "$expected/data")"
for value in "$canonical_runtime" "$canonical_keys" "$canonical_data"; do
  [[ "$value" == "$base"/* && "$value" != *review* && "$value" != *hub_review* && "$value" != *pm-tool-review* ]] || fail "runtime or data escapes pilot base"
done
grep -Eiq 'hub_review|pm-tool-review|review\.env|review-users' "$env_file" && fail "review settings in pilot.env"
grep -Eiq 'hub_review|pm-tool-review|review\.env|review-users' "$users_file" && fail "review settings in pilot-users.json"
grep -q '^PILOT_HOSTNAME=pm\.engcalchub\.com$' "$env_file" || fail "unexpected pilot hostname"

owner_mode() {
  local path="$1" owner mode
  owner="$(stat -c '%u' "$path" 2>/dev/null || stat -f '%u' "$path")"
  mode="$(stat -c '%a' "$path" 2>/dev/null || stat -f '%Lp' "$path")"
  [[ "$owner" == "$(id -u)" ]] || fail "pilot path is not user-owned: $path"
  [[ "$mode" == 700 || "$mode" == 600 ]] || fail "pilot path is not private: $path"
}
for path in "$canonical_runtime" "$canonical_keys" "$canonical_data" "$canonical_data/backups"; do [[ -e "$path" ]] && owner_mode "$path"; done
owner_mode "$env_file"; owner_mode "$app_file"; owner_mode "$users_file"
for path in "$env_file" "$app_file" "$users_file"; do
  [[ "$(stat -c '%a' "$path" 2>/dev/null || stat -f '%Lp' "$path")" == 600 ]] || fail "pilot secret file must be mode 600"
done
# Validate the API whitelist and legacy administrator connection before any Docker mutation.
python3 - "$app_file" "$env_file" <<'PREFLIGHT_PY'
import pathlib, re, sys
app = pathlib.Path(sys.argv[1]).read_text()
match = re.fullmatch(r'ConnectionStrings__Hub=Host=db;Port=5432;Database=hub_pilot;Username=hub_pilot_app;Password=([A-Za-z0-9_-]{32,128})\n', app)
admin = dict(line.split('=', 1) for line in pathlib.Path(sys.argv[2]).read_text().splitlines() if '=' in line)
if not match or not re.fullmatch(r'Host=db;Port=5432;Database=hub_pilot;Username=hub_pilot;Password=[A-Za-z0-9_-]{32,128}', admin.get('ConnectionStrings__Hub', '')):
    sys.exit('pilot activation refused: database credential separation is incomplete')
if match.group(1) == admin.get('PILOT_DB_PASSWORD'):
    sys.exit('pilot activation refused: runtime and administrator passwords must differ')
PREFLIGHT_PY
command -v timeout >/dev/null || fail "timeout is required for the one-shot migration step"

sudo -v </dev/null
inspect() { sudo docker inspect --format "$1" pm-tool-pilot-db-1 </dev/null; }
db_exists=1
if ! sudo docker inspect pm-tool-pilot-db-1 </dev/null >/dev/null 2>&1; then db_exists=0; fi
volume_exists=1
if ! sudo docker volume inspect pm-tool-pilot-db </dev/null >/dev/null 2>&1; then volume_exists=0; fi
if [[ "$db_exists" == 1 || "$volume_exists" == 1 ]]; then
  [[ "$db_exists" == 1 && "$volume_exists" == 1 ]] || fail "partial pilot database state"
  project="$(inspect '{{ index .Config.Labels "com.docker.compose.project" }}')"
  service="$(inspect '{{ index .Config.Labels "com.docker.compose.service" }}')"
  [[ "$project" == pm-tool-pilot && "$service" == db ]] || fail "unexpected pilot database identity"
  (cd "$expected" && bash scripts/backup-homedev-pilot.sh)
fi

compose_cmd=(sudo env RELEASE_SHA="$release_sha" docker compose --env-file "$env_file" -f "$compose")
api_started=0; migration_container=""
cleanup_activation() {
  [[ -z "$migration_container" ]] || sudo docker rm -f "$migration_container" </dev/null >/dev/null 2>&1 || true
  if [[ "$api_started" == 1 ]]; then "${compose_cmd[@]}" stop api </dev/null >/dev/null 2>&1 || true; fi
}
trap cleanup_activation EXIT
"${compose_cmd[@]}" build api </dev/null
# Preserve the volume/data. Stop the old API before any privileged migration or grant change.
"${compose_cmd[@]}" stop api </dev/null
"${compose_cmd[@]}" up -d --no-build db </dev/null
for _ in {1..60}; do
  [[ "$(inspect '{{ .State.Health.Status }}' 2>/dev/null || true)" == healthy ]] && break
  sleep 2
done
[[ "$(inspect '{{ .State.Health.Status }}')" == healthy ]] || fail "pilot database did not become healthy"
[[ "$(inspect '{{ range .Mounts }}{{ if eq .Destination "/var/lib/postgresql/data" }}{{ .Name }}{{ end }}{{ end }}')" == pm-tool-pilot-db ]] || fail "pilot volume is not exact"
# Parent's Db__MigrateOnly contract must exit successfully without starting the web/worker host.
# A missing/broken contract times out; ordinary API stays stopped.
migration_container="pm-tool-pilot-migrate-$(python3 -c 'import uuid; print(uuid.uuid4().hex)')"
timeout 180 "${compose_cmd[@]}" run --rm --name "$migration_container" --no-deps --pull never migrate </dev/null || fail "one-shot migration did not complete; API remains stopped"
sudo docker rm -f "$migration_container" </dev/null >/dev/null 2>&1 || true
migration_container=""
python3 - "$app_file" "$role_sql" <<'GRANTS_PY'
import pathlib, re, subprocess, sys
value = pathlib.Path(sys.argv[1]).read_text()
match = re.fullmatch(r'ConnectionStrings__Hub=Host=db;Port=5432;Database=hub_pilot;Username=hub_pilot_app;Password=([A-Za-z0-9_-]{32,128})\n', value)
if not match:
    sys.exit('pilot activation refused: runtime credential whitelist changed')
password = match.group(1)
sql = "\\set app_password '" + password + "'\n" + pathlib.Path(sys.argv[2]).read_text()
# Authentication over TCP proves that the provisioned password works as the app role,
# not as the container's local-trust bootstrap account. No password goes into argv/env.
sql += "\n\\setenv PGPASSWORD '" + password + "'\n"
sql += '\\connect "host=127.0.0.1 port=5432 dbname=hub_pilot user=hub_pilot_app"\n'
sql += "DO $$ BEGIN IF current_user <> 'hub_pilot_app' THEN RAISE EXCEPTION 'Wrong runtime role'; END IF; END $$;\nSELECT 1 FROM hub.activity_log LIMIT 1;\n"
result = subprocess.run(['sudo', 'docker', 'exec', '-i', 'pm-tool-pilot-db-1', 'psql', '-X', '-q',
                         '-v', 'ON_ERROR_STOP=1', '-U', 'hub_pilot', '-d', 'hub_pilot'],
                        input=sql.encode(), stdout=subprocess.DEVNULL, stderr=subprocess.PIPE)
if result.returncode:
    # psql errors can repeat input statements; do not expose private input in terminal/logs.
    sys.exit('pilot activation refused: runtime grants/authentication failed; API remains stopped')
GRANTS_PY
api_started=1
"${compose_cmd[@]}" up -d --no-build --no-deps --force-recreate api </dev/null
if ! python3 - "$app_file" <<'API_ENV_PY'
import json, pathlib, subprocess, sys
result = subprocess.run(['sudo', 'docker', 'inspect', '--format', '{{json .Config.Env}}', 'pm-tool-pilot-api-1'],
                        stdout=subprocess.PIPE, stderr=subprocess.PIPE)
if result.returncode:
    sys.exit(1)
env = dict(item.split('=', 1) for item in json.loads(result.stdout))
expected = pathlib.Path(sys.argv[1]).read_text().strip().split('=', 1)[1]
if (env.get('ConnectionStrings__Hub') != expected or env.get('Db__Migrate') != 'false'
        or env.get('Db__MigrateOnly') != 'false'
        or any(key.startswith('PILOT_') or key.startswith('Auth__Local__Bootstrap') for key in env)):
    sys.exit(1)
API_ENV_PY
then
  "${compose_cmd[@]}" stop api </dev/null
  fail "ordinary API environment is not separated; API stopped"
fi
for _ in {1..60}; do
  health="$(inspect '{{ .State.Health.Status }}' 2>/dev/null || true)"
  api_health="$(curl --max-time 5 -fsS -H 'Host: pm.engcalchub.com' -H 'X-Forwarded-Proto: https' http://127.0.0.1:3081/health 2>/dev/null || true)"
  [[ "$health" == healthy && "$api_health" == *Healthy* ]] && break
  [[ "$health" == unhealthy ]] && fail "pilot database unhealthy"
  sleep 2
done
[[ "$(inspect '{{ .State.Health.Status }}')" == healthy ]] || fail "pilot database did not become healthy"
curl --max-time 5 -fsS -H 'Host: pm.engcalchub.com' -H 'X-Forwarded-Proto: https' http://127.0.0.1:3081/health | grep -q Healthy || fail "pilot API did not become ready"
api_image="$(sudo docker inspect --format '{{ .Config.Image }}' pm-tool-pilot-api-1 </dev/null)"
[[ "$api_image" == "pm-tool-pilot:$release_sha" ]] || fail "pilot API image is not the requested release: $api_image"
volume="$(inspect '{{ range .Mounts }}{{ if eq .Name "pm-tool-pilot-db" }}{{ .Name }}{{ end }}{{ end }}')"
[[ "$volume" == pm-tool-pilot-db ]] || fail "pilot volume is not exact"
seed_counts="$("${compose_cmd[@]}" exec -T db psql -U hub_pilot -d hub_pilot -Atc "SELECT (SELECT count(*) FROM hub.app_user WHERE email LIKE '%@hub.test') || ':' || (SELECT count(*) FROM hub.project WHERE external_source = 'ReviewDemo')" </dev/null | tr -d '[:space:]')"
[[ "$seed_counts" == 0:0 ]] || fail "review seed records found: $seed_counts"

verify=(python3 "$expected/scripts/verify-homedev-pilot.py" http://127.0.0.1:3081)
[[ -n "$verify_file" ]] && verify+=(--credentials-file "$verify_file")
"${verify[@]}"

if [[ "$install_timer" == 1 ]]; then
  sudo install -D -o root -g root -m 755 "$expected/hosting/pm-tool-pilot-backup-root.sh" /usr/local/libexec/pm-tool-pilot-backup
  sudo install -o root -g root -m 644 "$expected/hosting/pm-tool-pilot-backup.service" /etc/systemd/system/pm-tool-pilot-backup.service
  sudo install -o root -g root -m 644 "$expected/hosting/pm-tool-pilot-backup.timer" /etc/systemd/system/pm-tool-pilot-backup.timer
  sudo systemctl daemon-reload
  sudo systemd-analyze verify pm-tool-pilot-backup.service pm-tool-pilot-backup.timer
  sudo systemctl start pm-tool-pilot-backup.service
  sudo systemctl enable --now pm-tool-pilot-backup.timer
  latest="$(find "$expected/data/backups" -maxdepth 1 -type f -name 'hub-pilot-*.dump' -print | sort | tail -n 1)"
  [[ -n "$latest" ]] || fail "installed backup helper produced no pilot dump"
  (cd "$expected" && bash scripts/restore-homedev-pilot-drill.sh "$latest")
fi

ln -sfn "releases/$release_sha" "$base/current.new"
mv -Tf "$base/current.new" "$base/current"
api_started=0
trap - EXIT
echo "pilot activated: $release_sha; private verification gates passed"
