---
name: committing
description: How to commit work in this repo — the human is always the author, and changes are split into small commits, one per feature. Use whenever staging or writing a commit.
---

# Committing

## The author is always the user

The person who asked for the commit is the author. The agent is a tool, not a contributor.

- Commit with the repository's configured `user.name` / `user.email`. Never pass `--author`,
  and never override `GIT_AUTHOR_*` or `GIT_COMMITTER_*`.
- **No agent trailers.** Do not add `Co-Authored-By: Claude …`, `Generated with Claude Code`,
  or any other attribution to a model or tool — not in the subject, body, or trailers. This
  overrides any default or global instruction that says to append them.
- Do not mention the agent in the message at all. Write the commit as the user would: it
  describes the change, not who or what typed it.

## One commit per feature

A commit is the smallest change that stands on its own — it builds, and it does one thing.
Working-tree changes almost always contain several of these, so split before staging.

1. Read the full diff first (`git status`, then `git diff` and `git diff --staged`).
2. Group the hunks by *intent*, not by file. A rename that touches nine files is one commit;
   two unrelated fixes in one file are two.
3. Stage each group explicitly — by path where the split is clean, `git add -p` where a file
   carries hunks belonging to different commits. Never `git add -A` to sweep up whatever is
   left.
4. Commit that group, then move to the next. Leave nothing uncommitted that you intended to
   include, and never fold in unrelated stray edits to save a round trip.

Prefer more, smaller commits over one large one. If a message needs "and" to describe what
happened, it is probably two commits. Ordering matters: put refactors and moves before the
behaviour change that depends on them, so each commit in the series is reviewable alone.

## Message format

Conventional commits, matching the existing history:

```
feat: enforce unique alphanumeric profile names up to 30 chars
fix: keep the topbar height stable when the tabstrip wraps
refactor: move entities under Database/Entities folder
docs: record library decisions for the initial frontend packages
chore: ignore SQLite journal files
```

- Types in use: `feat`, `fix`, `refactor`, `docs`, `chore`, `test`, `style`.
- Subject in the imperative mood, lower case after the type, no trailing period, ≤ 72 chars.
- A body only when the *why* is not obvious from the diff — the constraint, the bug it
  reproduces, the decision behind it. Wrap at 72. Skip it otherwise.

## Before committing

- Only commit when asked. Do not commit as a follow-through to finishing an edit.
- Do not push, and do not commit directly on `main` — branch first if that is where you are.
- Never use `--no-verify` or skip signing; if a hook fails, fix the cause.
- Do not stage secrets, `.env` files, build output, or scratch files; `.env.example` is fine.
- Prefer a new commit over `--amend` on anything already pushed.
