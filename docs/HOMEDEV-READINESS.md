# Homedev readiness for PM-Tool

Status checked 2026-09-27. This is a preparation snapshot, not a deployment record.

## Prepared on homedev

- Host: `homedev` (`jaypatel04`, Linux x86_64), reachable by SSH from the Mac.
- Isolated project directory: `/home/jaypatel04/Workspace/Projects/pm-tool`.
- `releases/`, `incoming/`, and `hosting/` exist with mode `750`; `.runtime/` and `data/` exist with mode `700`. All are owned by `jaypatel04`.
- About 366 GiB of disk and 5.3 GiB of memory were available at the check. Existing Dashboard, Budget, and Guide user services were active.
- Loopback ports `3080` and `20249` had no listeners at the check. They are candidates only, not reserved ports.

## Not yet configured

- No PM-Tool service, Cloudflare tunnel, or DNS record for `pm.engcalchub.com` was found. No application was uploaded or started.
- No `dotnet` runtime or PostgreSQL client was found on homedev. The user cannot access the Docker daemon without additional privilege. Choose the runtime and database after the maintained application source and deployment model are settled.
- The tracked repository currently contains the specification. The untracked `src/`, `web/`, and `tests/` paths contain build output and dependencies but no maintained application source or build manifest. There is no reproducible release to stage yet.

## Decisions before deployment

1. Confirm whether homedev is the final host for `pm.engcalchub.com` or a staging host before Azure production. The current architecture specification names Azure App Service, Azure Database for PostgreSQL, Key Vault, and Entra ID.
2. Establish a reproducible application build, health endpoint, environment configuration, and database migration/backup/restore procedure. Keep secrets and application data outside versioned releases.
3. Resolve authentication and company data approval before placing real internal project records on this personal server. The specification requires Entra authentication and treats project data as internal-confidential.
4. Once a local origin is healthy and access controls are verified, configure a dedicated PM-Tool Cloudflare tunnel and DNS route for `pm.engcalchub.com`; use an explicit tunnel config so other apps remain isolated.

The server guide at `/Users/jaypatel/Personal Server/AGENT-GUIDE.md` governs homedev operations and backups. Its Workspace backup scope includes the project directory, but a PM-Tool data restore has not been tested.
