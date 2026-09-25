# Spec Kit provenance

- Upstream: https://github.com/github/spec-kit
- Version: 1.0.8, release https://github.com/github/spec-kit/releases/tag/v1.0.8
  (CLI distribution `specify-cli==1.0.8`).
- Installed on 2026-09-24 by copying the managed files from the verified 1.0.8 installation in
  Jay's Dashboard repository. That installation was built on 2026-09-19 from the tagged source
  archive `https://codeload.github.com/github/spec-kit/tar.gz/refs/tags/v1.0.8`
  (SHA-256 `473dda96374badb2231a39585705b28a1c87c0ebc658bec15a5e049640f3ce27`).
  Nothing was downloaded, `specify init` was not run, and no global CLI was installed.
- Before use, all 22 managed files were checked against the SHA-256 hashes in
  `integrations/speckit.manifest.json` and `integrations/claude.manifest.json`, and all matched.
  The manifests, the bundled `speckit` workflow and registry, and the upstream `LICENSE` were
  copied unchanged, so the manifests' `installed_at` times record the Dashboard installation.

## Project-owned files

- `init-options.json` and `integration.json`: Claude is the only integration, using skills and
  the Bash scripts.
- `memory/constitution.md`, this file, and everything under `../specs/`,
  `../docs/SPEC-KIT-WORKFLOW.md`, and `../CLAUDE.md`.

Not installed: the Codex integration, the Git extension (so Spec Kit never creates or switches
branches), optional extensions or presets, and template overrides.

## Upgrades

Releases 1.0.9 to 1.0.11 (2026-09-21 to 2026-09-24) mostly change CLI internals, the workflow
runner, other agents' integrations, and catalogs. They also include small fixes to generated
skill text (converge wording, frontmatter handling) and an optional
`SPECIFY_FEATURE_NO_PERSIST` script variable. To upgrade, generate the new version in a
temporary directory, review the differences, and copy the managed files. Never run
`specify init --here --force` over this working tree.

## Health checks

From the repository root:

```sh
bash .specify/scripts/bash/resolve-template.sh plan-template --json
uvx --from specify-cli==1.0.8 specify integration status --json
```

The second command may download the pinned CLI into uv's cache. Both check the installation,
not application correctness.
