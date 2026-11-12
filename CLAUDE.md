# Core Guidelines
You are building a simple Football Manager 2027 plugin that lets you shortlist players without being bombarded by inbox items about them.

Always build code cleanly, modularly, and with proper architecture and separation of concerns. Never simply bolt on a feature just because I asked for it. If you can't find an easy way to build it naturally, and you'd recommend a refactor to make the whole thing cleanly, stop your work ahead of time and say so. Always keep `.wiki/ARCHITECTURE.md` updated: it states the intended architecture and lists where the code does not match it yet.

Aside from this requirement, this codebase is yours: the product is decided by the user, but how it's built is your call, and you'll be the one to keep on building on it. There is no obligation to maintain legacy functionality or code - If code is no longer used, remove it.

## Workflow
* The `.wiki/` folder contains relevant project information, and should be updated and maintained regularly by both you and the user. Start with `.wiki/INDEX.md`, which maps each document to what it owns; product requirements live in `.wiki/PRD.md`. The folder is intentionally excluded from git tracking via `.gitignore`. In a worktree it is a link to the main checkout's copy that you can read but not edit, so report the wiki updates you would have made instead.
* Features are implemented in feature-branches, never `main`.
* When implementing a feature, always follow the relevant feature spec in `.wiki/features`.
* When creating UI, take screenshots to verify the outcome. Present outcome to user with select screenshots included.
* Before reporting work as done, run the checks that cover it and include the commands and their output in the report.
* Before opening a PR, run `/code-review` on the branch. If the branch implements a spec in `.wiki/features/`, also have the `miniclaude:spec-checker` agent check the branch against it. Give the agent the spec path, the changed files and, for UI, the paths of your screenshots, but not your account of the work. Fix the findings that affect correctness and skip style findings.
* When a PR is approved, it is rebase-merged onto `main`.
* When a PR has been merged, always clean up local git branches that are completed.

## Commands
Record the exact commands here as soon as they exist, and correct them when the toolchain changes: install, run, test (the whole suite and a single file), typecheck, and lint.

None yet.

## Conventions
* Always follow TDD.
* Git history is memory. Make messages and descriptions informative to both human and AI, and focus on the "why" as well as the "what".
* Follow KISS, DRY, YAGNI - Don't overengineer a simple change.
