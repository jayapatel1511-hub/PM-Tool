# Working on the Hub with Spec Kit

This repository uses [GitHub Spec Kit](https://github.com/github/spec-kit) 1.0.8 to build the
Engineering Project Coordination Hub in small, reviewable packets. [CLAUDE.md](../CLAUDE.md) is
the entry point, the [constitution](../.specify/memory/constitution.md) holds the rules, and
[specs/README.md](../specs/README.md) indexes the 33 packets that hold the whole product
specification in Spec Kit form, including the first-release six-view amendment (§36).

## What to tell an agent

For planning:

> Use Spec Kit to specify and plan packet 00N from specs/README.md. Read CLAUDE.md, cite the
> product specification sections and IDs, and do not write application code. Show scope,
> non-goals, exact files, and acceptance checks.

For an authorised implementation:

> Implement tasks [IDs] in specs/00N-name/. Follow CLAUDE.md and the constitution, stay inside
> the plan's file boundaries, run its checks, and record them in verification.md. Put anything
> out of scope in proposed follow-ups.

An implementation request authorises that scope, so agents should not ask again at every step.
They must ask when the work would materially change scope. A planning request alone never
authorises application code.

## Commands

The skills are checked into `.claude/skills/`. Restart the Claude Code session after setup if
the `/speckit-*` commands are not listed.

| Stage | Command | Result |
| --- | --- | --- |
| Define | `/speckit-specify` | `spec.md` with scope, non-goals, requirements, and scenarios, citing specification IDs |
| Clarify, when needed | `/speckit-clarify` | Behaviour questions resolved before design |
| Plan | `/speckit-plan` | `plan.md` with the Constitution Check, file boundaries, and checks; `research.md`, `data-model.md`, `contracts/`, `quickstart.md` |
| Tasks | `/speckit-tasks` | `tasks.md` with exact paths, each task linked to requirements |
| Consistency gate | `/speckit-analyze` | Contradictions between spec, plan, and tasks resolved before coding |
| Implement | `/speckit-implement` | Only the authorised tasks, with results in `verification.md` |
| Remaining gaps | `/speckit-converge` | Acceptance gaps found; new ideas stay proposals |
| Requirement quality (optional) | `/speckit-checklist` | A checklist of the requirements themselves, not proof the code works |

`/speckit-constitution` is for amendments Jay directs, not for relaxing a rule to fit a
feature. `/speckit-taskstoissues` runs only on request.

## Rules that prevent drift

1. Name the feature directory explicitly: prefix shell helpers with
   `SPECIFY_FEATURE_DIRECTORY=specs/<feature>`. The local `.specify/feature.json` pointer is
   ignored by Git and may be stale. Spec Kit does not create or switch Git branches here.
2. Every packet requirement cites the specification ID it comes from. A change in behaviour
   amends `spec-parts/` and every packet that cites the changed IDs (see
   `specs/TRACEABILITY.md`) in the same change, then runs `python3 tools/build_spec.py` and
   `python3 tools/trace_spec.py`. Both have a `--check` mode that fails when something is stale
   or uncovered.
3. Declare file boundaries in the plan and preserve unrelated edits in those files.
4. Map every task to requirements and acceptance checks. A task list is not permission to build
   every idea in it.
5. Record passing, failing, skipped, and unrun checks in `verification.md`.
6. Use sub-agents or `specify workflow run` only when Jay asks. Commit, push, open issues, or
   deploy only on request.

## Installation health

```sh
bash .specify/scripts/bash/resolve-template.sh plan-template --json
uvx --from specify-cli==1.0.8 specify integration status --json
```

The second command may download the pinned CLI into uv's cache. See
[provenance](../.specify/UPSTREAM.md) for where the files came from and how to upgrade.
